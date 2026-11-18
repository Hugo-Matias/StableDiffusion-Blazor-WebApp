namespace BlazorWebApp.Data.Entities
{
    public enum CleanupIndexStatus
    {
        Pending,
        Indexed,
        MissingFile,
        Stale,
        Error
    }

    public enum CleanupEmbeddingStatus
    {
        Pending,
        Indexed,
        Stale,
        Error
    }

    public enum CleanupScoreStatus
    {
        Pending,
        Indexed,
        Stale,
        Error
    }

    public enum CleanupGroupRunStatus
    {
        Pending,
        Running,
        Completed,
        Cancelled,
        Error
    }

    public enum CleanupGroupingStrategy
    {
        ExactDuplicate,
        NearDuplicate,
        PromptFingerprint,
        PromptFuzzy,
        SameExperiment,
        VisualSimilarity,
        LowValueCandidates,
        OrphansAndMissingFiles,
        LargeStorageOffenders
    }

    public enum CleanupGroupMemberRole
    {
        Member,
        Representative,
        KeepCandidate,
        CleanupCandidate
    }

    public enum CleanupSuggestedAction
    {
        Review,
        Keep,
        Delete
    }
}