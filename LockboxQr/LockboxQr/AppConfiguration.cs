using System;
using System.IO;
using Microsoft.Extensions.Configuration;

namespace LockboxQr
{
    /// <summary>
    /// Uygulama yapılandırma ayarlarını yönetir
    /// </summary>
    public class AppConfiguration
    {
        private static AppConfiguration? _instance;
        private readonly IConfiguration _configuration;

        private AppConfiguration()
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true);

            _configuration = builder.Build();
        }

        /// <summary>
        /// Singleton instance
        /// </summary>
        public static AppConfiguration Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new AppConfiguration();
                }
                return _instance;
            }
        }

        /// <summary>
        /// Secret key değerini döndürür
        /// </summary>
        public string SecretKey => _configuration["AppSettings:SecretKey"] 
            ?? throw new InvalidOperationException("SecretKey yapılandırmada bulunamadı!");

        /// <summary>
        /// Varsayılan geçerlilik süresi (dakika)
        /// </summary>
        public int DefaultValidityMinutes => int.Parse(_configuration["AppSettings:DefaultValidityMinutes"] ?? "15");

        /// <summary>
        /// Zaman toleransı (saniye)
        /// </summary>
        public int TimeToleranceSeconds => int.Parse(_configuration["AppSettings:TimeToleranceSeconds"] ?? "20");

        /// <summary>
        /// Debounce süresi (milisaniye)
        /// </summary>
        public int DebounceMilliseconds => int.Parse(_configuration["AppSettings:DebounceMilliseconds"] ?? "1500");

        /// <summary>
        /// Başarı overlay süresi (saniye)
        /// </summary>
        public int SuccessOverlayDurationSeconds => int.Parse(_configuration["AppSettings:SuccessOverlayDurationSeconds"] ?? "5");

        /// <summary>
        /// Log dizini
        /// </summary>
        public string LogDirectory => _configuration["Paths:LogDirectory"] ?? "logs";

        /// <summary>
        /// Output dizini
        /// </summary>
        public string OutputDirectory => _configuration["Paths:OutputDirectory"] ?? "out";

        /// <summary>
        /// Erişim log dosyası yolu
        /// </summary>
        public string AccessLogFile => _configuration["Paths:AccessLogFile"] ?? "logs/access.csv";

        /// <summary>
        /// Kullanılmış nonce dosyası yolu
        /// </summary>
        public string UsedNoncesFile => _configuration["Paths:UsedNoncesFile"] ?? "logs/used_nonces.json";

        /// <summary>
        /// Minimum locker ID uzunluğu
        /// </summary>
        public int MinLockerIdLength => int.Parse(_configuration["Validation:MinLockerIdLength"] ?? "1");

        /// <summary>
        /// Maximum locker ID uzunluğu
        /// </summary>
        public int MaxLockerIdLength => int.Parse(_configuration["Validation:MaxLockerIdLength"] ?? "50");

        /// <summary>
        /// Minimum geçerlilik süresi (dakika)
        /// </summary>
        public int MinValidityMinutes => int.Parse(_configuration["Validation:MinValidityMinutes"] ?? "1");

        /// <summary>
        /// Maximum geçerlilik süresi (dakika)
        /// </summary>
        public int MaxValidityMinutes => int.Parse(_configuration["Validation:MaxValidityMinutes"] ?? "1440");

        /// <summary>
        /// Arduino entegrasyonu etkin mi?
        /// </summary>
        public bool ArduinoEnabled => bool.Parse(_configuration["Arduino:Enabled"] ?? "false");

        /// <summary>
        /// Arduino Serial Port adı (örn: "COM3", "/dev/ttyUSB0")
        /// </summary>
        public string ArduinoPortName => _configuration["Arduino:PortName"] ?? "COM3";

        /// <summary>
        /// Arduino Baud Rate
        /// </summary>
        public int ArduinoBaudRate => int.Parse(_configuration["Arduino:BaudRate"] ?? "9600");

        /// <summary>
        /// Arduino okuma zaman aşımı (milisaniye)
        /// </summary>
        public int ArduinoReadTimeout => int.Parse(_configuration["Arduino:ReadTimeout"] ?? "1000");

        /// <summary>
        /// Arduino yazma zaman aşımı (milisaniye)
        /// </summary>
        public int ArduinoWriteTimeout => int.Parse(_configuration["Arduino:WriteTimeout"] ?? "1000");

        /// <summary>
        /// Arduino otomatik bağlanma
        /// </summary>
        public bool ArduinoAutoConnect => bool.Parse(_configuration["Arduino:AutoConnect"] ?? "true");

        /// <summary>
        /// Arduino simülatör modu kullanılsın mı?
        /// </summary>
        public bool ArduinoUseSimulator => bool.Parse(_configuration["Arduino:UseSimulator"] ?? "false");
    }
}

