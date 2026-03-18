# Akıllı Kasa (QR) - Lockbox Management System

Offline çalışan C# WinForms uygulaması. QR token üretir ve webcam ile QR kod tarayarak doğrular.

## Özellikler

- **QR Token Üretimi**: Locker ID, geçerlilik süresi ve tek seferlik kullanım seçenekleri ile QR kod üretir
- **QR Kod Tarama**: Webcam ile canlı QR kod tarama ve doğrulama
- **Güvenlik**: HMAC-SHA256 imza doğrulama, zaman kontrolü, tek seferlik token takibi
- **Loglama**: Tüm erişim denemeleri CSV formatında loglanır
- **Kalıcılık**: Tek seferlik tokenlar uygulama yeniden başlatılsa bile hatırlanır
- **Arduino Entegrasyonu**: Fiziksel kasa kontrolü için Arduino Uno desteği
- **LED Durum Göstergesi**: Yeşil (Müsait), Sarı (Açık), Kırmızı (Kilitli) LED durumları
- **Servo Motor Kontrolü**: Otomatik kasa kilidi açma/kapama
- **Çoklu Kasa Yönetimi**: 6 kasa için görsel yönetim paneli
- **Durum Takibi**: Gerçek zamanlı kasa durumu takibi ve senkronizasyonu

<img src="https://github.com/user-attachments/assets/4e06bbb9-a818-4d8d-9026-d061861b1e67" width="600">

<img src="https://github.com/user-attachments/assets/3001214b-d6bd-469c-bd9c-f2d07cf6bb8e" width="600">



## Teknoloji

### Yazılım
- **.NET 8** WinForms
- **QRCoder**: QR kod üretimi
- **ZXing.Net**: QR kod okuma
- **AForge.Video.DirectShow**: Webcam erişimi
- **Microsoft.Extensions.Configuration**: Yapılandırma yönetimi
- **System.IO.Ports**: Arduino seri port iletişimi

### Donanım
- **Arduino Uno**: Fiziksel kasa kontrolü
- **Servo Motor (SG90)**: Kasa kilidi mekanizması
- **LED'ler**: Durum göstergesi (Yeşil, Sarı, Kırmızı)
- **Webcam**: QR kod tarama

## Kurulum

### Yazılım Kurulumu

1. .NET 8 SDK'nın yüklü olduğundan emin olun
2. Projeyi Visual Studio veya VS Code ile açın
3. NuGet paketleri otomatik olarak restore edilecektir
4. Projeyi derleyin ve çalıştırın

### Arduino Kurulumu

1. Arduino IDE'yi yükleyin
2. `LockboxQr/Arduino/lockbox_controller/lockbox_controller.ino` dosyasını açın
3. Arduino Uno'yu USB ile bilgisayara bağlayın
4. Tools > Board > Arduino Uno seçin
5. Tools > Port > COM port numaranızı seçin
6. Upload butonuna tıklayarak kodu Arduino'ya yükleyin
7. Serial Monitor'u kapatın (uygulama portu kullanacak)

### Donanım Bağlantıları

- **Servo Motor**: Pin 9 (PWM)
- **LED Yeşil**: Pin 7
- **LED Sarı**: Pin 4
- **LED Kırmızı**: Pin 6

## Kullanım

### Kasa Yönetimi (Ana Sekme)

1. **Arduino Bağlan**: Arduino'ya bağlanmak için butona tıklayın ve COM portunu seçin
2. **Kasa Seçimi**: Kiralanacak kasayı çift tıklayın (veya sağ tıklayıp menüden seçin)
3. **Durum Göstergesi**: 
   - 🟢 **Yeşil (Müsait)**: Kasa kiralanabilir, fiziksel olarak kilitli
   - 🟡 **Sarı (Açık)**: Kasa açık, ürün koyma/alma süreci
   - 🔴 **Kırmızı (Kilitli)**: Kasa kiralı ve kilitli

### QR Üret ve Kiralama

1. Müsait bir kasayı çift tıklayın
2. **QR Oluştur** butonuna tıklayın
3. QR kod üretilir ve kasa **sarı (açık)** duruma geçer
4. 15 saniye içinde ürünü kasaya koyun
5. Süre dolunca veya **Kapat** butonuna basınca kasa **kırmızı (kilitli)** duruma geçer

### QR Okutma ve Ürün Alma

1. **QR Tara** sekmesine geçin
2. **Kamera**: Kullanılacak webcam'i seçin
3. **Başlat** butonuna tıklayın
4. QR kodu kameraya gösterin
5. Kasa açılır ve **sarı (açık)** duruma geçer
6. 5 saniye içinde ürünü alın
7. Süre dolunca kasa otomatik olarak **yeşil (müsait)** duruma döner

### Kasa Durum Yönetimi

- **Sağ Tıklama**: Kilitli kasalar için sağ tıklayarak "Müsait Yap" seçeneğini kullanabilirsiniz
- **Otomatik Süre Kontrolü**: Süresi dolan kasalar otomatik olarak müsait duruma döner

## Token Formatı

Token formatı:
```
LCK|locker=<ID>|iat=<timestamp>|exp=<timestamp>|once=<0|1>|nonce=<GUID>|sig=<HMAC-SHA256>
```

