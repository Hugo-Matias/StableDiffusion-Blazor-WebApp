using TiktokenSharp;

namespace BlazorWebApp.Services
{
    public interface ITokenizerService
    {
        Task<int> CountTokensAsync(string text);
        Task<List<string>> TokenizeTextAsync(string text);
    }

    public class TokenizerService : ITokenizerService
    {
        private TikToken? _tikToken;
        private readonly SemaphoreSlim _initLock = new(1, 1);
        private bool _isInitialized;

        private async Task EnsureInitializedAsync()
        {
            if (_isInitialized) return;

            await _initLock.WaitAsync();
            try
            {
                if (_isInitialized) return;

                // Initialize on background thread to avoid blocking UI
                await Task.Run(() =>
                {
                    _tikToken = TikToken.GetEncoding("cl100k_base");
                });

                _isInitialized = true;
            }
            finally
            {
                _initLock.Release();
            }
        }

        public async Task<int> CountTokensAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return 0;

            try
            {
                await EnsureInitializedAsync();
                
                // Run encoding on background thread
                return await Task.Run(() => _tikToken!.Encode(text).Count);
            }
            catch
            {
                // Fallback to approximation if encoding fails
                return (int)Math.Ceiling(text.Length / 4.0);
            }
        }

        public async Task<List<string>> TokenizeTextAsync(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
                return new List<string>();

            try
            {
                await EnsureInitializedAsync();

                // Run tokenization on background thread
                return await Task.Run(() =>
                {
                    var tokens = _tikToken!.Encode(text);
                    var result = new List<string>();

                    foreach (var token in tokens)
                    {
                        var decoded = _tikToken.Decode(new List<int> { token });
                        result.Add(decoded);
                    }

                    return result;
                });
            }
            catch
            {
                // Fallback to word splitting if tokenization fails
                return text.Split(' ', StringSplitOptions.RemoveEmptyEntries).ToList();
            }
        }
    }
}
