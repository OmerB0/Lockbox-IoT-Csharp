using System;
using System.Drawing;
using System.Windows.Forms;
using QRCoder;

namespace LockboxQr
{
    /// <summary>
    /// Kasa kiralama ve QR üretim diyaloğu
    /// </summary>
    public class LockerRentalDialog : Form
    {
        private readonly string _lockerDisplayName;
        private readonly string _lockerCode;
        private readonly ITokenService _tokenService;
        private readonly IArduinoService? _arduinoService;

        // Dakika seçimi kaldırıldı - sadece tek seferlik QR kod
        private Button _btnGenerate = null!;
        private PictureBox _picQr = null!;
        private Label _lblTimer = null!;
        private Button _btnExtend = null!;
        private Button _btnClose = null!;

        private System.Windows.Forms.Timer? _timer;
        private int _timerSeconds;
        private int _phase; // 1: gösterim 15 sn, 2: uyarı 10 sn

        /// <summary>
        /// Üretilen token
        /// </summary>
        public string? GeneratedToken { get; private set; }

        /// <summary>
        /// Token geçerlilik bitiş zamanı (epoch)
        /// </summary>
        public long GeneratedExp { get; private set; }

        /// <summary>
        /// Tek seferlik flag
        /// </summary>
        public bool GeneratedOnce { get; private set; }

        /// <summary>
        /// Token nonce
        /// </summary>
        public string? GeneratedNonce { get; private set; }

        /// <summary>
        /// QR kod üretildiğinde tetiklenen event
        /// </summary>
        public event EventHandler? QrGenerated;

        public LockerRentalDialog(string lockerDisplayName, string lockerCode, ITokenService tokenService, IArduinoService? arduinoService = null)
        {
            _lockerDisplayName = lockerDisplayName;
            _lockerCode = lockerCode;
            _tokenService = tokenService ?? throw new ArgumentNullException(nameof(tokenService));
            _arduinoService = arduinoService;

            InitializeUi();
        }

        private void InitializeUi()
        {
            this.Text = $"{_lockerDisplayName} - QR Kod Oluştur";
            this.Size = new Size(520, 570); // Yükseklik azaltıldı (dakika seçimi kaldırıldı)
            this.FormBorderStyle = FormBorderStyle.FixedDialog;
            this.MaximizeBox = false;
            this.MinimizeBox = false;
            this.StartPosition = FormStartPosition.CenterParent;

            var lblHeader = new Label
            {
                Text = $"{_lockerDisplayName} için QR kod oluştur",
                Font = new Font("Segoe UI", 12, FontStyle.Bold),
                AutoSize = true,
                Location = new Point(20, 20)
            };

            var lblInfo = new Label
            {
                Text = "Tek seferlik QR kod oluşturulacak",
                Location = new Point(20, 50),
                AutoSize = true,
                ForeColor = Color.DarkGray,
                Font = new Font("Segoe UI", 9)
            };

            _btnGenerate = new Button
            {
                Text = "QR Oluştur",
                Location = new Point(20, 80),
                Size = new Size(120, 32)
            };
            _btnGenerate.Click += BtnGenerate_Click;

            _lblTimer = new Label
            {
                Text = "",
                AutoSize = true,
                ForeColor = Color.DarkSlateGray,
                Location = new Point(160, 88)
            };

            _picQr = new PictureBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = PictureBoxSizeMode.Zoom,
                Location = new Point(20, 125),
                Size = new Size(460, 360)
            };

            _btnExtend = new Button
            {
                Text = "Ek 15 saniye",
                Location = new Point(20, 500),
                Size = new Size(120, 32),
                Visible = false
            };
            _btnExtend.Click += BtnExtend_Click;

            _btnClose = new Button
            {
                Text = "Sayaçsız Kapat",
                Location = new Point(160, 500),
                Size = new Size(140, 32), // Genişlik artırıldı (metin uzadı)
                Visible = false // QR üretildikten sonra görünecek
            };
            _btnClose.Click += BtnCloseNow_Click;

            this.Controls.Add(lblHeader);
            this.Controls.Add(lblInfo);
            this.Controls.Add(_btnGenerate);
            this.Controls.Add(_lblTimer);
            this.Controls.Add(_picQr);
            this.Controls.Add(_btnExtend);
            this.Controls.Add(_btnClose);
        }