- **locker**: Kasa kimliği
- **iat**: Token oluşturulma zamanı (UTC epoch saniye)
- **exp**: Token sona erme zamanı (UTC epoch saniye)
- **once**: Tek seferlik kullanım (1) veya çoklu kullanım (0)
- **nonce**: Benzersiz tanımlayıcı (GUID)
- **sig**: HMAC-SHA256 imza

## Güvenlik

- **SECRET_KEY**: Token imzalama için kullanılan gizli anahtar (`appsettings.json` içinde tanımlı, production'da değiştirilmeli)
- **Zaman Toleransı**: Token süresi yapılandırılabilir toleransla kontrol edilir (varsayılan: 20 saniye)
- **Tek Seferlik Tokenlar**: `logs/used_nonces.json` dosyasında kalıcı olarak saklanır
- **Input Validation**: Tüm kullanıcı girdileri doğrulanır (injection riski önleme)
- **Spesifik Exception Handling**: Detaylı hata yönetimi ve özel exception sınıfları

## Loglama

Tüm erişim denemeleri `logs/access.csv` dosyasına kaydedilir:

| Sütun | Açıklama |
|-------|----------|
| timestamp_utc | Deneme zamanı (UTC) |
| locker_id | Kasa kimliği |
| result | Sonuç (OK veya ERR) |
| reason | Başarı/Hata nedeni |
| nonce | Token nonce değeri |
| exp_utc | Token sona erme zamanı (UTC) |

## Klasör Yapısı

```
LockboxQr/
├── out/              # Oluşturulan QR kod görselleri
├── logs/
│   ├── access.csv    # Erişim logları
│   └── used_nonces.json  # Kullanılmış tek seferlik tokenlar
└── ...
```

## Yapılandırma

Tüm ayarlar `appsettings.json` dosyasından yönetilir:

### Uygulama Ayarları
- **SecretKey**: Token imzalama anahtarı
- **DefaultValidityMinutes**: Varsayılan geçerlilik süresi (15 dakika)
- **TimeToleranceSeconds**: Zaman toleransı (20 saniye)
- **DebounceMilliseconds**: Debounce süresi (1500ms)
- **SuccessOverlayDurationSeconds**: Başarı mesajı süresi (5 saniye)
- **Validation**: Input validation limitleri

### Arduino Ayarları
- **Enabled**: Arduino desteğini aç/kapat (true/false)
- **PortName**: COM port numarası (örn: COM3)
- **BaudRate**: Seri port hızı (varsayılan: 9600)
- **AutoConnect**: Otomatik bağlantı (varsayılan: false)
- **UseSimulator**: Simülatör modu (test için, varsayılan: false)

## Mimari

- **Interface'ler**: `ITokenService`, `INonceTracker`, `IAccessLogger` - Test edilebilirlik için
- **Dependency Injection**: Constructor injection ile bağımlılık yönetimi
- **Separation of Concerns**: Her servis ayrı sınıfta
- **Custom Exceptions**: Spesifik hata yönetimi için özel exception sınıfları

## Demo Senaryosu

1. QR Üret sekmesinde:
   - Locker ID: `A1`
   - Geçerlilik: `15` dk
   - Tek Seferlik: `Açık`
   - QR Oluştur'a tıklayın

2. QR Tara sekmesinde:
   - Kamerayı başlatın
   - Oluşturulan QR'ı tarayın → "AÇILDI ✅" görünür
   - Aynı QR'ı tekrar tarayın → "Tek seferlik kod daha önce kullanıldı" hatası

3. `logs/access.csv` dosyasını kontrol edin:
   - İlk tarama: `OK, Başarılı`
   - İkinci tarama: `ERR, Tek seferlik kod daha önce kullanıldı`

4. Uygulamayı kapatıp tekrar açın ve aynı QR'ı tarayın:
   - Hala reddedilir (kalıcılık çalışıyor)

## Sistem Akışı

### Kiralama Süreci
1. **Başlangıç**: Kasa yeşil (müsait) durumda
2. **QR Üretimi**: Kasa sarı (açık) duruma geçer, servo motor açılır
3. **Ürün Koyma**: 15 saniye süre tanınır
4. **Kilitlenme**: Süre dolunca veya kapatılınca kasa kırmızı (kilitli) duruma geçer

### Ürün Alma Süreci
1. **QR Okutma**: QR kod okutulur
2. **Açılma**: Kasa sarı (açık) duruma geçer, servo motor açılır
3. **Ürün Alma**: 5 saniye süre tanınır
4. **Müsait Olma**: Süre dolunca kasa yeşil (müsait) duruma döner, servo motor kilitlenir

## Notlar

- Uygulama tamamen offline çalışır
- SECRET_KEY production ortamında değiştirilmelidir
- Webcam erişimi için uygun izinler gerekebilir
- Arduino IDE Serial Monitor kapalı olmalıdır (port çakışması önlemek için)
- Windows ortamında test edilmiştir
- Arduino bağlantısı için COM port izinleri gerekebilir

## Lisans

Bu proje özel kullanım içindir.

