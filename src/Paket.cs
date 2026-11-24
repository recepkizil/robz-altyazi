// CouchSubs'ın GTA 6 çeviri paketi: indirir, günceller ve cümle arar.
// Kaynak: github.com/yorulmazsinan/couchsubs (AGPL-3.0). Paket oyuncularla büyür, her gece yenilenir.
// Sadece indirir; bu bilgisayardan hiçbir satır gönderilmez.
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RobzAltyazi;

sealed class Paket
{
    const string Sunucu = "https://couchsubs-packs.enmera.workers.dev/v1/packs";
    const string PaketAdi = "gta-6";

    readonly Dictionary<ulong, string> sozluk = new();
    public int SatirSayisi => sozluk.Count;

    static string Klasor => Ayarlar.Klasor;
    static string PaketYolu => Path.Combine(Klasor, PaketAdi + ".json");
    static string DurumYolu => Path.Combine(Klasor, "paket-durum.json");

    // Sunucuda yeni sürüm varsa indirir. Hata olursa eldeki paket kalır.
    public static async Task<string> GuncelleAsync()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("RobzAltyazi/0.1");
            using var liste = JsonDocument.Parse(await http.GetStringAsync(Sunucu));
            var dizi = liste.RootElement.ValueKind == JsonValueKind.Array
                ? liste.RootElement
                : liste.RootElement.GetProperty("packs");
            JsonElement? bilgi = null;
            foreach (var p in dizi.EnumerateArray())
                if (p.TryGetProperty("id", out var id) && id.GetString() == PaketAdi) { bilgi = p; break; }
            if (bilgi is null) return "paket sunucuda yok";

            var surum = bilgi.Value.GetProperty("version").ToString();
            var satir = bilgi.Value.TryGetProperty("entries", out var e) ? e.ToString() : "?";
            var durum = DurumOku();
            if (File.Exists(PaketYolu) && durum == surum) return $"güncel (sürüm {surum}, {satir} satır)";

            var gz = await http.GetByteArrayAsync($"{Sunucu}/{PaketAdi}");
            var ozet = Convert.ToHexString(SHA256.HashData(gz)).ToLowerInvariant();
            var beklenen = bilgi.Value.TryGetProperty("sha256", out var s) ? s.GetString() : null;
            if (beklenen != null && ozet != beklenen.ToLowerInvariant()) return "indirilen dosya bozuk, eski sürüm kaldı";

            using var giris = new GZipStream(new MemoryStream(gz), CompressionMode.Decompress);
            using var cikis = new MemoryStream();
            await giris.CopyToAsync(cikis);
            var json = cikis.ToArray();
            using (JsonDocument.Parse(json)) { } // geçerli mi
            var gecici = PaketYolu + ".tmp";
            await File.WriteAllBytesAsync(gecici, json);
            File.Move(gecici, PaketYolu, overwrite: true);
            await File.WriteAllTextAsync(DurumYolu,
                JsonSerializer.Serialize(new Dictionary<string, object> {
                    [PaketAdi] = new Dictionary<string, string> { ["version"] = surum, ["entries"] = satir, ["tarih"] = DateTime.UtcNow.ToString("o") }
                }));
            return $"sürüm {surum} indirildi ({satir} satır)";
        }
        catch (Exception ex)
        {
            return File.Exists(PaketYolu) ? $"güncellenemedi, eldeki paket kullanılıyor ({ex.Message})" : $"indirilemedi: {ex.Message}";
        }
    }

    static string? DurumOku()
    {
        try
        {
            using var d = JsonDocument.Parse(File.ReadAllText(DurumYolu));
            return d.RootElement.GetProperty(PaketAdi).GetProperty("version").GetString();
        }
        catch { return null; }
    }

    public void Yukle()
    {
        sozluk.Clear();
        if (!File.Exists(PaketYolu)) return;
        using var akis = File.OpenRead(PaketYolu);
        using var json = JsonDocument.Parse(akis);
        foreach (var alan in new[] { "segments", "entries" })
        {
            if (!json.RootElement.TryGetProperty(alan, out var nesne) || nesne.ValueKind != JsonValueKind.Object) continue;
            foreach (var p in nesne.EnumerateObject())
                if (ulong.TryParse(p.Name, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var anahtar) && p.Value.GetString() is { } tr)
                    sozluk[anahtar] = tr;
        }
    }

    // Pakette varsa Türkçesi. Tek kelimelik yazılar bağlama göre değiştiği için aranmaz.
    public string? Bul(string ingilizce)
    {
        var sade = Sadelestir(ingilizce);
        if (sade.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length < 2) return null;
        var ozet = SHA256.HashData(Encoding.UTF8.GetBytes(sade));
        ulong anahtar = 0;
        for (int i = 0; i < 8; i++) anahtar = (anahtar << 8) | ozet[i];
        return sozluk.TryGetValue(anahtar, out var tr) ? tr : null;
    }

    // CouchSubs PackKey.canon ile aynı: etiketler boşluk, NFKC, küçük harf, aksanlar atılır,
    // harf/rakam dışı her şey boşluk, boşluklar teklenir.
    static readonly Regex Etiket = new("<[^<>]{0,40}>", RegexOptions.Compiled);
    public static string Sadelestir(string metin)
    {
        var s = Etiket.Replace(metin, " ")
            .Normalize(NormalizationForm.FormKC)
            .ToLowerInvariant()
            .Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(s.Length);
        bool bosluk = false;
        foreach (var r in s.EnumerateRunes())
        {
            switch (Rune.GetUnicodeCategory(r))
            {
                case UnicodeCategory.NonSpacingMark:
                case UnicodeCategory.SpacingCombiningMark:
                case UnicodeCategory.EnclosingMark:
                    continue;
                case UnicodeCategory.UppercaseLetter:
                case UnicodeCategory.LowercaseLetter:
                case UnicodeCategory.TitlecaseLetter:
                case UnicodeCategory.ModifierLetter:
                case UnicodeCategory.OtherLetter:
                case UnicodeCategory.DecimalDigitNumber:
                case UnicodeCategory.LetterNumber:
                case UnicodeCategory.OtherNumber:
                    if (bosluk && sb.Length > 0) sb.Append(' ');
                    bosluk = false;
                    sb.Append(r.ToString());
                    break;
                default:
                    bosluk = true;
                    break;
            }
        }
        return sb.ToString();
    }
}
