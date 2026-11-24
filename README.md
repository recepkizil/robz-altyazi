# Robz Altyazı (Windows)

[Robz Play](https://github.com/kutsalkiremitci/robz-play) ile PS5'te GTA VI oynarken **Türkçe altyazı**.
Robz Play penceresindeki İngilizce altyazıyı okur, [CouchSubs](https://github.com/yorulmazsinan/couchsubs)'ın
oyuncularla büyüyen GTA 6 Türkçe paketinden çevirir ve oyunun üstüne yazar.

- Pakette olmayan cümleler Google'ın ücretsiz çevirisiyle çevrilir (ayardan kapatılabilir).
- Paket her açılışta kendiliğinden güncellenir. Paket için bilgisayardan bir şey gönderilmez, sadece indirilir.
- Kurulum gerekmez: tek bir `RobzAltyazi.exe`.

## Gerekenler

- Windows 10 (2004 veya üstü) ya da Windows 11, 64 bit
- Robz Play (Windows), PS5'e bağlı ve çalışır durumda
- Windows'ta **İngilizce yazı tanıma** (çoğu bilgisayarda hazır; yoksa program açılışta ne yapılacağını söyler)

## Kullanım

1. [Sürümler](../../releases) sayfasından `RobzAltyazi.exe`'yi indir.
2. Çalıştır. "Windows bilgisayarınızı korudu" uyarısı çıkarsa **Ek bilgi → Yine de çalıştır**.
3. Saatin yanında bir simge belirir. İlk açılışta çeviri paketi iner (birkaç saniye).
4. Robz Play'i aç, oyuna bağlan. Biri konuşunca Türkçesi İngilizce altyazının üstünde görünür.
5. Kapatmak için: saatin yanındaki simgeye sağ tık → **Çıkış**.

**İpucu:** Robz Play'i pencerede ya da kenarlıksız tam ekranda kullan. Bazı "özel tam ekran" modlarında
altyazı kutusu oyunun arkasında kalabilir.

## İngilizce yazı tanıma yoksa

Program bunu söylerse: Başlat'a **PowerShell** yaz, sağ tık → **Yönetici olarak çalıştır**, şunu yapıştır:

```powershell
Add-WindowsCapability -Online -Name "Language.OCR~~~en-US~0.0.1.0"
```

Bitince Robz Altyazı'yı yeniden aç.

## Ayarlar

`%LOCALAPPDATA%\RobzAltyazi\ayarlar.json` (simgeye sağ tık → **Kayıt klasörünü aç**). Değiştirip programı yeniden aç.

| Ayar | Varsayılan | Ne işe yarar |
|---|---|---|
| `Fps` | 4 | Saniyede kaç kez okunsun. Oyun kasarsa 2-3 yap. |
| `Bolge` | `[0.15, 0.76, 0.70, 0.22]` | Altyazının arandığı yer (pencereye göre sol, üst, genişlik, yükseklik). |
| `CevrimiciCeviri` | `true` | Pakette olmayan cümleleri Google'la çevir. `false`: sadece paket. |
| `AltBosluk` | 0.07 | Türkçe yazının alttan uzaklığı. İngilizceyi tam kapatmıyorsa oynat. |
| `YaziBoyu` | 0.037 | Yazı büyüklüğü. |
| `SilmeSuresi` | 0.8 | Altyazı bittikten kaç saniye sonra silinsin. |

## Sorun olursa

Aynı klasördeki `kayit.txt`'de okunan her İngilizce cümle ve Türkçesi yazar. Sorun bildirirken bu dosyayı ekle.

## Teşekkür

Türkçe çeviriler [CouchSubs](https://github.com/yorulmazsinan/couchsubs) topluluk paketinden gelir (AGPL-3.0).
