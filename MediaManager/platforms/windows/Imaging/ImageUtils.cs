using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
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

    public static async Task<string> ConvertIconToBase64Async(string exePath, int size)
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
}