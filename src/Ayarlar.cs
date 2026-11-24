// Ayarlar: %LOCALAPPDATA%\RobzAltyazi\ayarlar.json (ilk açılışta varsayılanlarla oluşturulur).
using System.Text.Json;

namespace RobzAltyazi;

sealed class Ayarlar
{
    public static string Klasor { get; } = Directory.CreateDirectory(
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "RobzAltyazi")).FullName;
    static string Yol => Path.Combine(Klasor, "ayarlar.json");

    // Saniyede kaç kez okunsun (çok artırmak oyunu yavaşlatabilir)
    public double Fps { get; set; } = 4;
    // Altyazının arandığı bölge, Robz Play penceresine göre 0-1 oranları: sol, üst, genişlik, yükseklik
    public double[] Bolge { get; set; } = { 0.15, 0.76, 0.70, 0.22 };
    // Pakette olmayan cümleleri internetten (Google) çevir. Kapalıysa yalnız paketteki cümleler gösterilir.
    public bool CevrimiciCeviri { get; set; } = true;
    // Türkçe yazının pencerenin altından uzaklığı (pencere yüksekliğine oran)
    public double AltBosluk { get; set; } = 0.07;
    // Yazı boyu (pencere yüksekliğine oran)
    public double YaziBoyu { get; set; } = 0.037;
    // Altyazı kaybolduktan kaç saniye sonra silinsin
    public double SilmeSuresi { get; set; } = 0.8;
    // Aranacak pencere sahibi programın adı (küçük/büyük harf fark etmez, virgülle birden çok)
    public string Uygulama { get; set; } = "RobzPlay,Robz Play,chiaki";

    public static Ayarlar Oku()
    {
        var secenek = new JsonSerializerOptions { WriteIndented = true };
        try
        {
            if (File.Exists(Yol) && JsonSerializer.Deserialize<Ayarlar>(File.ReadAllText(Yol)) is { } a) return a;
        }
        catch { /* bozuksa varsayılana dön */ }
        var yeni = new Ayarlar();
        try { File.WriteAllText(Yol, JsonSerializer.Serialize(yeni, secenek)); } catch { }
        return yeni;
    }
}
