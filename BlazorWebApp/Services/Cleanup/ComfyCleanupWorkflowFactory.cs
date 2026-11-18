using System.Text.Json.Serialization;

namespace BlazorWebApp.Services.Cleanup
{
    internal static class ComfyCleanupWorkflowFactory
    {
        public const string EmbeddingNodeType = "BlazorCleanupImageEmbedding";
        public const string ScoreNodeType = "BlazorCleanupImageScore";

        public static ComfyCleanupBatchWorkflow CreateBatchPayload(
            IReadOnlyDictionary<int, string> uploadedFilenames,
            IReadOnlySet<int> embeddingImageIds,
            IReadOnlySet<int> scoreImageIds,
            CleanupEmbeddingOptions embeddingOptions,
            CleanupScoringOptions scoringOptions)
        {
            var prompt = new Dictionary<string, ComfyPromptNode>();
            var outputMap = new Dictionary<string, ComfyCleanupBatchOutput>();
            var nextNodeId = 1;

            foreach (var imageId in uploadedFilenames.Keys.OrderBy(id => id))
            {
                var loadNodeId = (nextNodeId++).ToString();
                prompt[loadNodeId] = new("LoadImage", new Dictionary<string, object?>
                {
                    ["image"] = uploadedFilenames[imageId]
                });

                if (embeddingImageIds.Contains(imageId))
                {
                    var embeddingNodeId = (nextNodeId++).ToString();
                    prompt[embeddingNodeId] = new(EmbeddingNodeType, new Dictionary<string, object?>
                    {
                        ["image"] = new object[] { loadNodeId, 0 },
                        ["model_key"] = embeddingOptions.Model.ModelKey,
                        ["dimensions"] = embeddingOptions.Model.Dimensions,
                        ["normalize"] = true
                    });
                    outputMap[embeddingNodeId] = new(imageId, ComfyCleanupBatchSignal.Embedding);
                }

                if (scoreImageIds.Contains(imageId))
                {
                    var scoreNodeId = (nextNodeId++).ToString();
                    prompt[scoreNodeId] = new(ScoreNodeType, new Dictionary<string, object?>
                    {
                        ["image"] = new object[] { loadNodeId, 0 },
                        ["model_key"] = scoringOptions.Model.ModelKey,
                        ["score_name"] = scoringOptions.Model.ScoreName,
                        ["min_score"] = scoringOptions.Model.MinScore,
                        ["max_score"] = scoringOptions.Model.MaxScore
                    });
                    outputMap[scoreNodeId] = new(imageId, ComfyCleanupBatchSignal.Score);
                }
            }

            return new ComfyCleanupBatchWorkflow(
                new
                {
                    prompt,
                    client_id = Guid.NewGuid().ToString()
                },
                outputMap);
        }

        public static object CreateEmbeddingPayload(string uploadedFilename, CleanupEmbeddingOptions options)
        {
            var prompt = new Dictionary<string, ComfyPromptNode>
            {
                ["1"] = new("LoadImage", new Dictionary<string, object?>
                {
                    ["image"] = uploadedFilename
                }),
                ["2"] = new(EmbeddingNodeType, new Dictionary<string, object?>
                {
                    ["image"] = new object[] { "1", 0 },
                    ["model_key"] = options.Model.ModelKey,
                    ["dimensions"] = options.Model.Dimensions,
                    ["normalize"] = true
                })
            };

            return new
            {
                prompt,
                client_id = Guid.NewGuid().ToString()
            };
        }

        public static object CreateScorePayload(string uploadedFilename, CleanupScoringOptions options)
        {
            var prompt = new Dictionary<string, ComfyPromptNode>
            {
                ["1"] = new("LoadImage", new Dictionary<string, object?>
                {
                    ["image"] = uploadedFilename
                }),
                ["2"] = new(ScoreNodeType, new Dictionary<string, object?>
                {
                    ["image"] = new object[] { "1", 0 },
                    ["model_key"] = options.Model.ModelKey,
                    ["score_name"] = options.Model.ScoreName,
                    ["min_score"] = options.Model.MinScore,
                    ["max_score"] = options.Model.MaxScore
                })
            };

            return new
            {
                prompt,
                client_id = Guid.NewGuid().ToString()
            };
        }

        private sealed record ComfyPromptNode(
            [property: JsonPropertyName("class_type")] string ClassType,
            [property: JsonPropertyName("inputs")] Dictionary<string, object?> Inputs);
    }

    internal enum ComfyCleanupBatchSignal
    {
        Embedding,
        Score
    }

    internal sealed record ComfyCleanupBatchOutput(int ImageId, ComfyCleanupBatchSignal Signal);

    internal sealed record ComfyCleanupBatchWorkflow(object Payload, IReadOnlyDictionary<string, ComfyCleanupBatchOutput> Outputs);
}