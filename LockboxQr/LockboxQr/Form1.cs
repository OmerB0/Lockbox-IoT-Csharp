using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using AForge.Video;
using AForge.Video.DirectShow;
using QRCoder;
using ZXing;
using ZXing.Windows.Compatibility;

namespace LockboxQr
{
    public partial class Form1 : Form
    {
        private readonly ITokenService _tokenService;
        private readonly INonceTracker _nonceTracker;
        private readonly IAccessLogger _accessLogger;
        private readonly AppConfiguration _config;
        private readonly IArduinoService? _arduinoService;
        
        private VideoCaptureDevice? _videoSource;
        private BarcodeReader _barcodeReader;
        private string? _lastScannedToken;
        private DateTime _lastScanTime;
        private System.Windows.Forms.Timer? _successOverlayTimer;
        private System.Windows.Forms.Timer? _expirationCheckTimer; // QR kod süre kontrolü için
        private readonly Dictionary<string, System.Windows.Forms.Timer> _qrScanTimers = new Dictionary<string, System.Windows.Forms.Timer>(); // QR okutulunca 10 saniye sonra yeşile geçiş için
        private readonly Dictionary<string, System.Windows.Forms.Timer> _servoTimers = new Dictionary<string, System.Windows.Forms.Timer>(); // Servo motor komutları için timer (çift komut önleme)
        private readonly Dictionary<string, DateTime> _qrScanTokenTimes = new Dictionary<string, DateTime>(); // QR tarama sonrası token eklendiği zaman (QR tarama senaryosunu tespit etmek için)

        // Locker management
        private readonly List<LockerState> _lockers = new List<LockerState>();
        private readonly Dictionary<int, Panel> _lockerTiles = new Dictionary<int, Panel>();
        private readonly Dictionary<int, Label> _lockerStatusLabels = new Dictionary<int, Label>();
        private TabPage? _tabPageLockers;
        private FlowLayoutPanel? _flowLockers;
        private Label? _lblLockersHeader;

        /// <summary>
        /// Kasa durumu enum'u
        /// </summary>
        private enum LockerStatus
        {
            Available,  // Müsait (kiralanabilir, fiziksel olarak kilitli)
            Rented,    // Kiralı (açık - anahtar koyma süreci)
            Locked,    // Kiralı ve kilitli (kullanımda)
            Expired    // Süresi dolmuş
        }

        private class LockerState
        {
            public int Id { get; set; }
            public string Code { get; set; } = string.Empty; // Örn: KASA1
            public string DisplayName { get; set; } = string.Empty; // Örn: Kasa 1
            public LockerStatus Status { get; set; } = LockerStatus.Available; // Durum enum'u
            public bool IsLocked => Status == LockerStatus.Locked || Status == LockerStatus.Available; // Backward compatibility
            public string? Token { get; set; }
            public long Exp { get; set; }
            public bool Once { get; set; }
            public string? Nonce { get; set; }
        }

