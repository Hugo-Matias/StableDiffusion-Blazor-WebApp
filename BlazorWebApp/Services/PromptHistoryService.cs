namespace BlazorWebApp.Services
{
    /// <summary>
    /// Session-scoped implementation of IPromptHistoryService for managing prompt undo/redo.
    /// Maintains a bounded history stack (max 50 entries) and a current position pointer.
    /// When a new entry is pushed while not at the end, the forward history is discarded.
    /// </summary>
    public class PromptHistoryService : IPromptHistoryService
    {
        private const int MaxHistorySize = 50;

        private readonly List<HistoryEntry> _history = new();
        private int _currentIndex = -1;

        public bool CanUndo => _currentIndex > 0;
        public bool CanRedo => _currentIndex < _history.Count - 1;
        public int CurrentIndex => _currentIndex;
        public int Count => _history.Count;

        public void PushPrompt(string prompt, string negativePrompt)
        {
            // Normalize nulls to empty strings for consistent comparison
            prompt = prompt ?? string.Empty;
            negativePrompt = negativePrompt ?? string.Empty;

            // Don't push duplicate state (same as current)
            if (_currentIndex >= 0 && _currentIndex < _history.Count)
            {
                var current = _history[_currentIndex];
                if (current.Prompt == prompt && current.NegativePrompt == negativePrompt)
                    return;
            }

            // Discard forward history if we're in the middle of the stack
            if (_currentIndex < _history.Count - 1)
            {
                _history.RemoveRange(_currentIndex + 1, _history.Count - _currentIndex - 1);
            }

            // Add new entry
            _history.Add(new HistoryEntry(prompt, negativePrompt, DateTime.UtcNow));
            _currentIndex = _history.Count - 1;

            // Trim old entries if we exceed the max size
            if (_history.Count > MaxHistorySize)
            {
                _history.RemoveAt(0);
                _currentIndex--;
            }
        }

        public (string Prompt, string NegativePrompt)? Undo()
        {
            if (!CanUndo) return null;

            _currentIndex--;
            var entry = _history[_currentIndex];
            return (entry.Prompt, entry.NegativePrompt);
        }

        public (string Prompt, string NegativePrompt)? Redo()
        {
            if (!CanRedo) return null;

            _currentIndex++;
            var entry = _history[_currentIndex];
            return (entry.Prompt, entry.NegativePrompt);
        }

        public void Clear()
        {
            _history.Clear();
            _currentIndex = -1;
        }

        private sealed record HistoryEntry(string Prompt, string NegativePrompt, DateTime Timestamp);
    }
}
