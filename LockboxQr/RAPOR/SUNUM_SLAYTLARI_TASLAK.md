# AKILLI KASA YÖNETİM SİSTEMİ
## QR Token Tabanlı Erişim Kontrolü ve Arduino Entegrasyonu

**Sunum Tarihi:** 2024  
**Proje Türü:** Yazılım Geliştirme Projesi

---

## SLIDE 1: BAŞLIK

# AKILLI KASA YÖNETİM SİSTEMİ
## QR Token Tabanlı Erişim Kontrolü ve Arduino Entegrasyonu

**Öğrenci:** [Adınız]  
**Ders:** [Ders Adı]  
**Tarih:** 2024

---

## SLIDE 2: İÇİNDEKİLER

# SUNUM İÇERİĞİ

1. Proje Tanımı ve Amacı
2. Problem Tanımı ve Çözüm
3. Sistem Özellikleri
4. Teknoloji Stack
5. Mimari Tasarım
6. Arduino Entegrasyonu
7. Güvenlik Özellikleri
8. Demo ve Test Sonuçları
9. Sonuç ve Öneriler

---

## SLIDE 3: PROJE TANIMI

# PROJE TANIMI

**Akıllı Kasa Yönetim Sistemi**

- QR kod tabanlı token sistemi
- Fiziksel kasa erişim kontrolü
- Offline çalışan Windows uygulaması
- Arduino Uno ile donanım entegrasyonu
- Servo motor ve LED kontrolü

**Hedef:**
Güvenli, kullanıcı dostu ve fiziksel kontrol sağlayan erişim sistemi

---

## SLIDE 4: PROBLEM TANIMI

# PROBLEM TANIMI

**Geleneksel Sistemlerin Sorunları:**

❌ Fiziksel anahtar gerektirir (kayıp/çalıntı riski)  
❌ Merkezi sunucu bağımlılığı (internet kesintilerinde çalışmaz)  
❌ Kullanım geçmişi takibi zor  
❌ Fiziksel durum geri bildirimi yok  
❌ Otomatik kilit kontrolü yok  

**Çözüm:**
QR token + Arduino entegrasyonu ile güvenli ve otomatik sistem

---

## SLIDE 5: SİSTEM ÖZELLİKLERİ

# SİSTEM ÖZELLİKLERİ

## Yazılım Özellikleri
✅ QR token üretimi ve doğrulama  
✅ HMAC-SHA256 kriptografik imza  
✅ Tek seferlik token desteği  
✅ Zaman tabanlı geçerlilik kontrolü  
✅ Kalıcı loglama sistemi  
✅ Çoklu kasa yönetimi (6 kasa)  

## Donanım Özellikleri
✅ Arduino Uno entegrasyonu  
✅ Servo motor kontrolü (açma/kapama)  
✅ LED durum göstergeleri (Yeşil/Sarı/Kırmızı)  
✅ Gerçek zamanlı senkronizasyon  

---

## SLIDE 6: TEKNOLOJİ STACK

# TEKNOLOJİ STACK

## Yazılım
- **Platform:** .NET 8 WinForms
- **Dil:** C#
- **QR Üretimi:** QRCoder 1.6.0
- **QR Okuma:** ZXing.Net 0.16.9
- **Kamera:** AForge.Video.DirectShow
- **Seri Port:** System.IO.Ports

## Donanım
- **Mikrodenetleyici:** Arduino Uno
- **Aktüatör:** Servo Motor (SG90)
- **Göstergeler:** 3 LED (Yeşil, Sarı, Kırmızı)
- **Giriş:** USB Webcam

---

## SLIDE 7: MİMARİ TASARIM

# MİMARİ TASARIM

```
┌─────────────────────────┐
│   Presentation Layer    │
│   (WinForms UI)         │
└───────────┬─────────────┘
            │
┌───────────▼─────────────┐
│   Business Logic Layer  │
│  TokenService           │
│  ArduinoService         │
│  NonceTracker           │
│  AccessLogger           │
└───────────┬─────────────┘
            │
┌───────────▼─────────────┐
│   Data Access Layer     │
│  JSON/CSV Files         │
│  Serial Port            │
└─────────────────────────┘
```

**Tasarım Desenleri:**
- Dependency Injection
- Interface-based Design
- Singleton Pattern
- Observer Pattern

---

