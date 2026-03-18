using System;
using System.Threading;
using System.Threading.Tasks;

namespace LockboxQr
{
    /// <summary>
    /// Arduino simülatör servisi - Gerçek Arduino olmadan test için
    /// </summary>
    public class ArduinoSimulatorService : IArduinoService, IDisposable
    {
        private bool _isConnected = false;
        private bool _disposed = false;

        /// <summary>
        /// Arduino'dan mesaj geldiğinde tetiklenen event
        /// </summary>
        public event EventHandler<ArduinoMessageEventArgs>? MessageReceived;

        /// <summary>
        /// Arduino bağlantı durumu (simülatörde her zaman true)
        /// </summary>
        public bool IsConnected => _isConnected && !_disposed;

        /// <summary>
        /// Arduino bağlantısını başlatır (simülatörde her zaman başarılı)
        /// </summary>
        public bool Connect(string? portName = null)
        {
            if (_disposed)
                return false;

            _isConnected = true;
            System.Diagnostics.Debug.WriteLine("[ArduinoSimulator] Simülatör bağlantısı kuruldu");
            return true;
        }

        /// <summary>
        /// Arduino bağlantısını kapatır
        /// </summary>
        public void Disconnect()
        {
            _isConnected = false;
            System.Diagnostics.Debug.WriteLine("[ArduinoSimulator] Simülatör bağlantısı kesildi");
        }

        /// <summary>
        /// Belirtilen kasayı açar (simülatörde mesaj gönderir)
        /// </summary>
        public bool UnlockLocker(string lockerId)
        {
            if (!IsConnected)
            {
                throw new ArduinoException("Simülatör bağlantısı yok");
            }

            if (string.IsNullOrWhiteSpace(lockerId))
            {
                throw new ArgumentException("Locker ID boş olamaz", nameof(lockerId));
            }

            System.Diagnostics.Debug.WriteLine($"[ArduinoSimulator] UNLOCK komutu simüle edildi: {lockerId}");

            // Simüle edilmiş mesaj gönder (async olarak)
            Task.Run(() =>
            {
                Thread.Sleep(100); // Kısa bir gecikme
                var args = new ArduinoMessageEventArgs
                {
                    Message = $"OK:UNLOCKED:{lockerId}",
                    LockerId = lockerId,
                    IsUnlocked = true
                };
                MessageReceived?.Invoke(this, args);

                // 15 saniye sonra otomatik kilitlen (simüle et)
                Task.Run(async () =>
                {
                    await Task.Delay(15000);
                    if (IsConnected)
                    {
                        var lockArgs = new ArduinoMessageEventArgs
                        {
                            Message = $"OK:LOCKED:{lockerId}",
                            LockerId = lockerId,
                            IsLocked = true
                        };
                        MessageReceived?.Invoke(this, lockArgs);
                    }
                });
            });

            return true;
        }

        /// <summary>
        /// Belirtilen kasayı kilitler (simülatörde mesaj gönderir)
        /// </summary>
        public bool LockLocker(string lockerId)
        {
            if (!IsConnected)
            {
                throw new ArduinoException("Simülatör bağlantısı yok");
            }

            if (string.IsNullOrWhiteSpace(lockerId))
            {
                throw new ArgumentException("Locker ID boş olamaz", nameof(lockerId));
            }

            System.Diagnostics.Debug.WriteLine($"[ArduinoSimulator] LOCK komutu simüle edildi: {lockerId}");

            // Simüle edilmiş mesaj gönder (async olarak)
            Task.Run(() =>
            {
                Thread.Sleep(100); // Kısa bir gecikme
                var args = new ArduinoMessageEventArgs
                {
                    Message = $"OK:LOCKED:{lockerId}",
                    LockerId = lockerId,
                    IsLocked = true
                };
                MessageReceived?.Invoke(this, args);
            });

            return true;
        }

        /// <summary>
        /// Arduino durumunu sorgular (simülatörde her zaman "SIMULATOR_MODE")
        /// </summary>
        public string? GetStatus()
        {
            if (!IsConnected)
            {
                return "DISCONNECTED";
            }

            return "STATUS:SIMULATOR_MODE:READY";
        }

        /// <summary>
        /// Kasa durumunu Arduino'ya bildirir (simülatörde sadece loglama yapar)
        /// </summary>
        /// <param name="lockerId">Kasa kimliği (örn: "KASA1")</param>
        /// <param name="status">Durum: "AVAILABLE" (Yeşil), "RENTED" (Sarı), "LOCKED" (Kırmızı)</param>
        /// <returns>Komut başarılı ise true</returns>
        public bool SetLockerStatus(string lockerId, string status)
        {
            if (!IsConnected)
            {
                System.Diagnostics.Debug.WriteLine("[ArduinoSimulator] SetLockerStatus: Simülatör bağlantısı yok");
                return false;
            }

            if (string.IsNullOrWhiteSpace(lockerId))
            {
                throw new ArgumentException("Locker ID boş olamaz", nameof(lockerId));
            }

            if (string.IsNullOrWhiteSpace(status))
            {
                throw new ArgumentException("Status boş olamaz", nameof(status));
            }

            System.Diagnostics.Debug.WriteLine($"[ArduinoSimulator] SET_STATUS komutu simüle edildi: {lockerId} -> {status}");
            
            // Simülatörde sadece loglama yapıyoruz, fiziksel LED yok
            return true;
        }

        /// <summary>
        /// Dispose pattern
        /// </summary>
        public void Dispose()
        {
            if (!_disposed)
            {
                Disconnect();
                _disposed = true;
            }
        }
    }
}

