using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Drawing;
using SixLabors.ImageSharp.Drawing.Processing;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace CurrentMedia.Imaging;

static class OverlayRenderer
{
    private static readonly Color OverlayBg = Color.FromRgba(0, 0, 0, 153);
    private static readonly Color SymbolColor = Color.White;

    public static Image<Rgba32> Apply(
        Image<Rgba32> baseImage,
        MediaState info,
        OverlayDisplayMode mode,
        Image<Rgba32>? iconImage)
    {
        var result = ImageExtensions.CloneImage(baseImage);

        var padding = (int)(baseImage.Width * 0.05);
        var iconSize = (int)(baseImage.Width * 0.25);

        var showIcon = (mode == OverlayDisplayMode.Icon || mode == OverlayDisplayMode.Both) && iconImage != null;
        var showStatus = (mode == OverlayDisplayMode.Status || mode == OverlayDisplayMode.Both)
            && !string.IsNullOrEmpty(info.Status);

        if (showIcon)
        {
            var bgRect = new RectangleF(padding - 2, padding - 2, iconSize + 4, iconSize + 4);
            using var resizedIcon = iconImage!.Clone(ctx => ctx.Resize(iconSize, iconSize));
            result.Mutate(ctx =>
            {
                ctx.Fill(OverlayBg, CreateEllipse(bgRect));
                ctx.DrawImage(resizedIcon, new Point(padding, padding), 1f);
            });
        }

        if (showStatus)
        {
            var statusX = baseImage.Width - padding - iconSize;
            var statusY = padding;
            var bgRect = new RectangleF(statusX - 2, statusY - 2, iconSize + 4, iconSize + 4);

            result.Mutate(ctx =>
            {
                ctx.Fill(OverlayBg, CreateEllipse(bgRect));

                var centerX = statusX + iconSize / 2f;
                var centerY = statusY + iconSize / 2f;
                var symbolSize = iconSize * 0.5f;

                if (info.Status == "Playing")
                {
                    var points = new PointF[]
                    {
                        new(centerX - symbolSize / 3, centerY - symbolSize / 2),
                        new(centerX + symbolSize / 2, centerY),
                        new(centerX - symbolSize / 3, centerY + symbolSize / 2)
                    };
                    ctx.FillPolygon(SymbolColor, points);
                }
                else
                {
                    var barWidth = symbolSize / 4;
                    var barHeight = symbolSize;
                    ctx.Fill(SymbolColor, new RectangularPolygon(
                        centerX - symbolSize / 3, centerY - barHeight / 2, barWidth, barHeight));
                    ctx.Fill(SymbolColor, new RectangularPolygon(
                        centerX + symbolSize / 6 - barWidth / 2, centerY - barHeight / 2, barWidth, barHeight));
                }
            });
        }

        return result;
    }

    private static EllipsePolygon CreateEllipse(RectangleF rect)
    {
        return new EllipsePolygon(
            rect.X + rect.Width / 2f,
            rect.Y + rect.Height / 2f,
            rect.Width / 2f,
            rect.Height / 2f);
    }
}