## SLIDE 8: TOKEN SİSTEMİ

# TOKEN SİSTEMİ

## Token Formatı
```
LCK|locker=KASA1|iat=<timestamp>|exp=<timestamp>|
once=<0|1>|nonce=<GUID>|sig=<HMAC-SHA256>
```

## Güvenlik Özellikleri
🔐 **HMAC-SHA256** kriptografik imza  
⏰ **Zaman tabanlı** geçerlilik kontrolü  
🔒 **Tek seferlik** token desteği  
📝 **Kalıcı loglama** (CSV formatında)  

## Token Akışı
1. Token üretimi → HMAC imza hesaplama
2. QR kod oluşturma
3. QR okutma → Token doğrulama
4. İmza kontrolü → Erişim izni

---

## SLIDE 9: ARDUINO ENTEGRASYONU

# ARDUINO ENTEGRASYONU

## Donanım Bağlantıları
- **Servo Motor:** Pin 9 (PWM)
- **LED Yeşil:** Pin 7 (Müsait)
- **LED Sarı:** Pin 4 (Açık)
- **LED Kırmızı:** Pin 6 (Kilitli)

## İletişim Protokolü
- **Baud Rate:** 9600
- **Komutlar:**
  - `UNLOCK:KASA1` - Kasa açma
  - `LOCK:KASA1` - Kasa kilitleme
  - `SET_STATUS:KASA1:AVAILABLE` - LED yeşil
  - `SET_STATUS:KASA1:RENTED` - LED sarı
  - `SET_STATUS:KASA1:LOCKED` - LED kırmızı

---

## SLIDE 10: DURUM YÖNETİMİ

# KASA DURUM YÖNETİMİ

| Durum | LED | Servo Motor | Açıklama |
|-------|-----|-------------|----------|
| 🟢 **Müsait** | Yeşil | Kilitli | Kiralanabilir |
| 🟡 **Açık** | Sarı | Açık | Ürün koyma/alma |
| 🔴 **Kilitli** | Kırmızı | Kilitli | Kiralı |

## Durum Geçişleri

**Kiralama:**
Müsait → QR Üret → Açık (15 sn) → Kilitli

**Ürün Alma:**
Kilitli → QR Okut → Açık (5 sn) → Müsait

---

## SLIDE 11: SİSTEM AKIŞI

# SİSTEM AKIŞI

## Kiralama Süreci
1. Kasa seçilir (yeşil LED)
2. QR üretilir → Kasa açılır (sarı LED, servo açık)
3. 15 saniye içinde ürün konur
4. Kasa kilitlenir (kırmızı LED, servo kilitli)

## Ürün Alma Süreci
1. QR kod okutulur
2. Kasa açılır (sarı LED, servo açık)
3. 5 saniye içinde ürün alınır
4. Kasa müsait olur (yeşil LED, servo kilitli)

---

## SLIDE 12: GÜVENLİK ÖZELLİKLERİ

# GÜVENLİK ÖZELLİKLERİ

## Kriptografik Güvenlik
🔐 **HMAC-SHA256** imza doğrulama  
🔑 Secret key token içinde değil  
✏️ Token değiştirilemez (imza tutmaz)  

## Güvenlik Kontrolleri
✅ Token manipulation koruması  
✅ Replay attack koruması (nonce + tek seferlik)  
✅ Time-based expiration  
✅ Input validation (injection koruması)  
✅ Thread-safe operations  

## Güvenlik Zayıflıkları
⚠️ Secret key config'de (production'da environment variable)  
⚠️ Fiziksel erişim kontrolü gerekli  

---

## SLIDE 13: KULLANICI ARAYÜZÜ

# KULLANICI ARAYÜZÜ

## Ana Özellikler
- **Kasa Yönetim Paneli:** 6 kasa için görsel kartlar
- **Durum Göstergeleri:** Renk kodlu durumlar
- **QR Üretim Diyaloğu:** Timer ile süre yönetimi
- **QR Tarama Sekmesi:** Canlı kamera görüntüsü
- **Arduino Bağlantı:** Manuel port seçimi

## Kullanım Senaryoları
1. Kasa kiralama (çift tıklama)
2. QR üretimi ve görüntüleme
3. QR okutma ve doğrulama
4. Durum takibi (gerçek zamanlı)

---

## SLIDE 14: TEST SONUÇLARI

