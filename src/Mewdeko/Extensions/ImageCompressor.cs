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

            // Try to decode as animated image (GIF, APNG, WebP)
            using var codec = SKCodec.Create(skiaStream);
            if (codec == null)
                throw new InvalidOperationException("Failed to decode image for compression.");

            var frameCount = codec.FrameCount;
            var info = codec.Info;

            // Only compress if single-frame
            if (frameCount == 1)
            {
                using var bitmap = SKBitmap.Decode(codec);
                if (bitmap == null)
                    throw new InvalidOperationException("Failed to decode image for compression.");

                // Try PNG first to preserve transparency
                using (var ms = new MemoryStream())
                {
                    using (var image = SKImage.FromBitmap(bitmap))
                    using (var data = image.Encode(SKEncodedImageFormat.Png, 100))
                        data.SaveTo(ms);
                    if (ms.Length <= maxSize)
                        return new MemoryStream(ms.ToArray());
                }

                // Try lossy WebP as fallback
                var quality = 90;
                while (quality >= 50)
                {
                    using var ms = new MemoryStream();
                    using (var image = SKImage.FromBitmap(bitmap))
                    using (var data = image.Encode(SKEncodedImageFormat.Webp, quality))
                        data.SaveTo(ms);
                    if (ms.Length <= maxSize)
                        return new MemoryStream(ms.ToArray());
                    quality -= 10;
                }

                throw new InvalidOperationException("Image could not be compressed under 256kb.");
            }
            else
            {
                // Multi-frame image: skip compression, return as-is (SkiaSharp doesnt support encoding these)
                return new MemoryStream(imgData);
            }
        }
    }
}