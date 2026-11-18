using System.Buffers.Binary;

namespace BlazorWebApp.Services.Cleanup
{
    public class CleanupEmbeddingVectorCodec : ICleanupEmbeddingVectorCodec
    {
        public byte[] Serialize(ReadOnlySpan<float> vector)
        {
            if (vector.Length == 0)
            {
                throw new ArgumentException("Embedding vector cannot be empty.", nameof(vector));
            }

            var bytes = new byte[vector.Length * sizeof(float)];
            for (var index = 0; index < vector.Length; index++)
            {
                ValidateFinite(vector[index], nameof(vector));
                BinaryPrimitives.WriteSingleLittleEndian(bytes.AsSpan(index * sizeof(float), sizeof(float)), vector[index]);
            }

            return bytes;
        }

        public float[] Deserialize(byte[] vectorBytes, int dimensions)
        {
            if (dimensions <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(dimensions), "Dimensions must be greater than zero.");
            }

            if (vectorBytes.Length != dimensions * sizeof(float))
            {
                throw new ArgumentException("Vector byte length does not match the expected dimensions.", nameof(vectorBytes));
            }

            var vector = new float[dimensions];
            for (var index = 0; index < dimensions; index++)
            {
                vector[index] = BinaryPrimitives.ReadSingleLittleEndian(vectorBytes.AsSpan(index * sizeof(float), sizeof(float)));
                ValidateFinite(vector[index], nameof(vectorBytes));
            }

            return vector;
        }

        public float[] Normalize(ReadOnlySpan<float> vector)
        {
            if (vector.Length == 0)
            {
                throw new ArgumentException("Embedding vector cannot be empty.", nameof(vector));
            }

            double sumSquares = 0;
            foreach (var value in vector)
            {
                ValidateFinite(value, nameof(vector));
                sumSquares += value * value;
            }

            if (sumSquares == 0)
            {
                throw new ArgumentException("Embedding vector cannot be normalized because its magnitude is zero.", nameof(vector));
            }

            var norm = Math.Sqrt(sumSquares);
            var normalized = new float[vector.Length];
            for (var index = 0; index < vector.Length; index++)
            {
                normalized[index] = (float)(vector[index] / norm);
            }

            return normalized;
        }

        public float CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right)
        {
            if (left.Length == 0 || left.Length != right.Length)
            {
                throw new ArgumentException("Vectors must be non-empty and have matching dimensions.");
            }

            double dot = 0;
            double leftSquares = 0;
            double rightSquares = 0;

            for (var index = 0; index < left.Length; index++)
            {
                ValidateFinite(left[index], nameof(left));
                ValidateFinite(right[index], nameof(right));
                dot += left[index] * right[index];
                leftSquares += left[index] * left[index];
                rightSquares += right[index] * right[index];
            }

            if (leftSquares == 0 || rightSquares == 0)
            {
                throw new ArgumentException("Cosine similarity cannot be computed for a zero-magnitude vector.");
            }

            return (float)(dot / (Math.Sqrt(leftSquares) * Math.Sqrt(rightSquares)));
        }

        private static void ValidateFinite(float value, string paramName)
        {
            if (float.IsNaN(value) || float.IsInfinity(value))
            {
                throw new ArgumentException("Embedding vector values must be finite.", paramName);
            }
        }
    }
}