# TEST SONUÇLARI

## Fonksiyonel Testler
✅ Token üretimi ve doğrulama  
✅ Tek seferlik token kontrolü  
✅ Zaman tabanlı expiration  
✅ Kalıcılık (uygulama restart)  
✅ Arduino bağlantısı ve komut gönderme  
✅ Servo motor kontrolü  
✅ LED durum göstergeleri  
✅ Durum senkronizasyonu  

## Performans Metrikleri
- Token üretimi: < 10ms
- Token doğrulama: < 50ms
- Arduino komut: < 50ms
- QR okuma: < 200ms

---

## SLIDE 15: DEMO

# DEMO SENARYOSU

## Senaryo 1: Kasa Kiralama
1. Müsait kasayı seç
2. QR üret → Kasa açılır (sarı LED)
3. 15 saniye içinde ürün koy
4. Kasa kilitlenir (kırmızı LED)

## Senaryo 2: Ürün Alma
1. QR kodu okut
2. Kasa açılır (sarı LED)
3. 5 saniye içinde ürün al
4. Kasa müsait olur (yeşil LED)

## Senaryo 3: Güvenlik Testi
- Aynı QR tekrar okut → Hata
- Süresi dolmuş QR → Hata

---

## SLIDE 16: PROJE BAŞARILARI

# PROJE BAŞARILARI

✅ **Fonksiyonel Gereksinimler:** Tüm gereksinimler karşılandı  
✅ **Güvenlik:** HMAC-SHA256 ile güçlü kriptografik koruma  
✅ **Kullanılabilirlik:** Basit ve sezgisel arayüz  
✅ **Mimari:** Modüler, genişletilebilir yapı  
✅ **Donanım Entegrasyonu:** Arduino ile başarılı fiziksel kontrol  
✅ **Görsel Geri Bildirim:** LED durum göstergeleri  
✅ **Senkronizasyon:** Yazılım-donanım durum senkronizasyonu  
✅ **Kod Kalitesi:** Clean code, XML documentation  

---

## SLIDE 17: KARŞILAŞILAN ZORLUKLAR

# KARŞILAŞILAN ZORLUKLAR

| Zorluk | Çözüm |
|--------|-------|
| Thread safety (camera) | InvokeRequired pattern |
| Token format parsing | Strict validation + exception handling |
| Arduino senkronizasyonu | Event-based communication + state management |
| Race conditions | Timer mekanizması + durum kontrolü |
| Seri port hata yönetimi | Try-catch + graceful degradation |
| Durum geçişleri | State machine pattern |

---

## SLIDE 18: GELECEK ÇALIŞMALAR

# GELECEK ÇALIŞMALAR

## Kısa Vadeli
- Unit testler (%60+ coverage)
- Integration testler
- Performance optimization
- Error logging framework

## Orta Vadeli
- Multi-user support (admin paneli)
- Token revocation mekanizması
- Audit report generation
- Mobile app (QR üretimi)

## Uzun Vadeli
- Cloud synchronization
- Biometric integration
- Multi-platform support
- Web-based management portal

---

## SLIDE 19: ÖĞRENİLENLER

# ÖĞRENİLENLER

🔐 **Kriptografi:** HMAC-SHA256 uygulaması ve güvenlik prensipleri  
🏗️ **Mimari:** Dependency injection ve interface-based design  
🧵 **Thread Safety:** UI thread ve background thread yönetimi  
⚙️ **Configuration:** Merkezi yapılandırma yönetimi  
🚨 **Exception Handling:** Spesifik exception'lar ve error recovery  
🔌 **IoT Entegrasyonu:** Arduino ile seri port iletişimi  
🎛️ **Donanım Kontrolü:** Servo motor ve LED kontrolü  
🔄 **State Management:** Yazılım-donanım durum senkronizasyonu  

---

## SLIDE 20: SONUÇ

# SONUÇ

Bu proje, modern yazılım geliştirme prensipleri ve güvenlik standartları kullanarak:

✅ **Güvenli** token sistemi (HMAC-SHA256)  
✅ **Offline** çalışan erişim kontrolü  
✅ **Fiziksel** donanım entegrasyonu (Arduino)  
✅ **Kullanıcı dostu** arayüz  
✅ **Gerçek zamanlı** durum senkronizasyonu  
✅ **Modüler** ve **genişletilebilir** mimari  

