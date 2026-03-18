/*
 * Lockbox Controller - Arduino Uno - ULTRA OPTIMIZED FOR RAM
 * 
 * Bağlantılar:
 * - Servo Motor: Pin 9 (PWM) - Kasa kilidi kontrolü
 * - LED Yeşil: Pin 7 - Müsait durumu (AVAILABLE)
 * - LED Sarı: Pin 4 - Açık durumu (RENTED)
 * - LED Kırmızı: Pin 6 - Kilitli durumu (LOCKED)
 */
#define USE_SERVO
#define USE_LED

#ifdef USE_SERVO
#include <Servo.h>
#endif

#include <string.h> // strchr, strncpy, strcmp için

// Pin tanımlamaları
#ifdef USE_SERVO
const int SERVO_PIN = 9;
Servo lockServo;
// Servo motor açı değerleri - SG90 için genellikle:
// 0° = Kilitli (kapalı) - Bazı servo motorlarda 0° yerine 10-20° kullanılabilir
// 90° = Açık (açık) - Bazı servo motorlarda 90° yerine 80-100° kullanılabilir
// Eğer servo motor çalışmıyorsa, bu değerleri değiştir:
// - SERVO_LOCKED: 0-30 arası deneyin
// - SERVO_UNLOCKED: 80-120 arası deneyin
const int SERVO_LOCKED = 0;      // Kilitli pozisyon (0-30 arası deneyin)
const int SERVO_UNLOCKED = 90;    // Açık pozisyon (80-120 arası deneyin)
#endif

#ifdef USE_LED
const int LED_GREEN_PIN = 7;
const int LED_YELLOW_PIN = 4;
const int LED_RED_PIN = 6;
#endif

// Mevcut durum (char array kullanarak - RAM tasarrufu)
bool isLocked = true;
char currentLocker[8] = "";

// LED durumu (0=AVAILABLE, 1=RENTED, 2=LOCKED)
byte lockerStatus = 0;

// Non-blocking delay
unsigned long unlockStartTime = 0;
bool isUnlocking = false;
const unsigned long UNLOCK_DURATION_MS = 15000;

// Serial buffer (char array - RAM tasarrufu)
char inputBuffer[32] = "";
byte inputIndex = 0;
bool stringComplete = false;

void setup() {
  Serial.begin(9600);
  
  #ifdef USE_SERVO
  lockServo.attach(SERVO_PIN);
  // Servo motoru başlangıç pozisyonuna getir (kilitli)
  lockServo.write(SERVO_LOCKED);
  delay(500); // Servo motorun pozisyonu alması için bekle
  
  // Servo motor testi (isteğe bağlı - test için açıp kapat)
  Serial.println("SERVO_TEST: Testing servo motor...");
  lockServo.write(SERVO_UNLOCKED);
  delay(1000);
  lockServo.write(SERVO_LOCKED);
  delay(500);
  Serial.println("SERVO_TEST: Servo motor test completed");
  #endif
  
  #ifdef USE_LED
  pinMode(LED_GREEN_PIN, OUTPUT);
  pinMode(LED_YELLOW_PIN, OUTPUT);
  pinMode(LED_RED_PIN, OUTPUT);
  updateLEDStatus();
  #endif
  
  Serial.println("READY");
}

void loop() {
  // Serial okuma
  while (Serial.available() > 0) {
    char inChar = (char)Serial.read();
    if (inChar == '\n' || inChar == '\r') {
      if (inputIndex > 0) {
        inputBuffer[inputIndex] = '\0';
        stringComplete = true;
        // Satır sonu bulundu, döngüden çık (break yerine flag kullan)
        while (Serial.available() > 0) Serial.read(); // Kalan veriyi temizle
        break;
      }
    } else if (inputIndex < 31) {
      inputBuffer[inputIndex++] = inChar;
    } else {
      // Buffer dolu, eski veriyi temizle
      inputIndex = 0;
      inputBuffer[0] = '\0';
    }
  }
  
  if (stringComplete) {
    processCommand(inputBuffer);
    inputIndex = 0;
    inputBuffer[0] = '\0';
    stringComplete = false;
  }
  
  // Auto-lock timer
  if (isUnlocking && !isLocked) {
    if (millis() - unlockStartTime >= UNLOCK_DURATION_MS) {
      lockLocker();
      isUnlocking = false;
    }
  }
  
  delay(10);
}

// Yardımcı fonksiyonlar
void toUpper(char* str) {
  for (byte i = 0; str[i]; i++) {
    if (str[i] >= 'a' && str[i] <= 'z') str[i] -= 32;
  }
}

bool startsWith(const char* str, const char* prefix) {
  while (*prefix) if (*str++ != *prefix++) return false;
  return true;
}

bool equals(const char* a, const char* b) {
  while (*a && *b) if (*a++ != *b++) return false;
  return *a == '\0' && *b == '\0';
}

