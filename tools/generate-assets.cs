#:package System.Drawing.Common@10.0.0
#:property TargetFramework=net10.0-windows
#pragma warning disable CA1416 // Outil Windows uniquement

// Génère le logo de SafeVault (cadenas dessiné, sans police d'icônes) aux tailles demandées
// par le paquet MSIX, ainsi que l'icône .ico de l'exécutable.
// Usage : dotnet run tools/generate-assets.cs -- <dossier Assets>

using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;

var output = args.Length > 0 ? args[0] : Path.Combine("src", "SafeVault.App", "Assets");
Directory.CreateDirectory(output);

var start = Color.FromArgb(0x4F, 0x46, 0xE5); // indigo
var end = Color.FromArgb(0x7C, 0x3A, 0xED);   // violet

// Logos carrés (nom, taille) : la mise à l'échelle 200 % est celle que Windows utilise le plus.
(string Name, int Size)[] squares =
[
    ("Square44x44Logo.scale-100.png", 44),
    ("Square44x44Logo.scale-200.png", 88),
    ("Square44x44Logo.targetsize-16_altform-unplated.png", 16),
    ("Square44x44Logo.targetsize-24_altform-unplated.png", 24),
    ("Square44x44Logo.targetsize-32_altform-unplated.png", 32),
    ("Square44x44Logo.targetsize-48_altform-unplated.png", 48),
    ("Square44x44Logo.targetsize-256_altform-unplated.png", 256),
    ("Square150x150Logo.scale-100.png", 150),
    ("Square150x150Logo.scale-200.png", 300),
    ("StoreLogo.scale-100.png", 50),
    ("StoreLogo.scale-200.png", 100),
    ("LockScreenLogo.scale-200.png", 48),
];

foreach (var (name, size) in squares)
{
    using var bmp = Render(size, size, size);
    bmp.Save(Path.Combine(output, name), ImageFormat.Png);
}

// Logos rectangulaires : cadenas centré sur fond dégradé.
foreach (var (name, w, h) in new[] { ("Wide310x150Logo.scale-200.png", 620, 300), ("SplashScreen.scale-200.png", 1240, 600) })
{
    using var bmp = Render(w, h, h * 2 / 3);
    bmp.Save(Path.Combine(output, name), ImageFormat.Png);
}

// Icône Windows (.ico) multi-tailles, images PNG intégrées.
int[] icoSizes = [16, 24, 32, 48, 64, 128, 256];
var pngs = icoSizes.Select(s =>
{
    using var bmp = Render(s, s, s);
    using var ms = new MemoryStream();
    bmp.Save(ms, ImageFormat.Png);
    return ms.ToArray();
}).ToList();

using (var ico = new BinaryWriter(File.Create(Path.Combine(output, "SafeVault.ico"))))
{
    ico.Write((short)0); ico.Write((short)1); ico.Write((short)icoSizes.Length);
    var offset = 6 + 16 * icoSizes.Length;
    for (var i = 0; i < icoSizes.Length; i++)
    {
        var s = icoSizes[i];
        ico.Write((byte)(s >= 256 ? 0 : s)); ico.Write((byte)(s >= 256 ? 0 : s));
        ico.Write((byte)0); ico.Write((byte)0);
        ico.Write((short)1); ico.Write((short)32);
        ico.Write(pngs[i].Length); ico.Write(offset);
        offset += pngs[i].Length;
    }

    pngs.ForEach(ico.Write);
}

Console.WriteLine($"Ressources générées dans {Path.GetFullPath(output)}");

// Dessine le logo : carré arrondi en dégradé (ou bandeau) et cadenas blanc.
Bitmap Render(int width, int height, int logoSize)
{
    var bmp = new Bitmap(width, height, PixelFormat.Format32bppArgb);
    using var g = Graphics.FromImage(bmp);
    g.SmoothingMode = SmoothingMode.AntiAlias;
    g.PixelOffsetMode = PixelOffsetMode.HighQuality;
    g.Clear(Color.Transparent);

    var square = width == height;
    var bg = square ? new RectangleF(0, 0, width, height) : new RectangleF(0, 0, width, height);
    using (var brush = new LinearGradientBrush(bg, start, end, 45f))
    using (var path = square ? RoundedRect(bg, width * 0.22f) : RoundedRect(bg, 0))
    {
        g.FillPath(brush, path);
    }

    // Cadenas centré, proportionnel à logoSize.
    var u = logoSize / 100f;
    var cx = width / 2f;
    var cy = height / 2f;
    var bodyW = 50 * u;
    var bodyH = 38 * u;
    var body = new RectangleF(cx - bodyW / 2, cy - 6 * u, bodyW, bodyH);

    using (var pen = new Pen(Color.White, Math.Max(1.5f, 8 * u)))
    {
        pen.StartCap = pen.EndCap = LineCap.Round;
        var shackleW = 32 * u;
        var shackle = new RectangleF(cx - shackleW / 2, cy - 30 * u, shackleW, 34 * u);
        using var arc = new GraphicsPath();
        arc.AddLine(shackle.Left, body.Top, shackle.Left, shackle.Top + shackleW / 2);
        arc.AddArc(shackle.Left, shackle.Top, shackleW, shackleW, 180, 180);
        arc.AddLine(shackle.Right, shackle.Top + shackleW / 2, shackle.Right, body.Top);
        g.DrawPath(pen, arc);
    }

    using (var white = new SolidBrush(Color.White))
    using (var bodyPath = RoundedRect(body, 7 * u))
    {
        g.FillPath(white, bodyPath);
    }

    if (logoSize >= 24)
    {
        // Trou de serrure, couleur du fond.
        using var hole = new SolidBrush(start);
        var r = 5 * u;
        g.FillEllipse(hole, cx - r, body.Top + 10 * u, 2 * r, 2 * r);
        g.FillRectangle(hole, cx - 2 * u, body.Top + 16 * u, 4 * u, 11 * u);
    }

    return bmp;
}

static GraphicsPath RoundedRect(RectangleF r, float radius)
{
    var path = new GraphicsPath();
    if (radius <= 0)
    {
        path.AddRectangle(r);
        return path;
    }

    var d = radius * 2;
    path.AddArc(r.Left, r.Top, d, d, 180, 90);
    path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
    path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
    path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
    path.CloseFigure();
    return path;
}
