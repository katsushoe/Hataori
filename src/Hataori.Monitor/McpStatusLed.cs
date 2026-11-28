using System.Drawing.Drawing2D;

namespace Hataori.Monitor;

/// <summary>MCPサーバーの状態ランプ（横長のピル型）画像を作り、使い回します。</summary>
internal sealed class McpStatusLed : IDisposable
{
    public static readonly Color RunningColor = Color.FromArgb(40, 190, 70);
    public static readonly Color NotRespondingColor = Color.FromArgb(240, 170, 30);
    public static readonly Color ErrorColor = Color.FromArgb(215, 50, 45);
    public static readonly Color StoppedColor = Color.FromArgb(150, 150, 150);

    private readonly Dictionary<Color, Bitmap> _images;

    public McpStatusLed()
    {
        _images = new[] { RunningColor, NotRespondingColor, ErrorColor, StoppedColor }
            .ToDictionary(color => color, Create);
    }

    /// <summary>状態に対応するランプの基本色です。未確認は停止中と同じ灰色です。</summary>
    public static Color ColorFor(McpServerState state) => state switch
    {
        McpServerState.Running => RunningColor,
        McpServerState.NotResponding => NotRespondingColor,
        McpServerState.Error => ErrorColor,
        _ => StoppedColor,
    };

    public Bitmap ImageFor(McpServerState state) => _images[ColorFor(state)];

    public void Dispose()
    {
        foreach (var image in _images.Values) image.Dispose();
        _images.Clear();
    }

    /// <summary>16×16の透明ビットマップに、グラデーション・縁・光沢付きのピル型ランプを描きます。</summary>
    internal static Bitmap Create(Color color)
    {
        var bitmap = new Bitmap(16, 16, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.SmoothingMode = SmoothingMode.AntiAlias;
        graphics.Clear(Color.Transparent);
        var body = new RectangleF(0.5F, 4.5F, 15F, 7F);
        using (var path = Pill(body))
        using (var fill = new LinearGradientBrush(body, Lighten(color, 0.6F), Darken(color, 0.1F), LinearGradientMode.Vertical))
        using (var rim = new Pen(Darken(color, 0.45F), 1F))
        {
            graphics.FillPath(fill, path);
            graphics.DrawPath(rim, path);
        }

        var gloss = new RectangleF(body.X + 3F, body.Y + 1.2F, body.Width - 6F, 2F);
        using (var path = Pill(gloss))
        using (var shine = new SolidBrush(Color.FromArgb(160, Color.White)))
        {
            graphics.FillPath(shine, path);
        }

        return bitmap;
    }

    internal static Color Lighten(Color color, float amount) => Color.FromArgb(
        color.A,
        (int)Math.Round(color.R + ((255 - color.R) * amount)),
        (int)Math.Round(color.G + ((255 - color.G) * amount)),
        (int)Math.Round(color.B + ((255 - color.B) * amount)));

    internal static Color Darken(Color color, float amount) => Color.FromArgb(
        color.A,
        (int)Math.Round(color.R * (1F - amount)),
        (int)Math.Round(color.G * (1F - amount)),
        (int)Math.Round(color.B * (1F - amount)));

    private static GraphicsPath Pill(RectangleF bounds)
    {
        var diameter = bounds.Height;
        var path = new GraphicsPath();
        path.AddArc(bounds.X, bounds.Y, diameter, diameter, 90, 180);
        path.AddArc(bounds.Right - diameter, bounds.Y, diameter, diameter, 270, 180);
        path.CloseFigure();
        return path;
    }
}
