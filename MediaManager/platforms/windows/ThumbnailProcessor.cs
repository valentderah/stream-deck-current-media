using System;
using System.Collections.Generic;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Graphics.Imaging;
using Windows.Storage.Streams;

namespace MediaManager.Windows;

static class ThumbnailProcessor
{
    private const int TargetSize = 144;
    private const int PartSize = 72;

    public static async Task ProcessThumbnailAsync(IRandomAccessStreamReference thumbnail, MediaInfo info)
    {
        try
        {
            using var thumbnailStream = await thumbnail.OpenReadAsync();
            var decoder = await BitmapDecoder.CreateAsync(thumbnailStream);

            var (scaledWidth, scaledHeight, offsetX, offsetY) = CalculateScaleAndOffset(
                decoder.PixelWidth,
                decoder.PixelHeight,
                TargetSize
            );

            var transform = new BitmapTransform
            {
                ScaledWidth = scaledWidth,
                ScaledHeight = scaledHeight,
                InterpolationMode = BitmapInterpolationMode.Linear
            };

            var pixelData = await decoder.GetPixelDataAsync(
                BitmapPixelFormat.Rgba8,
                BitmapAlphaMode.Premultiplied,
                transform,
                ExifOrientationMode.RespectExifOrientation,
                ColorManagementMode.ColorManageToSRgb
            );

            var scaledPixelBytes = pixelData.DetachPixelData();
            var finalPixels = CreateCenteredImage(scaledPixelBytes, scaledWidth, scaledHeight, TargetSize, offsetX, offsetY);

            info.CoverArtBase64 = await EncodeImageToBase64Async(finalPixels, TargetSize);

            var parts = await SplitImageIntoPartsAsync(finalPixels, TargetSize);
            if (parts.Count >= 4)
            {
                info.CoverArtPart1Base64 = Convert.ToBase64String(parts[0]);
                info.CoverArtPart2Base64 = Convert.ToBase64String(parts[1]);
                info.CoverArtPart3Base64 = Convert.ToBase64String(parts[2]);
                info.CoverArtPart4Base64 = Convert.ToBase64String(parts[3]);
            }
        }
        catch
        {
        }
    }

    private static (uint scaledWidth, uint scaledHeight, int offsetX, int offsetY) CalculateScaleAndOffset(
        uint originalWidth,
        uint originalHeight,
        int targetSize)
    {
        var aspectRatio = (double)originalWidth / originalHeight;
        uint scaledWidth, scaledHeight;
        int offsetX = 0;
        int offsetY = 0;

        if (aspectRatio > 1.0)
        {
            scaledWidth = (uint)targetSize;
            scaledHeight = (uint)Math.Round(targetSize / aspectRatio);
        }
        else
        {
            scaledHeight = (uint)targetSize;
            scaledWidth = (uint)Math.Round(targetSize * aspectRatio);
            offsetX = (targetSize - (int)scaledWidth) / 2;
        }

        return (scaledWidth, scaledHeight, offsetX, offsetY);
    }

    private static byte[] CreateCenteredImage(
        byte[] scaledPixels,
        uint scaledWidth,
        uint scaledHeight,
        int targetSize,
        int offsetX,
        int offsetY)
    {
        var finalPixels = new byte[targetSize * targetSize * 4];

        for (int y = 0; y < targetSize; y++)
        {
            for (int x = 0; x < targetSize; x++)
            {
                var targetIndex = (y * targetSize + x) * 4;

                if (x >= offsetX && x < offsetX + scaledWidth &&
                    y >= offsetY && y < offsetY + scaledHeight)
                {
                    var sourceX = x - offsetX;
                    var sourceY = y - offsetY;
                    var sourceIndex = (sourceY * (int)scaledWidth + sourceX) * 4;

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

    private static async Task<string> EncodeImageToBase64Async(byte[] pixels, int size)
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

    private static async Task<List<byte[]>> SplitImageIntoPartsAsync(byte[] sourcePixels, int sourceSize)
    {
        var parts = new List<byte[]>(4);

        for (int row = 0; row < 2; row++)
        {
            for (int col = 0; col < 2; col++)
            {
                var partPixels = new byte[PartSize * PartSize * 4];

                for (int y = 0; y < PartSize; y++)
                {
                    for (int x = 0; x < PartSize; x++)
                    {
                        var sourceX = col * PartSize + x;
                        var sourceY = row * PartSize + y;
                        var sourceIndex = (sourceY * sourceSize + sourceX) * 4;
                        var targetIndex = (y * PartSize + x) * 4;

                        if (sourceIndex < sourcePixels.Length && targetIndex < partPixels.Length)
                        {
                            partPixels[targetIndex] = sourcePixels[sourceIndex];
                            partPixels[targetIndex + 1] = sourcePixels[sourceIndex + 1];
                            partPixels[targetIndex + 2] = sourcePixels[sourceIndex + 2];
                            partPixels[targetIndex + 3] = sourcePixels[sourceIndex + 3];
                        }
                    }
                }

                var partBytes = await EncodePartToBytesAsync(partPixels, PartSize);
                parts.Add(partBytes);
            }
        }

        return parts;
    }

    private static async Task<byte[]> EncodePartToBytesAsync(byte[] pixels, int size)
    {
        using var partStream = new InMemoryRandomAccessStream();
        var partEncoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, partStream);
        partEncoder.SetPixelData(
            BitmapPixelFormat.Rgba8,
            BitmapAlphaMode.Premultiplied,
            (uint)size,
            (uint)size,
            96.0,
            96.0,
            pixels
        );
        await partEncoder.FlushAsync();

        partStream.Seek(0);
        var partBuffer = new global::Windows.Storage.Streams.Buffer((uint)partStream.Size);
        await partStream.ReadAsync(partBuffer, (uint)partStream.Size, InputStreamOptions.None);
        return partBuffer.ToArray();
    }
}
