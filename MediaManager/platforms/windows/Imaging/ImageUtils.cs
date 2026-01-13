using System;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MediaManager.Windows.Imaging;

static class ImageUtils
{
    public static async Task<string> EncodeImageToBase64Async(byte[] pixels, int size)
    {
        using var outputStream = new InMemoryRandomAccessStream();
        var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, outputStream);
        encoder.SetPixelData(
            BitmapPixelFormat.Rgba8,
            BitmapAlphaMode.Premultiplied,
            (uint)size,
            (uint)size,
            96.0,
            96.0,
            pixels
        );
        await encoder.FlushAsync();

        outputStream.Seek(0);
        var outputBuffer = new global::Windows.Storage.Streams.Buffer((uint)outputStream.Size);
        await outputStream.ReadAsync(outputBuffer, (uint)outputStream.Size, InputStreamOptions.None);

        return Convert.ToBase64String(outputBuffer.ToArray());
    }

    public static byte[] CropToSquare(byte[] sourcePixelBytes, uint width, uint height, int targetSize)
    {
        if (width == height && width == targetSize)
        {
            var result = new byte[targetSize * targetSize * 4];
            Array.Copy(sourcePixelBytes, result, result.Length);
            return result;
        }

        var minDimension = Math.Min(width, height);
        var scale = (double)targetSize / minDimension;

        var scaledWidth = (uint)Math.Round(width * scale);
        var scaledHeight = (uint)Math.Round(height * scale);

        var offsetX = scaledWidth > targetSize ? (int)((scaledWidth - targetSize) / 2) : 0;
        var offsetY = scaledHeight > targetSize ? (int)((scaledHeight - targetSize) / 2) : 0;

        if (width == height && Math.Abs(scale - 1.0) < 0.001 && offsetX == 0 && offsetY == 0)
        {
            var result = new byte[targetSize * targetSize * 4];
            Array.Copy(sourcePixelBytes, result, result.Length);
            return result;
        }

        var scaledPixels = new byte[scaledWidth * scaledHeight * 4];

        for (uint y = 0; y < scaledHeight; y++)
        {
            for (uint x = 0; x < scaledWidth; x++)
            {
                var srcX = x / scale;
                var srcY = y / scale;

                var x1 = (uint)Math.Floor(srcX);
                var y1 = (uint)Math.Floor(srcY);
                var x2 = (uint)Math.Min(x1 + 1, width - 1);
                var y2 = (uint)Math.Min(y1 + 1, height - 1);

                var fx = srcX - x1;
                var fy = srcY - y1;

                var p11 = GetPixel(sourcePixelBytes, width, x1, y1);
                var p21 = GetPixel(sourcePixelBytes, width, x2, y1);
                var p12 = GetPixel(sourcePixelBytes, width, x1, y2);
                var p22 = GetPixel(sourcePixelBytes, width, x2, y2);

                var p = InterpolatePixels(p11, p21, p12, p22, fx, fy);

                var targetIndex = (y * scaledWidth + x) * 4;
                scaledPixels[targetIndex] = p.R;
                scaledPixels[targetIndex + 1] = p.G;
                scaledPixels[targetIndex + 2] = p.B;
                scaledPixels[targetIndex + 3] = p.A;
            }
        }

        var finalPixels = new byte[targetSize * targetSize * 4];

        for (int y = 0; y < targetSize; y++)
        {
            for (int x = 0; x < targetSize; x++)
            {
                var srcX = x + offsetX;
                var srcY = y + offsetY;

                var targetIndex = (y * targetSize + x) * 4;

                if (srcX < scaledWidth && srcY < scaledHeight)
                {
                    var sourceIndex = (srcY * scaledWidth + srcX) * 4;
                    finalPixels[targetIndex] = scaledPixels[sourceIndex];
                    finalPixels[targetIndex + 1] = scaledPixels[sourceIndex + 1];
                    finalPixels[targetIndex + 2] = scaledPixels[sourceIndex + 2];
                    finalPixels[targetIndex + 3] = scaledPixels[sourceIndex + 3];
                }
                else
                {
                    finalPixels[targetIndex] = 0;
                    finalPixels[targetIndex + 1] = 0;
                    finalPixels[targetIndex + 2] = 0;
                    finalPixels[targetIndex + 3] = 255;
                }
            }
        }

        return finalPixels;
    }

    private static (byte R, byte G, byte B, byte A) GetPixel(byte[] pixels, uint width, uint x, uint y)
    {
        var index = (y * width + x) * 4;
        if (index + 3 < pixels.Length)
        {
            return (pixels[index], pixels[index + 1], pixels[index + 2], pixels[index + 3]);
        }
        return (0, 0, 0, 255);
    }

    private static (byte R, byte G, byte B, byte A) InterpolatePixels(
        (byte R, byte G, byte B, byte A) p11,
        (byte R, byte G, byte B, byte A) p21,
        (byte R, byte G, byte B, byte A) p12,
        (byte R, byte G, byte B, byte A) p22,
        double fx, double fy)
    {
        var r = (byte)Math.Round(
            p11.R * (1 - fx) * (1 - fy) +
            p21.R * fx * (1 - fy) +
            p12.R * (1 - fx) * fy +
            p22.R * fx * fy
        );
        var g = (byte)Math.Round(
            p11.G * (1 - fx) * (1 - fy) +
            p21.G * fx * (1 - fy) +
            p12.G * (1 - fx) * fy +
            p22.G * fx * fy
        );
        var b = (byte)Math.Round(
            p11.B * (1 - fx) * (1 - fy) +
            p21.B * fx * (1 - fy) +
            p12.B * (1 - fx) * fy +
            p22.B * fx * fy
        );
        var a = (byte)Math.Round(
            p11.A * (1 - fx) * (1 - fy) +
            p21.A * fx * (1 - fy) +
            p12.A * (1 - fx) * fy +
            p22.A * fx * fy
        );
        return (r, g, b, a);
    }

    public static async Task<string> ConvertIconToBase64Async(string exePath, int size)
    {
        IntPtr hIcon = IntPtr.Zero;
        IntPtr hIconResized = IntPtr.Zero;

        try
        {
            hIcon = ExtractIcon(IntPtr.Zero, exePath, 0);
            if (hIcon == IntPtr.Zero) return string.Empty;

            hIconResized = CopyImage(hIcon, IMAGE_ICON, size, size, LR_DEFAULTCOLOR);
            if (hIconResized == IntPtr.Zero) return string.Empty;

            if (!GetIconInfo(hIconResized, out var iconInfo)) return string.Empty;

            byte[]? pixelBytes = null;
            IntPtr hdc = IntPtr.Zero;
            try
            {
                hdc = GetDC(IntPtr.Zero);
                var bmi = new BitmapInfo
                {
                    biSize = Marshal.SizeOf(typeof(BitmapInfo)),
                    biWidth = size,
                    biHeight = -size, // Top-down DIB
                    biPlanes = 1,
                    biBitCount = 32,
                    biCompression = 0 // BI_RGB
                };

                pixelBytes = new byte[size * size * 4];
                if (GetDIBits(hdc, iconInfo.hbmColor, 0, (uint)size, pixelBytes, ref bmi, 0) == 0)
                {
                    return string.Empty;
                }
            }
            finally
            {
                if (iconInfo.hbmColor != IntPtr.Zero) DeleteObject(iconInfo.hbmColor);
                if (iconInfo.hbmMask != IntPtr.Zero) DeleteObject(iconInfo.hbmMask);
                if (hdc != IntPtr.Zero) ReleaseDC(IntPtr.Zero, hdc);
            }

            var rgbaBytes = new byte[size * size * 4];
            for (int i = 0; i < pixelBytes.Length; i += 4)
            {
                rgbaBytes[i] = pixelBytes[i + 2];     // R
                rgbaBytes[i + 1] = pixelBytes[i + 1]; // G
                rgbaBytes[i + 2] = pixelBytes[i];     // B
                rgbaBytes[i + 3] = pixelBytes[i + 3]; // A
            }

            return await EncodeImageToBase64Async(rgbaBytes, size);
        }
        finally
        {
            if (hIconResized != IntPtr.Zero) DestroyIcon(hIconResized);
            if (hIcon != IntPtr.Zero) DestroyIcon(hIcon);
        }
    }

    #region Win32 Interop

    [DllImport("shell32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr ExtractIcon(IntPtr hInst, string lpszExeFileName, int nIconIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    [DllImport("user32.dll")]
    private static extern IntPtr CopyImage(IntPtr hImage, uint uType, int cxDesired, int cyDesired, uint fuFlags);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr hIcon, out IconInfo piconinfo);

    [DllImport("gdi32.dll")]
    private static extern int GetDIBits(IntPtr hdc, IntPtr hbm, uint start, uint cLines, byte[] lpvBits, ref BitmapInfo lpbmi, uint usage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr hObject);

    [DllImport("user32.dll")]
    private static extern IntPtr GetDC(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern int ReleaseDC(IntPtr hWnd, IntPtr hDC);

    private const uint IMAGE_ICON = 1;
    private const uint LR_DEFAULTCOLOR = 0x0000;

    [StructLayout(LayoutKind.Sequential)]
    private struct IconInfo
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public int biSize;
        public int biWidth;
        public int biHeight;
        public short biPlanes;
        public short biBitCount;
        public int biCompression;
        public int biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public int biClrUsed;
        public int biClrImportant;
    }

    #endregion
}
