using ImageMagick;
using System.Security.Cryptography;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupImageHashService : ICleanupImageHashService
    {
        public CleanupImageHashService()
        {
            MagickNET.Initialize();
        }

        public async Task<string> ComputeExactHashAsync(string imagePath, CancellationToken cancellationToken = default)
        {
            await using var stream = File.OpenRead(imagePath);
            var hash = await SHA256.HashDataAsync(stream, cancellationToken);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }

        public string ComputePerceptualHash(string imagePath)
        {
            using var image = new MagickImage(imagePath);
            image.AutoOrient();
            image.Resize(new MagickGeometry(8, 8) { IgnoreAspectRatio = true });
            image.Grayscale(PixelIntensityMethod.Rec709Luminance);
            image.Depth = 8;

            var pixels = image.ToByteArray(MagickFormat.Rgb);
            if (pixels.Length == 0)
            {
                throw new InvalidOperationException("ImageMagick returned no pixel data for perceptual hash generation.");
            }

            var stride = Math.Max(1, pixels.Length / 64);
            var luminance = new double[64];
            for (var i = 0; i < luminance.Length; i++)
            {
                var offset = Math.Min(i * stride, pixels.Length - 1);
                luminance[i] = ReadPixelIntensity(pixels, offset, stride);
            }

            var average = luminance.Average();
            ulong bits = 0;
            for (var i = 0; i < luminance.Length; i++)
            {
                if (luminance[i] >= average)
                {
                    bits |= 1UL << (63 - i);
                }
            }

            return bits.ToString("x16");
        }

        private static double ReadPixelIntensity(byte[] pixels, int offset, int stride)
        {
            if (stride >= 3 && offset + 2 < pixels.Length)
            {
                return (pixels[offset] + pixels[offset + 1] + pixels[offset + 2]) / 3.0;
            }

            return pixels[offset];
        }
    }
}