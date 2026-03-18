# Akıllı Kasa Yönetim Sistemi - Sunum Özeti

## Proje Tanımı
QR kod tabanlı token sistemi kullanarak fiziksel kasa erişim kontrolü sağlayan offline Windows masaüstü uygulaması.

## Teknik Özellikler

### Yazılım
- **Platform**: .NET 8 WinForms
- **Dil**: C#
- **Güvenlik**: HMAC-SHA256 kriptografik imza
- **QR Kod**: Üretim (QRCoder) ve Tarama (ZXing.Net)
- **Mimari**: Dependency Injection, Interface-based design

### Donanım
- **Mikrodenetleyici**: Arduino Uno
- **Aktüatör**: Servo Motor (SG90) - Kasa kilidi
- **Göstergeler**: 3 LED (Yeşil, Sarı, Kırmızı)
- **Giriş**: Webcam (QR kod tarama)

## Ana Özellikler

### 1. QR Token Sistemi
- Güvenli token üretimi (HMAC-SHA256)
- Zaman tabanlı geçerlilik kontrolü
- Tek seferlik kullanım desteği
- Offline çalışma

### 2. Kasa Yönetimi
- 6 kasa için görsel yönetim paneli
- Gerçek zamanlı durum takibi
- Otomatik süre kontrolü
- Manuel durum yönetimi

### 3. Arduino Entegrasyonu
- Seri port iletişimi
- Servo motor kontrolü (açma/kapama)
- LED durum göstergesi
- İki yönlü mesajlaşma

### 4. Güvenlik
- Kriptografik imza doğrulama
- Tek seferlik token takibi
- Kalıcı loglama (CSV)
- Input validation

## Sistem Akışı

### Kiralama Senaryosu
1. **Başlangıç**: Kasa yeşil (müsait) - Fiziksel olarak kilitli
2. **QR Üretimi**: Kasa sarı (açık) - Servo motor açılır
3. **Ürün Koyma**: 15 saniye süre
4. **Kilitlenme**: Kasa kırmızı (kilitli) - Servo motor kilitlenir

### Ürün Alma Senaryosu
1. **QR Okutma**: QR kod tarama
2. **Açılma**: Kasa sarı (açık) - Servo motor açılır
3. **Ürün Alma**: 5 saniye süre
4. **Müsait Olma**: Kasa yeşil (müsait) - Servo motor kilitlenir

## LED Durum Göstergeleri

| Durum | LED | Açıklama | Servo Motor |
|-------|-----|----------|-------------|
| Müsait | 🟢 Yeşil | Kiralanabilir | Kilitli |
| Açık | 🟡 Sarı | Ürün koyma/alma | Açık |
| Kilitli | 🔴 Kırmızı | Kiralı | Kilitli |

## Teknik Detaylar

### Token Formatı
```
LCK|locker=<ID>|iat=<timestamp>|exp=<timestamp>|once=<0|1>|nonce=<GUID>|sig=<HMAC-SHA256>
```

### Arduino Komutları
- `UNLOCK:KASA1` - Kasa açma
- `LOCK:KASA1` - Kasa kilitleme
- `SET_STATUS:KASA1:AVAILABLE` - LED yeşil
- `SET_STATUS:KASA1:RENTED` - LED sarı
- `SET_STATUS:KASA1:LOCKED` - LED kırmızı
- `STATUS` - Durum sorgulama

### Güvenlik Özellikleri
- HMAC-SHA256 imza doğrulama
- Zaman toleransı kontrolü (20 saniye)
- Tek seferlik token takibi (kalıcı)
- Input validation (injection koruması)

## Proje Yapısı

```
LockboxQr/
├── Form1.cs              # Ana form, kasa yönetimi
├── TokenService.cs      # Token üretimi ve doğrulama
├── ArduinoService.cs    # Arduino iletişimi
├── AccessLogger.cs      # Loglama
├── NonceTracker.cs      # Tek seferlik token takibi
├── Arduino/
│   └── lockbox_controller.ino  # Arduino kodu
└── appsettings.json     # Yapılandırma
```

## Başarılar

✅ Offline çalışan güvenli token sistemi  
✅ Arduino entegrasyonu ile fiziksel kontrol  
✅ Gerçek zamanlı durum senkronizasyonu  
✅ Kullanıcı dostu görsel arayüz  
✅ Kalıcı loglama ve takip sistemi  
✅ Güvenli kriptografik imza doğrulama  

## Demo Senaryosu

1. **Kasa Kiralama**:
   - Müsait kasayı seç → QR üret → Kasa açılır (sarı)
   - 15 saniye içinde ürün koy → Kasa kilitlenir (kırmızı)

2. **Ürün Alma**:
   - QR kodu okut → Kasa açılır (sarı)
   - 5 saniye içinde ürün al → Kasa müsait olur (yeşil)

3. **Güvenlik Testi**:
   - Aynı QR'ı tekrar okut → "Tek seferlik kod kullanıldı" hatası
   - Süresi dolmuş QR okut → "Token süresi doldu" hatası

## Sonuç

Bu proje, modern yazılım geliştirme prensipleri (Dependency Injection, Interface-based design) ve güvenlik standartları (HMAC-SHA256) kullanarak, fiziksel donanım entegrasyonu ile gerçek dünya uygulaması geliştirme deneyimi sunmaktadır.
