using System;

namespace LockboxQr
{
    /// <summary>
    /// Token işlemleri sırasında oluşan hatalar için özel exception
    /// </summary>
    public class TokenException : Exception
    {
        public TokenException(string message) : base(message) { }
        public TokenException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Token imza doğrulama hatası
    /// </summary>
    public class TokenSignatureException : TokenException
    {
        public TokenSignatureException(string message) : base(message) { }
    }

    /// <summary>
    /// Token süresi dolmuş hatası
    /// </summary>
    public class TokenExpiredException : TokenException
    {
        public TokenExpiredException(string message) : base(message) { }
    }

    /// <summary>
    /// Token format hatası
    /// </summary>
    public class TokenFormatException : TokenException
    {
        public TokenFormatException(string message) : base(message) { }
        public TokenFormatException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Nonce işlemleri sırasında oluşan hatalar
    /// </summary>
    public class NonceException : Exception
    {
        public NonceException(string message) : base(message) { }
        public NonceException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Loglama işlemleri sırasında oluşan hatalar
    /// </summary>
    public class LoggingException : Exception
    {
        public LoggingException(string message) : base(message) { }
        public LoggingException(string message, Exception innerException) : base(message, innerException) { }
    }

    /// <summary>
    /// Arduino iletişim hataları
    /// </summary>
    public class ArduinoException : Exception
    {
        public ArduinoException(string message) : base(message) { }
        public ArduinoException(string message, Exception innerException) : base(message, innerException) { }
    }
}

