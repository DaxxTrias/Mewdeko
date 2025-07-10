using System.IO;
using SkiaSharp;

namespace Mewdeko.Extensions
{
    /// <summary>
    /// Provides methods for compressing images to ensure they are under a specified size limit.
    /// </summary>
    public static class ImageCompressor
    {
        /// <summary>
        /// Compresses the image data to ensure it is under 256KB.
        /// </summary>
        /// <param name="imgData">The image data to compress.</param>
        /// <returns>A stream containing the compressed image data.</returns>
        /// <exception cref="InvalidOperationException">Thrown when the image cannot be compressed under 256KB.</exception>
        public static Stream EnsureImageUnder256Kb(byte[] imgData)
        {
            const int maxSize = 262144; // 256kb
            if (imgData.Length <= maxSize)
                return new MemoryStream(imgData);

            using var inputStream = new MemoryStream(imgData);
            using var skiaStream = new SKManagedStream(inputStream);
            using var bitmap = SKBitmap.Decode(skiaStream);
            if (bitmap == null)
                throw new InvalidOperationException("Failed to decode image for compression.");

            // Try JPEG first for best compression
            var quality = 90;
            byte[]? result = null;
            while (quality >= 50)
            {
                using var ms = new MemoryStream();
                using (var image = SKImage.FromBitmap(bitmap))
                using (var data = image.Encode(SKEncodedImageFormat.Jpeg, quality))
                    data.SaveTo(ms);
                if (ms.Length <= maxSize)
                {
                    result = ms.ToArray();
                    break;
                }
                quality -= 10;
            }

            // If JPEG didn't work, try PNG (lossless, but sometimes smaller for simple images)
            if (result == null)
            {
                using var ms = new MemoryStream();
                using (var image = SKImage.FromBitmap(bitmap))
                using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                    data.SaveTo(ms);
                if (ms.Length <= maxSize)
                    result = ms.ToArray();
            }

            // If still too large, throw
            if (result == null || result.Length > maxSize)
                throw new InvalidOperationException("Image could not be compressed under 256kb.");

            return new MemoryStream(result);
        }
    }
}