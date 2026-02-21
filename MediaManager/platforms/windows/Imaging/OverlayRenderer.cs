using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using BarRaider.SdTools;

namespace CurrentMedia.Imaging;

static class OverlayRenderer
{
    public static Image CreatePlaceholderImage(int size, string htmlColor)
    {
        var bitmap = new Bitmap(size, size);
        using var g = Graphics.FromImage(bitmap);
        using var brush = new SolidBrush(ColorTranslator.FromHtml(htmlColor));
        g.FillRectangle(brush, 0, 0, size, size);
        return bitmap;
    }

    public static Image ApplyOverlay(Image baseImage, MediaInfo info, OverlayDisplayMode overlayMode)
    {
        var result = new Bitmap(baseImage.Width, baseImage.Height);
        using var g = Graphics.FromImage(result);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        g.DrawImage(baseImage, 0, 0, baseImage.Width, baseImage.Height);

        var padding = (int)(baseImage.Width * 0.05);
        var iconSize = (int)(baseImage.Width * 0.25);

        var showIcon = (overlayMode == OverlayDisplayMode.Icon || overlayMode == OverlayDisplayMode.Both) && !string.IsNullOrEmpty(info.AppIconBase64);
        var showStatus = (overlayMode == OverlayDisplayMode.Status || overlayMode == OverlayDisplayMode.Both) && !string.IsNullOrEmpty(info.Status);

        if (showIcon)
        {
            try
            {
                var iconBytes = Convert.FromBase64String(info.AppIconBase64);
                using var iconMs = new MemoryStream(iconBytes);
                using var iconImage = Image.FromStream(iconMs);

                using var bgBrush = new SolidBrush(Color.FromArgb(153, 0, 0, 0));
                g.FillEllipse(bgBrush, padding - 2, padding - 2, iconSize + 4, iconSize + 4);
                g.DrawImage(iconImage, padding, padding, iconSize, iconSize);
            }
            catch (Exception ex)
            {
                Logger.Instance.LogMessage(TracingLevel.WARN, $"Error drawing app icon overlay: {ex.Message}");
            }
        }

        if (showStatus)
        {
            var statusX = baseImage.Width - padding - iconSize;
            var statusY = padding;
            var radius = iconSize / 2;

            using var bgBrush = new SolidBrush(Color.FromArgb(153, 0, 0, 0));
            g.FillEllipse(bgBrush, statusX - 2, statusY - 2, iconSize + 4, iconSize + 4);

            using var iconBrush = new SolidBrush(Color.White);
            var centerX = statusX + radius;
            var centerY = statusY + radius;
            var symbolSize = iconSize * 0.5f;

            if (info.Status == "Playing")
            {
                var points = new PointF[]
                {
                    new(centerX - symbolSize / 3, centerY - symbolSize / 2),
                    new(centerX + symbolSize / 2, centerY),
                    new(centerX - symbolSize / 3, centerY + symbolSize / 2)
                };
                g.FillPolygon(iconBrush, points);
            }
            else
            {
                var barWidth = symbolSize / 4;
                var barHeight = symbolSize;
                g.FillRectangle(iconBrush, centerX - symbolSize / 3, centerY - barHeight / 2, barWidth, barHeight);
                g.FillRectangle(iconBrush, centerX + symbolSize / 6 - barWidth / 2, centerY - barHeight / 2, barWidth, barHeight);
            }
        }

        return result;
    }
}
