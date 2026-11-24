// Robz Play penceresini bulur, altyazı bölgesinin görüntüsünü alır ve Windows'un yazı tanıma (OCR)
// özelliğiyle İngilizce altyazıyı okur. Mac sürümündeki seçme kurallarının aynısı:
// ortalanmış, çoğunlukla harf, en büyük yazılı, en alttaki yazı bloğu.
using System.Diagnostics;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Graphics.Imaging;
using Windows.Media.Ocr;

namespace RobzAltyazi;

sealed class Okuyucu
{
    readonly Ayarlar a;
    readonly OcrEngine ocr;

    Okuyucu(Ayarlar a, OcrEngine ocr) { this.a = a; this.ocr = ocr; }

    // İngilizce OCR dili yüklü değilse null döner
    public static Okuyucu? Olustur(Ayarlar a)
    {
        var motor = OcrEngine.TryCreateFromLanguage(new Windows.Globalization.Language("en-US"));
        if (motor == null)
        {
            foreach (var dil in OcrEngine.AvailableRecognizerLanguages)
                if (dil.LanguageTag.StartsWith("en", StringComparison.OrdinalIgnoreCase))
                {
                    motor = OcrEngine.TryCreateFromLanguage(dil);
                    if (motor != null) break;
                }
        }
        return motor == null ? null : new Okuyucu(a, motor);
    }

    // ---- Pencere bulma -------------------------------------------------------------------

    // Robz Play penceresinin içerik alanı (ekran pikseli). Küçültülmüşse ya da yoksa null.
    public Rectangle? OyunPenceresi()
    {
        var adlar = a.Uygulama.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(x => x.Replace(" ", "").ToLowerInvariant()).ToArray();
        Rectangle? en = null;
        foreach (var p in Process.GetProcesses())
        {
            try
            {
                var ad = p.ProcessName.Replace(" ", "").ToLowerInvariant();
                if (!adlar.Any(ad.Contains)) continue;
                var h = p.MainWindowHandle;
                if (h == IntPtr.Zero || !Yerel.IsWindowVisible(h) || Yerel.IsIconic(h)) continue;
                if (!Yerel.GetClientRect(h, out var ic)) continue;
                var sol = new Yerel.POINT { X = 0, Y = 0 };
                Yerel.ClientToScreen(h, ref sol);
                var r = new Rectangle(sol.X, sol.Y, ic.Right - ic.Left, ic.Bottom - ic.Top);
                if (r.Width < 300 || r.Height < 200) continue;
                if (en == null || r.Width * r.Height > en.Value.Width * en.Value.Height) en = r;
            }
            catch { /* erişilemeyen süreçler */ }
            finally { p.Dispose(); }
        }
        return en;
    }

    // ---- Okuma ----------------------------------------------------------------------------

    public async Task<string> OkuAsync(Rectangle oyun)
    {
        var b = a.Bolge;
        var bolge = new Rectangle(
            oyun.X + (int)(b[0] * oyun.Width), oyun.Y + (int)(b[1] * oyun.Height),
            Math.Max(1, (int)(b[2] * oyun.Width)), Math.Max(1, (int)(b[3] * oyun.Height)));

        using var ekran = new Bitmap(bolge.Width, bolge.Height, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(ekran))
            g.CopyFromScreen(bolge.Location, Point.Empty, bolge.Size, CopyPixelOperation.SourceCopy);

        // OCR'ın kabul ettiği en büyük boyutu aşarsa küçült (4K ekranlar)
        var enFazla = (int)OcrEngine.MaxImageDimension;
        var oran = Math.Min(1.0, (double)enFazla / Math.Max(ekran.Width, ekran.Height));
        using var resim = oran < 1.0
            ? new Bitmap(ekran, Math.Max(1, (int)(ekran.Width * oran)), Math.Max(1, (int)(ekran.Height * oran)))
            : (Bitmap)ekran.Clone();

        using var yazilim = YazilimResmi(resim);
        var sonuc = await ocr.RecognizeAsync(yazilim);

        var satirlar = new List<(RectangleF kutu, string yazi)>();
        foreach (var satir in sonuc.Lines)
        {
            if (satir.Words.Count == 0) continue;
            float x0 = float.MaxValue, y0 = float.MaxValue, x1 = 0, y1 = 0;
            foreach (var k in satir.Words)
            {
                var r = k.BoundingRect;
                x0 = Math.Min(x0, (float)r.X); y0 = Math.Min(y0, (float)r.Y);
                x1 = Math.Max(x1, (float)(r.X + r.Width)); y1 = Math.Max(y1, (float)(r.Y + r.Height));
            }
            // 0-1 oranlarına çevir (sol üst başlangıç)
            var kutu = new RectangleF(x0 / resim.Width, y0 / resim.Height, (x1 - x0) / resim.Width, (y1 - y0) / resim.Height);
            // Çok küçük yazıları (HUD, harita) yok say
            if (kutu.Height < 0.04f) continue;
            satirlar.Add((kutu, satir.Text.Trim()));
        }
        return AltyaziSec(satirlar);
    }

