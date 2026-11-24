// Oyunun üstüne Türkçe altyazıyı yazan şeffaf pencere.
// Tıklamaları geçirir, odağı çalmaz, görev çubuğunda görünmez. Ekran yakalamalarından hariç tutulur:
// böylece altyazı okuyucusu kendi yazdığı Türkçeyi tekrar okumaz.
using System.Runtime.InteropServices;

namespace RobzAltyazi;

sealed class Katman : Form
{
    readonly Ayarlar a;
    string metin = "";
    Font? yazi;
    float yaziPiksel;

    public Katman(Ayarlar a)
    {
        this.a = a;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        TopMost = true;
        StartPosition = FormStartPosition.Manual;
        BackColor = Color.Black;
        Opacity = 0.88;
        DoubleBuffered = true;
        Size = new Size(10, 10);
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            const int WS_EX_TRANSPARENT = 0x20, WS_EX_TOOLWINDOW = 0x80, WS_EX_LAYERED = 0x80000,
                      WS_EX_NOACTIVATE = 0x8000000, WS_EX_TOPMOST = 0x8;
            var cp = base.CreateParams;
            cp.ExStyle |= WS_EX_TRANSPARENT | WS_EX_TOOLWINDOW | WS_EX_LAYERED | WS_EX_NOACTIVATE | WS_EX_TOPMOST;
            return cp;
        }
    }

    protected override void OnHandleCreated(EventArgs e)
    {
        base.OnHandleCreated(e);
        // Windows 10 2004+: ekran görüntüsü/yakalamada görünmez (okuyucu bunu okumasın)
        Yerel.SetWindowDisplayAffinity(Handle, Yerel.WDA_EXCLUDEFROMCAPTURE);
    }

    // Boş metin gizler. oyun: Robz Play penceresinin içerik alanı (ekran pikseli)
    public void Goster(string yeni, Rectangle? oyun)
    {
        if (string.IsNullOrWhiteSpace(yeni) || oyun is not { } o)
        {
            if (Visible) Hide();
            metin = "";
            return;
        }
        var boy = Math.Clamp((float)(o.Height * a.YaziBoyu), 16f, 64f);
        if (yazi == null || Math.Abs(boy - yaziPiksel) > 0.5f)
        {
            yazi?.Dispose();
            yazi = new Font("Segoe UI Semibold", boy, FontStyle.Regular, GraphicsUnit.Pixel);
            yaziPiksel = boy;
        }
        metin = yeni;

        var enFazla = (int)(o.Width * 0.8);
        var olcu = TextRenderer.MeasureText(metin, yazi, new Size(enFazla, int.MaxValue),
            TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter);
        var payX = (int)(boy * 0.7f); var payY = (int)(boy * 0.35f);
        var kutu = new Size(Math.Min(enFazla, olcu.Width) + payX * 2, olcu.Height + payY * 2);
        var x = o.X + (o.Width - kutu.Width) / 2;
        var y = o.Bottom - (int)(o.Height * a.AltBosluk) - kutu.Height;
        Bounds = new Rectangle(x, y, kutu.Width, kutu.Height);
        var hrgn = CreateRoundRectRgn(0, 0, kutu.Width + 1, kutu.Height + 1, 18, 18);
        Region?.Dispose();
        Region = Region.FromHrgn(hrgn);
        DeleteObject(hrgn);
        if (!Visible) Show();
        Invalidate();
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        if (yazi == null || metin.Length == 0) return;
        var alan = ClientRectangle;
        var bicim = TextFormatFlags.WordBreak | TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter;
        // ince siyah kenar + beyaz yazı
        foreach (var (dx, dy) in new[] { (-1, 0), (1, 0), (0, -1), (0, 1) })
            TextRenderer.DrawText(e.Graphics, metin, yazi, new Rectangle(alan.X + dx, alan.Y + dy, alan.Width, alan.Height), Color.Black, bicim);
        TextRenderer.DrawText(e.Graphics, metin, yazi, alan, Color.White, bicim);
    }

    [DllImport("gdi32.dll")]
    static extern bool DeleteObject(IntPtr h);

    [DllImport("gdi32.dll")]
    static extern IntPtr CreateRoundRectRgn(int x1, int y1, int x2, int y2, int w, int h);
}
