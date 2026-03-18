using System;
using System.Text.RegularExpressions;

namespace LockboxQr
{
    /// <summary>
    /// Kullanıcı girdilerini doğrulamak için validation sınıfı
    /// </summary>
    public static class InputValidator
    {
        private static AppConfiguration Config => AppConfiguration.Instance;
        
        // Token formatında kullanılan özel karakterler - injection riski
        private static readonly char[] FORBIDDEN_CHARS = { '|', '=', '\n', '\r', '\t' };
        
        /// <summary>
        /// Locker ID'yi doğrular
        /// </summary>
        /// <param name="lockerId">Doğrulanacak locker ID</param>
        /// <param name="errorMessage">Hata durumunda döndürülecek mesaj</param>
        /// <returns>Geçerli ise true, değilse false</returns>
        public static bool ValidateLockerId(string lockerId, out string errorMessage)
        {
            errorMessage = string.Empty;
            
            if (string.IsNullOrWhiteSpace(lockerId))
            {
                errorMessage = "Locker ID boş olamaz!";
                return false;
            }
            
            var trimmed = lockerId.Trim();
            var minLength = Config.MinLockerIdLength;
            var maxLength = Config.MaxLockerIdLength;
            
            if (trimmed.Length < minLength)
            {
                errorMessage = $"Locker ID en az {minLength} karakter olmalıdır!";
                return false;
            }
            
            if (trimmed.Length > maxLength)
            {
                errorMessage = $"Locker ID en fazla {maxLength} karakter olabilir!";
                return false;
            }
            
            // Özel karakter kontrolü - injection riski önleme
            foreach (var forbiddenChar in FORBIDDEN_CHARS)
            {
                if (trimmed.Contains(forbiddenChar))
                {
                    errorMessage = $"Locker ID '{forbiddenChar}' karakterini içeremez!";
                    return false;
                }
            }
            
            return true;
        }
        
        /// <summary>
        /// Geçerlilik süresini (dakika) doğrular
        /// </summary>
        /// <param name="validityMinutes">Doğrulanacak geçerlilik süresi</param>
        /// <param name="errorMessage">Hata durumunda döndürülecek mesaj</param>
        /// <returns>Geçerli ise true, değilse false</returns>
        public static bool ValidateValidityMinutes(int validityMinutes, out string errorMessage)
        {
            errorMessage = string.Empty;
            var min = Config.MinValidityMinutes;
            var max = Config.MaxValidityMinutes;
            
            if (validityMinutes < min)
            {
                errorMessage = $"Geçerlilik süresi en az {min} dakika olmalıdır!";
                return false;
            }
            
            if (validityMinutes > max)
            {
                errorMessage = $"Geçerlilik süresi en fazla {max} dakika (24 saat) olabilir!";
                return false;
            }
            
            return true;
        }
        
        /// <summary>
        /// Token string'inin temel format kontrolünü yapar (injection riski önleme)
        /// </summary>
        /// <param name="token">Doğrulanacak token</param>
        /// <param name="errorMessage">Hata durumunda döndürülecek mesaj</param>
        /// <returns>Geçerli ise true, değilse false</returns>
        public static bool ValidateTokenFormat(string token, out string errorMessage)
        {
            errorMessage = string.Empty;
            
            if (string.IsNullOrWhiteSpace(token))
            {
                errorMessage = "Token boş olamaz!";
                return false;
            }
            
            // Çok uzun token kontrolü (DoS saldırısı önleme)
            if (token.Length > 1000)
            {
                errorMessage = "Token çok uzun!";
                return false;
            }
            
            // Temel format kontrolü - LCK ile başlamalı
            if (!token.StartsWith("LCK|"))
            {
                errorMessage = "Geçersiz token formatı!";
                return false;
            }
            
            return true;
        }
    }
}
