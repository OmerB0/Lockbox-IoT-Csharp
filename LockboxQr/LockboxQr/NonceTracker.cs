using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace LockboxQr
{
    /// <summary>
    /// Tek seferlik token takibi için servis
    /// </summary>
    public class NonceTracker : INonceTracker
    {
        private readonly string _usedNoncesFile;
        private readonly HashSet<string> _usedNonces;
        private readonly Dictionary<string, NonceRecord> _nonceRecords;
        private readonly object _lock = new object();

        /// <summary>
        /// NonceTracker constructor
        /// </summary>
        public NonceTracker() : this(AppConfiguration.Instance) { }

        /// <summary>
        /// NonceTracker constructor (test için dependency injection)
        /// </summary>
        /// <param name="config">Yapılandırma instance'ı</param>
        public NonceTracker(AppConfiguration config)
        {
            _usedNoncesFile = config?.UsedNoncesFile ?? "logs/used_nonces.json";
            _usedNonces = new HashSet<string>();
            _nonceRecords = new Dictionary<string, NonceRecord>();
            LoadUsedNonces();
        }

        /// <summary>
        /// Nonce'un daha önce kullanılıp kullanılmadığını kontrol eder
        /// </summary>
        /// <param name="nonce">Kontrol edilecek nonce</param>
        /// <returns>Kullanılmışsa true, değilse false</returns>
        public bool IsNonceUsed(string nonce)
        {
            if (string.IsNullOrEmpty(nonce))
            {
                return false;
            }

            lock (_lock)
            {
                return _usedNonces.Contains(nonce);
            }
        }

        /// <summary>
        /// Nonce'u kullanılmış olarak işaretler
        /// </summary>
        /// <param name="nonce">İşaretlenecek nonce</param>
        /// <param name="lockerId">İlişkili kasa kimliği</param>
        /// <exception cref="ArgumentException">Geçersiz parametreler</exception>
        /// <exception cref="NonceException">Nonce işaretleme hatası</exception>
        public void MarkNonceAsUsed(string nonce, string lockerId)
        {
            if (string.IsNullOrEmpty(nonce))
            {
                throw new ArgumentException("Nonce boş olamaz", nameof(nonce));
            }

            if (string.IsNullOrEmpty(lockerId))
            {
                throw new ArgumentException("Locker ID boş olamaz", nameof(lockerId));
            }

            lock (_lock)
            {
                if (_usedNonces.Contains(nonce))
                {
                    return; // Zaten işaretlenmiş
                }

                try
                {
                    _usedNonces.Add(nonce);
                    _nonceRecords[nonce] = new NonceRecord
                    {
                        Nonce = nonce,
                        LockerId = lockerId,
                        FirstUsedAt = DateTimeOffset.UtcNow
                    };
                    SaveUsedNonces();
                }
                catch (Exception ex)
                {
                    // Rollback
                    _usedNonces.Remove(nonce);
                    _nonceRecords.Remove(nonce);
                    throw new NonceException($"Nonce işaretlenirken hata: {ex.Message}", ex);
                }
            }
        }

        /// <summary>
        /// Kullanılmış nonce'ları dosyadan yükler
        /// </summary>
        /// <exception cref="NonceException">Yükleme hatası</exception>
        private void LoadUsedNonces()
        {
            try
            {
                var dir = Path.GetDirectoryName(_usedNoncesFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                if (File.Exists(_usedNoncesFile))
                {
                    var json = File.ReadAllText(_usedNoncesFile);
                    if (!string.IsNullOrWhiteSpace(json))
                    {
                        var records = JsonSerializer.Deserialize<List<NonceRecord>>(json);
                        if (records != null)
                        {
                            foreach (var record in records)
                            {
                                if (!string.IsNullOrEmpty(record.Nonce))
                                {
                                    _usedNonces.Add(record.Nonce);
                                    _nonceRecords[record.Nonce] = record;
                                }
                            }
                        }
                    }
                }
            }
            catch (JsonException ex)
            {
                throw new NonceException($"Nonce dosyası parse edilemedi: {ex.Message}", ex);
            }
            catch (IOException ex)
            {
                throw new NonceException($"Nonce dosyası okunamadı: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new NonceException($"Nonce yüklenirken hata: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Kullanılmış nonce'ları dosyaya kaydeder
        /// </summary>
        /// <exception cref="NonceException">Kaydetme hatası</exception>
        private void SaveUsedNonces()
        {
            try
            {
                var dir = Path.GetDirectoryName(_usedNoncesFile);
                if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
                {
                    Directory.CreateDirectory(dir);
                }

                var records = _nonceRecords.Values.ToList();
                var json = JsonSerializer.Serialize(records, new JsonSerializerOptions { WriteIndented = true });
                
                // Atomic write: önce temp dosyaya yaz, sonra değiştir
                var tempFile = _usedNoncesFile + ".tmp";
                File.WriteAllText(tempFile, json);
                File.Move(tempFile, _usedNoncesFile, overwrite: true);
            }
            catch (IOException ex)
            {
                throw new NonceException($"Nonce dosyası yazılamadı: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new NonceException($"Nonce kaydedilirken hata: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// Nonce kayıt veri yapısı
        /// </summary>
        private class NonceRecord
        {
            public string Nonce { get; set; } = string.Empty;
            public string LockerId { get; set; } = string.Empty;
            public DateTimeOffset FirstUsedAt { get; set; }
        }
    }
}
