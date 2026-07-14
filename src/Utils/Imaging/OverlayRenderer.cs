using SkiaSharp;

namespace CurrentMedia.Imaging;

static class OverlayRenderer
{
    public static SKBitmap Apply(SKBitmap baseImage, MediaState info, OverlayDisplayMode mode, SKBitmap? iconBitmap)
    {
        var result = new SKBitmap(baseImage.Width, baseImage.Height, SKColorType.Rgba8888, SKAlphaType.Premul);

        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);

        using var bitmapPaint = new SKPaint
        {
            IsAntialias = true,
            FilterQuality = SKFilterQuality.Medium
        };

        canvas.DrawBitmap(baseImage, 0, 0, bitmapPaint);

        var padding = (int)(baseImage.Width * 0.05);
        var iconSize = (int)(baseImage.Width * 0.25);

        var showIcon = (mode == OverlayDisplayMode.Icon || mode == OverlayDisplayMode.Both) && iconBitmap != null;
        var showStatus = (mode == OverlayDisplayMode.Status || mode == OverlayDisplayMode.Both) && !string.IsNullOrEmpty(info.Status);

        if (showIcon)
        {
            using var bgPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 153),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            canvas.DrawOval(new SKRect(padding - 2, padding - 2, padding + iconSize + 2, padding + iconSize + 2), bgPaint);
            canvas.DrawBitmap(iconBitmap, new SKRect(padding, padding, padding + iconSize, padding + iconSize), bitmapPaint);
        }

        if (showStatus)
        {
            var statusX = baseImage.Width - padding - iconSize;
            var statusY = padding;
            var radius = iconSize / 2f;

            using var bgPaint = new SKPaint
            {
                Color = new SKColor(0, 0, 0, 153),
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            canvas.DrawOval(new SKRect(statusX - 2, statusY - 2, statusX + iconSize + 2, statusY + iconSize + 2), bgPaint);

            using var symbolPaint = new SKPaint
            {
                Color = SKColors.White,
                IsAntialias = true,
                Style = SKPaintStyle.Fill
            };

            var centerX = statusX + radius;
            var centerY = statusY + radius;
            var symbolSize = iconSize * 0.5f;

            if (info.Status == "Playing")
            {
                using var path = new SKPath();
                path.MoveTo(centerX - symbolSize / 3, centerY - symbolSize / 2);
                path.LineTo(centerX + symbolSize / 2, centerY);
                path.LineTo(centerX - symbolSize / 3, centerY + symbolSize / 2);
                path.Close();
                canvas.DrawPath(path, symbolPaint);
            }
            else
            {
                var barWidth = symbolSize / 4;
                var barHeight = symbolSize;
                canvas.DrawRect(SKRect.Create(centerX - symbolSize / 3, centerY - barHeight / 2, barWidth, barHeight), symbolPaint);
                canvas.DrawRect(SKRect.Create(centerX + symbolSize / 6 - barWidth / 2, centerY - barHeight / 2, barWidth, barHeight), symbolPaint);
            }
        }

        return result;
    }
}