        /// <summary>
        /// Form1 constructor - Dependency Injection ile
        /// </summary>
        /// <param name="tokenService">Token servisi (null ise default oluşturulur)</param>
        /// <param name="nonceTracker">Nonce tracker (null ise default oluşturulur)</param>
        /// <param name="accessLogger">Access logger (null ise default oluşturulur)</param>
        /// <param name="config">Yapılandırma (null ise default kullanılır)</param>
        /// <param name="arduinoService">Arduino servisi (null ise yapılandırmaya göre oluşturulur veya null kalır)</param>
        public Form1(
            ITokenService? tokenService = null,
            INonceTracker? nonceTracker = null,
            IAccessLogger? accessLogger = null,
            AppConfiguration? config = null,
            IArduinoService? arduinoService = null)
        {
            InitializeComponent();
            
            _config = config ?? AppConfiguration.Instance;
            _tokenService = tokenService ?? new TokenService(_config);
            _nonceTracker = nonceTracker ?? new NonceTracker(_config);
            _accessLogger = accessLogger ?? new AccessLogger(_config);
            
            // Arduino servisi - yapılandırmaya göre oluştur veya null bırak
            try
            {
                if (arduinoService != null)
                {
                    _arduinoService = arduinoService;
                }
                else if (_config.ArduinoEnabled)
                {
                    // Simülatör modu varsa simülatör kullan, yoksa gerçek Arduino servisi
                    if (_config.ArduinoUseSimulator)
                    {
                        _arduinoService = new ArduinoSimulatorService();
                        System.Diagnostics.Debug.WriteLine("[Form1] Arduino Simülatör modu aktif!");
                    }
                    else
                    {
                        _arduinoService = new ArduinoService(_config);
                    }
                    
                    _arduinoService.MessageReceived += ArduinoService_MessageReceived;
                    
                    // Bağlantıyı Load event'inde yap (Form tamamen yüklendikten sonra)
                    this.Load += Form1_Load;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Arduino servisi oluşturulurken hata:\n{ex.Message}\n\n{ex.StackTrace}", 
                    "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            
            _barcodeReader = new BarcodeReader();
            _lastScanTime = DateTime.MinValue;

            // Eski QR Üret sekmesini gizle (artık LockerRentalDialog kullanılıyor)
            if (tabControl.TabPages.Contains(tabPageGenerate))
            {
                tabControl.TabPages.Remove(tabPageGenerate);
            }
            
            InitializeLockers();
            BuildLockersTab();

            InitializeCameras();
            UpdateInfoLabel();
            
            // Expiration timer'ı başlat (her 10 saniyede bir kontrol et)
            _expirationCheckTimer = new System.Windows.Forms.Timer();
            _expirationCheckTimer.Interval = 10000; // 10 saniye
            _expirationCheckTimer.Tick += ExpirationCheckTimer_Tick;
            _expirationCheckTimer.Start();
        }
        
        /// <summary>
        /// QR kod sürelerini kontrol eder ve süresi dolan kasaları açar
        /// </summary>
        private void ExpirationCheckTimer_Tick(object? sender, EventArgs e)
        {
            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var tolerance = _config.TimeToleranceSeconds;
            
            foreach (var locker in _lockers)
            {
                // Eğer kasa kilitli ve token varsa, süresini kontrol et
                if (locker.Status == LockerStatus.Locked && locker.Token != null && locker.Exp > 0)
                {
                    // Süre dolmuş mu?
                    if (now > locker.Exp + tolerance)
                    {
                        // Süre doldu - kasayı müsait yap
                        locker.Status = LockerStatus.Available;
                        locker.Token = null;
                        locker.Exp = 0;
                        locker.Once = false;
                        locker.Nonce = null;
                        UpdateLockerTileState(locker);
                        
                        System.Diagnostics.Debug.WriteLine($"[Expiration] {locker.Code} süresi doldu, müsait yapıldı");
                    }
                }
            }
        }

        /// <summary>
        /// Form yüklendiğinde Arduino bağlantısını başlat (AutoConnect kapalı - manuel bağlantı yapılacak)
        /// </summary>
        private void Form1_Load(object? sender, EventArgs e)
        {
            // AutoConnect kapalı - kullanıcı manuel olarak "Arduino Bağlan" butonundan bağlanacak
        }

        /// <summary>
        /// Arduino'dan STATUS komutu ile durum bilgisi alır
        /// </summary>
        private void CheckArduinoStatus()
        {
            if (_arduinoService == null)
            {
                MessageBox.Show("Arduino servisi aktif değil!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (!_arduinoService.IsConnected)
            {
                MessageBox.Show("Arduino bağlı değil!\nÖnce bağlantıyı kurun.", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                string? status = _arduinoService.GetStatus();
                if (string.IsNullOrEmpty(status))
                {
                    MessageBox.Show("STATUS komutu yanıt vermedi.\nArduino Serial Monitor'ü kontrol edin.", 
                        "Yanıt Yok", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    MessageBox.Show($"Arduino Durum:\n{status}\n\nNot: Arduino'dan gelen mesajlar otomatik olarak işlenir ve kasalar güncellenir.", 
                        "Arduino Durum", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"STATUS komutu hatası:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void InitializeCameras()
        {
            try
            {
                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                cmbCameras.Items.Clear();
                
                foreach (FilterInfo device in videoDevices)
                {
                    cmbCameras.Items.Add(device.Name);
                }
                
                if (cmbCameras.Items.Count > 0)
                {
                    cmbCameras.SelectedIndex = 0;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kamera bulunamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void UpdateInfoLabel()
        {
            var validity = (int)numValidity.Value;
            var oneTime = chkOneTime.Checked ? "açık" : "kapalı";
            lblInfo.Text = $"Geçerlilik: {validity} dk, Tek seferlik: {oneTime}";
        }

        /// <summary>
        /// Kasaları ve durumlarını başlangıçta oluşturur
        /// </summary>
        private void InitializeLockers()
        {
            _lockers.Clear();
            _lockerTiles.Clear();
            _lockerStatusLabels.Clear();

            for (int i = 1; i <= 6; i++)
            {
                _lockers.Add(new LockerState
                {
                    Id = i,
                    Code = $"KASA{i}",
                    DisplayName = $"Kasa {i}",
                    Status = LockerStatus.Available // Başlangıçta müsait (fiziksel olarak kilitli ama kiralanabilir)
                });
            }
        }

        /// <summary>
        /// Kasalar sekmesini oluşturur ve TabControl'e ekler
        /// </summary>
        private void BuildLockersTab()
        {
            _tabPageLockers = new TabPage("Kasalar")
            {
                Padding = new Padding(10),
                BackColor = Color.White
            };

            _lblLockersHeader = new Label
            {
                Text = "Kiralık Kasalar",
                Font = new Font("Segoe UI", 14, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(10, 10)
            };

            // Arduino bağlantı butonu - header'ın yanında görünür yerde
            var btnArduinoConnect = new Button
            {
                Text = _arduinoService?.IsConnected == true ? "Arduino Bağlı ✓" : "Arduino Bağlan",
                Size = new Size(150, 35),
                Location = new Point(200, 8), // Label'ın yanında
                BackColor = _arduinoService?.IsConnected == true ? Color.LightGreen : Color.LightBlue,
                FlatStyle = FlatStyle.Flat
            };
            btnArduinoConnect.Click += BtnArduinoConnect_Click;

            _flowLockers = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.LeftToRight,
                WrapContents = true,
                AutoScroll = true,
                Padding = new Padding(5),
                Location = new Point(0, 0),
                Margin = new Padding(0)
            };

            var headerPanel = new Panel
            {
                Dock = DockStyle.Top,
                Height = 50
            };
            headerPanel.Controls.Add(_lblLockersHeader);
            headerPanel.Controls.Add(btnArduinoConnect);

            var container = new Panel
            {
                Dock = DockStyle.Fill
            };
            container.Controls.Add(_flowLockers);
            container.Controls.Add(headerPanel);

            _tabPageLockers.Controls.Add(container);

            // TabControl'e başa ekle ve seçili yap
            tabControl.TabPages.Insert(0, _tabPageLockers);
            tabControl.SelectedTab = _tabPageLockers;

            RenderLockerTiles();
        }

        /// <summary>
        /// Locker kartlarını oluşturur ve FlowLayoutPanel'e ekler
        /// </summary>
        private void RenderLockerTiles()
        {
            if (_flowLockers == null) return;

            _flowLockers.Controls.Clear();
            foreach (var locker in _lockers)
            {
                var tile = CreateLockerTile(locker);
                _lockerTiles[locker.Id] = tile;
                _flowLockers.Controls.Add(tile);
                UpdateLockerTileState(locker);
            }
        }

        private Panel CreateLockerTile(LockerState locker)
        {
            var panel = new Panel
            {
                Width = 200,
                Height = 120,
                BorderStyle = BorderStyle.FixedSingle,
                Margin = new Padding(10),
                BackColor = Color.White,
                Tag = locker.Id,
                Cursor = Cursors.Hand
            };

            var lblName = new Label
            {
                Text = locker.DisplayName,
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = false,
                Dock = DockStyle.Top,
                Height = 30,
                TextAlign = ContentAlignment.MiddleCenter,
                Tag = locker.Id
            };

            var lblStatus = new Label
            {
                Text = "Müsait",
                Font = new Font("Segoe UI", 10, FontStyle.Regular),
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Tag = locker.Id
            };

            panel.Controls.Add(lblStatus);
            panel.Controls.Add(lblName);

            panel.DoubleClick += LockerTile_DoubleClick;
            panel.MouseClick += LockerTile_MouseClick; // Sağ tıklama için
            lblName.DoubleClick += LockerTile_DoubleClick;
            lblStatus.DoubleClick += LockerTile_DoubleClick;
            lblName.MouseClick += LockerTile_MouseClick; // Sağ tıklama için
            lblStatus.MouseClick += LockerTile_MouseClick; // Sağ tıklama için

            _lockerStatusLabels[locker.Id] = lblStatus;
            return panel;
        }

        private void UpdateLockerTileState(LockerState locker)
        {
            System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] === BAŞLANGIÇ === Locker: {locker.Code}, Status: {locker.Status}, Thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
            
            if (!_lockerTiles.TryGetValue(locker.Id, out var panel))
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✗ Panel bulunamadı: {locker.Id}");
                return;
            }
            if (!_lockerStatusLabels.TryGetValue(locker.Id, out var lblStatus))
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✗ Label bulunamadı: {locker.Id}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] UI güncelleniyor: {locker.Code} → {locker.Status}");
            string oldText = lblStatus.Text;
            Color oldColor = lblStatus.ForeColor;
            Color oldBackColor = panel.BackColor;
            
            switch (locker.Status)
            {
                case LockerStatus.Available:
                    panel.BackColor = Color.FromArgb(235, 255, 235);
                    lblStatus.Text = $"Müsait\n{locker.Code}";
                    lblStatus.ForeColor = Color.DarkGreen;
                    System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✓ UI güncellendi: Available (Yeşil/Müsait) - Text: '{lblStatus.Text}', ForeColor: {lblStatus.ForeColor.Name}, BackColor: {panel.BackColor.Name}");
                    break;
                case LockerStatus.Rented:
                    panel.BackColor = Color.FromArgb(255, 255, 200);
                    lblStatus.Text = $"Açık\n{locker.Code}";
                    lblStatus.ForeColor = Color.Orange;
                    System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✓ UI güncellendi: Rented (Sarı/Açık) - Text: '{lblStatus.Text}', ForeColor: {lblStatus.ForeColor.Name}, BackColor: {panel.BackColor.Name}");
                    break;
                case LockerStatus.Locked:
                    panel.BackColor = Color.FromArgb(255, 245, 235);
                    lblStatus.Text = $"Kilitli\n{locker.Code}";
                    lblStatus.ForeColor = Color.DarkRed;
                    System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✓ UI güncellendi: Locked (Kırmızı/Kilitli) - Text: '{lblStatus.Text}', ForeColor: {lblStatus.ForeColor.Name}, BackColor: {panel.BackColor.Name}");
                    break;
                case LockerStatus.Expired:
                    panel.BackColor = Color.FromArgb(255, 230, 230);
                    lblStatus.Text = $"Süresi Doldu\n{locker.Code}";
                    lblStatus.ForeColor = Color.DarkOrange;
                    System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✓ UI güncellendi: Expired (Turuncu/Süresi Doldu) - Text: '{lblStatus.Text}', ForeColor: {lblStatus.ForeColor.Name}, BackColor: {panel.BackColor.Name}");
                    break;
            }
            
            System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] UI değişiklikleri: Text: '{oldText}' → '{lblStatus.Text}', ForeColor: {oldColor.Name} → {lblStatus.ForeColor.Name}, BackColor: {oldBackColor.Name} → {panel.BackColor.Name}");

            // KASA1 için Arduino'ya LED durumu ve servo motor durumu gönder
            System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] Arduino kontrolü: Code={locker.Code}, IsKASA1={locker.Code.Equals("KASA1", StringComparison.OrdinalIgnoreCase)}, Service={_arduinoService != null}, Connected={_arduinoService?.IsConnected}");
            
            if (locker.Code.Equals("KASA1", StringComparison.OrdinalIgnoreCase) && _arduinoService != null && _arduinoService.IsConnected)
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] KASA1 için Arduino komutları gönderilecek");
                try
                {
                    // Duruma göre LED durumu: Available (Yeşil), Rented (Sarı), Locked (Kırmızı)
                    string status;
                    string statusName;
                    System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] Durum belirleniyor: {locker.Status}");
                    switch (locker.Status)
                    {
                        case LockerStatus.Available:
                            status = "AVAILABLE";
                            statusName = "Yeşil";
                            break;
                        case LockerStatus.Rented:
                            status = "RENTED";
                            statusName = "Sarı";
                            break;
                        case LockerStatus.Locked:
                        case LockerStatus.Expired:
                            status = "LOCKED";
                            statusName = "Kırmızı";
                            break;
                        default:
                            status = "AVAILABLE";
                            statusName = "Yeşil";
                            break;
                    }
                    
                    // ÖNCE LED durumunu gönder (öncelikli - görsel geri bildirim)
                    System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] >>> LED komutu gönderiliyor: SET_STATUS:KASA1:{status}");
                    try
                    {
                        bool ledResult = _arduinoService.SetLockerStatus("KASA1", status);
                        System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✓ LED komutu gönderildi: SET_STATUS:KASA1:{status} ({statusName}) - Sonuç: {ledResult}");
                        
                        // LED komutu gönderildikten SONRA servo motor komutunu gönder (100ms gecikme ile - Arduino'nun LED komutunu işlemesi için)
                        // Servo motor davranışı:
                        // - LED Sarı (Rented) → Servo motor AÇIK (UNLOCK)
                        // - LED Kırmızı (Locked) → Servo motor KİLİTLİ (LOCK)
                        // - LED Yeşil (Available) → Servo motor KİLİTLİ (LOCK) - Müsait ama fiziksel olarak kilitli
                        System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] Servo motor komutu belirleniyor: Status={locker.Status}, Rented={locker.Status == LockerStatus.Rented}");
                        
                        // Önceki timer'ı durdur (varsa - çift komut önleme)
                        if (_servoTimers.TryGetValue(locker.Code, out var existingServoTimer))
                        {
                            System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] Önceki servo timer durduruluyor: {locker.Code}");
                            existingServoTimer.Stop();
                            existingServoTimer.Dispose();
                            _servoTimers.Remove(locker.Code);
                        }
                        
                        var servoTimer = new System.Windows.Forms.Timer();
                        servoTimer.Interval = 100; // 100ms gecikme - LED komutunun işlenmesi için
                        servoTimer.Tick += (s, e) =>
                        {
                            servoTimer.Stop();
                            servoTimer.Dispose();
                            _servoTimers.Remove(locker.Code);
                            
                            try
                            {
                                // Durum kontrolü - timer tetiklendiğinde mevcut duruma göre komut gönder
                                System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] Servo timer tetiklendi, mevcut durum kontrolü: {locker.Status}");
                                
                                // Mevcut duruma göre statusName belirle
                                string currentStatusName;
                                switch (locker.Status)
                                {
                                    case LockerStatus.Available:
                                        currentStatusName = "Yeşil";
                                        break;
                                    case LockerStatus.Rented:
                                        currentStatusName = "Sarı";
                                        break;
                                    case LockerStatus.Locked:
                                    case LockerStatus.Expired:
                                        currentStatusName = "Kırmızı";
                                        break;
                                    default:
                                        currentStatusName = "Yeşil";
                                        break;
                                }
                                
                                if (locker.Status == LockerStatus.Rented)
                                {
                                    // LED Sarı → Servo motor açık
                                    System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] >>> Servo motor açma komutu gönderiliyor: UNLOCK:KASA1 (100ms gecikme sonrası)");
                                    bool unlockResult = _arduinoService.UnlockLocker("KASA1");
                                    System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✓ Servo motor açıldı (LED Sarı - Rented durumu) - Sonuç: {unlockResult}");
                                }
                                else
                                {
                                    // LED Kırmızı (Locked) veya Yeşil (Available) → Servo motor kilitli
                                    System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] >>> Servo motor kilitleme komutu gönderiliyor: LOCK:KASA1 (100ms gecikme sonrası)");
                                    bool lockResult = _arduinoService.LockLocker("KASA1");
                                    System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✓ Servo motor kilitlendi (LED {currentStatusName} - {locker.Status} durumu) - Sonuç: {lockResult}");
                                }
                            }
                            catch (Exception servoEx)
                            {
                                System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✗ Servo motor komut hatası: {servoEx.Message}");
                                System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✗ Servo motor komut hatası StackTrace: {servoEx.StackTrace}");
                            }
                        };
                        _servoTimers[locker.Code] = servoTimer;
                        servoTimer.Start();
                        System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] Servo motor komutu 100ms sonra gönderilecek (timer başlatıldı)");
                    }
                    catch (Exception ledEx)
                    {
                        System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✗ LED komut hatası: {ledEx.Message}");
                        System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✗ LED komut hatası StackTrace: {ledEx.StackTrace}");
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✗ Arduino durum gönderme hatası: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] ✗ Arduino durum gönderme hatası StackTrace: {ex.StackTrace}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] Arduino komutları gönderilmedi: Code={locker.Code}, IsKASA1={locker.Code.Equals("KASA1", StringComparison.OrdinalIgnoreCase)}, Service={_arduinoService != null}, Connected={_arduinoService?.IsConnected}");
            }
            
