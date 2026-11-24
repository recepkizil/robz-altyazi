// Çeviri: önce CouchSubs paketi, pakette yoksa (ayar açıksa) Google'ın ücretsiz çevirisi.
using System.Text.Json;

namespace RobzAltyazi;

sealed class Cevirmen
{
    readonly Paket paket;
    readonly Ayarlar a;
    readonly Dictionary<string, string> onbellek = new();
    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(4) };

    public Cevirmen(Paket paket, Ayarlar a) { this.paket = paket; this.a = a; }

    // (çeviri, nereden) — çeviri yoksa null
    public async Task<(string? tr, string kaynak)> CevirAsync(string ingilizce)
    {
        if (paket.Bul(ingilizce) is { } hazir) return (hazir, "paket");
        var anahtar = Paket.Sadelestir(ingilizce);
        if (onbellek.TryGetValue(anahtar, out var eski)) return (eski, "önbellek");
        if (!a.CevrimiciCeviri) return (null, "yok");
        try
        {
            var url = "https://translate.googleapis.com/translate_a/single?client=gtx&sl=en&tl=tr&dt=t&q="
                      + Uri.EscapeDataString(ingilizce);
            using var json = JsonDocument.Parse(await http.GetStringAsync(url));
            var parcalar = json.RootElement[0].EnumerateArray()
                .Select(p => p[0].GetString()).Where(s => !string.IsNullOrEmpty(s));
            var tr = string.Concat(parcalar).Trim();
            if (tr.Length == 0) return (null, "boş");
            onbellek[anahtar] = tr;
            return (tr, "google");
        }
        catch (Exception ex)
        {
            Kayit.Yaz($"⚠️ Çevrimiçi çeviri olmadı: {ex.Message}");
            return (null, "hata");
        }
    }
}
