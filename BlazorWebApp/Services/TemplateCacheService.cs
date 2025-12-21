using Scriban;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for caching compiled Scriban templates.
    /// Provides shared access to pre-compiled templates for performance.
    /// </summary>
    public interface ITemplateCacheService
    {
        /// <summary>
        /// Gets a compiled template from the cache, parsing if not cached.
        /// </summary>
        /// <param name="templatePath">Full path to the template file.</param>
        /// <returns>The compiled template, or null if file doesn't exist.</returns>
        Template? GetOrCompile(string templatePath);

        /// <summary>
        /// Gets a compiled template from text, using the path as cache key.
        /// </summary>
        /// <param name="cacheKey">Cache key (typically file path).</param>
        /// <param name="templateText">The template text to compile.</param>
        /// <returns>The compiled template.</returns>
        Template GetOrCompile(string cacheKey, string templateText);

        /// <summary>
        /// Pre-compiles a template and adds it to the cache.
        /// </summary>
        /// <param name="templatePath">Full path to the template file.</param>
        /// <returns>True if compilation succeeded, false otherwise.</returns>
        bool Precompile(string templatePath);

        /// <summary>
        /// Pre-compiles all templates in the specified directory.
        /// </summary>
        /// <param name="directory">Directory to scan for .sbn files.</param>
        /// <param name="recursive">Whether to scan subdirectories.</param>
        /// <returns>Number of templates compiled.</returns>
        int PrecompileAll(string directory, bool recursive = true);

        /// <summary>
        /// Clears a specific template from the cache.
        /// </summary>
        /// <param name="templatePath">Path of the template to clear.</param>
        void Invalidate(string templatePath);

        /// <summary>
        /// Clears all templates from the cache.
        /// </summary>
        void ClearAll();

        /// <summary>
        /// Gets the number of templates currently cached.
        /// </summary>
        int CachedCount { get; }

        /// <summary>
        /// Gets all compilation errors that occurred during precompilation.
        /// </summary>
        IReadOnlyList<TemplateCompilationError> CompilationErrors { get; }
    }

    /// <summary>
    /// Represents a template compilation error.
    /// </summary>
    public class TemplateCompilationError
    {
        public string FilePath { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
        public int? Line { get; init; }
        public int? Column { get; init; }
    }

    /// <summary>
    /// Implementation of template cache service.
    /// </summary>
    public class TemplateCacheService : ITemplateCacheService
    {
        private readonly Dictionary<string, Template> _cache = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<TemplateCompilationError> _errors = new();
        private readonly object _lock = new();
        private readonly ILogger<TemplateCacheService> _logger;

        public TemplateCacheService(ILogger<TemplateCacheService> logger)
        {
            _logger = logger;
        }

        /// <inheritdoc />
        public int CachedCount
        {
            get
            {
                lock (_lock)
                {
                    return _cache.Count;
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<TemplateCompilationError> CompilationErrors
        {
            get
            {
                lock (_lock)
                {
                    return _errors.ToList();
                }
            }
        }

        /// <inheritdoc />
        public Template? GetOrCompile(string templatePath)
        {
            if (string.IsNullOrEmpty(templatePath))
                return null;

            lock (_lock)
            {
                if (_cache.TryGetValue(templatePath, out var cached))
                    return cached;
            }

            if (!File.Exists(templatePath))
            {
                _logger.LogWarning("Template file not found: {Path}", templatePath);
                return null;
            }

            try
            {
                var templateText = File.ReadAllText(templatePath);
                return GetOrCompile(templatePath, templateText);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reading template: {Path}", templatePath);
                return null;
            }
        }

        /// <inheritdoc />
        public Template GetOrCompile(string cacheKey, string templateText)
        {
            lock (_lock)
            {
                if (_cache.TryGetValue(cacheKey, out var cached))
                    return cached;
            }

            var template = Template.Parse(templateText);

            if (template.HasErrors)
            {
                lock (_lock)
                {
                    foreach (var message in template.Messages.Where(m => m.Type == Scriban.Parsing.ParserMessageType.Error))
                    {
                        _errors.Add(new TemplateCompilationError
                        {
                            FilePath = cacheKey,
                            Message = message.Message,
                            Line = message.Span.Start.Line,
                            Column = message.Span.Start.Column
                        });
                    }
                }
                _logger.LogWarning("Template has compilation errors: {Path}", cacheKey);
            }
            else
            {
                lock (_lock)
                {
                    _cache[cacheKey] = template;
                }
                _logger.LogTrace("Cached template: {Path}", cacheKey);
            }

            return template;
        }

        /// <inheritdoc />
        public bool Precompile(string templatePath)
        {
            if (!File.Exists(templatePath))
                return false;

            try
            {
                var templateText = File.ReadAllText(templatePath);

                // For fragments, strip #meta block before compiling
                if (templatePath.Contains("Fragments", StringComparison.OrdinalIgnoreCase))
                {
                    templateText = System.Text.RegularExpressions.Regex.Replace(
                        templateText, 
                        @"#meta\s*[\s\S]*?\s*#end", 
                        "", 
                        System.Text.RegularExpressions.RegexOptions.None);
                }

                var template = Template.Parse(templateText);

                if (template.HasErrors)
                {
                    lock (_lock)
                    {
                        foreach (var message in template.Messages.Where(m => m.Type == Scriban.Parsing.ParserMessageType.Error))
                        {
                            _errors.Add(new TemplateCompilationError
                            {
                                FilePath = templatePath,
                                Message = message.Message,
                                Line = message.Span.Start.Line,
                                Column = message.Span.Start.Column
                            });
                        }
                    }
                    return false;
                }

                lock (_lock)
                {
                    _cache[templatePath] = template;
                }
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error precompiling template: {Path}", templatePath);
                lock (_lock)
                {
                    _errors.Add(new TemplateCompilationError
                    {
                        FilePath = templatePath,
                        Message = ex.Message
                    });
                }
                return false;
            }
        }

        /// <inheritdoc />
        public int PrecompileAll(string directory, bool recursive = true)
        {
            if (!Directory.Exists(directory))
            {
                _logger.LogWarning("Template directory not found: {Path}", directory);
                return 0;
            }

            var searchOption = recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly;
            var files = Directory.GetFiles(directory, "*.sbn", searchOption);
            var compiled = 0;

            foreach (var file in files)
            {
                if (Precompile(file))
                    compiled++;
            }

            _logger.LogInformation("Pre-compiled {Compiled}/{Total} templates from {Directory}", 
                compiled, files.Length, directory);

            return compiled;
        }

        /// <inheritdoc />
        public void Invalidate(string templatePath)
        {
            lock (_lock)
            {
                _cache.Remove(templatePath);
            }
            _logger.LogDebug("Invalidated template cache: {Path}", templatePath);
        }

        /// <inheritdoc />
        public void ClearAll()
        {
            lock (_lock)
            {
                _cache.Clear();
                _errors.Clear();
            }
            _logger.LogDebug("Template cache cleared");
        }
    }
}