        private void BtnGenerate_Click(object? sender, EventArgs e)
        {
            // Butonu disable et (birden fazla tıklanmayı önle) - EN BAŞTA
            if (!_btnGenerate.Enabled) return; // Zaten disable ise çık
            _btnGenerate.Enabled = false;
            
            try
            {
                var lockerId = _lockerDisplayName.Replace(" ", "").ToUpperInvariant();
                // Sadece tek seferlik QR kod, validity 1 dakika (sadece token formatı için gerekli)
                var validity = 1;
                var oneTime = true; // Her zaman tek seferlik

                var token = _tokenService.GenerateToken(lockerId, validity, oneTime);
                GeneratedToken = token;
                GeneratedOnce = oneTime;

                // Exp'i token içinden parse etmeyelim; tahmini hesaplayalım (UTC now + validity)
                GeneratedExp = DateTimeOffset.UtcNow.AddMinutes(validity).ToUnixTimeSeconds();

                // Token'dan nonce'u parse et
                try
                {
                    var parts = token.Split('|');
                    foreach (var part in parts)
                    {
                        if (part.StartsWith("nonce="))
                        {
                            GeneratedNonce = part.Substring(6);
                            break;
                        }
                    }
                }
                catch
                {
                    // Nonce parse edilemezse boş bırak
                    GeneratedNonce = null;
                }

                using (var qrGenerator = new QRCodeGenerator())
                {
                    var qrData = qrGenerator.CreateQrCode(token, QRCodeGenerator.ECCLevel.Q);
                    using (var qrCode = new QRCode(qrData))
                    {
                        var qrBitmap = qrCode.GetGraphic(20);
                        _picQr.Image?.Dispose();
                        _picQr.Image = new Bitmap(qrBitmap);
                    }
                }

                StartTimers();

                // QR üretildiyse Kapat butonu görünür olsun
                _btnClose.Visible = true;

                // QR üretildi event'ini tetikle (Form1'e haber ver, sarı yapsın)
                // Form1'de UpdateLockerTileState çağrılacak ve servo motor komutu gönderilecek
                QrGenerated?.Invoke(this, EventArgs.Empty);
            }
            catch (ArgumentException ex)
            {
                MessageBox.Show(ex.Message, "Hata", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            catch (TokenException ex)
            {
                MessageBox.Show($"Token üretilemedi: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Beklenmeyen hata: {ex.Message}", "Hata", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void StartTimers()
        {
            _phase = 1;
            _timerSeconds = 0;
            _btnExtend.Visible = false;
            // _btnClose görünür kalsın (QR üretildiyse)

            _timer?.Stop();
            _timer?.Dispose();

            _timer = new System.Windows.Forms.Timer();
            _timer.Interval = 1000;
            _timer.Tick += Timer_Tick;
            _timer.Start();

            UpdateTimerLabel();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            _timerSeconds++;

            if (_phase == 1 && _timerSeconds >= 15)
            {
                // Uyarı fazına geç
                _phase = 2;
                _timerSeconds = 0;
                _btnExtend.Visible = true;
                _lblTimer.Text = "QR birazdan kapanacak (10 sn)";
                return;
            }

            if (_phase == 2 && _timerSeconds >= 10)
            {
                // 15 saniye + 10 saniye = 25 saniye doldu, kilitlen ve kapat
                CloseWithOk();
                return;
            }

            UpdateTimerLabel();
        }

        private void UpdateTimerLabel()
        {
            if (_phase == 1)
            {
                var remaining = Math.Max(0, 15 - _timerSeconds);
                _lblTimer.Text = $"QR görüntüleme: {remaining} sn";
            }
            else
            {
                var remaining = Math.Max(0, 10 - _timerSeconds);
                _lblTimer.Text = $"QR kapanacak: {remaining} sn";
            }
        }

        private void BtnExtend_Click(object? sender, EventArgs e)
        {
            // 15 saniyelik yeni gösterim
            _phase = 1;
            _timerSeconds = 0;
            _btnExtend.Visible = false;
            _btnClose.Visible = false;
            UpdateTimerLabel();
        }

        private void BtnCloseNow_Click(object? sender, EventArgs e)
        {
            // Sayaç iptal et
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            // QR kodu kapat (görüntüyü temizle)
            _picQr.Image?.Dispose();
            _picQr.Image = null;

            // Not: Arduino komutları (LED ve servo motor) Form1'de UpdateLockerTileState içinde gönderilecek
            // Dialog kapandığında Form1'de UpdateLockerTileState çağrılacak ve servo motor kilitlenecek

            // Token üretildiyse, OK dön (kasa kiralı durumuna geçecek)
            this.DialogResult = GeneratedToken != null ? DialogResult.OK : DialogResult.Cancel;
            this.Close();
        }

        private void CloseWithOk()
        {
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;

            // Not: Arduino komutları (LED ve servo motor) Form1'de UpdateLockerTileState içinde gönderilecek
            // Dialog kapandığında Form1'de UpdateLockerTileState çağrılacak ve servo motor kilitlenecek

            // Token üretildiyse, timer bitmeden de OK dön
            this.DialogResult = GeneratedToken != null ? DialogResult.OK : DialogResult.Cancel;
            this.Close();
        }

        protected override void OnFormClosing(FormClosingEventArgs e)
        {
            // X'e basılsa bile, token varsa OK sayalım (kiralama tamamlandı kabul)
            _timer?.Stop();
            _timer?.Dispose();
            _timer = null;
            if (GeneratedToken != null)
            {
                this.DialogResult = DialogResult.OK;
            }
            base.OnFormClosing(e);
        }
    }
}

