# 🔌 Arduino Bağlantı Rehberi - Sıfırdan Başlayanlar İçin

## 📦 Gerekli Malzemeler

1. ✅ **Arduino Uno** (veya klon) - ~100-150 TL
2. ✅ **USB kablosu** (Arduino ile birlikte gelir)
3. ✅ **Breadboard** (devre tahtası - küçük veya orta boy) - ~10 TL
4. ✅ **3 adet LED** (1 Yeşil, 1 Sarı, 1 Kırmızı) - ~3 TL
5. ✅ **3 adet 220Ω direnç** (renkli çizgiler: kırmızı-kırmızı-kahverengi) - ~1.50 TL
6. ✅ **Servo Motor** (SG90 veya benzeri) - ~30-50 TL
7. ✅ **Jumper kablolar** (erkek-erkek) - ~5 TL (en az 8-10 adet)

**Toplam Maliyet: ~150-220 TL**

---

## 🎯 Bağlantı Şeması (Genel Bakış)

```
Arduino Uno:
┌─────────────────────┐
│                     │
│  Pin 6 ────────────┼──► Kırmızı LED (uzun bacak) ──► Direnç ──► GND
│  Pin 7 ────────────┼──► Yeşil LED (uzun bacak) ──► Direnç ──► GND
│  Pin 4 ────────────┼──► Sarı LED (uzun bacak) ──► Direnç ──► GND
│                     │
│  Pin 9 ────────────┼──► Servo Motor (Sinyal - turuncu/sarı kablo)
│                     │
│  5V ───────────────┼──► Servo Motor (Güç - kırmızı kablo)
│                     │
│  GND ───────────────┼──► Breadboard GND hattı ──► Servo Motor (GND - kahverengi/siyah)
│                     │
└─────────────────────┘

LED Durumları:
- Müsait (Available): Yeşil LED yanar (Pin 7)
- Açık (Rented): Sarı LED yanar (Pin 4)
- Kilitli (Locked): Kırmızı LED yanar (Pin 6)

Servo Motor:
- 0°: Kilitli (kapalı)
- 90°: Açık (açık)
```

---

## 📝 ADIM ADIM BAĞLANTI (Çok Detaylı)

### ⚠️ ÖNEMLİ KURALLAR

1. **Arduino'yu USB'ye bağlamadan önce bağlantıları yap!**
2. **LED'in yönü çok önemli!** Uzun bacak +, kısa bacak -
3. **Direnç mutlaka olmalı!** Direnç olmadan LED bozulabilir
4. **Bağlantıları yaparken Arduino'ya güç verme!**

---

## ADIM 1: Malzemeleri Hazırla ve Tanı

### 1.1: Arduino Uno'yu Tanı

Arduino Uno'nun üzerinde şunlar var:
- **Dijital Pinler**: 0-13 (üstte, "DIGITAL" yazan yer)
- **Analog Pinler**: A0-A5 (altta, "ANALOG IN" yazan yer)
- **GND Pinleri**: Birkaç tane var (üstte ve altta)
- **5V Pin**: Güç çıkışı (üstte, "POWER" bölümünde)
- **USB Port**: Bilgisayara bağlamak için (yan tarafta)

**Şimdilik şu pinleri kullanacağız:**
- **Pin 4**: Sarı LED için (RENTED - Açık)
- **Pin 6**: Kırmızı LED için (LOCKED - Kilitli)
- **Pin 7**: Yeşil LED için (AVAILABLE - Müsait)
- **Pin 9**: Servo motor için (PWM pin - ~ işareti var)
- **5V**: Servo motor güç için
- **GND**: Toprak (ortak bağlantı)

### 1.2: Breadboard'ı Tanı

Breadboard'ın yapısı:
```
┌─────────────────────────────────┐
│  A  B  C  D  E │ F  G  H  I  J  │  ← Üst satırlar
│  A  B  C  D  E │ F  G  H  I  J  │     (A-E birbirine bağlı)
│  ...                            │     (F-J birbirine bağlı)
│─────────────────────────────────│  ← Ortadaki çizgi (ayırıcı)
│  ...                            │
│  A  B  C  D  E │ F  G  H  I  J  │  ← Alt satırlar
│  A  B  C  D  E │ F  G  H  I  J  │     (A-E birbirine bağlı)
└─────────────────────────────────┘     (F-J birbirine bağlı)

Üst ve alt kenarlarda:
┌─────────────────────────────────┐
│  +  +  +  +  + │ +  +  +  +  + │  ← Kırmızı çizgi (VCC - güç)
│─────────────────────────────────│
│  ...                            │
│─────────────────────────────────│
│  -  -  -  -  - │ -  -  -  -  - │  ← Mavi çizgi (GND - toprak)
└─────────────────────────────────┘
```

