using System;
using System.Security.Cryptography;
using System.Text;

namespace LockboxQr
{
    /// <summary>
    /// Token üretimi ve doğrulama servisi
    /// </summary>
    public class TokenService : ITokenService
    {
        private readonly AppConfiguration _config;

        /// <summary>
        /// Token veri yapısı
        /// </summary>
        public class TokenData
        {
            /// <summary>
            /// Kasa kimliği
            /// </summary>
            public string LockerId { get; set; } = string.Empty;
            
            /// <summary>
            /// Token oluşturulma zamanı (UTC epoch saniye)
            /// </summary>
            public long Iat { get; set; }
            
            /// <summary>
            /// Token sona erme zamanı (UTC epoch saniye)
            /// </summary>
            public long Exp { get; set; }
            
            /// <summary>
            /// Tek seferlik kullanım (1) veya çoklu kullanım (0)
            /// </summary>
            public int Once { get; set; }
            
            /// <summary>
            /// Benzersiz tanımlayıcı (GUID)
            /// </summary>
            public string Nonce { get; set; } = string.Empty;
            
            /// <summary>
            /// HMAC-SHA256 imza
            /// </summary>
            public string Sig { get; set; } = string.Empty;
        }

        /// <summary>
        /// TokenService constructor
        /// </summary>
        public TokenService() : this(AppConfiguration.Instance) { }

        /// <summary>
        /// TokenService constructor (test için dependency injection)
        /// </summary>
        /// <param name="config">Yapılandırma instance'ı</param>
        public TokenService(AppConfiguration config)
        {
            _config = config ?? throw new ArgumentNullException(nameof(config));
        }

        /// <summary>
        /// Yeni bir token üretir
        /// </summary>
        /// <param name="lockerId">Kasa kimliği</param>
        /// <param name="validityMinutes">Geçerlilik süresi (dakika)</param>
        /// <param name="oneTime">Tek seferlik kullanım</param>
        /// <returns>Üretilen token string</returns>
        /// <exception cref="ArgumentException">Geçersiz parametreler için</exception>
        /// <exception cref="TokenException">Token üretim hatası</exception>
        public string GenerateToken(string lockerId, int validityMinutes, bool oneTime)
        {
            try
            {
                // Input validation
                if (!InputValidator.ValidateLockerId(lockerId, out string lockerIdError))
                {
                    throw new ArgumentException(lockerIdError, nameof(lockerId));
                }
                
                if (!InputValidator.ValidateValidityMinutes(validityMinutes, out string validityError))
                {
                    throw new ArgumentException(validityError, nameof(validityMinutes));
                }
                
                var now = DateTimeOffset.UtcNow;
                var iat = now.ToUnixTimeSeconds();
                var exp = now.AddMinutes(validityMinutes).ToUnixTimeSeconds();
                var once = oneTime ? 1 : 0;
                var nonce = Guid.NewGuid().ToString("N");

                var payload = $"LCK|locker={lockerId.Trim()}|iat={iat}|exp={exp}|once={once}|nonce={nonce}";
                var sig = ComputeSignature(payload);

                return $"{payload}|sig={sig}";
            }
            catch (ArgumentException)
            {
                throw; // Re-throw validation exceptions
            }
            catch (Exception ex)
            {
                throw new TokenException("Token üretilirken hata oluştu", ex);
            }
        }

        /// <summary>
        /// Token'ı doğrular
        /// </summary>
        /// <param name="token">Doğrulanacak token</param>
        /// <returns>Doğrulama sonucu, token verisi ve hata mesajı</returns>
        public (bool IsValid, TokenData? Data, string? Error) ValidateToken(string token)
        {
            try
            {
                // Input validation - Token format
                if (!InputValidator.ValidateTokenFormat(token, out string formatError))
                {
                    return (false, null, formatError);
                }
                
                var data = ParseToken(token);
                if (data == null)
                {
                    return (false, null, "Yanlış/bozuk kod");
                }

                // Verify signature
                var payload = $"LCK|locker={data.LockerId}|iat={data.Iat}|exp={data.Exp}|once={data.Once}|nonce={data.Nonce}";
                var expectedSig = ComputeSignature(payload);
                if (data.Sig != expectedSig)
                {
                    return (false, data, "Yanlış/bozuk kod");
                }

                // Check expiration
                var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                var tolerance = _config.TimeToleranceSeconds;
                if (now > data.Exp + tolerance)
                {
                    return (false, data, "Süresi geçti");
                }

                return (true, data, null);
            }
            catch (TokenFormatException ex)
            {
                return (false, null, ex.Message);
            }
            catch (TokenSignatureException ex)
            {
                return (false, null, ex.Message);
            }
            catch (TokenExpiredException ex)
            {
                return (false, null, ex.Message);
            }
            catch (Exception ex)
            {
                return (false, null, $"Token doğrulanırken hata: {ex.Message}");
            }
        }

        /// <summary>
        /// Token string'ini parse eder
        /// </summary>
        /// <param name="token">Parse edilecek token</param>
        /// <returns>TokenData veya null</returns>
        /// <exception cref="TokenFormatException">Token formatı geçersiz</exception>
        private TokenData? ParseToken(string token)
        {
            try
            {
                var parts = token.Split('|');
                if (parts.Length < 6 || parts[0] != "LCK")
                {
                    throw new TokenFormatException("Token formatı geçersiz: LCK prefix bulunamadı");
                }

                var data = new TokenData();

                foreach (var part in parts)
                {
                    if (part.StartsWith("locker="))
                        data.LockerId = part.Substring(7);
                    else if (part.StartsWith("iat="))
                        data.Iat = long.Parse(part.Substring(4));
                    else if (part.StartsWith("exp="))
                        data.Exp = long.Parse(part.Substring(4));
                    else if (part.StartsWith("once="))
                        data.Once = int.Parse(part.Substring(5));
                    else if (part.StartsWith("nonce="))
                        data.Nonce = part.Substring(6);
                    else if (part.StartsWith("sig="))
                        data.Sig = part.Substring(4);
                }

                // Gerekli alanların dolu olduğunu kontrol et
                if (string.IsNullOrEmpty(data.LockerId) || 
                    data.Iat == 0 || 
                    data.Exp == 0 || 
                    string.IsNullOrEmpty(data.Nonce) || 
                    string.IsNullOrEmpty(data.Sig))
                {
                    throw new TokenFormatException("Token eksik alanlar içeriyor");
                }

                return data;
            }
            catch (TokenFormatException)
            {
                throw;
            }
            catch (FormatException ex)
            {
                throw new TokenFormatException($"Token parse hatası: {ex.Message}", ex);
            }
            catch (Exception ex)
            {
                throw new TokenFormatException($"Token parse edilemedi: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// HMAC-SHA256 ile imza hesaplar
        /// </summary>
        /// <param name="payload">İmzalanacak payload</param>
        /// <returns>Hesaplanan imza (hex string)</returns>
        /// <exception cref="TokenException">İmza hesaplama hatası</exception>
        private string ComputeSignature(string payload)
        {
            try
            {
                var secretKey = _config.SecretKey;
                if (string.IsNullOrEmpty(secretKey))
                {
                    throw new TokenException("Secret key yapılandırmada bulunamadı!");
                }

                using (var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secretKey)))
                {
                    var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
                    return BitConverter.ToString(hash).Replace("-", "").ToLowerInvariant();
                }
            }
            catch (Exception ex)
            {
                throw new TokenException("İmza hesaplanırken hata oluştu", ex);
            }
        }
    }
}