**Gerçek dünya uygulaması geliştirme deneyimi kazanıldı.**

---

## SLIDE 21: SORULAR

# SORULAR VE CEVAPLAR

**Teşekkürler!**

Sorularınız için hazırım.

---

## SLIDE 22: İLETİŞİM

# İLETİŞİM

**Proje Bilgileri:**
- **Proje Adı:** Akıllı Kasa Yönetim Sistemi
- **Versiyon:** 2.0 (Arduino Entegrasyonlu)
- **Durum:** Tamamlandı ✅

**Teknik Detaylar:**
- GitHub: [Repository URL]
- Dokümantasyon: README.md
- Rapor: PROJE_RAPORU_TASLAK.md

---

## SLIDE NOTLARI

### Slide 1: Başlık
- Proje adı ve öğrenci bilgileri
- Görsel: Proje logosu veya görsel

### Slide 2: İçindekiler
- Sunum yapısını özetle
- Her bölüm için kısa açıklama

### Slide 3: Proje Tanımı
- Projenin ne olduğunu açıkla
- Ana hedefleri vurgula

### Slide 4: Problem Tanımı
- Geleneksel sistemlerin sorunlarını göster
- Çözüm yaklaşımını belirt

### Slide 5: Sistem Özellikleri
- Yazılım ve donanım özelliklerini listele
- Görsel: Ekran görüntüleri

### Slide 6: Teknoloji Stack
- Kullanılan teknolojileri göster
- Versiyon bilgilerini ekle

### Slide 7: Mimari Tasarım
- Mimari diyagramı göster
- Tasarım desenlerini açıkla

### Slide 8: Token Sistemi
- Token formatını göster
- Güvenlik özelliklerini vurgula

### Slide 9: Arduino Entegrasyonu
- Donanım bağlantılarını göster
- İletişim protokolünü açıkla
- Görsel: Arduino bağlantı şeması

### Slide 10: Durum Yönetimi
- Durum tablosunu göster
- Geçiş akışlarını açıkla

### Slide 11: Sistem Akışı
- Kiralama ve ürün alma süreçlerini göster
- Görsel: Akış diyagramı

### Slide 12: Güvenlik Özellikleri
- Güvenlik mekanizmalarını açıkla
- Güvenlik zayıflıklarını belirt

### Slide 13: Kullanıcı Arayüzü
- UI ekran görüntülerini göster
- Kullanım senaryolarını açıkla

### Slide 14: Test Sonuçları
- Test sonuçlarını göster
- Performans metriklerini listele

### Slide 15: Demo
- Demo senaryolarını açıkla
- Canlı demo yapılacaksa hazır ol

### Slide 16: Proje Başarıları
- Başarıları listeleyin
- Görsel: Başarı göstergeleri

### Slide 17: Karşılaşılan Zorluklar
- Zorlukları ve çözümleri göster
- Öğrenilen dersleri vurgula

### Slide 18: Gelecek Çalışmalar
- Gelecek planları göster
- Öncelik sıralaması yap

### Slide 19: Öğrenilenler
- Teknik öğrenilenleri listele
- Deneyim kazanımlarını vurgula

### Slide 20: Sonuç
- Projeyi özetle
- Ana başarıları vurgula

### Slide 21: Sorular
- Soru-cevap için hazır ol
- Ek bilgi vermeye hazır ol

### Slide 22: İletişim
- İletişim bilgilerini ver
- Proje kaynaklarını paylaş

---

## SUNUM İPUÇLARI

1. **Zaman Yönetimi:** Her slayt için 1-2 dakika ayırın
2. **Görsel Kullanım:** Ekran görüntüleri ve diyagramlar ekleyin
3. **Demo Hazırlığı:** Canlı demo için fiziksel prototip hazır olsun
4. **Sorular:** Olası sorulara hazırlıklı olun
5. **Teknik Detaylar:** Hoca teknik detay isterse hazır olun
6. **Güvenlik:** Güvenlik özelliklerini vurgulayın
7. **Mimari:** Mimari tasarımı detaylı açıklayın
8. **Test:** Test sonuçlarını görsel olarak gösterin

---

**Sunum Süresi:** 15-20 dakika  
**Slayt Sayısı:** 22 slayt  
**Demo Süresi:** 5 dakika (opsiyonel)