**Önemli:** 
- Aynı satırdaki delikler birbirine bağlıdır (örn: A1, B1, C1, D1, E1 birbirine bağlı)
- Üst ve alt kenarlardaki çizgiler (kırmızı ve mavi) tüm uzunluğu boyunca birbirine bağlıdır

### 1.3: LED'leri Tanı

**3 adet LED gerekiyor:**
- **1 adet Yeşil LED** (Pin 7 - Müsait durumu)
- **1 adet Sarı LED** (Pin 4 - Açık durumu)
- **1 adet Kırmızı LED** (Pin 6 - Kilitli durumu)

Her LED'in 2 bacağı var:
- **Uzun bacak** = Pozitif (+), Anot (LED'in + ucu)
- **Kısa bacak** = Negatif (-), Katot (LED'in - ucu)

**Çok önemli:** LED'in yönü çok önemli! Yanlış takarsan yanmaz.

### 1.4: Direnci Tanı

**3 adet 220Ω direnç gerekiyor** (her LED için bir tane)
- Renkli çizgiler: kırmızı-kırmızı-kahverengi
- Direncin yönü önemli değil (iki yöne de takılabilir)
- LED'i korumak için mutlaka gerekli

### 1.5: Servo Motoru Tanı

Servo motorun 3 kablosu var:
- **Kırmızı kablo** → Güç (+5V)
- **Kahverengi/Siyah kablo** → GND (toprak)
- **Turuncu/Sarı kablo** → Sinyal (Pin 9)

**Not:** Bazı servo motorlarda renkler farklı olabilir. Genellikle:
- En koyu renk (kahverengi/siyah) = GND
- En açık renk (turuncu/sarı/beyaz) = Sinyal
- Kırmızı = Güç

---

## ADIM 2: Arduino'yu Breadboard'a Bağla (GND)

### 2.1: GND Bağlantısı

1. **Bir jumper kablo al** (erkek-erkek)
2. **Arduino'nun GND pinine tak** 
   - Arduino'nun üzerinde "GND" yazan pinlerden birini kullan
   - Genellikle üstte "POWER" bölümünde veya altta "ANALOG IN" bölümünde var
   - Herhangi bir GND pinini kullanabilirsin (hepsi birbirine bağlı)
3. **Diğer ucunu breadboard'ın mavi çizgisine (GND hattına) tak**
   - Mavi çizginin herhangi bir deliğine takabilirsin
   - Örneğin: Mavi çizginin en solundaki deliğe tak

**✅ Şimdi Arduino'nun GND'si breadboard'ın GND hattına bağlı**

**Görsel:**
```
Arduino GND ────[Jumper Kablo]──── Breadboard GND (mavi çizgi)
```

---

## ADIM 3: Yeşil LED'i Bağla

### 3.1: LED'i Breadboard'a Tak

1. **Yeşil LED'i al**
2. **LED'in yönünü kontrol et:**
   - **Uzun bacak** = Pozitif (+)
   - **Kısa bacak** = Negatif (-)
3. **Breadboard'ın sol tarafına tak:**
   - **Uzun bacak** → Örneğin **A1** deliğine tak
   - **Kısa bacak** → Örneğin **A2** deliğine tak (hemen altına)

**Görsel:**
```
Breadboard:
┌─────────────────┐
│ A1 [Uzun bacak] │ ← Yeşil LED (uzun bacak - pozitif)
│ A2 [Kısa bacak] │ ← Yeşil LED (kısa bacak - negatif)
└─────────────────┘
```

**✅ LED breadboard'a takıldı**

### 3.2: Direnci Bağla

1. **220Ω direnci al** (renkli çizgiler: kırmızı-kırmızı-kahverengi)
2. **Direncin bir ucunu** → LED'in **kısa bacağıyla aynı satıra** tak
   - LED'in kısa bacağı A2'de
   - Direncin bir ucunu B2'ye tak (A2 ile aynı satır)
3. **Direncin diğer ucunu** → Breadboard'ın **mavi çizgisine (GND)** tak
   - Mavi çizginin herhangi bir deliğine takabilirsin

**Görsel:**
```
Breadboard:
┌─────────────────┐
│ A1 [Uzun bacak] │ ← Yeşil LED
│ A2 [Kısa bacak] │ ← Yeşil LED
│ B2 [Direnç ucu] │ ← Direnç (LED'in kısa bacağıyla aynı satır)
│ ...             │
│ GND [Direnç]    │ ← Direncin diğer ucu (mavi çizgi)
└─────────────────┘
```

**✅ Direnç bağlandı**

### 3.3: Arduino Pin 7'yi LED'e Bağla

1. **Bir jumper kablo al**
2. **Arduino'nun Pin 7'sine tak**
   - Arduino'nun üzerinde "7" yazan dijital pin
   - Üstte "DIGITAL" bölümünde
3. **Diğer ucunu** → LED'in **uzun bacağıyla aynı satıra** tak
   - LED'in uzun bacağı A1'de
   - Jumper kabloyu C1'e tak (A1 ile aynı satır)

**Görsel:**
```
Arduino Pin 7 ────[Jumper Kablo]──── Breadboard C1
                                          │
                                          └─── A1 (LED uzun bacak)
```

**Tam görsel:**
```
Breadboard:
┌─────────────────┐
│ A1 [Uzun bacak] │ ← Yeşil LED
│ C1 [Pin 7]      │ ← Arduino Pin 7 (LED'in uzun bacağıyla aynı satır)
│ A2 [Kısa bacak] │ ← Yeşil LED
│ B2 [Direnç ucu] │ ← Direnç
│ ...             │
│ GND [Direnç]    │ ← Direncin diğer ucu
└─────────────────┘
```

**✅ Yeşil LED bağlantısı tamam!**

---

## ADIM 4: Sarı LED'i Bağla

### 4.1: Sarı LED'i Breadboard'a Tak

1. **Sarı LED'i al**
2. **LED'in yönünü kontrol et:**
   - **Uzun bacak** = Pozitif (+)
   - **Kısa bacak** = Negatif (-)
3. **Breadboard'ın sol tarafına tak** (Yeşil LED'in yanına):
   - **Uzun bacak** → Örneğin **A5** deliğine tak
   - **Kısa bacak** → Örneğin **A6** deliğine tak (hemen altına)

### 4.2: Direnci Bağla

1. **İkinci 220Ω direnci al**
2. **Direncin bir ucunu** → LED'in **kısa bacağıyla aynı satıra** tak
   - LED'in kısa bacağı A6'da
   - Direncin bir ucunu B6'ya tak (A6 ile aynı satır)
3. **Direncin diğer ucunu** → Breadboard'ın **mavi çizgisine (GND)** tak

### 4.3: Arduino Pin 4'ü LED'e Bağla

1. **Bir jumper kablo al**
2. **Arduino'nun Pin 4'üne tak**
3. **Diğer ucunu** → LED'in **uzun bacağıyla aynı satıra** tak
   - LED'in uzun bacağı A5'te
   - Jumper kabloyu C5'e tak (A5 ile aynı satır)

**✅ Sarı LED bağlantısı tamam!**

---

## ADIM 5: Kırmızı LED'i Bağla

### 5.1: Kırmızı LED'i Breadboard'a Tak

1. **Kırmızı LED'i al**
2. **LED'in yönünü kontrol et:**
   - **Uzun bacak** = Pozitif (+)
   - **Kısa bacak** = Negatif (-)
3. **Breadboard'ın sol tarafına tak** (Sarı LED'in yanına):
   - **Uzun bacak** → Örneğin **A9** deliğine tak
   - **Kısa bacak** → Örneğin **A10** deliğine tak (hemen altına)

