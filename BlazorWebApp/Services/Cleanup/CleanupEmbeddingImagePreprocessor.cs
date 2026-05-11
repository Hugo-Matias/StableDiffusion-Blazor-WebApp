using ImageMagick;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupEmbeddingImagePreprocessor : ICleanupEmbeddingImagePreprocessor
    {
        public CleanupEmbeddingTensor Preprocess(string imagePath, CleanupEmbeddingModelOptions model)
        {
            if (string.IsNullOrWhiteSpace(imagePath))
            {
                throw new ArgumentException("Image path is required.", nameof(imagePath));
            }

            if (!File.Exists(imagePath))
            {
                throw new FileNotFoundException("Image file was not found.", imagePath);
            }

            if (model.InputWidth <= 0 || model.InputHeight <= 0)
            {
                throw new ArgumentException("Embedding input dimensions must be greater than zero.", nameof(model));
            }

            using var image = new MagickImage(imagePath);
            image.AutoOrient();
            image.ColorSpace = ColorSpace.sRGB;
            image.Resize(new MagickGeometry((uint)model.InputWidth, (uint)model.InputHeight)
            {
                FillArea = true
            });
            image.Crop((uint)model.InputWidth, (uint)model.InputHeight, Gravity.Center);

            var channels = ReadRgbChannels(image);
            var values = string.Equals(model.InputLayout, "NHWC", StringComparison.OrdinalIgnoreCase)
                ? BuildNhwcTensor(channels, model)
                : BuildNchwTensor(channels, model);
            var dimensions = string.Equals(model.InputLayout, "NHWC", StringComparison.OrdinalIgnoreCase)
                ? new[] { 1, model.InputHeight, model.InputWidth, 3 }
                : new[] { 1, 3, model.InputHeight, model.InputWidth };

            return new CleanupEmbeddingTensor
            {
                Values = values,
                Dimensions = dimensions
            };
        }

        private static ushort[] ReadRgbChannels(MagickImage image)
        {
            var width = (int)image.Width;
            var height = (int)image.Height;
            var channels = new ushort[width * height * 3];
            var pixels = image.GetPixels();

            for (var y = 0; y < height; y++)
            {
                for (var x = 0; x < width; x++)
                {
                    var values = pixels.GetPixel(x, y).ToArray();
                    var offset = ((y * width) + x) * 3;
                    channels[offset] = values.Length > 0 ? values[0] : (ushort)0;
                    channels[offset + 1] = values.Length > 1 ? values[1] : (ushort)0;
                    channels[offset + 2] = values.Length > 2 ? values[2] : (ushort)0;
                }
            }

            return channels;
        }

        private static float[] BuildNchwTensor(ushort[] channels, CleanupEmbeddingModelOptions model)
        {
            var pixelCount = model.InputWidth * model.InputHeight;
            var tensor = new float[pixelCount * 3];

            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                for (var channel = 0; channel < 3; channel++)
                {
                    tensor[channel * pixelCount + pixel] = NormalizeChannel(channels[pixel * 3 + channel], channel, model);
                }
            }

            return tensor;
        }

        private static float[] BuildNhwcTensor(ushort[] channels, CleanupEmbeddingModelOptions model)
        {
            var pixelCount = model.InputWidth * model.InputHeight;
            var tensor = new float[pixelCount * 3];

            for (var pixel = 0; pixel < pixelCount; pixel++)
            {
                for (var channel = 0; channel < 3; channel++)
                {
                    tensor[pixel * 3 + channel] = NormalizeChannel(channels[pixel * 3 + channel], channel, model);
                }
            }

            return tensor;
        }

        private static float NormalizeChannel(ushort value, int channel, CleanupEmbeddingModelOptions model)
            => ((value / 65535f) - model.Mean[channel]) / model.StandardDeviation[channel];
    }
}