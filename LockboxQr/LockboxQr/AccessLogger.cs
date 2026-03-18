using System;
using System.Globalization;
using System.IO;

namespace LockboxQr
{
    /// <summary>
    /// Erişim loglama servisi
    /// </summary>
    public class AccessLogger : IAccessLogger
    {
        private readonly string _logFile;
        private static readonly object _lock = new object();

        /// <summary>
        /// AccessLogger constructor
        /// </summary>
        public AccessLogger() : this(AppConfiguration.Instance) { }

        /// <summary>
        /// AccessLogger constructor (test için dependency injection)
        /// </summary>
        /// <param name="config">Yapılandırma instance'ı</param>
        public AccessLogger(AppConfiguration config)
        {
            _logFile = config?.AccessLogFile ?? "logs/access.csv";
            EnsureLogFileExists();
        }

        /// <summary>
        /// Erişim denemesini loglar
        /// </summary>
        /// <param name="lockerId">Kasa kimliği</param>
        /// <param name="result">Sonuç (OK veya ERR)</param>
        /// <param name="reason">Başarı/Hata nedeni</param>
        /// <param name="nonce">Token nonce değeri</param>
        /// <param name="expUtc">Token sona erme zamanı (UTC epoch saniye)</param>
        /// <exception cref="ArgumentException">Geçersiz parametreler</exception>
        /// <exception cref="LoggingException">Loglama hatası</exception>
        public void Log(string lockerId, string result, string reason, string nonce, long expUtc)
        {
            if (string.IsNullOrEmpty(result))
            {
                throw new ArgumentException("Result boş olamaz", nameof(result));
            }

            lock (_lock)
            {
                try
                {
                    var dir = Path.GetDirectoryName(_logFile);
                    if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                    {
                        Directory.CreateDirectory(dir);
                    }

                    var timestamp = DateTimeOffset.UtcNow.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
                    var expStr = expUtc > 0 
                        ? DateTimeOffset.FromUnixTimeSeconds(expUtc).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)
                        : "N/A";
                    
                    var line = $"{timestamp},{EscapeCsv(lockerId ?? "UNKNOWN")},{EscapeCsv(result)},{EscapeCsv(reason ?? "")},{EscapeCsv(nonce ?? "")},{expStr}";
                    
                    File.AppendAllText(_logFile, line + Environment.NewLine);
                }
                catch (IOException ex)
                {
                    throw new LoggingException($"Log dosyası yazılamadı: {ex.Message}", ex);
                }
                catch (UnauthorizedAccessException ex)
                {
                    throw new LoggingException($"Log dosyasına erişim reddedildi: {ex.Message}", ex);
                }
                catch (Exception ex)
                {
                    throw new LoggingException($"Loglama sırasında hata: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Log dosyasının varlığını garanti eder
        /// </summary>
        /// <exception cref="LoggingException">Dosya oluşturma hatası</exception>
        private void EnsureLogFileExists()
        {
            try
            {
                var dir = Path.GetDirectoryName(_logFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (!File.Exists(_logFile))
                {
                    File.WriteAllText(_logFile, "timestamp_utc,locker_id,result,reason,nonce,exp_utc" + Environment.NewLine);
                }
            }
            catch (IOException ex)
            {
                throw new LoggingException($"Log dosyası oluşturulamadı: {ex.Message}", ex);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new LoggingException($"Log dizinine erişim reddedildi: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new LoggingException($"Log dosyası kontrolü sırasında hata: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// CSV değerini escape eder
        /// </summary>
        /// <param name="value">Escape edilecek değer</param>
        /// <returns>Escape edilmiş değer</returns>
        private string EscapeCsv(string value)
        {
            if (string.IsNullOrEmpty(value))
                return string.Empty;

            if (value.Contains(",") || value.Contains("\"") || value.Contains("\n"))
            {
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            }
            return value;
        }
    }
}