### 5.2: Direnci Bağla

1. **Üçüncü 220Ω direnci al**
2. **Direncin bir ucunu** → LED'in **kısa bacağıyla aynı satıra** tak
   - LED'in kısa bacağı A10'da
   - Direncin bir ucunu B10'a tak (A10 ile aynı satır)
3. **Direncin diğer ucunu** → Breadboard'ın **mavi çizgisine (GND)** tak

### 5.3: Arduino Pin 6'yı LED'e Bağla

1. **Bir jumper kablo al**
2. **Arduino'nun Pin 6'sına tak**
3. **Diğer ucunu** → LED'in **uzun bacağıyla aynı satıra** tak
   - LED'in uzun bacağı A9'da
   - Jumper kabloyu C9'a tak (A9 ile aynı satır)

**✅ Kırmızı LED bağlantısı tamam!**

---

## ADIM 6: Servo Motoru Bağla (YouTube Tarzı Detaylı Anlatım)

### 6.1: Servo Motor Nedir ve Nasıl Çalışır?

**Servo motor**, Arduino'dan gelen sinyale göre belirli açılarda dönen bir motordur. Bizim projemizde:
- **0°** = Kilitli (kapalı) pozisyon
- **90°** = Açık pozisyon

**Nasıl çalışır?**
- Arduino'dan Pin 9'a sinyal gönderilir
- Servo motor bu sinyali alır ve açısını ayarlar
- Güç için 5V gereklidir
- GND (toprak) ortak olmalıdır

