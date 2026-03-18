using System;

namespace LockboxQr
{
    /// <summary>
    /// Token üretimi ve doğrulama işlemleri için interface
    /// </summary>
    public interface ITokenService
    {
        /// <summary>
        /// Yeni bir token üretir
        /// </summary>
        /// <param name="lockerId">Kasa kimliği</param>
        /// <param name="validityMinutes">Geçerlilik süresi (dakika)</param>
        /// <param name="oneTime">Tek seferlik kullanım</param>
        /// <returns>Üretilen token string</returns>
        /// <exception cref="ArgumentException">Geçersiz parametreler için</exception>
        string GenerateToken(string lockerId, int validityMinutes, bool oneTime);

        /// <summary>
        /// Token'ı doğrular
        /// </summary>
        /// <param name="token">Doğrulanacak token</param>
        /// <returns>Doğrulama sonucu, token verisi ve hata mesajı</returns>
        (bool IsValid, TokenService.TokenData? Data, string? Error) ValidateToken(string token);
    }

    /// <summary>
    /// Tek seferlik token takibi için interface
    /// </summary>
    public interface INonceTracker
    {
        /// <summary>
        /// Nonce'un daha önce kullanılıp kullanılmadığını kontrol eder
        /// </summary>
        /// <param name="nonce">Kontrol edilecek nonce</param>
        /// <returns>Kullanılmışsa true, değilse false</returns>
        bool IsNonceUsed(string nonce);

        /// <summary>
        /// Nonce'u kullanılmış olarak işaretler
        /// </summary>
        /// <param name="nonce">İşaretlenecek nonce</param>
        /// <param name="lockerId">İlişkili kasa kimliği</param>
        /// <exception cref="NonceException">Nonce işaretleme hatası</exception>
        void MarkNonceAsUsed(string nonce, string lockerId);
    }

    /// <summary>
    /// Erişim loglama işlemleri için interface
    /// </summary>
    public interface IAccessLogger
    {
        /// <summary>
        /// Erişim denemesini loglar
        /// </summary>
        /// <param name="lockerId">Kasa kimliği</param>
        /// <param name="result">Sonuç (OK veya ERR)</param>
        /// <param name="reason">Başarı/Hata nedeni</param>
        /// <param name="nonce">Token nonce değeri</param>
        /// <param name="expUtc">Token sona erme zamanı (UTC epoch saniye)</param>
        /// <exception cref="LoggingException">Loglama hatası</exception>
        void Log(string lockerId, string result, string reason, string nonce, long expUtc);
    }

    /// <summary>
    /// Arduino mesaj event argümanları
    /// </summary>
    public class ArduinoMessageEventArgs : EventArgs
    {
        public string Message { get; set; } = string.Empty;
        public string? LockerId { get; set; }
        public bool IsUnlocked { get; set; }
        public bool IsLocked { get; set; }
    }

    /// <summary>
    /// Arduino ile iletişim için interface
    /// </summary>
    public interface IArduinoService
    {
        /// <summary>
        /// Arduino'dan mesaj geldiğinde tetiklenen event
        /// </summary>
        event EventHandler<ArduinoMessageEventArgs>? MessageReceived;

        /// <summary>
        /// Arduino bağlantısını başlatır
        /// </summary>
        /// <param name="portName">Port adı (null ise config'den okunur)</param>
        /// <returns>Bağlantı başarılı ise true</returns>
        bool Connect(string? portName = null);

        /// <summary>
        /// Arduino bağlantısını kapatır
        /// </summary>
        void Disconnect();

        /// <summary>
        /// Arduino'nun bağlı olup olmadığını kontrol eder
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Belirtilen kasayı açar
        /// </summary>
        /// <param name="lockerId">Açılacak kasa kimliği (örn: "KASA1")</param>
        /// <returns>Komut başarılı ise true</returns>
        /// <exception cref="ArduinoException">Arduino iletişim hatası</exception>
        bool UnlockLocker(string lockerId);

        /// <summary>
        /// Belirtilen kasayı kilitler
        /// </summary>
        /// <param name="lockerId">Kilitlenecek kasa kimliği (örn: "KASA1")</param>
        /// <returns>Komut başarılı ise true</returns>
        /// <exception cref="ArduinoException">Arduino iletişim hatası</exception>
        bool LockLocker(string lockerId);

        /// <summary>
        /// Arduino durumunu sorgular
        /// </summary>
        /// <returns>Durum bilgisi</returns>
        string? GetStatus();

        /// <summary>
        /// Kasa durumunu Arduino'ya bildirir (LED gösterimi için)
        /// </summary>
        /// <param name="lockerId">Kasa kimliği (örn: "KASA1")</param>
        /// <param name="status">Durum: "AVAILABLE" (Yeşil), "RENTED" (Sarı), "LOCKED" (Kırmızı)</param>
        /// <returns>Komut başarılı ise true</returns>
        /// <exception cref="ArduinoException">Arduino iletişim hatası</exception>
        bool SetLockerStatus(string lockerId, string status);
    }
}

