namespace BlazorWebApp.Services
{
    /// <summary>
    /// Session-scoped service for managing prompt undo/redo history on the Generate page.
    /// History is not persisted to AppState and is lost when the session ends.
    /// </summary>
    public interface IPromptHistoryService
    {
        /// <summary>
        /// Pushes a new prompt state to the history stack.
        /// Called when the user modifies the prompt (on blur, not during typing).
        /// </summary>
        void PushPrompt(string prompt, string negativePrompt);

        /// <summary>
        /// Undoes the last prompt change and returns the previous state.
        /// Returns null if there is nothing to undo.
        /// </summary>
        (string Prompt, string NegativePrompt)? Undo();

        /// <summary>
        /// Redoes the next prompt change and returns the next state.
        /// Returns null if there is nothing to redo.
        /// </summary>
        (string Prompt, string NegativePrompt)? Redo();

        /// <summary>
        /// Returns true if there are previous states available to undo.
        /// </summary>
        bool CanUndo { get; }

        /// <summary>
        /// Returns true if there are next states available to redo.
        /// </summary>
        bool CanRedo { get; }

        /// <summary>
        /// Clears the entire history stack.
        /// </summary>
        void Clear();

        /// <summary>
        /// Gets the current index in the history stack (for debugging).
        /// </summary>
        int CurrentIndex { get; }

        /// <summary>
        /// Gets the total count of history entries (for debugging).
        /// </summary>
        int Count { get; }
    }
}
