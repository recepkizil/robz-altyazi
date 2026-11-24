// Robz Altyazı: Robz Play ile oynarken GTA 6'nın İngilizce altyazısını okur, CouchSubs'ın Türkçe
// paketinden (yoksa Google'dan) çevirir ve oyunun üstüne yazar. Sistem tepsisinde (saatin yanında) durur.
namespace RobzAltyazi;

static class Kayit
{
    static readonly string Yol = Path.Combine(Ayarlar.Klasor, "kayit.txt");
    static readonly object Kilit = new();
    public static void Yaz(string satir)
    {
        lock (Kilit)
        {
            try
            {
                if (File.Exists(Yol) && new FileInfo(Yol).Length > 2_000_000) File.Delete(Yol);
                File.AppendAllText(Yol, $"[{DateTime.Now:HH:mm:ss}] {satir}{Environment.NewLine}");
            }
            catch { }
        }
    }
}

static class Program
{
    [STAThread]
    static void Main()
    {
        using var tek = new Mutex(true, "RobzAltyazi-tek", out var ilk);
        if (!ilk) { MessageBox.Show("Robz Altyazı zaten çalışıyor (saatin yanındaki simge).", "Robz Altyazı"); return; }
        ApplicationConfiguration.Initialize();
        Application.Run(new Uygulama());
    }
}

sealed class Uygulama : ApplicationContext
{
    readonly Ayarlar a = Ayarlar.Oku();
    readonly NotifyIcon simge;
    readonly Katman katman;
    readonly CancellationTokenSource dur = new();

    public Uygulama()
    {
        katman = new Katman(a);
        var menu = new ContextMenuStrip();
        menu.Items.Add("Kayıt klasörünü aç", null, (_, _) =>
            System.Diagnostics.Process.Start("explorer.exe", Ayarlar.Klasor));
        menu.Items.Add("Çıkış", null, (_, _) => Kapat());
        simge = new NotifyIcon
        {
            Icon = SystemIcons.Information,
            Text = "Robz Altyazı: hazırlanıyor…",
            ContextMenuStrip = menu,
            Visible = true,
        };
        _ = CalisAsync();
    }

    void Durum(string s)
    {
        simge.Text = ("Robz Altyazı: " + s).Length > 63 ? ("Robz Altyazı: " + s)[..63] : "Robz Altyazı: " + s;
        Kayit.Yaz(s);
    }

    void Kapat()
    {
        dur.Cancel();
        simge.Visible = false;
        katman.Close();
        ExitThread();
    }

    async Task CalisAsync()
    {
        await Task.Yield(); // mesaj döngüsü başlasın (erken kapanışta Application.Run askıda kalmasın)
        Kayit.Yaz("---- Robz Altyazı açıldı ----");
        var okuyucu = Okuyucu.Olustur(a);
        if (okuyucu == null)
        {
            Durum("İngilizce yazı tanıma yok");
            MessageBox.Show(
                "Windows'ta İngilizce yazı tanıma (OCR) yüklü değil.\n\n" +
                "Başlat menüsünde PowerShell'e sağ tıklayıp \"Yönetici olarak çalıştır\" deyin ve şunu yazın:\n\n" +
                "Add-WindowsCapability -Online -Name \"Language.OCR~~~en-US~0.0.1.0\"\n\n" +
                "Bitince Robz Altyazı'yı yeniden açın.", "Robz Altyazı", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            Kapat();
            return;
        }

        Durum("çeviri paketi kontrol ediliyor…");
        var sonuc = await Paket.GuncelleAsync();
        var paket = new Paket();
        await Task.Run(paket.Yukle);
        Durum($"GTA 6 paketi: {sonuc}; {paket.SatirSayisi} satır yüklendi");
        if (paket.SatirSayisi == 0 && !a.CevrimiciCeviri)
            MessageBox.Show("Çeviri paketi indirilemedi ve çevrimiçi çeviri kapalı. İnternet bağlantısını kontrol edin.", "Robz Altyazı");
        var cevirmen = new Cevirmen(paket, a);
        simge.ShowBalloonTip(4000, "Robz Altyazı çalışıyor",
            $"Robz Play'de oyuna girince Türkçe altyazı görünecek. Paket: {paket.SatirSayisi:N0} satır.", ToolTipIcon.Info);

        var aralik = TimeSpan.FromSeconds(1.0 / Math.Clamp(a.Fps, 0.5, 10));
        string sonIngilizce = "", ekranda = "";
        double bosSure = 0;
        bool pencereVardi = false;
        int hata = 0;

        while (!dur.IsCancellationRequested)
        {
            var basla = DateTime.UtcNow;
            try
            {
                var oyun = okuyucu.OyunPenceresi();
                if (oyun is not { } o)
                {
                    if (pencereVardi) { Durum("Robz Play penceresi bekleniyor"); pencereVardi = false; }
                    katman.Goster("", null); ekranda = ""; sonIngilizce = "";
                    await Task.Delay(1000, dur.Token);
                    continue;
                }
                if (!pencereVardi) { Durum($"Robz Play okunuyor ({o.Width}x{o.Height})"); pencereVardi = true; }

                var yazi = await okuyucu.OkuAsync(o);
                hata = 0;
                if (yazi.Length < 3)
                {
                    bosSure += aralik.TotalSeconds;
                    if (ekranda.Length > 0 && bosSure >= a.SilmeSuresi) { ekranda = ""; sonIngilizce = ""; }
                }
                else
                {
                    bosSure = 0;
                    // Ekrandakiyle aynı altyazıysa tekrar çevirme
                    if (sonIngilizce.Length == 0 || Benzerlik(yazi, sonIngilizce) <= 0.85)
                    {
                        sonIngilizce = yazi;
                        var (tr, kaynak) = await cevirmen.CevirAsync(yazi);
                        if (tr != null)
                        {
                            ekranda = tr;
                            Kayit.Yaz($"EN: {yazi}\n           TR ({kaynak}): {tr}");
                        }
                    }
                }
                katman.Goster(ekranda, o);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                if (hata++ == 0) Kayit.Yaz($"⚠️ {ex.GetType().Name}: {ex.Message}");
                try { await Task.Delay(500, dur.Token); } catch { break; }
            }
            var kalan = aralik - (DateTime.UtcNow - basla);
            if (kalan > TimeSpan.Zero)
                try { await Task.Delay(kalan, dur.Token); } catch { break; }
        }
    }

    // 0-1 arası benzerlik (harf/rakam üzerinden Levenshtein)
    static double Benzerlik(string a, string b)
    {
        static string Sade(string s) => new(s.ToLowerInvariant().Where(c => char.IsLetterOrDigit(c) || c == ' ').ToArray());
        var x = Sade(a); var y = Sade(b);
        if (x.Length == 0 && y.Length == 0) return 1;
        if (x.Length == 0 || y.Length == 0) return 0;
        var onceki = Enumerable.Range(0, y.Length + 1).ToArray();
        var simdiki = new int[y.Length + 1];
        for (int i = 1; i <= x.Length; i++)
        {
            simdiki[0] = i;
            for (int j = 1; j <= y.Length; j++)
                simdiki[j] = x[i - 1] == y[j - 1] ? onceki[j - 1] : 1 + Math.Min(onceki[j - 1], Math.Min(onceki[j], simdiki[j - 1]));
            (onceki, simdiki) = (simdiki, onceki);
        }
        return 1 - (double)onceki[y.Length] / Math.Max(x.Length, y.Length);
    }
}