---

### 6.2: Servo Motor Kablolarını Tanı ve Renklerini Kontrol Et

**Servo motorun 3 kablosu var.** Genellikle şu renklerde olur:

```
Servo Motor Kabloları:
┌─────────────────────┐
│                     │
│  🔴 Kırmızı    ────┼──► Güç (+5V) - Arduino'nun 5V pinine
│                     │
│  ⚫ Kahverengi ────┼──► GND (Toprak) - Arduino'nun GND pinine
│     (veya Siyah)    │
│                     │
│  🟡 Turuncu    ────┼──► Sinyal (Pin 9) - Arduino'nun Pin 9'una
│     (veya Sarı)     │
│                     │
└─────────────────────┘
```

**⚠️ ÖNEMLİ:** Servo motor markasına göre renkler değişebilir! Eğer renkler farklıysa:

1. **En koyu renk** (kahverengi/siyah) = **GND** (toprak)
2. **En açık renk** (turuncu/sarı/beyaz) = **Sinyal** (Pin 9)
3. **Kırmızı** = **Güç** (+5V)

**Kontrol etmek için:**
- Servo motorun üzerinde veya kutusunda renk şeması olabilir
- İnternette servo motor modelini aratarak renk şemasını bulabilirsin

---

### 6.3: Servo Motor Güç Bağlantısı (Kırmızı Kablo) - ADIM ADIM

**🎯 HEDEF:** Servo motorun kırmızı kablosunu Arduino'nun 5V pinine bağlamak

#### Yöntem 1: Breadboard Üzerinden (ÖNERİLEN - Daha Kolay ve Düzenli)

