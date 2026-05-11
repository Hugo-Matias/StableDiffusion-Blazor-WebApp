namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupEmbeddingVectorCodec
    {
        byte[] Serialize(ReadOnlySpan<float> vector);

        float[] Deserialize(byte[] vectorBytes, int dimensions);

        float[] Normalize(ReadOnlySpan<float> vector);

        float CosineSimilarity(ReadOnlySpan<float> left, ReadOnlySpan<float> right);
    }
}