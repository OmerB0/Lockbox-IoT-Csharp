using System;
using System.IO.Ports;
using System.Text;
using System.Threading;

namespace LockboxQr
{
    /// <summary>
    /// Arduino ile Serial/Bluetooth iletişimi sağlayan servis
    /// </summary>
    public class ArduinoService : IArduinoService, IDisposable
    {
        private readonly AppConfiguration _config;
        private SerialPort? _serialPort;
        private readonly object _lockObject = new object();
        private bool _disposed = false;
        private Thread? _readThread;
        private bool _shouldRead = false;
        private string? _partialMessage = null; // Eksik mesajları birleştirmek için buffer

        /// <summary>
        /// Arduino'dan mesaj geldiğinde tetiklenen event
        /// </summary>
        public event EventHandler<ArduinoMessageEventArgs>? MessageReceived;

        /// <summary>
        /// Arduino bağlantı durumu
        /// </summary>
        public bool IsConnected => _serialPort?.IsOpen ?? false;

        /// <summary>
        /// ArduinoService constructor
        /// </summary>
        /// <param name="config">Uygulama yapılandırması</param>
        public ArduinoService(AppConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Port'un mevcut olup olmadığını kontrol eder
        /// </summary>
        public static bool IsPortAvailable(string portName)
        {
            try
            {
                var availablePorts = System.IO.Ports.SerialPort.GetPortNames();
                return availablePorts.Contains(portName, StringComparer.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        /// <summary>
        /// Port'un kullanımda olup olmadığını test eder
        /// </summary>
        public static bool IsPortInUse(string portName)
        {
            try
            {
                using (var testPort = new SerialPort(portName))
                {
                    testPort.Open();
                    testPort.Close();
                    return false; // Port kullanılabilir
                }
            }
            catch (UnauthorizedAccessException)
            {
                return true; // Port kullanımda
            }
            catch
            {
                return false; // Diğer hatalar (port mevcut değil vs.)
            }
        }

        /// <summary>
        /// Arduino bağlantısını başlatır
        /// </summary>
        /// <param name="portName">Port adı (null ise config'den okunur)</param>
        public bool Connect(string? portName = null)
        {
            var portToUse = portName ?? _config.ArduinoPortName;
            System.Diagnostics.Debug.WriteLine("========================================");
            System.Diagnostics.Debug.WriteLine("[Arduino Connect] Bağlantı başlatılıyor...");
            System.Diagnostics.Debug.WriteLine($"[Arduino Connect] Port: {portToUse}, BaudRate: {_config.ArduinoBaudRate}");
            
            // Port kontrolü
            if (string.IsNullOrWhiteSpace(portToUse))
            {
                throw new ArduinoException("Port adı belirtilmedi!");
            }

            if (!IsPortAvailable(portToUse))
            {
                throw new ArduinoException(
                    $"Port '{portToUse}' sistemde bulunamadı!\n\n" +
                    "Çözüm:\n" +
                    "1. Arduino'nun USB ile bağlı olduğundan emin olun\n" +
                    "2. Device Manager'dan COM portunu kontrol edin\n" +
                    "3. Arduino sürücüsünün yüklü olduğundan emin olun");
            }

            if (IsPortInUse(portToUse))
            {
                throw new ArduinoException(
                    $"Port '{portToUse}' şu anda kullanımda!\n\n" +
                    "Çözüm:\n" +
                    "1. Arduino IDE'de Serial Monitor'ü KAPATIN (en yaygın neden!)\n" +
                    "2. Başka bir programın portu kullanmadığından emin olun\n" +
                    "3. Arduino'yu USB'den çıkarıp tekrar takın");
            }
            
            lock (_lockObject)
            {
                if (_serialPort?.IsOpen == true)
                {
                    System.Diagnostics.Debug.WriteLine("[Arduino Connect] Zaten bağlı!");
                    return true; // Zaten bağlı
                }

                try
                {
                    System.Diagnostics.Debug.WriteLine("[Arduino Connect] SerialPort oluşturuluyor...");
                    
                    // Serial port ayarları
                    _serialPort = new SerialPort(
                        portToUse,
                        _config.ArduinoBaudRate,
                        Parity.None,
                        8,
                        StopBits.One)
                    {
                        ReadTimeout = _config.ArduinoReadTimeout,
                        WriteTimeout = _config.ArduinoWriteTimeout,
                        Handshake = Handshake.None,
                        DtrEnable = true,
                        RtsEnable = true
                    };

                    System.Diagnostics.Debug.WriteLine("[Arduino Connect] Port açılıyor...");
                    _serialPort.Open();
                    System.Diagnostics.Debug.WriteLine($"[Arduino Connect] Port açıldı! IsOpen: {_serialPort.IsOpen}");

                    // Bağlantı kontrolü için kısa bir bekleme
                    Thread.Sleep(500);

                    // Arduino'dan gelen mesajları dinlemek için thread başlat
                    System.Diagnostics.Debug.WriteLine("[Arduino Connect] Okuma thread'i başlatılıyor...");
                    StartReadingThread();

                    System.Diagnostics.Debug.WriteLine("[Arduino Connect] Bağlantı başarılı!");
                    System.Diagnostics.Debug.WriteLine("========================================");
                    
                    return _serialPort.IsOpen;
                }
                catch (UnauthorizedAccessException ex)
                {
                    // Port zaten kullanımda (Arduino IDE Serial Monitor açık olabilir)
                    System.Diagnostics.Debug.WriteLine($"[Arduino Connect] Port kullanımda hatası: {ex.Message}");
                    _serialPort?.Dispose();
                    _serialPort = null;
                    throw new ArduinoException(
                        $"Port '{portToUse}' zaten kullanımda!\n\n" +
                        "Çözüm:\n" +
                        "1. Arduino IDE'de Serial Monitor'ü KAPATIN (en önemli!)\n" +
                        "2. Başka bir programın portu kullanmadığından emin olun\n" +
                        "3. Arduino'yu USB'den çıkarıp tekrar takın", ex);
                }
                catch (ArgumentException ex)
                {
                    // Geçersiz port adı
                    System.Diagnostics.Debug.WriteLine($"[Arduino Connect] Geçersiz port hatası: {ex.Message}");
                    _serialPort?.Dispose();
                    _serialPort = null;
                    throw new ArduinoException(
                        $"Geçersiz port adı: '{portToUse}'\n\n" +
                        "Lütfen doğru COM portunu seçin.", ex);
                }
                catch (System.IO.IOException ex)
                {
                    // Port erişim hatası
                    System.Diagnostics.Debug.WriteLine($"[Arduino Connect] IO hatası: {ex.Message}");
                    _serialPort?.Dispose();
                    _serialPort = null;
                    throw new ArduinoException(
                        $"Port '{portToUse}' erişilemiyor!\n\n" +
                        "Olası nedenler:\n" +
                        "1. Arduino bağlı değil\n" +
                        "2. Port sürücüsü yüklü değil\n" +
                        "3. Port başka bir program tarafından kullanılıyor", ex);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"========================================");
                    System.Diagnostics.Debug.WriteLine($"[Arduino Connect] HATA: {ex.Message}");
                    System.Diagnostics.Debug.WriteLine($"[Arduino Connect] Hata tipi: {ex.GetType().Name}");
                    System.Diagnostics.Debug.WriteLine($"[Arduino Connect] StackTrace: {ex.StackTrace}");
                    System.Diagnostics.Debug.WriteLine("========================================");
                    _serialPort?.Dispose();
                    _serialPort = null;
                    throw new ArduinoException(
                        $"Arduino bağlantı hatası: {ex.Message}\n\n" +
                        $"Hata tipi: {ex.GetType().Name}\n\n" +
                        "Çözüm önerileri:\n" +
                        "1. Arduino IDE Serial Monitor'ü KAPATIN\n" +
                        "2. Arduino'yu USB'den çıkarıp tekrar takın\n" +
                        "3. Doğru COM portunu seçtiğinizden emin olun\n" +
                        "4. Arduino'nun çalıştığından emin olun (Serial Monitor'de test edin)", ex);
                }
            }
        }

        /// <summary>
        /// Arduino bağlantısını kapatır
        /// </summary>
        public void Disconnect()
        {
            lock (_lockObject)
            {
                // Okuma thread'ini durdur
                StopReadingThread();

                if (_serialPort?.IsOpen == true)
                {
                    try
                    {
                        _serialPort.Close();
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Arduino kapatma hatası: {ex.Message}");
                    }
                }

                _serialPort?.Dispose();
                _serialPort = null;
            }
        }

        /// <summary>
        /// Arduino'dan gelen mesajları okumak için thread başlatır
        /// </summary>
        private void StartReadingThread()
        {
            if (_readThread != null && _readThread.IsAlive)
            {
                System.Diagnostics.Debug.WriteLine("[StartReadingThread] Thread zaten çalışıyor!");
                return; // Zaten çalışıyor
            }

            System.Diagnostics.Debug.WriteLine("[StartReadingThread] Yeni thread oluşturuluyor...");
            _shouldRead = true;
            _readThread = new Thread(ReadSerialData)
            {
                IsBackground = true,
                Name = "ArduinoReadThread"
            };
            _readThread.Start();
            System.Diagnostics.Debug.WriteLine($"[StartReadingThread] Thread başlatıldı! IsAlive: {_readThread.IsAlive}");
        }

        /// <summary>
        /// Okuma thread'ini durdurur
        /// </summary>
        private void StopReadingThread()
        {
            _shouldRead = false;
            if (_readThread != null)
            {
                try
                {
                    if (_readThread.IsAlive)
                    {
                        _readThread.Join(1000); // 1 saniye bekle
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Thread durdurma hatası: {ex.Message}");
                }
                _readThread = null;
            }
        }

        /// <summary>
        /// Serial port'tan sürekli veri okur ve event tetikler
        /// </summary>
        private void ReadSerialData()
        {
            System.Diagnostics.Debug.WriteLine("[ReadSerialData] Thread başlatıldı");
            
            while (_shouldRead && !_disposed)
            {
                try
                {
                    SerialPort? port = null;
                    lock (_lockObject)
                    {
                        port = _serialPort;
                    }

                    if (port == null || !port.IsOpen)
                    {
                        System.Diagnostics.Debug.WriteLine("[ReadSerialData] Port kapalı veya null, bekleniyor...");
                        Thread.Sleep(100);
                        continue;
                    }

                    // Veri varsa oku
                    int bytesToRead = port.BytesToRead;
                    if (bytesToRead > 0)
                    {
                        System.Diagnostics.Debug.WriteLine($"[ReadSerialData] {bytesToRead} byte veri var, okunuyor...");
                        try
                        {
                            // ReadLine() yerine ReadExisting() kullanarak tüm buffer'ı oku
                            string data = port.ReadExisting();
                            System.Diagnostics.Debug.WriteLine($"[ReadSerialData] Ham veri alındı: '{data}'");
                            
                            // Satır satır işle
                            string[] lines = data.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                            foreach (string line in lines)
                            {
                                string trimmedLine = line.Trim();
                                if (!string.IsNullOrWhiteSpace(trimmedLine))
                                {
                                    System.Diagnostics.Debug.WriteLine($"[ReadSerialData] İşleniyor: '{trimmedLine}'");
                                    ProcessReceivedMessage(trimmedLine);
                                }
                            }
                        }
                        catch (TimeoutException ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ReadSerialData] Timeout: {ex.Message}");
                            // Timeout normal, devam et
                        }
                        catch (Exception ex)
                        {
                            System.Diagnostics.Debug.WriteLine($"[ReadSerialData] Serial okuma hatası: {ex.Message}");
                        }
                    }
                    else
                    {
                        Thread.Sleep(50); // Veri yoksa kısa bekle
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[ReadSerialData] Genel hata: {ex.Message}");
                    Thread.Sleep(100);
                }
            }
            
            System.Diagnostics.Debug.WriteLine("[ReadSerialData] Thread sonlandırıldı");
        }

        /// <summary>
        /// Arduino'dan gelen mesajı işler ve event tetikler
        /// </summary>
        private void ProcessReceivedMessage(string message)
        {
            System.Diagnostics.Debug.WriteLine($"[ProcessReceivedMessage] Ham mesaj: '{message}'");

            // Eksik mesajları birleştir (OK:UNLOCK + ED:KASA1 veya OK:LOC + KED:KASA1)
            if (_partialMessage != null)
            {
                message = _partialMessage + message;
                _partialMessage = null;
                System.Diagnostics.Debug.WriteLine($"[ProcessReceivedMessage] Eksik mesaj birleştirildi: '{message}'");
            }
            
            // Eksik mesaj kontrolü: OK:UNLOCK veya OK:LOC ile başlayan ama tamamlanmamış mesajlar
            if (message.Equals("OK:UNLOCK", StringComparison.OrdinalIgnoreCase) || 
                message.Equals("OK:LOC", StringComparison.OrdinalIgnoreCase) ||
                (message.StartsWith("OK:UNLOCK", StringComparison.OrdinalIgnoreCase) && !message.Contains(":")) ||
                (message.StartsWith("OK:LOC", StringComparison.OrdinalIgnoreCase) && !message.EndsWith(":", StringComparison.OrdinalIgnoreCase) && message.Split(':').Length < 3))
            {
                _partialMessage = message;
                System.Diagnostics.Debug.WriteLine($"[ProcessReceivedMessage] Eksik mesaj buffer'a alındı: '{_partialMessage}'");
                return; // Bir sonraki satırı bekle
            }
            
            // ED:KASA1 veya KED:KASA1 gibi devam mesajları
            if (message.StartsWith("ED:", StringComparison.OrdinalIgnoreCase) && _partialMessage != null && _partialMessage.Equals("OK:UNLOCK", StringComparison.OrdinalIgnoreCase))
            {
                message = "OK:UNLOCKED:" + message.Substring(3); // OK:UNLOCK + ED:KASA1 = OK:UNLOCKED:KASA1
                _partialMessage = null;
                System.Diagnostics.Debug.WriteLine($"[ProcessReceivedMessage] Eksik mesaj birleştirildi: '{message}'");
            }
            else if (message.StartsWith("KED:", StringComparison.OrdinalIgnoreCase) && _partialMessage != null && _partialMessage.Equals("OK:LOC", StringComparison.OrdinalIgnoreCase))
            {
                message = "OK:LOCKED:" + message.Substring(4); // OK:LOC + KED:KASA1 = OK:LOCKED:KASA1
                _partialMessage = null;
                System.Diagnostics.Debug.WriteLine($"[ProcessReceivedMessage] Eksik mesaj birleştirildi: '{message}'");
            }
            else if (message.StartsWith(":KASA1", StringComparison.OrdinalIgnoreCase) && _partialMessage != null && (_partialMessage.Equals("OK:LOCKED", StringComparison.OrdinalIgnoreCase) || _partialMessage.Equals("OK:UNLOCKED", StringComparison.OrdinalIgnoreCase)))
            {
                message = _partialMessage + message; // OK:LOCKED + :KASA1 = OK:LOCKED:KASA1
                _partialMessage = null;
                System.Diagnostics.Debug.WriteLine($"[ProcessReceivedMessage] Eksik mesaj birleştirildi: '{message}'");
            }

            var args = new ArduinoMessageEventArgs
            {
                Message = message
            };

            // Mesaj formatlarını parse et
            if (message.StartsWith("OK:UNLOCKED:", StringComparison.OrdinalIgnoreCase))
            {
                args.IsUnlocked = true;
                args.LockerId = message.Substring(12).Trim(); // "OK:UNLOCKED:" sonrası
                System.Diagnostics.Debug.WriteLine($"[Arduino] Parse: Kasa açıldı - LockerId: '{args.LockerId}'");
            }
            else if (message.StartsWith("OK:LOCKED:", StringComparison.OrdinalIgnoreCase))
            {
                args.IsLocked = true;
                args.LockerId = message.Substring(10).Trim(); // "OK:LOCKED:" sonrası
                System.Diagnostics.Debug.WriteLine($"[Arduino] Parse: Kasa kilitlendi - LockerId: '{args.LockerId}'");
            }
            else if (message.StartsWith("STATUS:", StringComparison.OrdinalIgnoreCase))
            {
                // STATUS:LOCKED:KASA1 formatı
                var parts = message.Split(':');
                System.Diagnostics.Debug.WriteLine($"[Arduino] Parse: STATUS mesajı - Parts: {string.Join("|", parts)}");
                if (parts.Length >= 3)
                {
                    args.LockerId = parts[2].Trim();
                    if (parts[1].Equals("LOCKED", StringComparison.OrdinalIgnoreCase))
                    {
                        args.IsLocked = true;
                    }
                    else if (parts[1].Equals("UNLOCKED", StringComparison.OrdinalIgnoreCase))
                    {
                        args.IsUnlocked = true;
                    }
                    System.Diagnostics.Debug.WriteLine($"[Arduino] Parse: STATUS - LockerId: '{args.LockerId}', IsLocked: {args.IsLocked}, IsUnlocked: {args.IsUnlocked}");
                }
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Arduino] Parse: Bilinmeyen mesaj formatı: '{message}'");
            }

            // Event'i tetikle (sadece LockerId varsa)
            if (!string.IsNullOrWhiteSpace(args.LockerId))
            {
                System.Diagnostics.Debug.WriteLine($"[Arduino] Event tetikleniyor - LockerId: '{args.LockerId}', IsUnlocked: {args.IsUnlocked}, IsLocked: {args.IsLocked}");
                MessageReceived?.Invoke(this, args);
            }
            else
            {
                System.Diagnostics.Debug.WriteLine($"[Arduino] Event tetiklenmedi - LockerId boş");
            }
        }

        /// <summary>
        /// Belirtilen kasayı açar
        /// </summary>
        /// <param name="lockerId">Açılacak kasa kimliği (örn: "KASA1")</param>
        /// <returns>Komut başarılı ise true</returns>
        public bool UnlockLocker(string lockerId)
        {
            if (string.IsNullOrWhiteSpace(lockerId))
            {
                throw new ArgumentException("Locker ID boş olamaz", nameof(lockerId));
            }

            lock (_lockObject)
            {
                if (!IsConnected)
                {
                    // Otomatik bağlanmayı dene
                    if (!Connect())
                    {
                        throw new ArduinoException("Arduino bağlantısı yok. Lütfen Arduino'yu bağlayın ve tekrar deneyin.");
                    }
                }

                try
                {
                    // Komut gönder: "UNLOCK:KASA1\n"
                    string command = $"UNLOCK:{lockerId.ToUpper()}\n";
                    byte[] commandBytes = Encoding.ASCII.GetBytes(command);
                    _serialPort!.Write(commandBytes, 0, commandBytes.Length);

                    System.Diagnostics.Debug.WriteLine($"[UnlockLocker] Komut gönderildi: {command.Trim()}");
                    
                    // Yanıt event-based olarak ReadSerialData thread'i tarafından işlenecek
                    // Burada yanıt bekleme gerekmiyor, sadece komut gönderildiğini logla
                            return true;
                        }
                catch (TimeoutException ex)
                {
                    throw new ArduinoException($"Arduino iletişim zaman aşımı: {ex.Message}", ex);
                }
                catch (InvalidOperationException ex)
                {
                    throw new ArduinoException($"Arduino port hatası: {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    throw new ArduinoException($"Arduino komut gönderme hatası: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Belirtilen kasayı kilitler
        /// </summary>
        /// <param name="lockerId">Kilitlenecek kasa kimliği (örn: "KASA1")</param>
        /// <returns>Komut başarılı ise true</returns>
        public bool LockLocker(string lockerId)
        {
            if (string.IsNullOrWhiteSpace(lockerId))
            {
                throw new ArgumentException("Locker ID boş olamaz", nameof(lockerId));
            }

            lock (_lockObject)
            {
                if (!IsConnected)
                {
                    // Otomatik bağlanmayı dene
                    if (!Connect())
                        {
                        throw new ArduinoException("Arduino bağlantısı yok. Lütfen Arduino'yu bağlayın ve tekrar deneyin.");
                        }
                    }

                try
                {
                    // Komut gönder: "LOCK:KASA1\n"
                    string command = $"LOCK:{lockerId.ToUpper()}\n";
                    byte[] commandBytes = Encoding.ASCII.GetBytes(command);
                    _serialPort!.Write(commandBytes, 0, commandBytes.Length);

                    System.Diagnostics.Debug.WriteLine($"[LockLocker] Komut gönderildi: {command.Trim()}");
                    
                    // Yanıt event-based olarak ReadSerialData thread'i tarafından işlenecek
                    // Burada yanıt bekleme gerekmiyor, sadece komut gönderildiğini logla
                    return true;
                }
                catch (TimeoutException ex)
                {
                    throw new ArduinoException($"Arduino iletişim zaman aşımı: {ex.Message}", ex);
                }
                catch (InvalidOperationException ex)
                {
                    throw new ArduinoException($"Arduino port hatası: {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    throw new ArduinoException($"Arduino komut gönderme hatası: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Arduino durumunu sorgular
        /// </summary>
        /// <returns>Durum bilgisi</returns>
        public string? GetStatus()
        {
            lock (_lockObject)
            {
                if (!IsConnected)
                {
                    return "DISCONNECTED";
                }

                try
                {
                    // Komut gönder: "STATUS\n"
                    string command = "STATUS\n";
                    byte[] commandBytes = Encoding.ASCII.GetBytes(command);
                    _serialPort!.Write(commandBytes, 0, commandBytes.Length);

                    // Yanıt bekle
                    Thread.Sleep(200);
                    if (_serialPort.BytesToRead > 0)
                    {
                        string response = _serialPort.ReadLine();
                        return response;
                    }

                    return "NO_RESPONSE";
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Status sorgulama hatası: {ex.Message}");
                    return $"ERROR:{ex.Message}";
                }
            }
        }

        /// <summary>
        /// Kasa durumunu Arduino'ya bildirir (LED gösterimi için)
        /// </summary>
        /// <param name="lockerId">Kasa kimliği (örn: "KASA1")</param>
        /// <param name="status">Durum: "AVAILABLE" (Yeşil), "RENTED" (Sarı), "LOCKED" (Kırmızı)</param>
        /// <returns>Komut başarılı ise true</returns>
        public bool SetLockerStatus(string lockerId, string status)
        {
            if (string.IsNullOrWhiteSpace(lockerId))
            {
                throw new ArgumentException("Locker ID boş olamaz", nameof(lockerId));
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                throw new ArgumentException("Status boş olamaz", nameof(status));
            }

            lock (_lockObject)
            {
                if (!IsConnected)
                {
                    // Otomatik bağlanmayı dene
                    if (!Connect())
                    {
                        System.Diagnostics.Debug.WriteLine("[SetLockerStatus] Arduino bağlantısı yok, komut gönderilemedi");
                        return false;
                    }
                }

                try
                {
                    // Komut gönder: "SET_STATUS:KASA1:AVAILABLE\n", "SET_STATUS:KASA1:RENTED\n", veya "SET_STATUS:KASA1:LOCKED\n"
                    string command = $"SET_STATUS:{lockerId.ToUpper()}:{status.ToUpper()}\n";
                    byte[] commandBytes = Encoding.ASCII.GetBytes(command);
                    _serialPort!.Write(commandBytes, 0, commandBytes.Length);

                    System.Diagnostics.Debug.WriteLine($"[SetLockerStatus] Komut gönderildi: {command.Trim()}");
                    
                    // Yanıt event-based olarak ReadSerialData thread'i tarafından işlenecek
                    // Burada yanıt bekleme gerekmiyor, sadece komut gönderildiğini logla
                    return true;
                }
                catch (TimeoutException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SetLockerStatus] Zaman aşımı: {ex.Message}");
                    return false;
                }
                catch (InvalidOperationException ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SetLockerStatus] Port hatası: {ex.Message}");
                    return false;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"[SetLockerStatus] Hata: {ex.Message}");
                    return false;
                }
            }
        }

        /// <summary>
        /// Dispose pattern
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                _shouldRead = false;
                Disconnect();
                StopReadingThread();
                _disposed = true;
            }
        }
    }
}

