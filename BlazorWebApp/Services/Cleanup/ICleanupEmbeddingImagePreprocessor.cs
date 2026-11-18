namespace BlazorWebApp.Services.Cleanup
{
    public interface ICleanupEmbeddingImagePreprocessor
    {
        CleanupEmbeddingTensor Preprocess(string imagePath, CleanupEmbeddingModelOptions model);
    }
}