using Hataori.Monitor;

namespace Hataori.Monitor.Tests;

public sealed class MonitorIconTests
{
    [Theory]
    [InlineData(16)]
    [InlineData(20)]
    [InlineData(24)]
    [InlineData(32)]
    [InlineData(48)]
    [InlineData(128)]
    public void Load_WhenSizeIsEmbedded_ReturnsMatchingFrame(int edge)
    {
        using var icon = MonitorIcon.Load(new Size(edge, edge));

        Assert.Equal(new Size(edge, edge), icon.Size);
    }

    [Fact]
    public void Resource_WhenRead_ContainsAllShellIconSizes()
    {
        // System.Drawing.Icon は 256px フレームを選ばないため、ICONDIR を直接読んで確認する。
        using var stream = typeof(MonitorIcon).Assembly.GetManifestResourceStream(MonitorIcon.ResourceName);
        Assert.NotNull(stream);
        using var reader = new BinaryReader(stream);
        reader.ReadUInt16();
        Assert.Equal(1, reader.ReadUInt16());
        int count = reader.ReadUInt16();
        var edges = new List<int>();
        for (var i = 0; i < count; i++)
        {
            int width = reader.ReadByte();
            edges.Add(width == 0 ? 256 : width);
            reader.ReadBytes(15);
        }

        Assert.Equal([16, 20, 24, 32, 40, 48, 64, 128, 256], edges.Order());
    }

    [Fact]
    public void Load_WhenFrameIsRendered_KeepsWhiteFillAndBlackOutline()
    {
        using var icon = MonitorIcon.Load(new Size(128, 128));
        using var bitmap = icon.ToBitmap();

        var fill = bitmap.GetPixel(64, 28);
        var outline = bitmap.GetPixel(64, 20);
        var corner = bitmap.GetPixel(0, 127);

        Assert.True(fill is { A: > 250, R: > 245, G: > 245, B: > 245 }, $"fill={fill}");
        Assert.True(outline is { A: > 250, R: < 10, G: < 10, B: < 10 }, $"outline={outline}");
        Assert.True(corner.A < 5, $"corner={corner}");
    }

    [Fact]
    public void Load_WhenTrayFrameIsRendered_HasNineSeparateDots()
    {
        using var icon = MonitorIcon.Load(new Size(16, 16));
        using var bitmap = icon.ToBitmap();
        var dots = 0;
        for (var y = 3; y <= 12; y++)
        {
            for (var x = 3; x <= 12; x++)
            {
                if (bitmap.GetPixel(x, y).ToArgb() == Color.Black.ToArgb() &&
                    bitmap.GetPixel(x - 1, y).ToArgb() == Color.White.ToArgb() &&
                    bitmap.GetPixel(x, y - 1).ToArgb() == Color.White.ToArgb())
                {
                    dots++;
                }
            }
        }

        Assert.Equal(9, dots);
    }
}