void processCommand(char* cmd) {
  // Boşlukları temizle
  while (*cmd == ' ' || *cmd == '\t') cmd++;
  
  // Komut yoksa çık
  if (!cmd[0]) return;
  
  // DEBUG: Gelen komutu Serial'e yazdır
  Serial.print("DEBUG: Received command: ");
  Serial.println(cmd);
  
  // Büyük harfe çevir (orijinal buffer'ı değiştirir, ama sorun değil)
  toUpper(cmd);
  
  if (startsWith(cmd, "UNLOCK:")) {
    char* id = cmd + 7;
    // Boşlukları temizle
    while (*id == ' ' || *id == '\t') id++;
    Serial.print("DEBUG: Processing UNLOCK command for: ");
    Serial.println(id);
    unlockLocker(id);
  }
  else if (startsWith(cmd, "LOCK:")) {
    char* id = cmd + 5;
    // Boşlukları temizle
    while (*id == ' ' || *id == '\t') id++;
    Serial.print("DEBUG: Processing LOCK command for: ");
    Serial.println(id);
    lockLocker(id);
  }
  else if (equals(cmd, "STATUS")) {
    sendStatus();
  }
  else if (startsWith(cmd, "SET_STATUS:")) {
    char* part = cmd + 11;
    char* colon = strchr(part, ':');
    if (colon) {
      *colon = '\0';
      // Boşlukları temizle
      while (*part == ' ' || *part == '\t') part++;
      if (equals(part, "KASA1")) {
        char* status = colon + 1;
        // Boşlukları temizle
        while (*status == ' ' || *status == '\t') status++;
        if (equals(status, "AVAILABLE")) {
          lockerStatus = 0;
          updateLEDStatus();
        }
        else if (equals(status, "RENTED")) {
          lockerStatus = 1;
          updateLEDStatus();
        }
        else if (equals(status, "LOCKED")) {
          lockerStatus = 2;
          updateLEDStatus();
        }
        else {
          Serial.println("ERROR:INVALID_STATUS");
        }
      }
      else {
        Serial.println("ERROR:UNSUPPORTED_LOCKER");
      }
      *colon = ':'; // Geri al
    }
    else {
      Serial.println("ERROR:INVALID_FORMAT");
    }
  }
  else {
    Serial.println("ERROR:UNKNOWN_COMMAND");
    #ifdef USE_LED
    blinkLED(LED_RED_PIN, 2);
    #endif
  }
}

void unlockLocker(const char* id) {
  if (!id || !id[0]) {
    Serial.println("ERROR:EMPTY_LOCKER_ID");
    return;
  }
  
  #ifdef USE_SERVO
  Serial.print("DEBUG: Servo motor açılıyor - Açı: ");
  Serial.println(SERVO_UNLOCKED);
  lockServo.write(SERVO_UNLOCKED);
  delay(500); // Servo motorun pozisyonu alması için bekle (500ms yeterli)
  Serial.println("DEBUG: Servo motor açıldı (90°)");
  #endif
  
  isLocked = false;
  // Locker ID'yi kopyala (max 7 karakter)
  byte len = 0;
  while (id[len] && len < 7 && id[len] != '\n' && id[len] != '\r') {
    currentLocker[len] = id[len];
    len++;
  }
  currentLocker[len] = '\0';
  
  Serial.print("OK:UNLOCKED:");
  Serial.println(currentLocker);
  
  unlockStartTime = millis();
  isUnlocking = true;
}

void lockLocker() {
  if (currentLocker[0]) {
    lockLocker(currentLocker);
  } else {
    Serial.println("ERROR:NO_LOCKER_ID");
  }
}

void lockLocker(const char* id) {
  if (!id || !id[0]) {
    Serial.println("ERROR:EMPTY_LOCKER_ID");
    return;
  }
  
  #ifdef USE_SERVO
  Serial.print("DEBUG: Servo motor kilitleniyor - Açı: ");
  Serial.println(SERVO_LOCKED);
  lockServo.write(SERVO_LOCKED);
  delay(500); // Servo motorun pozisyonu alması için bekle (500ms yeterli)
  Serial.println("DEBUG: Servo motor kilitlendi (0°)");
  #endif
  
  isLocked = true;
  isUnlocking = false;
  
  // Locker ID'yi kopyala (max 7 karakter) - eğer farklıysa
  char tempId[8] = "";
  byte len = 0;
  while (id[len] && len < 7 && id[len] != '\n' && id[len] != '\r') {
    tempId[len] = id[len];
    len++;
  }
  tempId[len] = '\0';
  
  Serial.print("OK:LOCKED:");
  Serial.println(tempId);
  
  // Eğer bu kasa currentLocker ise temizle
  if (strcmp(currentLocker, tempId) == 0) {
    currentLocker[0] = '\0';
  }
}

void sendStatus() {
  Serial.print("STATUS:");
  Serial.print(isLocked ? "LOCKED" : "UNLOCKED");
  Serial.print(":");
  Serial.println(currentLocker[0] ? currentLocker : "");
}

#ifdef USE_LED
void updateLEDStatus() {
  digitalWrite(LED_GREEN_PIN, LOW);
  digitalWrite(LED_YELLOW_PIN, LOW);
  digitalWrite(LED_RED_PIN, LOW);
  
  if (lockerStatus == 0) {
    digitalWrite(LED_GREEN_PIN, HIGH);
  }
  else if (lockerStatus == 1) {
    digitalWrite(LED_YELLOW_PIN, HIGH);
  }
  else if (lockerStatus == 2) {
    digitalWrite(LED_RED_PIN, HIGH);
  }
}

void blinkLED(int pin, int times) {
  for (int i = 0; i < times; i++) {
    digitalWrite(pin, HIGH);
    delay(100);
    digitalWrite(pin, LOW);
    delay(100);
  }
  updateLEDStatus();
}
#endif
