using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace CurrentMedia.Imaging;

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

        var scaledWidth = (uint)Math.Max(1, Math.Round(width * scale));
        var scaledHeight = (uint)Math.Max(1, Math.Round(height * scale));

        var offsetX = scaledWidth > targetSize ? (int)((scaledWidth - targetSize) / 2) : 0;
        var offsetY = scaledHeight > targetSize ? (int)((scaledHeight - targetSize) / 2) : 0;

        if (width == height && Math.Abs(scale - 1.0) < 0.001 && offsetX == 0 && offsetY == 0)
        {
            var result = new byte[targetSize * targetSize * 4];
            Array.Copy(sourcePixelBytes, result, result.Length);
            return result;
        }

        var scaledPixels = ScaleBilinear(sourcePixelBytes, width, height, scaledWidth, scaledHeight, scale);
        return PlaceOnCanvas(scaledPixels, scaledWidth, scaledHeight, targetSize, offsetX, offsetY, 0x00, 0x00, 0x00, 0xFF);
    }

    public static byte[] FitToTop(byte[] sourcePixelBytes, uint width, uint height, int targetSize)
    {
        var maxDimension = Math.Max(width, height);
        var scale = (double)targetSize / maxDimension;

        var scaledWidth = (uint)Math.Max(1, Math.Round(width * scale));
        var scaledHeight = (uint)Math.Max(1, Math.Round(height * scale));

        var offsetX = -(int)((targetSize - scaledWidth) / 2);

        var scaledPixels = ScaleBilinear(sourcePixelBytes, width, height, scaledWidth, scaledHeight, scale);
        return PlaceOnCanvas(scaledPixels, scaledWidth, scaledHeight, targetSize, offsetX, 0, 0x00, 0x00, 0x00, 0xFF);
    }

    private static byte[] ScaleBilinear(byte[] source, uint srcWidth, uint srcHeight, uint dstWidth, uint dstHeight, double scale)
    {
        var result = new byte[dstWidth * dstHeight * 4];

        for (uint y = 0; y < dstHeight; y++)
        {
            for (uint x = 0; x < dstWidth; x++)
            {
                var origX = x / scale;
                var origY = y / scale;

                var x1 = (uint)Math.Floor(origX);
                var y1 = (uint)Math.Floor(origY);
                var x2 = (uint)Math.Min(x1 + 1, srcWidth - 1);
                var y2 = (uint)Math.Min(y1 + 1, srcHeight - 1);

                var fx = origX - x1;
                var fy = origY - y1;

                var p = InterpolatePixels(
                    GetPixel(source, srcWidth, x1, y1),
                    GetPixel(source, srcWidth, x2, y1),
                    GetPixel(source, srcWidth, x1, y2),
                    GetPixel(source, srcWidth, x2, y2),
                    fx, fy
                );

                var i = (y * dstWidth + x) * 4;
                result[i] = p.R;
                result[i + 1] = p.G;
                result[i + 2] = p.B;
                result[i + 3] = p.A;
            }
        }

        return result;
    }

    private static byte[] PlaceOnCanvas(
        byte[] scaledPixels, uint scaledWidth, uint scaledHeight,
        int canvasSize, int offsetX, int offsetY,
        byte fillR, byte fillG, byte fillB, byte fillA)
    {
        var canvas = new byte[canvasSize * canvasSize * 4];

        for (int y = 0; y < canvasSize; y++)
        {
            for (int x = 0; x < canvasSize; x++)
            {
                var ci = (y * canvasSize + x) * 4;
                var sx = x + offsetX;
                var sy = y + offsetY;

                if (sx >= 0 && sx < scaledWidth && sy >= 0 && sy < scaledHeight)
                {
                    var si = (sy * (int)scaledWidth + sx) * 4;
                    canvas[ci] = scaledPixels[si];
                    canvas[ci + 1] = scaledPixels[si + 1];
                    canvas[ci + 2] = scaledPixels[si + 2];
                    canvas[ci + 3] = scaledPixels[si + 3];
                }
                else
                {
                    canvas[ci] = fillR;
                    canvas[ci + 1] = fillG;
                    canvas[ci + 2] = fillB;
                    canvas[ci + 3] = fillA;
                }
            }
        }

        return canvas;
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
        try
        {
            using var icon = Icon.ExtractAssociatedIcon(exePath);
            if (icon == null)
            {
                return string.Empty;
            }

            using var bitmap = new Bitmap(icon.ToBitmap(), size, size);
            var bitmapData = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.ReadOnly,
                PixelFormat.Format32bppArgb);

            try
            {
                var width = bitmap.Width;
                var height = bitmap.Height;
                var stride = bitmapData.Stride;

                var bgraBytes = new byte[stride * height];
                Marshal.Copy(bitmapData.Scan0, bgraBytes, 0, bgraBytes.Length);

                var rgbaBytes = new byte[width * height * 4];

                for (int y = 0; y < height; y++)
                {
                    int srcRowOffset = y * stride;
                    int dstRowOffset = y * width * 4;

                    for (int x = 0; x < width; x++)
                    {
                        int srcIndex = srcRowOffset + (x * 4);
                        int dstIndex = dstRowOffset + (x * 4);

                        rgbaBytes[dstIndex] = bgraBytes[srcIndex + 2];
                        rgbaBytes[dstIndex + 1] = bgraBytes[srcIndex + 1];
                        rgbaBytes[dstIndex + 2] = bgraBytes[srcIndex];
                        rgbaBytes[dstIndex + 3] = bgraBytes[srcIndex + 3];
                    }
                }

                return await EncodeImageToBase64Async(rgbaBytes, width);
            }
            finally
            {
                bitmap.UnlockBits(bitmapData);
            }
        }
        catch
        {
            return string.Empty;
        }
    }
}
