namespace BlazorWebApp.Models
{
    public class AppStateModel
    {
        public AppStatePrompts Prompts { get; set; } = new();
        public AppStateResources Resources { get; set; } = new();

        public AppStateModel() { }
        public AppStateModel(AppSettings settings)
        {
            Prompts = new()
            {
                Wildcards = new()
                {
                    GenerationAmount = settings.Prompts.Wildcards.Generation.Value
                }
            };
        }
    }

    public class AppStateResources
    {
        public int ActiveTabIndex { get; set; } = 0;
    }

    public class AppStatePrompts
    {
        public int ActiveTabIndex { get; set; } = 0;
        public AppStatePromptsWildcards Wildcards { get; set; } = new();
    }

    public class AppStatePromptsWildcards
    {
        public List<string> GeneratedPrompts { get; set; } = new();
        public int GenerationAmount { get; set; } = 0;
        public string Template { get; set; } = string.Empty;
        public int ActivePromptTabIndex { get; set; } = 0;
        public int ActiveActionTabIndex { get; set; } = 0;
    }
}