    // GDI resmini Windows OCR'ın istediği SoftwareBitmap'e çevirir (BGRA, satır boşluksuz)
    static SoftwareBitmap YazilimResmi(Bitmap resim)
    {
        int w = resim.Width, h = resim.Height;
        var veri = resim.LockBits(new Rectangle(0, 0, w, h), ImageLockMode.ReadOnly, PixelFormat.Format32bppArgb);
        try
        {
            var bayt = new byte[w * 4 * h];
            for (int y = 0; y < h; y++)
                Marshal.Copy(IntPtr.Add(veri.Scan0, y * veri.Stride), bayt, y * w * 4, w * 4);
            return SoftwareBitmap.CreateCopyFromBuffer(bayt.AsBuffer(), BitmapPixelFormat.Bgra8, w, h, BitmapAlphaMode.Premultiplied);
        }
        finally { resim.UnlockBits(veri); }
    }

    // Okunan satırlar arasından gerçek altyazıyı seçer.
    static string AltyaziSec(List<(RectangleF kutu, string yazi)> satirlar)
    {
        static int Harf(string s) => s.Count(char.IsLetter);
        // 1) Ortalanmış ve çoğunlukla harften oluşan satırlar (köşedeki FPS/HUD/sayılar elenir)
        var adaylar = satirlar.Where(s =>
        {
            var m = new string(s.yazi.Where(c => !char.IsWhiteSpace(c)).ToArray());
            var h = Harf(m);
            var ortaX = s.kutu.X + s.kutu.Width / 2;
            return Math.Abs(ortaX - 0.5f) <= 0.2f && h >= 2 && (double)h / Math.Max(m.Length, 1) >= 0.6;
        }).ToList();
        if (adaylar.Count == 0) return "";

        // 2) Altyazı boyundaki satırlar (en büyük yazının en az %60'ı)
        var enBuyuk = adaylar.Max(s => s.kutu.Height);
        adaylar = adaylar.Where(s => s.kutu.Height >= enBuyuk * 0.6f).OrderByDescending(s => s.kutu.Y).ToList(); // alttan yukarı

        // 3) Satırları bloklara ayır (aralarındaki boşluk küçükse aynı blok)
        var bloklar = new List<List<(RectangleF kutu, string yazi)>>();
        foreach (var s in adaylar)
        {
            var son = bloklar.Count > 0 ? bloklar[^1][^1] : default;
            if (bloklar.Count > 0 && son.kutu.Y - (s.kutu.Y + s.kutu.Height) <= son.kutu.Height * 1.2f)
                bloklar[^1].Add(s);
            else
                bloklar.Add(new() { s });
        }
        // 4) En büyük yazılı blok; boyları yakınsa en alttaki
        static float Boy(List<(RectangleF kutu, string yazi)> b) => b.Max(s => s.kutu.Height);
        var enIri = bloklar.Max(Boy);
        var blok = bloklar.First(b => Boy(b) >= enIri * 0.9f);

        var sonuc = string.Join(" ", Enumerable.Reverse(blok).Select(s => s.yazi)); // yukarıdan aşağı
        sonuc = System.Text.RegularExpressions.Regex.Replace(sonuc, @"\s+", " ").Trim();

        // Kısa yazılar: plaka/kod gibi rakamlı ya da çok az harfli şeyler altyazı değildir
        var kelimeler = sonuc.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (kelimeler.Length <= 2 && sonuc.Any(char.IsDigit)) return "";
        if (Harf(sonuc) < 4) return "";
        return sonuc;
    }
}

static class Yerel
{
    [StructLayout(LayoutKind.Sequential)] public struct RECT { public int Left, Top, Right, Bottom; }
    [StructLayout(LayoutKind.Sequential)] public struct POINT { public int X, Y; }
    [DllImport("user32.dll")] public static extern bool GetClientRect(IntPtr h, out RECT r);
    [DllImport("user32.dll")] public static extern bool ClientToScreen(IntPtr h, ref POINT p);
    [DllImport("user32.dll")] public static extern bool IsWindowVisible(IntPtr h);
    [DllImport("user32.dll")] public static extern bool IsIconic(IntPtr h);
    [DllImport("user32.dll")] public static extern bool SetWindowDisplayAffinity(IntPtr h, uint affinity);
    public const uint WDA_EXCLUDEFROMCAPTURE = 0x11;
}