**Adım 1:** Arduino'nun 5V pinini bul
- Arduino'nun üzerinde "POWER" bölümüne bak
- "5V" yazan pini bul (genellikle üstte, GND'in yanında)

**Adım 2:** Jumper kablo ile 5V'u breadboard'a bağla
1. **Bir jumper kablo al** (erkek-erkek)
2. **Bir ucunu Arduino'nun 5V pinine tak**
3. **Diğer ucunu breadboard'ın kırmızı çizgisine (VCC hattına) tak**
   - Breadboard'ın üst veya alt kenarındaki kırmızı çizgiye
   - Herhangi bir deliğe takabilirsin (tüm kırmızı çizgi birbirine bağlı)

**Adım 3:** Servo motorun kırmızı kablosunu breadboard'a tak
1. **Servo motorun kırmızı kablosunu bul**
2. **Breadboard'ın kırmızı çizgisine tak** (herhangi bir deliğe)

**Görsel Şema:**
```
Arduino Uno:
┌─────────────────────┐
│  5V ────[Jumper]────┼──► Breadboard VCC (kırmızı çizgi)
│                     │              │
│                     │              └─── Servo Motor (🔴 kırmızı kablo)
└─────────────────────┘
```

**✅ Kontrol:** Arduino'nun 5V'u → Breadboard VCC → Servo Motor (kırmızı)

---

#### Yöntem 2: Direkt Bağlantı (Alternatif)

Eğer breadboard kullanmak istemiyorsan:
1. **Bir jumper kablo al**
2. **Bir ucunu Arduino'nun 5V pinine tak**
3. **Diğer ucunu servo motorun kırmızı kablosuna direkt bağla**
   - Jumper kablonun diğer ucunu servo motorun kırmızı kablosuna takabilirsin
   - Veya servo motorun kırmızı kablosunu jumper kabloya bağlayabilirsin

**⚠️ DİKKAT:** Bu yöntem daha karmaşık olabilir, breadboard yöntemi daha kolay!

---

### 6.4: Servo Motor GND Bağlantısı (Kahverengi/Siyah Kablo) - ADIM ADIM

**🎯 HEDEF:** Servo motorun kahverengi/siyah kablosunu Arduino'nun GND'sine bağlamak

**Adım 1:** Servo motorun GND kablosunu bul
- En koyu renkli kablo (genellikle kahverengi veya siyah)

**Adım 2:** Breadboard'ın GND hattına tak
1. **Servo motorun kahverengi/siyah kablosunu al**
2. **Breadboard'ın mavi çizgisine (GND hattına) tak**
   - Breadboard'ın üst veya alt kenarındaki mavi çizgiye
   - Herhangi bir deliğe takabilirsin (tüm mavi çizgi birbirine bağlı)
   - Arduino GND zaten mavi çizgiye bağlı (ADIM 2'de yaptık), bu yüzden servo motor da aynı GND'ye bağlanmış olacak

**Görsel Şema:**
```
Breadboard GND (mavi çizgi):
    │
    ├─── Arduino GND (ADIM 2'de bağlandı) ✅
    └─── Servo Motor (⚫ kahverengi/siyah kablo) ✅
```

**✅ Kontrol:** Arduino GND → Breadboard GND → Servo Motor (kahverengi/siyah)

**💡 İPUCU:** Arduino GND zaten breadboard'a bağlı olduğu için, servo motorun GND'sini sadece breadboard'a takman yeterli!

---

### 6.5: Servo Motor Sinyal Bağlantısı (Turuncu/Sarı Kablo) - ADIM ADIM

**🎯 HEDEF:** Servo motorun turuncu/sarı kablosunu Arduino'nun Pin 9'una bağlamak

**⚠️ ÇOK ÖNEMLİ:** Pin 9 PWM pin olmalı! Arduino Uno'da Pin 9'un yanında "~" işareti var.

#### Yöntem 1: Breadboard Üzerinden (ÖNERİLEN)

**Adım 1:** Arduino'nun Pin 9'unu bul
- Arduino'nun üzerinde "DIGITAL" bölümüne bak
- "9" yazan pini bul
- Pin 9'un yanında "~" işareti olmalı (PWM pin)

**Adım 2:** Pin 9'u breadboard'a bağla
1. **Bir jumper kablo al**
2. **Bir ucunu Arduino'nun Pin 9'suna tak**
3. **Diğer ucunu breadboard'a tak** (örneğin D5 deliğine)
   - Breadboard'ın sol tarafındaki D5 deliğine tak

**Adım 3:** Servo motorun sinyal kablosunu aynı satıra tak
1. **Servo motorun turuncu/sarı kablosunu bul**
2. **Breadboard'ın A5 deliğine tak** (D5 ile aynı satır)
   - A5 ve D5 aynı satırda, birbirine bağlı!

**Görsel Şema:**
```
Arduino Uno:
┌─────────────────────┐
│  Pin 9 (~) ────[Jumper]────┼──► Breadboard D5
│                     │              │
│                     │              └─── A5 ──── Servo Motor (🟡 turuncu/sarı kablo)
└─────────────────────┘
```

**✅ Kontrol:** Arduino Pin 9 → Breadboard D5 → A5 → Servo Motor (turuncu/sarı)

---

#### Yöntem 2: Direkt Bağlantı (Alternatif)

Eğer breadboard kullanmak istemiyorsan:
1. **Bir jumper kablo al**
2. **Bir ucunu Arduino'nun Pin 9'suna tak**
3. **Diğer ucunu servo motorun turuncu/sarı kablosuna direkt bağla**

**⚠️ DİKKAT:** Bu yöntem daha karmaşık olabilir, breadboard yöntemi daha kolay!

---

### 6.6: Servo Motor Bağlantı Özeti (Tüm Bağlantılar)

**Tam Bağlantı Şeması:**
```
Arduino Uno:
┌─────────────────────┐
│  5V ────────────────┼──► Breadboard VCC ──── Servo Motor (🔴 kırmızı)
│                     │
│  GND ───────────────┼──► Breadboard GND ──── Servo Motor (⚫ kahverengi/siyah)
│                     │
│  Pin 9 (~) ─────────┼──► Breadboard D5 ──── A5 ──── Servo Motor (🟡 turuncu/sarı)
│                     │
└─────────────────────┘
```

**✅ Tüm Bağlantılar:**
- ✅ Servo Motor (kırmızı) → Breadboard VCC → Arduino 5V
- ✅ Servo Motor (kahverengi/siyah) → Breadboard GND → Arduino GND
- ✅ Servo Motor (turuncu/sarı) → Breadboard A5 → Breadboard D5 → Arduino Pin 9

**🎉 Servo motor bağlantısı tamam!**

---

## ADIM 7: Bağlantı Kontrolü

Bağlantılarını kontrol et:

### ✅ Yeşil LED (Pin 7):
- ✅ Arduino Pin 7 → Yeşil LED (uzun bacak)
- ✅ Yeşil LED (kısa bacak) → Direnç → GND

### ✅ Sarı LED (Pin 4):
- ✅ Arduino Pin 4 → Sarı LED (uzun bacak)
- ✅ Sarı LED (kısa bacak) → Direnç → GND

### ✅ Kırmızı LED (Pin 6):
- ✅ Arduino Pin 6 → Kırmızı LED (uzun bacak)
- ✅ Kırmızı LED (kısa bacak) → Direnç → GND

### ✅ Servo Motor:
- ✅ Arduino 5V → Servo Motor (kırmızı kablo)
- ✅ Arduino GND → Servo Motor (kahverengi/siyah kablo)
- ✅ Arduino Pin 9 → Servo Motor (turuncu/sarı kablo)

### ✅ GND:
- ✅ Arduino GND → Breadboard GND hattı (mavi çizgi)
- ✅ Tüm LED dirençleri → Breadboard GND hattı
- ✅ Servo Motor GND → Breadboard GND hattı

**✅ Tüm bağlantılar tamam!**

---

## ADIM 8: Arduino Kodunu Yükle

### 8.1: Arduino IDE'yi İndir ve Kur

1. **Arduino IDE'yi indir:**
   - https://www.arduino.cc/en/software adresine git
   - "Windows Installer" seçeneğini indir
   - İndirilen dosyayı çalıştır ve kurulumu tamamla

### 8.2: Arduino'yu Bilgisayara Bağla

1. **Arduino'yu USB kablosu ile bilgisayara bağla**
2. **Windows'ta COM portunu kontrol et:**
   - Windows tuşu + X → "Aygıt Yöneticisi"
   - "Portlar (COM & LPT)" bölümünü aç
   - "Arduino Uno (COM3)" veya benzeri bir şey görmelisin
   - COM port numarasını not et (örneğin: COM3)

### 8.3: Arduino IDE'de Ayarları Yap

1. **Arduino IDE'yi aç**
2. **Tools > Board > Arduino Uno** seç
3. **Tools > Port > COM3** seç (veya Arduino'nun bağlı olduğu port)

### 8.4: Kodu Yükle

1. **lockbox_controller.ino dosyasını aç**
   - Arduino IDE'de File > Open
   - Proje klasöründeki `LockboxQr/Arduino/lockbox_controller/lockbox_controller.ino` dosyasını seç
2. **Upload butonuna tıkla** (→ ikonu, sağ üstte)
3. **"Done uploading"** mesajını bekle
4. **Eğer hata varsa:**
   - COM portunu kontrol et
   - Arduino'nun USB kablosunun bağlı olduğundan emin ol
   - Board seçimini kontrol et

**✅ Kod yüklendi!**

---

## ADIM 9: İlk Test (Serial Monitor)

### 9.1: Serial Monitor'ü Aç

1. **Arduino IDE'de Serial Monitor'ü aç** (sağ üstteki 🔍 ikonu)
2. **Baud Rate: 9600** seç (sağ altta)
3. **Şu mesajları görmelisin:**
   ```
   ========================================
   LOCKBOX_CONTROLLER_READY
   ========================================
   Servo Motor: AKTİF (Pin 9)
   LED: AKTİF (Pin 4, 6, 7)
     - Yeşil (Pin 7): AVAILABLE
     - Sarı (Pin 4): RENTED
     - Kırmızı (Pin 6): LOCKED
   Baud Rate: 9600
   Otomatik Kilitlenme: 15 saniye
   ========================================
   ```

**✅ Arduino çalışıyor!**

### 9.2: LED Testi

1. **Yeşil LED yanmalı** (başlangıçta müsait durumu)
2. **Serial Monitor'de şu komutu yaz:**
   ```
   SET_STATUS:KASA1:RENTED
   ```
3. **Enter'a bas**
4. **Sarı LED yanmalı**, Yeşil ve Kırmızı LED sönmeli (açık durumu)
5. **Şu komutu yaz:**
   ```
   SET_STATUS:KASA1:LOCKED
   ```
6. **Enter'a bas**
7. **Kırmızı LED yanmalı**, Yeşil ve Sarı LED sönmeli (kilitli durumu)
8. **Şu komutu yaz:**
   ```
   SET_STATUS:KASA1:AVAILABLE
   ```
9. **Enter'a bas**
10. **Yeşil LED yanmalı**, Sarı ve Kırmızı LED sönmeli (müsait durumu)

**✅ Tüm LED'ler çalışıyor!**

### 9.3: Servo Motor Testi

1. **Serial Monitor'de şu komutu yaz:**
   ```
   UNLOCK:KASA1
   ```
2. **Enter'a bas**
3. **Servo motor 90° dönmeli** (açık pozisyon)
4. **15 saniye bekle**
5. **Servo motor otomatik olarak 0° dönmeli** (kilitli pozisyon)

**Veya manuel olarak:**

1. **Şu komutu yaz:**
   ```
   LOCK:KASA1
   ```
2. **Enter'a bas**
3. **Servo motor 0° dönmeli** (kilitli pozisyon)

**✅ Servo motor çalışıyor!**

### 9.4: Serial Monitor'ü Kapat

**⚠️ ÇOK ÖNEMLİ:** Serial Monitor'ü kapat! C# uygulaması portu kullanamaz.

1. **Serial Monitor penceresini kapat**

---

## ADIM 10: C# Uygulaması ile Test

### 10.1: Visual Studio'da Uygulamayı Çalıştır

1. **Visual Studio'yu aç**
2. **Projeyi aç** (LockboxQr.sln)
3. **F5'e bas** (veya Debug > Start Debugging)

### 10.2: Arduino Bağlantısını Kur

1. **Uygulama açıldığında "Kasalar" sekmesine git**
2. **"Arduino Bağlan" butonuna tıkla**
3. **Port seçim dialogunda:**
   - COM portunu seç (örneğin: COM3)
   - "Bağlan" butonuna tıkla
4. **Bağlantı başarılı mesajını gör**

**✅ Arduino bağlandı!**

### 10.3: Kasa Testi

1. **KASA1'e çift tıkla**
2. **QR kod oluştur dialogunda "QR Oluştur" butonuna tıkla**
3. **Şunları gözlemle:**
   - ✅ Servo motor 90° dönmeli (açık)
   - ✅ **Sarı LED yanmalı** (açık durumu - RENTED)
   - ✅ Yeşil ve Kırmızı LED sönmeli
   - ✅ Kasa rengi sarı olmalı (açık)
4. **"Sayaçsız Kapat" butonuna tıkla**
5. **Şunları gözlemle:**
   - ✅ Servo motor 0° dönmeli (kilitli)
   - ✅ **Kırmızı LED yanmalı** (kilitli durumu - LOCKED)
   - ✅ Yeşil ve Sarı LED sönmeli
   - ✅ Kasa rengi kırmızı olmalı (kilitli)
6. **KASA1'e tekrar çift tıkla ve "Müsait Yap" butonuna bas** (veya başlangıçta)
7. **Şunları gözlemle:**
   - ✅ **Yeşil LED yanmalı** (müsait durumu - AVAILABLE)
   - ✅ Sarı ve Kırmızı LED sönmeli
   - ✅ Kasa rengi yeşil olmalı (müsait)

**✅ Tüm sistem çalışıyor!**

---

## ❌ SORUN GİDERME

### LED Yanmıyor

**Kontrol 1: LED'in yönü**
- ✅ LED'in **uzun bacağı** Arduino pinine bağlı mı?
- ✅ LED'in **kısa bacağı** dirence bağlı mı?
- ❌ **Yanlışsa:** LED'i ters çevir (uzun bacak +, kısa bacak -)

**Kontrol 2: Direnç**
- ✅ Her LED için direnç bağlı mı? (3 adet direnç gerekiyor)
- ✅ Direncin bir ucu LED'in kısa bacağıyla aynı satırda mı?
- ✅ Direncin diğer ucu GND'ye bağlı mı?

**Kontrol 3: Arduino Pinler**
- ✅ Arduino Pin 6 (Kırmızı LED) doğru bağlı mı?
- ✅ Arduino Pin 7 (Yeşil LED) doğru bağlı mı?
- ✅ Arduino Pin 4 (Sarı LED) doğru bağlı mı?
- ✅ Jumper kablolar gevşek değil mi?

**Kontrol 4: GND**
- ✅ Arduino GND → Breadboard GND bağlı mı?
- ✅ Tüm LED dirençleri GND'ye bağlı mı?

**Kontrol 5: Kod**
- ✅ Kod yüklendi mi? (Upload butonuna bastın mı?)
- ✅ Serial Monitor'de "LOCKBOX_CONTROLLER_READY" mesajı görünüyor mu?
- ✅ Serial Monitor'de "LED: AKTİF (Pin 4, 6, 7)" mesajı görünüyor mu?

**Kontrol 6: Test Komutları**
- ✅ `SET_STATUS:KASA1:AVAILABLE` komutu → Yeşil LED yanıyor mu?
- ✅ `SET_STATUS:KASA1:RENTED` komutu → Sarı LED yanıyor mu?
- ✅ `SET_STATUS:KASA1:LOCKED` komutu → Kırmızı LED yanıyor mu?

### Servo Motor Çalışmıyor

**Kontrol 1: Güç Bağlantısı**
- ✅ Servo motorun kırmızı kablosu 5V'a bağlı mı?
- ✅ Arduino'ya güç verildi mi? (USB bağlı mı?)

**Kontrol 2: GND Bağlantısı**
- ✅ Servo motorun kahverengi/siyah kablosu GND'ye bağlı mı?
- ✅ Arduino GND ile servo GND ortak mı?

**Kontrol 3: Sinyal Bağlantısı**
- ✅ Servo motorun turuncu/sarı kablosu Pin 9'a bağlı mı?
- ✅ Pin 9 doğru pin mi? (PWM pin - ~ işareti var mı?)

**Kontrol 4: Servo Motor**
- ✅ Servo motor çalışıyor mu? (Elle döndürebiliyor musun?)
- ✅ Servo motorun kabloları kopuk değil mi?

**Kontrol 5: Kod**
- ✅ Kod yüklendi mi?
- ✅ Serial Monitor'de "Servo Motor: AKTİF" mesajı görünüyor mu?
- ✅ `UNLOCK:KASA1` komutu gönderildiğinde servo hareket ediyor mu?

### Arduino Bağlanmıyor (C# Uygulaması)

**Kontrol 1: COM Port**
- ✅ Arduino IDE'de hangi COM portu görünüyor?
- ✅ C# uygulamasında aynı COM portu seçtin mi?

**Kontrol 2: Serial Monitor**
- ✅ Serial Monitor kapalı mı? (Çok önemli! C# uygulaması portu kullanamaz)

**Kontrol 3: USB Kablo**
- ✅ USB kablosu bağlı mı?
- ✅ USB kablosu veri aktarımı yapıyor mu? (Sadece şarj kablosu değil)

**Kontrol 4: Arduino**
- ✅ Arduino çalışıyor mu? (LED yanıyor mu? Serial Monitor'de mesaj görünüyor mu?)

---

## 💡 İPUÇLARI

1. **LED'in yönü çok önemli!** Uzun bacak +, kısa bacak -
2. **Her LED için direnç mutlaka olmalı!** 3 LED = 3 direnç gerekiyor. Direnç olmadan LED yanar ama çok parlak yanar ve bozulabilir
3. **GND ortak olmalı!** Arduino GND, tüm LED dirençleri ve servo GND aynı GND hattına bağlı olmalı
4. **Breadboard'da aynı satır = bağlı!** Aynı satırdaki delikler birbirine bağlıdır
5. **Test etmeden önce kontrol et!** Bağlantıları 2-3 kez kontrol et
6. **Servo motor için PWM pin gerekli!** Pin 9 PWM pin (~ işareti var)
7. **Serial Monitor'ü kapat!** C# uygulaması portu kullanamaz
8. **Aynı anda sadece bir LED yanar!** Duruma göre ilgili LED yanar, diğerleri söner
9. **Pin numaralarını karıştırma!** Pin 4 (Sarı), Pin 6 (Kırmızı), Pin 7 (Yeşil)

---

## ✅ BAŞARI KRİTERLERİ

- ✅ Arduino kodları yüklendi
- ✅ Serial Monitor'de "LOCKBOX_CONTROLLER_READY" mesajı görünüyor
- ✅ Serial Monitor'de "LED: AKTİF (Pin 4, 6, 7)" mesajı görünüyor
- ✅ Başlangıçta Yeşil LED yanıyor (müsait - AVAILABLE)
- ✅ `SET_STATUS:KASA1:RENTED` komutu → Sarı LED yanıyor, diğerleri sönüyor
- ✅ `SET_STATUS:KASA1:LOCKED` komutu → Kırmızı LED yanıyor, diğerleri sönüyor
- ✅ `SET_STATUS:KASA1:AVAILABLE` komutu → Yeşil LED yanıyor, diğerleri sönüyor
- ✅ `UNLOCK:KASA1` komutu → Servo motor 90° dönüyor
- ✅ `LOCK:KASA1` komutu → Servo motor 0° dönüyor
- ✅ C# uygulaması ile bağlantı kuruldu
- ✅ KASA1 kiralanınca (QR üretilince) Servo motor açılıyor ve Sarı LED yanıyor
- ✅ KASA1 kilitlenince Servo motor kilitleniyor ve Kırmızı LED yanıyor
- ✅ KASA1 müsait olunca Yeşil LED yanıyor

---

## 🎉 TAMAMLANDI!

Arduino bağlantısı tamamlandı! Artık C# uygulaması ile test edebilirsin.

**Sonraki adım:** Hocaya sunum için hazır!

---

## 📞 YARDIM

Eğer bağlantı yaparken sorun yaşarsan:

1. **Fotoğraf çek** (breadboard ve Arduino)
2. **Hata mesajlarını not et**
3. **Serial Monitor çıktısını paylaş**
4. **Bağlantı şemasını kontrol et**

**Unutma:** 
- LED'in yönü çok önemli! Uzun bacak +, kısa bacak -
- Direnç mutlaka olmalı!
- Serial Monitor'ü kapat! C# uygulaması portu kullanamaz