            System.Diagnostics.Debug.WriteLine($"[UpdateLockerTileState] === BİTİŞ === Locker: {locker.Code}, Status: {locker.Status}");
        }

        private void LockerTile_DoubleClick(object? sender, EventArgs e)
        {
            if (sender is not Control c) return;

            // Child control'e tıklanmış olabilir, parent panel'in Tag'ini kullan
            int? lockerId = null;
            if (c.Tag is int id1)
                lockerId = id1;
            else if (c.Parent is Control p && p.Tag is int id2)
                lockerId = id2;

            if (lockerId == null) return;

            var locker = _lockers.FirstOrDefault(l => l.Id == lockerId.Value);
            if (locker == null) return;

            if (locker.Status == LockerStatus.Available)
            {
                OpenRentalDialog(locker);
            }
            else
            {
                HandleLockedLocker(locker);
            }
        }

        private void LockerTile_MouseClick(object? sender, MouseEventArgs e)
        {
            // Sadece sağ tıklama için
            if (e.Button != MouseButtons.Right) return;

            if (sender is not Control c) return;

            // Child control'e tıklanmış olabilir, parent panel'in Tag'ini kullan
            int? lockerId = null;
            if (c.Tag is int id1)
                lockerId = id1;
            else if (c.Parent is Control p && p.Tag is int id2)
                lockerId = id2;

            if (lockerId == null) return;

            var locker = _lockers.FirstOrDefault(l => l.Id == lockerId.Value);
            if (locker == null) return;

            // Sadece kilitli kasalar için context menu göster
            if (locker.Status == LockerStatus.Locked || locker.Status == LockerStatus.Expired)
            {
                ShowLockerContextMenu(locker, c.PointToScreen(e.Location));
            }
        }

        private void ShowLockerContextMenu(LockerState locker, Point screenLocation)
        {
            var contextMenu = new ContextMenuStrip();
            
            var menuItemMüsaitYap = new ToolStripMenuItem("Müsait Yap");
            menuItemMüsaitYap.Click += (s, e) =>
            {
                System.Diagnostics.Debug.WriteLine($"[ShowLockerContextMenu] ========== MÜSAİT YAP ========== Locker: {locker.Code}");
                System.Diagnostics.Debug.WriteLine($"[ShowLockerContextMenu] Önceki durum: {locker.Status}");
                
                // Kasa durumunu müsait yap
                locker.Status = LockerStatus.Available;
                locker.Token = null;
                locker.Exp = 0;
                locker.Once = false;
                locker.Nonce = null;
                
                // Token zamanını temizle
                _qrScanTokenTimes.Remove(locker.Code);
                
                System.Diagnostics.Debug.WriteLine($"[ShowLockerContextMenu] Yeni durum: {locker.Status}");
                System.Diagnostics.Debug.WriteLine($"[ShowLockerContextMenu] UpdateLockerTileState çağrılıyor...");
                
                // UI ve Arduino komutlarını güncelle
                UpdateLockerTileState(locker);
                
                System.Diagnostics.Debug.WriteLine($"[ShowLockerContextMenu] ✓ Kasa müsait yapıldı: {locker.Code}");
                System.Diagnostics.Debug.WriteLine($"[ShowLockerContextMenu] ========================================");
            };
            
            contextMenu.Items.Add(menuItemMüsaitYap);
            contextMenu.Show(screenLocation);
        }

        private void OpenRentalDialog(LockerState locker)
        {
            using (var dialog = new LockerRentalDialog(locker.DisplayName, locker.Code, _tokenService, _arduinoService))
            {
                // QR üretildiğinde hemen sarı (Rented) yap
                // UpdateLockerTileState içinde LED ve servo motor komutları gönderilecek
                dialog.QrGenerated += (sender, e) =>
                {
                    System.Diagnostics.Debug.WriteLine($"[OpenRentalDialog] ========== QR ÜRETİLDİ ========== Locker: {locker.Code}");
                    System.Diagnostics.Debug.WriteLine($"[OpenRentalDialog] Önceki durum: {locker.Status}");
                    locker.Status = LockerStatus.Rented; // Açık durum (sarı)
                    System.Diagnostics.Debug.WriteLine($"[OpenRentalDialog] Yeni durum: {locker.Status}");
                    System.Diagnostics.Debug.WriteLine($"[OpenRentalDialog] UpdateLockerTileState çağrılıyor...");
                    UpdateLockerTileState(locker); // LED ve servo motor komutları burada gönderilecek
                    System.Diagnostics.Debug.WriteLine($"[OpenRentalDialog] ✓ QR üretildi, kasa sarı (Rented) yapıldı: {locker.Code}");
                    System.Diagnostics.Debug.WriteLine($"[OpenRentalDialog] ========================================");
                };

                var result = dialog.ShowDialog(this);
                if (result == DialogResult.OK && dialog.GeneratedToken != null)
                {
                    // Token bilgilerini kaydet
                    locker.Token = dialog.GeneratedToken;
                    locker.Exp = dialog.GeneratedExp;
                    locker.Once = dialog.GeneratedOnce;
                    locker.Nonce = dialog.GeneratedNonce;

                    // Dialog kapanınca durum güncellenecek:
                    // - QR üretilince hemen Rented (sarı) yapıldı (QrGenerated event ile)
                    // - Dialog kapanınca (Kapat butonuna basınca veya timer bitince) kasa kilitlenmeli (kırmızı)
                    System.Diagnostics.Debug.WriteLine($"[OpenRentalDialog] ========== DIALOG KAPANDI ========== Locker: {locker.Code}");
                    System.Diagnostics.Debug.WriteLine($"[OpenRentalDialog] Önceki durum: {locker.Status}");
                    locker.Status = LockerStatus.Locked; // Kasa kilitli (kırmızı)
                    System.Diagnostics.Debug.WriteLine($"[OpenRentalDialog] Yeni durum: {locker.Status}");
                    System.Diagnostics.Debug.WriteLine($"[OpenRentalDialog] UpdateLockerTileState çağrılıyor...");
                    
                    // UI'yi güncelle (kırmızı/kilitli göster)
                    // UpdateLockerTileState içinde LED ve servo motor komutları gönderilecek
                    UpdateLockerTileState(locker);
                    System.Diagnostics.Debug.WriteLine($"[OpenRentalDialog] ✓ Dialog kapandı, kasa Locked (kırmızı) yapıldı: {locker.Code}");
                    System.Diagnostics.Debug.WriteLine($"[OpenRentalDialog] ========================================");
                }
            }
        }

        private void HandleLockedLocker(LockerState locker)
        {
            using (var dialog = new LockerLockedDialog(locker.DisplayName))
            {
                var result = dialog.ShowDialog(this);
                if (result == DialogResult.OK)
                {
                    // Şimdilik QR tarama sekmesine geçiyoruz.
                    // İleride buradan bağımsız bir QR tarama diyaloğu açabiliriz.
                    tabControl.SelectedTab = tabPageScan;
                }
            }
        }

        private void UnlockLockerByCode(string lockerCode)
        {
            var locker = _lockers.FirstOrDefault(l => l.Code.Equals(lockerCode, StringComparison.OrdinalIgnoreCase));
            if (locker == null) return;

            // Bu metod artık sadece kiralama bittiğinde (15 saniye sonra) çağrılacak
            // Token bilgilerini sil ve müsait yap
            locker.Status = LockerStatus.Available;
            locker.Token = null;
            locker.Exp = 0;
            locker.Once = false;
            locker.Nonce = null;
            UpdateLockerTileState(locker);
        }

        private void BtnGenerate_Click(object? sender, EventArgs e)
        {
            try
            {
                var lockerId = txtLockerId.Text.Trim();
                
                // Input validation - Locker ID
                if (!InputValidator.ValidateLockerId(lockerId, out string lockerIdError))
                {
                    MessageBox.Show(lockerIdError, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    txtLockerId.Focus();
                    return;
                }

                var validityMinutes = (int)numValidity.Value;
                
                // Input validation - Validity Minutes
                if (!InputValidator.ValidateValidityMinutes(validityMinutes, out string validityError))
                {
                    MessageBox.Show(validityError, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    numValidity.Focus();
                    return;
                }
                
                var oneTime = chkOneTime.Checked;
                
                string token;
                try
                {
                    token = _tokenService.GenerateToken(lockerId, validityMinutes, oneTime);
                }
                catch (ArgumentException ex)
                {
                    MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }
                catch (TokenException ex)
                {
                    MessageBox.Show($"Token üretilemedi: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }
                
                txtToken.Text = token;
                
                // Generate QR Code
                using (var qrGenerator = new QRCodeGenerator())
                {
                    var qrData = qrGenerator.CreateQrCode(token, QRCodeGenerator.ECCLevel.Q);
                    using (var qrCode = new QRCode(qrData))
                    {
                        var qrBitmap = qrCode.GetGraphic(20);
                        picQRCode.Image?.Dispose();
                        picQRCode.Image = new Bitmap(qrBitmap);
                    }
                }
                
                // Save QR to out/ folder
                try
                {
                    var outDir = _config.OutputDirectory;
                    if (!Directory.Exists(outDir))
                    {
                        Directory.CreateDirectory(outDir);
                    }
                    
                    var fileName = $"QR_{lockerId}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
                    var filePath = Path.Combine(outDir, fileName);
                    picQRCode.Image.Save(filePath, ImageFormat.Png);
                }
                catch (UnauthorizedAccessException ex)
                {
                    MessageBox.Show($"QR kaydedilemedi: Dosyaya erişim reddedildi. {ex.Message}", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (IOException ex)
                {
                    MessageBox.Show($"QR kaydedilemedi: Dosya hatası. {ex.Message}", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"QR kaydedilemedi: {ex.Message}", "Uyarı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                
                UpdateInfoLabel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"QR oluşturulurken hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStart_Click(object? sender, EventArgs e)
        {
            try
            {
                if (cmbCameras.SelectedIndex < 0)
                {
                    MessageBox.Show("Lütfen bir kamera seçin!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    return;
                }

                var videoDevices = new FilterInfoCollection(FilterCategory.VideoInputDevice);
                if (cmbCameras.SelectedIndex >= videoDevices.Count)
                {
                    MessageBox.Show("Seçilen kamera bulunamadı!", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                var device = videoDevices[cmbCameras.SelectedIndex];
                _videoSource = new VideoCaptureDevice(device.MonikerString);
                _videoSource.NewFrame += VideoSource_NewFrame;
                _videoSource.Start();
                
                btnStart.Enabled = false;
                btnStop.Enabled = true;
                cmbCameras.Enabled = false;
                lblStatus.Text = "Beklemede";
                lblStatus.ForeColor = Color.Black;
            }
            catch (System.Runtime.InteropServices.COMException ex)
            {
                MessageBox.Show($"Kamera başlatılamadı: COM hatası. {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Kamera başlatılamadı: Erişim reddedildi. {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Kamera başlatılamadı: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void BtnStop_Click(object? sender, EventArgs e)
        {
            StopCamera();
        }

        private void StopCamera()
        {
            // VideoSource işlemlerini thread-safe yap
            VideoCaptureDevice? videoSource = null;
            lock (this)
            {
                if (_videoSource != null && _videoSource.IsRunning)
                {
                    videoSource = _videoSource;
                    _videoSource.SignalToStop();
                    _videoSource.WaitForStop();
                    _videoSource.NewFrame -= VideoSource_NewFrame;
                    _videoSource = null;
                }
            }
            
            // UI kontrollerine thread-safe erişim
            if (picPreview.InvokeRequired)
            {
                picPreview.Invoke(new Action(() =>
                {
                    picPreview.Image?.Dispose();
                    picPreview.Image = null;
                    btnStart.Enabled = true;
                    btnStop.Enabled = false;
                    cmbCameras.Enabled = true;
                    lblStatus.Text = "Durduruldu";
                    lblStatus.ForeColor = Color.Gray;
                }));
            }
            else
            {
                picPreview.Image?.Dispose();
                picPreview.Image = null;
                btnStart.Enabled = true;
                btnStop.Enabled = false;
                cmbCameras.Enabled = true;
                lblStatus.Text = "Durduruldu";
                lblStatus.ForeColor = Color.Gray;
            }
        }

        private void VideoSource_NewFrame(object sender, NewFrameEventArgs eventArgs)
        {
            try
            {
                var bitmap = (Bitmap)eventArgs.Frame.Clone();
                
                // Update preview
                if (picPreview.InvokeRequired)
                {
                    picPreview.Invoke(new Action(() =>
                    {
                        picPreview.Image?.Dispose();
                        picPreview.Image = (Bitmap)bitmap.Clone();
                    }));
                }
                else
                {
                    picPreview.Image?.Dispose();
                    picPreview.Image = (Bitmap)bitmap.Clone();
                }
                
                // Try to decode QR code
                var result = _barcodeReader.Decode(bitmap);
                if (result != null && !string.IsNullOrEmpty(result.Text))
                {
                    ProcessScannedToken(result.Text);
                }
                
                bitmap.Dispose();
            }
            catch
            {
                // Ignore errors in frame processing
            }
        }

        private void ProcessScannedToken(string token)
        {
            // Debounce check
            var now = DateTime.Now;
            var debounceMs = _config.DebounceMilliseconds;
            if (token == _lastScannedToken && (now - _lastScanTime).TotalMilliseconds < debounceMs)
            {
                return;
            }
            
            _lastScannedToken = token;
            _lastScanTime = now;
            
            // Validate token
            var (isValid, data, error) = _tokenService.ValidateToken(token);
            
            if (!isValid || data == null)
            {
                LogAndShowError(data?.LockerId ?? "UNKNOWN", error ?? "Yanlış/bozuk kod", data?.Nonce ?? "", data?.Exp ?? 0);
                return;
            }
            
            // Check one-time usage
            if (data.Once == 1 && _nonceTracker.IsNonceUsed(data.Nonce))
            {
                LogAndShowError(data.LockerId, "Tek seferlik kod daha önce kullanıldı", data.Nonce, data.Exp);
                return;
            }
            
            // Mark as used if one-time
            if (data.Once == 1)
            {
                try
                {
                    _nonceTracker.MarkNonceAsUsed(data.Nonce, data.LockerId);
                }
                catch (NonceException ex)
                {
                    LogAndShowError(data.LockerId, $"Nonce işaretlenemedi: {ex.Message}", data.Nonce, data.Exp);
                    return;
                }
            }
            
            // Log success
            try
            {
                _accessLogger.Log(data.LockerId, "OK", "Başarılı", data.Nonce, data.Exp);
            }
            catch (LoggingException ex)
            {
                System.Diagnostics.Debug.WriteLine($"Loglama hatası: {ex.Message}");
            }
            
            // Locker state'i bul ve token bilgilerini kaydet
            System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ========== QR OKUTULDU ========== LockerId: {data.LockerId}");
            var locker = _lockers.FirstOrDefault(l => l.Code.Equals(data.LockerId, StringComparison.OrdinalIgnoreCase));
            if (locker != null)
            {
                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] Locker bulundu: {locker.Code}, Mevcut durum: {locker.Status}");
                
                // Token bilgilerini kaydet (HandleArduinoMessage için gerekli)
                locker.Token = token;
                locker.Exp = data.Exp;
                locker.Once = data.Once == 1;
                locker.Nonce = data.Nonce;
                
                // QR tarama senaryosunu tespit etmek için token eklendiği zamanı kaydet
                _qrScanTokenTimes[locker.Code] = DateTime.Now;
                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] Token bilgileri kaydedildi: Exp={data.Exp}, Once={data.Once == 1}");
                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] QR tarama zamanı kaydedildi: {locker.Code} - {DateTime.Now:HH:mm:ss.fff}");
                
                // QR tarandı, kasa açıldı - önce sarıya, sonra hemen yeşile geç
                // 1. Önce sarı (Rented) durumuna geç
                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] Durum değiştiriliyor: {locker.Status} → Rented");
                locker.Status = LockerStatus.Rented;
                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ✓ QR tarandı, kasa Rented (sarı) yapıldı: {locker.Code}");
                
                // UI'ı güncelle (sarı göster)
                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] UpdateLockerTileState çağrılıyor (Rented durumu)...");
                if (this.InvokeRequired)
                {
                    System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] InvokeRequired=true, BeginInvoke kullanılıyor");
                    this.BeginInvoke(new Action(() => 
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] BeginInvoke içinde - UpdateLockerTileState çağrılıyor");
                        UpdateLockerTileState(locker);
                        
                        // Hemen ardından yeşile geç (500ms sonra - kısa görsel geri bildirim için)
                        System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] 500ms timer başlatılıyor (Available'a geçiş için)");
                        
                        // Önceki timer'ı durdur (varsa)
                        if (_qrScanTimers.TryGetValue(locker.Code, out var existingTimer))
                        {
                            existingTimer.Stop();
                            existingTimer.Dispose();
                            _qrScanTimers.Remove(locker.Code);
                        }
                        
                        var quickTimer = new System.Windows.Forms.Timer();
                        quickTimer.Interval = 5000; // 5 saniye - ürün alınması için süre
                        System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] Timer oluşturuldu: Interval={quickTimer.Interval}ms, Locker={locker.Code}");
                        quickTimer.Tick += (s, e) =>
                        {
                            System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ========== TIMER TETİKLENDİ (5 saniye) ==========");
                            System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] Thread ID: {System.Threading.Thread.CurrentThread.ManagedThreadId}");
                            System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] Önceki durum: {locker.Status}, Code: {locker.Code}");
                            
                            try
                            {
                                quickTimer.Stop();
                                quickTimer.Dispose();
                                _qrScanTimers.Remove(locker.Code);
                                
                                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] Timer durduruldu ve temizlendi");
                                
                                locker.Status = LockerStatus.Available;
                                locker.Token = null;
                                locker.Exp = 0;
                                locker.Once = false;
                                locker.Nonce = null;
                                
                                // Token zamanını temizle (timer tetiklendi, artık gerek yok)
                                _qrScanTokenTimes.Remove(locker.Code);
                                
                                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] Durum değiştirildi: Rented → Available");
                                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] UpdateLockerTileState çağrılıyor (Available durumu)...");
                                
                                UpdateLockerTileState(locker);
                                
                                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ✓ Kasa 5 saniye sonra Available (yeşil) yapıldı: {locker.Code}");
                                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ========================================");
                            }
                            catch (Exception ex)
                            {
                                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ✗ Timer Tick hatası: {ex.Message}");
                                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ✗ StackTrace: {ex.StackTrace}");
                            }
                        };
                        _qrScanTimers[locker.Code] = quickTimer;
                        quickTimer.Start();
                        System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ✓ Timer başlatıldı: Locker={locker.Code}, Interval={quickTimer.Interval}ms, Enabled={quickTimer.Enabled}");
                    }));
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] InvokeRequired=false, direkt UpdateLockerTileState çağrılıyor");
                    UpdateLockerTileState(locker);
                    
                    // 5 saniye sonra yeşile geç (ürün alınması için süre)
                    System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] 5 saniye timer başlatılıyor (Available'a geçiş için)");
                    
                    // Önceki timer'ı durdur (varsa)
                    if (_qrScanTimers.TryGetValue(locker.Code, out var existingTimer))
                    {
                        existingTimer.Stop();
                        existingTimer.Dispose();
                        _qrScanTimers.Remove(locker.Code);
                    }
                    
                    var quickTimer = new System.Windows.Forms.Timer();
                    quickTimer.Interval = 5000; // 5 saniye - ürün alınması için süre
                    quickTimer.Tick += (s, e) =>
                    {
                        System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ========== TIMER TETİKLENDİ (5 saniye) ==========");
                        System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] Önceki durum: {locker.Status}");
                        quickTimer.Stop();
                        quickTimer.Dispose();
                        _qrScanTimers.Remove(locker.Code);
                        locker.Status = LockerStatus.Available;
                        locker.Token = null;
                        locker.Exp = 0;
                        locker.Once = false;
                        locker.Nonce = null;
                        
                        // Token zamanını temizle (timer tetiklendi, artık gerek yok)
                        _qrScanTokenTimes.Remove(locker.Code);
                        
                        System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] Yeni durum: {locker.Status}");
                        System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] UpdateLockerTileState çağrılıyor (Available durumu)...");
                        UpdateLockerTileState(locker);
                        System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ✓ Kasa 5 saniye sonra Available (yeşil) yapıldı: {locker.Code}");
                        System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ========================================");
                    };
                    _qrScanTimers[locker.Code] = quickTimer;
                    quickTimer.Start();
                        System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] Timer başlatıldı, 5 saniye sonra Available'a geçecek");
                }
                
                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ✓ Locker state bulundu: {locker.Code}, yeni durum: {locker.Status}");
                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ========================================");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ✗ Locker state bulunamadı: {data.LockerId}");
                System.Diagnostics.Debug.WriteLine($"[ProcessScannedToken] ========================================");
            }
            
            // Not: Arduino komutları (LED ve servo motor) UpdateLockerTileState içinde gönderiliyor
            // Burada sadece UI güncellemesi yapılıyor, UpdateLockerTileState servo motor komutunu gönderecek
            
            // Show success
            ShowSuccess();
            
            // Update status
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(new Action(() =>
                {
                    lblStatus.Text = $"Açıldı ✅ - Locker: {data.LockerId}";
                    lblStatus.ForeColor = Color.Green;
                }));
            }
            else
            {
                lblStatus.Text = $"Açıldı ✅ - Locker: {data.LockerId}";
                lblStatus.ForeColor = Color.Green;
            }
        }

        private void LogAndShowError(string lockerId, string reason, string nonce, long exp)
        {
            try
            {
                _accessLogger.Log(lockerId, "ERR", reason, nonce, exp);
            }
            catch (LoggingException ex)
            {
                // Loglama hatası uygulamayı durdurmamalı
                System.Diagnostics.Debug.WriteLine($"Loglama hatası: {ex.Message}");
            }
            
            if (lblStatus.InvokeRequired)
            {
                lblStatus.Invoke(new Action(() =>
                {
                    lblStatus.Text = $"Hata: {reason}";
                    lblStatus.ForeColor = Color.Red;
                }));
            }
            else
            {
                lblStatus.Text = $"Hata: {reason}";
                lblStatus.ForeColor = Color.Red;
            }
        }

        private void ShowSuccess()
        {
            // UI thread'inde çalıştır
            if (lblSuccessOverlay.InvokeRequired)
            {
                lblSuccessOverlay.Invoke(new Action(() =>
                {
                    ShowSuccessInternal();
                }));
            }
            else
            {
                ShowSuccessInternal();
            }
        }

        private void ShowSuccessInternal()
        {
            // Overlay'i göster
            lblSuccessOverlay.Visible = true;
            lblSuccessOverlay.BringToFront();
            
            // Önceki timer'ı durdur
            if (_successOverlayTimer != null)
            {
                _successOverlayTimer.Stop();
                _successOverlayTimer.Dispose();
            }
            
            // Yeni timer oluştur - configuration'dan süre al
            _successOverlayTimer = new System.Windows.Forms.Timer();
            _successOverlayTimer.Interval = _config.SuccessOverlayDurationSeconds * 1000;
            _successOverlayTimer.Tick += (s, e) =>
            {
                _successOverlayTimer.Stop();
                _successOverlayTimer.Dispose();
                _successOverlayTimer = null;
                
                // Overlay'i gizle ve kamerayı kapat
                lblSuccessOverlay.Visible = false;
                StopCamera();
            };
            
            // Timer'ı başlat
            _successOverlayTimer.Start();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            StopCamera();
            _successOverlayTimer?.Stop();
            _successOverlayTimer?.Dispose();
            
            // QR scan timer'larını temizle
            foreach (var timer in _qrScanTimers.Values)
            {
                timer.Stop();
                timer.Dispose();
            }
            _qrScanTimers.Clear();
            
            // Servo timer'larını temizle
            foreach (var timer in _servoTimers.Values)
            {
                timer.Stop();
                timer.Dispose();
            }
            _servoTimers.Clear();
            
            // Arduino bağlantısını kapat
            if (_arduinoService != null)
            {
                try
                {
                    _arduinoService.Disconnect();
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Arduino kapatma hatası: {ex.Message}");
                }
                
                // IDisposable ise dispose et
                if (_arduinoService is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }
            
            base.OnFormClosing(e);
        }

        private void numValidity_ValueChanged(object? sender, EventArgs e)
        {
            UpdateInfoLabel();
        }

        private void chkOneTime_CheckedChanged(object? sender, EventArgs e)
        {
            UpdateInfoLabel();
        }

        /// <summary>
        /// Arduino'dan gelen mesajları işler ve UI'ı günceller
        /// </summary>
        private void ArduinoService_MessageReceived(object? sender, ArduinoMessageEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[ArduinoService_MessageReceived] Mesaj alındı - LockerId: '{e.LockerId}', Message: '{e.Message}', IsUnlocked: {e.IsUnlocked}, IsLocked: {e.IsLocked}");
            
            if (string.IsNullOrWhiteSpace(e.LockerId))
            {
                System.Diagnostics.Debug.WriteLine($"[Arduino] Locker ID boş, mesaj işlenmedi: {e.Message}");
                return; // Locker ID yoksa işlem yapma
            }

            // Locker ID'yi parse et (KASA1 -> Id: 1)
            string lockerCode = e.LockerId.ToUpper().Trim();
            System.Diagnostics.Debug.WriteLine($"[Arduino] Aranan locker kodu: '{lockerCode}'");
            
            // Mevcut locker'ları listele
            foreach (var l in _lockers)
            {
                System.Diagnostics.Debug.WriteLine($"[Arduino] Mevcut locker: Code='{l.Code}', Id={l.Id}");
            }
            
            var locker = _lockers.FirstOrDefault(l => l.Code.Equals(lockerCode, StringComparison.OrdinalIgnoreCase));
            
            if (locker == null)
            {
                System.Diagnostics.Debug.WriteLine($"[Arduino] Bilinmeyen kasa: '{e.LockerId}' (Aranan: '{lockerCode}')");
                System.Diagnostics.Debug.WriteLine($"[Arduino] Mevcut kasalar: {string.Join(", ", _lockers.Select(l => l.Code))}");
                return;
            }

            System.Diagnostics.Debug.WriteLine($"[Arduino] Kasa bulundu: {locker.Code} (Id: {locker.Id})");

            // UI thread'inde güncelle
            if (this.InvokeRequired)
            {
                this.BeginInvoke(new Action(() => HandleArduinoMessage(locker, e)));
            }
            else
            {
                HandleArduinoMessage(locker, e);
            }
        }

        /// <summary>
        /// Arduino mesajını işler ve locker state'ini günceller
        /// </summary>
        private void HandleArduinoMessage(LockerState locker, ArduinoMessageEventArgs e)
        {
            System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] ========== ARDUINO MESAJI ALINDI ==========");
            System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] Locker: {locker.Code}");
            System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] IsUnlocked: {e.IsUnlocked}, IsLocked: {e.IsLocked}");
            System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] Message: {e.Message}");
            System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] Mevcut durum: {locker.Status}");
            
            bool statusChanged = false;
            LockerStatus oldStatus = locker.Status;
            System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] Eski durum: {oldStatus}");
            
            if (e.IsUnlocked)
            {
                // Kasa açıldı
                if (locker.Status == LockerStatus.Locked && locker.Token != null)
                {
                    // QR tarandı, kasa açıldı (kırmızıdan sarıya geçiş)
                    // Not: ProcessScannedToken içinde zaten Rented yapılmış ve hemen ardından Available yapılmış olabilir
                    // Bu durumda sadece durumu güncelle, timer ProcessScannedToken içinde başlatıldı
                    if (locker.Status != LockerStatus.Rented && locker.Status != LockerStatus.Available)
                    {
                        locker.Status = LockerStatus.Rented; // Açık durum (sarı)
                        statusChanged = true;
                        System.Diagnostics.Debug.WriteLine($"[Arduino] Kasa açıldı (QR tarandı): {locker.Code} → Rented (sarı)");
                    }
                }
                else if (locker.Status == LockerStatus.Rented && locker.Token != null)
                {
                    // QR tarandı, ProcessScannedToken içinde zaten Rented yapıldı
                    // ProcessScannedToken içinde zaten hemen ardından Available yapılacak, burada bir şey yapmaya gerek yok
                    System.Diagnostics.Debug.WriteLine($"[Arduino] Kasa zaten Rented durumda, ProcessScannedToken içinde Available yapılacak: {locker.Code}");
                }
                else if (locker.Status == LockerStatus.Available)
                {
                    // QR üretilince açıldı (anahtar koyma süreci - 15 saniye açık kalacak)
                    // ÖNEMLİ: Eğer token yoksa, bu timer'ın Available yaptığı durumdur (QR tarama senaryosu)
                    // Bu durumda UNLOCKED mesajını görmezden gelmeliyiz, çünkü timer zaten Available yaptı
                    if (locker.Token == null)
                    {
                        System.Diagnostics.Debug.WriteLine($"[Arduino] Durum Available ve token yok - timer zaten Available yaptı, UNLOCKED mesajı görmezden gelindi: {locker.Code}");
                        System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] ========== ARDUINO MESAJI İŞLENDİ ==========");
                        return;
                    }
                    
                    // Token varsa QR üretme senaryosu - Rented yap
                    if (locker.Status != LockerStatus.Rented)
                    {
                        locker.Status = LockerStatus.Rented; // Açık durum (sarı)
                        statusChanged = true;
                        System.Diagnostics.Debug.WriteLine($"[Arduino] Kasa açıldı (QR üretildi): {locker.Code} → Rented (sarı)");
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"[Arduino] Kasa zaten Rented durumda, güncelleme atlandı: {locker.Code}");
                    }
                }
            }
            else if (e.IsLocked)
            {
                // Kasa kilitlendi
                
                // Eğer durum zaten Available ise, LOCKED mesajını görmezden gel
                // Çünkü timer zaten Available yaptı, tekrar Locked yapmaya gerek yok
                if (locker.Status == LockerStatus.Available)
                {
                    System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] Durum zaten Available, LOCKED mesajı görmezden gelindi: {locker.Code}");
                    System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] ========== ARDUINO MESAJI İŞLENDİ ==========");
                    return;
                }
                
                if (locker.Status == LockerStatus.Rented && locker.Token != null)
                {
                    // Açık durumdan kilitlendi
                    // ÖNEMLİ: QR üretme senaryosunda, dialog kapatıldığında durum zaten Locked yapılıyor (OpenRentalDialog içinde)
                    // Bu yüzden burada durum Rented olamaz QR üretme senaryosunda
                    // Eğer durum Rented ise ve token varsa, bu kesinlikle QR tarama senaryosudur!
                    // Her zaman Available yapmalıyız
                    
                    System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] QR tarama senaryosu tespit edildi: Durum=Rented, Token var");
                    System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] QR üretme senaryosunda dialog kapatıldığında durum zaten Locked yapılıyor, bu yüzden burada Rented olamaz");
                    
                    // QR tarama senaryosu: ProcessScannedToken içinde timer başlatıldı, Available yapılacak
                    // Arduino'dan gelen LOCKED mesajı, timer'dan önce veya sonra gelirse Available yap (senkronizasyon için)
                    // Timer zaten Available yapacak veya yaptı, ama senkronizasyon için şimdi Available yap
                    locker.Status = LockerStatus.Available;
                    locker.Token = null;
                    locker.Exp = 0;
                    locker.Once = false;
                    locker.Nonce = null;
                    statusChanged = true;
                    
                    // Token zamanını temizle
                    _qrScanTokenTimes.Remove(locker.Code);
                    
                    System.Diagnostics.Debug.WriteLine($"[Arduino] QR tarama senaryosu: Kasa kilitlendi ve müsait yapıldı: {locker.Code} → Available (yeşil)");
                }
                else if (locker.Status == LockerStatus.Rented && locker.Token == null)
                {
                    // ÖNEMLİ: QR tarama senaryosunda timer Available yaptıktan sonra durum Available olmalı
                    // Ama eğer UNLOCKED mesajı durumu tekrar Rented yaptıysa, burada LOCKED mesajı geldiğinde
                    // Available yapmalıyız, Locked yapmamalıyız
                    // QR üretme senaryosunda ise token hiç olmaz (çünkü dialog kapatıldığında token kaydedilir ama sonra kullanıcı kasa açtığında token null olabilir)
                    // QR tarama senaryosunu tespit etmek için _qrScanTokenTimes kullanabiliriz
                    if (_qrScanTokenTimes.ContainsKey(locker.Code))
                    {
                        // QR tarama senaryosu - timer Available yaptı, ama UNLOCKED mesajı durumu Rented yaptı
                        // Şimdi LOCKED mesajı geldi, Available yapmalıyız
                        locker.Status = LockerStatus.Available;
                        _qrScanTokenTimes.Remove(locker.Code);
                        statusChanged = true;
                        System.Diagnostics.Debug.WriteLine($"[Arduino] QR tarama senaryosu: Rented+NoToken durumunda LOCKED mesajı geldi, Available yapıldı: {locker.Code}");
                    }
                    else
                    {
                        // QR üretilince açıldı, 15 saniye sonra kilitlendi (anahtar koyma tamamlandı)
                        if (locker.Status != LockerStatus.Locked)
                        {
                            locker.Status = LockerStatus.Locked;
                            statusChanged = true;
                            System.Diagnostics.Debug.WriteLine($"[Arduino] Kasa kilitlendi (QR üretildi, anahtar koyuldu): {locker.Code} → Locked (kırmızı)");
                        }
                    }
                }
                else if (locker.Status != LockerStatus.Locked)
                {
                    // Diğer durumlar - sadece durum değiştiyse güncelle
                    locker.Status = LockerStatus.Locked;
                    statusChanged = true;
                    System.Diagnostics.Debug.WriteLine($"[Arduino] Kasa kilitlendi: {locker.Code} → Locked (kırmızı)");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Arduino] Mesaj işlendi ama durum değişmedi: {e.Message}");
            }
            
            // Durum gerçekten değiştiyse UI ve Arduino komutlarını güncelle
            // NOT: UpdateLockerTileState çağrılıyor çünkü:
            // - Arduino mesajı geldiğinde durum değişikliği yapıldı
            // - LED ve servo motor senkronizasyonu için UpdateLockerTileState çağrılmalı
            // - UpdateLockerTileState içinde çift komut kontrolü yapılıyor (durum kontrolü ile)
            System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] Durum kontrolü: statusChanged={statusChanged}, oldStatus={oldStatus}, newStatus={locker.Status}");
            if (statusChanged && oldStatus != locker.Status)
            {
                System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] ✓ Durum değişti, UpdateLockerTileState çağrılıyor: {oldStatus} → {locker.Status}");
                // UpdateLockerTileState çağrılmalı - LED ve servo motor senkronizasyonu için
                UpdateLockerTileState(locker);
                System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] ✓ UpdateLockerTileState çağrıldı, LED ve servo motor komutları gönderildi");
            }
            else if (!statusChanged)
            {
                System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] Durum değişmedi (statusChanged=false), UpdateLockerTileState atlandı: {locker.Code}");
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] Durum değişmedi (oldStatus==newStatus), UpdateLockerTileState atlandı: {locker.Code}");
            }
            
            System.Diagnostics.Debug.WriteLine($"[HandleArduinoMessage] ========== ARDUINO MESAJI İŞLENDİ ==========");
        }

        /// <summary>
        /// QR okutulunca kasa açıldığında 10 saniye sonra yeşile geçiş için timer başlatır
        /// </summary>
        private void StartQrScanTimer(LockerState locker)
        {
            // Önceki timer'ı durdur (varsa)
            if (_qrScanTimers.TryGetValue(locker.Code, out var existingTimer))
            {
                existingTimer.Stop();
                existingTimer.Dispose();
                _qrScanTimers.Remove(locker.Code);
            }

            // Yeni timer oluştur
            var timer = new System.Windows.Forms.Timer();
            timer.Interval = 10000; // 10 saniye
            timer.Tick += (s, e) =>
            {
                timer.Stop();
                timer.Dispose();
                _qrScanTimers.Remove(locker.Code);

                // 10 saniye sonra yeşile (AVAILABLE) geç - durum ne olursa olsun
                System.Diagnostics.Debug.WriteLine($"[QrScanTimer] Timer tick - Locker: {locker.Code}, Mevcut durum: {locker.Status}");
                
                locker.Status = LockerStatus.Available;
                locker.Token = null;
                locker.Exp = 0;
                locker.Once = false;
                locker.Nonce = null;

                // UI thread'inde güncelle
                if (this.InvokeRequired)
                {
                    this.BeginInvoke(new Action(() => UpdateLockerTileState(locker)));
                }
                else
                {
                    UpdateLockerTileState(locker);
                }
                
                System.Diagnostics.Debug.WriteLine($"[QrScanTimer] 10 saniye doldu, kasa AVAILABLE (yeşil) yapıldı: {locker.Code}");
            };

            _qrScanTimers[locker.Code] = timer;
            timer.Start();
            System.Diagnostics.Debug.WriteLine($"[QrScanTimer] Timer başlatıldı: {locker.Code}, 10 saniye sonra yeşile geçecek");
        }

        /// <summary>
        /// Arduino bağlantı butonu tıklandığında
        /// </summary>
        private void BtnArduinoConnect_Click(object? sender, EventArgs e)
        {
            if (_arduinoService == null)
            {
                MessageBox.Show("Arduino servisi aktif değil!\nappsettings.json'da Arduino:Enabled: true olmalı.", 
                    "Arduino Devre Dışı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            if (_arduinoService.IsConnected)
            {
                // Bağlantıyı kes
                try
                {
                    _arduinoService.Disconnect();
                    MessageBox.Show("Arduino bağlantısı kesildi.", "Bağlantı Kesildi", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    
                    // Buton metnini güncelle
                    if (sender is Button btn)
                    {
                        btn.Text = "Arduino Bağlan";
                        btn.BackColor = Color.LightBlue;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Bağlantı kesilirken hata:\n{ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            else
            {
                // Port seçimi dialogu göster
                var availablePorts = System.IO.Ports.SerialPort.GetPortNames();
                if (availablePorts.Length == 0)
                {
                    var result = MessageBox.Show(
                        "Hiçbir COM portu bulunamadı!\n\n" +
                        "Olası nedenler:\n" +
                        "1. Arduino USB ile bağlı değil\n" +
                        "2. Arduino sürücüsü yüklü değil\n" +
                        "3. Arduino başka bir USB portunda\n\n" +
                        "Simülatör modunu kullanmak ister misiniz?",
                        "Port Bulunamadı", 
                        MessageBoxButtons.YesNo, 
                        MessageBoxIcon.Warning);
                    
                    if (result == DialogResult.Yes)
                    {
                        MessageBox.Show(
                            "Simülatör modunu aktif etmek için:\n\n" +
                            "1. appsettings.json dosyasını açın\n" +
                            "2. 'Arduino:UseSimulator' değerini 'true' yapın\n" +
                            "3. Uygulamayı yeniden başlatın",
                            "Simülatör Modu", 
                            MessageBoxButtons.OK, 
                            MessageBoxIcon.Information);
                    }
                    return;
                }

                // Port seçimi için basit bir dialog
                using (var portDialog = new Form
                {
                    Text = "Arduino Port Seç",
                    Size = new Size(420, 180), // Genişlik ve yükseklik artırıldı (butonlar tam görünsün)
                    StartPosition = FormStartPosition.CenterParent,
                    FormBorderStyle = FormBorderStyle.FixedDialog,
                    MaximizeBox = false,
                    MinimizeBox = false
                })
                {
                    var lblPort = new Label
                    {
                        Text = "COM Port:",
                        Location = new Point(10, 20),
                        AutoSize = true
                    };

                    var cmbPort = new ComboBox
                    {
                        Location = new Point(80, 17),
                        Width = 200,
                        DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    cmbPort.Items.AddRange(availablePorts);
                    
                    // Port bilgisi label'ı ekle
                    var lblInfo = new Label
                    {
                        Text = "Not: Arduino IDE Serial Monitor kapalı olmalı!",
                        Location = new Point(10, 50),
                        AutoSize = true,
                        ForeColor = Color.DarkGray,
                        Font = new Font("Segoe UI", 8)
                    };
                    
                    // Mevcut port varsa seçili yap
                    var currentPort = _config.ArduinoPortName;
                    if (cmbPort.Items.Contains(currentPort))
                    {
                        cmbPort.SelectedItem = currentPort;
                    }
                    else if (cmbPort.Items.Count > 0)
                    {
                        cmbPort.SelectedIndex = 0;
                    }

                    // Test Et butonu - Port'un kullanılabilir olup olmadığını kontrol eder
                    // Bu buton portun kullanılabilir olup olmadığını test eder (bağlanmadan önce kontrol)
                    var btnTest = new Button
                    {
                        Text = "Test Et",
                        Location = new Point(290, 15),
                        Size = new Size(90, 25) // Genişlik artırıldı
                    };
                    btnTest.Click += (s, e) =>
                    {
                        if (cmbPort.SelectedItem == null)
                        {
                            MessageBox.Show("Lütfen bir port seçin!", "Port Seçilmedi", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                            return;
                        }

                        var testPort = cmbPort.SelectedItem.ToString();
                        if (string.IsNullOrEmpty(testPort)) return;

                        try
                        {
                            if (!ArduinoService.IsPortAvailable(testPort))
                            {
                                MessageBox.Show($"Port '{testPort}' sistemde bulunamadı!", "Port Bulunamadı", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                return;
                            }

                            if (ArduinoService.IsPortInUse(testPort))
                            {
                                MessageBox.Show(
                                    $"Port '{testPort}' kullanımda!\n\n" +
                                    "Arduino IDE Serial Monitor'ü KAPATIN ve tekrar deneyin.",
                                    "Port Kullanımda", 
                                    MessageBoxButtons.OK, 
                                    MessageBoxIcon.Warning);
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"Port '{testPort}' kullanılabilir! ✓\n\n" +
                                    "Bağlan butonuna tıklayarak bağlantıyı deneyebilirsiniz.",
                                    "Port Test Başarılı", 
                                    MessageBoxButtons.OK, 
                                    MessageBoxIcon.Information);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show($"Test hatası: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    };

                    var btnConnect = new Button
                    {
                        Text = "Bağlan",
                        Location = new Point(140, 110), // Y konumu aşağı kaydırıldı
                        Size = new Size(80, 30),
                        DialogResult = DialogResult.OK
                    };

                    var btnCancel = new Button
                    {
                        Text = "İptal",
                        Location = new Point(230, 110), // Y konumu aşağı kaydırıldı
                        Size = new Size(80, 30),
                        DialogResult = DialogResult.Cancel
                    };

                    portDialog.Controls.AddRange(new Control[] { lblPort, cmbPort, btnTest, lblInfo, btnConnect, btnCancel });
                    portDialog.Height = 180; // Dialog yüksekliği artırıldı
                    portDialog.AcceptButton = btnConnect;
                    portDialog.CancelButton = btnCancel;

                    if (portDialog.ShowDialog(this) == DialogResult.OK && cmbPort.SelectedItem != null)
                    {
                        var selectedPort = cmbPort.SelectedItem.ToString();
                        if (string.IsNullOrEmpty(selectedPort)) return;

                        try
                        {
                            // Port test et
                            if (!ArduinoService.IsPortAvailable(selectedPort))
                            {
                                MessageBox.Show(
                                    $"Port '{selectedPort}' sistemde bulunamadı!\n\n" +
                                    "Lütfen:\n" +
                                    "1. Arduino'nun USB ile bağlı olduğundan emin olun\n" +
                                    "2. Device Manager'dan COM portunu kontrol edin",
                                    "Port Bulunamadı", 
                                    MessageBoxButtons.OK, 
                                    MessageBoxIcon.Error);
                                return;
                            }

                            if (ArduinoService.IsPortInUse(selectedPort))
                            {
                                MessageBox.Show(
                                    $"Port '{selectedPort}' kullanımda!\n\n" +
                                    "Lütfen:\n" +
                                    "1. Arduino IDE Serial Monitor'ü KAPATIN\n" +
                                    "2. Başka bir programın portu kullanmadığından emin olun",
                                    "Port Kullanımda", 
                                    MessageBoxButtons.OK, 
                                    MessageBoxIcon.Warning);
                                return;
                            }

                            // Yeni port ile bağlan
                            bool connected = _arduinoService.Connect(selectedPort);
                            
                            if (connected)
                            {
                                MessageBox.Show(
                                    $"Arduino bağlandı! ✓\n\n" +
                                    $"Port: {selectedPort}\n" +
                                    $"BaudRate: {_config.ArduinoBaudRate}\n\n" +
                                    "Bağlantı test ediliyor...", 
                                    "Bağlantı Başarılı", 
                                    MessageBoxButtons.OK, 
                                    MessageBoxIcon.Information);
                                
                                // Buton metnini güncelle
                                if (sender is Button btn)
                                {
                                    btn.Text = $"Arduino Bağlı ✓\n{selectedPort}";
                                    btn.BackColor = Color.LightGreen;
                                }

                                // Bağlantıyı test et - Arduino'dan "LOCKBOX_CONTROLLER_READY" mesajı bekleniyor
                                System.Diagnostics.Debug.WriteLine($"[BtnArduinoConnect] ========== ARDUINO BAĞLANDI ==========");
                                System.Diagnostics.Debug.WriteLine($"[BtnArduinoConnect] Port: {selectedPort}");
                                System.Threading.Thread.Sleep(1000);
                                var status = _arduinoService.GetStatus();
                                System.Diagnostics.Debug.WriteLine($"[BtnArduinoConnect] Bağlantı testi - Status: {status}");
                                
                                // Arduino bağlandığında başlangıç durumunu gönder (servo motor kilitli olmalı)
                                // Tüm kasaların durumunu güncelle (özellikle KASA1 için LED ve servo motor komutları gönderilecek)
                                System.Diagnostics.Debug.WriteLine($"[BtnArduinoConnect] Başlangıç durumları gönderiliyor...");
                                foreach (var locker in _lockers)
                                {
                                    System.Diagnostics.Debug.WriteLine($"[BtnArduinoConnect] Başlangıç durumu gönderiliyor: {locker.Code} - {locker.Status}");
                                    UpdateLockerTileState(locker);
                                }
                                System.Diagnostics.Debug.WriteLine($"[BtnArduinoConnect] ✓ Arduino bağlandı, başlangıç durumları gönderildi (servo motorlar kilitli)");
                                System.Diagnostics.Debug.WriteLine($"[BtnArduinoConnect] ========================================");
                            }
                            else
                            {
                                MessageBox.Show(
                                    $"Arduino bağlanamadı!\n\n" +
                                    $"Port: {selectedPort}\n\n" +
                                    "Lütfen:\n" +
                                    "1. Arduino IDE Serial Monitor'ü KAPATIN\n" +
                                    "2. Port'un doğru olduğundan emin olun\n" +
                                    "3. Arduino'nun çalıştığından emin olun", 
                                    "Bağlantı Hatası", 
                                    MessageBoxButtons.OK, 
                                    MessageBoxIcon.Warning);
                            }
                        }
                        catch (ArduinoException ex)
                        {
                            // Detaylı Arduino hata mesajı
                            var result = MessageBox.Show(
                                $"{ex.Message}\n\n" +
                                "Simülatör modunu kullanmak ister misiniz?\n" +
                                "(Gerçek Arduino olmadan test edebilirsiniz)",
                                "Arduino Bağlantı Hatası", 
                                MessageBoxButtons.YesNo, 
                                MessageBoxIcon.Warning);
                            
                            if (result == DialogResult.Yes)
                            {
                                // Simülatör moduna geçiş önerisi
                                MessageBox.Show(
                                    "Simülatör modunu aktif etmek için:\n\n" +
                                    "1. appsettings.json dosyasını açın\n" +
                                    "2. 'Arduino:UseSimulator' değerini 'true' yapın\n" +
                                    "3. Uygulamayı yeniden başlatın\n\n" +
                                    "Simülatör modu gerçek Arduino olmadan test yapmanızı sağlar.",
                                    "Simülatör Modu", 
                                    MessageBoxButtons.OK, 
                                    MessageBoxIcon.Information);
                            }
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(
                                $"Beklenmeyen hata:\n{ex.Message}\n\n" +
                                $"Hata tipi: {ex.GetType().Name}\n" +
                                $"Port: {selectedPort}\n\n" +
                                "Detaylar için Visual Studio Output penceresine bakın.",
                                "Hata", 
                                MessageBoxButtons.OK, 
                                MessageBoxIcon.Error);
                        }
                    }
                }
            }
        }
    }
}
