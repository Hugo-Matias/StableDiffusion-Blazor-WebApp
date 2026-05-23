namespace BlazorWebApp.Services
{
    /// <summary>
    /// Resolves safe local file paths for HuggingFace downloads.
    /// Handles both flat and folder-preserving modes and detects collisions before any file write.
    /// </summary>
    public static class HuggingFacePathResolver
    {
        /// <summary>
        /// Resolves the local destination path for a single HF repo file.
        /// </summary>
        /// <param name="destinationRoot">Absolute root directory (e.g. {ResourcesPath}/{Type}/{SubType}).</param>
        /// <param name="repoFilePath">Repo-relative path, e.g. "unet/diffusion_pytorch_model.safetensors".</param>
        /// <param name="keepFolderStructure">When true, the repo-relative folder is preserved under <paramref name="destinationRoot"/>.</param>
        /// <param name="outputFilename">Optional override for the bare output filename. When set, replaces only the filename portion.</param>
        /// <returns>Absolute path to the destination file.</returns>
        /// <exception cref="ArgumentException">Thrown when the resolved path escapes <paramref name="destinationRoot"/>.</exception>
        public static string Resolve(string destinationRoot, string repoFilePath, bool keepFolderStructure, string? outputFilename = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(destinationRoot);
            ArgumentException.ThrowIfNullOrEmpty(repoFilePath);

            var normalizedRepo = NormalizeRepoPath(repoFilePath);
            string effectiveName = !string.IsNullOrWhiteSpace(outputFilename)
                ? Path.GetFileName(outputFilename)      // bare name only, strip any injected path
                : Path.GetFileName(normalizedRepo);

            string relativePath;
            if (keepFolderStructure)
            {
                var dir = Path.GetDirectoryName(normalizedRepo) ?? string.Empty;
                relativePath = string.IsNullOrEmpty(dir)
                    ? effectiveName
                    : Path.Combine(dir, effectiveName);
            }
            else
            {
                relativePath = effectiveName;
            }

            var resolved = Path.GetFullPath(Path.Combine(destinationRoot, relativePath));
            var rootFull = Path.GetFullPath(destinationRoot.TrimEnd('/', '\\') + Path.DirectorySeparatorChar);

            if (!resolved.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException(
                    $"Resolved path '{resolved}' escapes destination root '{destinationRoot}'.",
                    nameof(repoFilePath));

            return resolved;
        }

        /// <summary>
        /// Returns the filename portion that will be stored in <c>Resource.Filename</c>.
        /// <para>
        /// When <paramref name="keepFolderStructure"/> is true, the filename includes the relative folder,
        /// using forward slashes as separators for cross-platform DB storage
        /// (e.g. "unet/diffusion_pytorch_model.safetensors").
        /// When false, only the bare filename is stored.
        /// </para>
        /// </summary>
        public static string ResolveDbFilename(string repoFilePath, bool keepFolderStructure, string? outputFilename = null)
        {
            ArgumentException.ThrowIfNullOrEmpty(repoFilePath);
            var normalized = NormalizeRepoPath(repoFilePath);
            string effectiveName = !string.IsNullOrWhiteSpace(outputFilename)
                ? Path.GetFileName(outputFilename)
                : Path.GetFileName(normalized);

            if (keepFolderStructure)
            {
                var dir = (Path.GetDirectoryName(normalized) ?? string.Empty).Replace('\\', '/');
                return string.IsNullOrEmpty(dir) ? effectiveName : $"{dir}/{effectiveName}";
            }
            return effectiveName;
        }

        /// <summary>
        /// Checks a set of repo-relative paths for flat-download filename collisions.
        /// Returns the set of bare filenames that would collide.
        /// </summary>
        public static IReadOnlySet<string> DetectFlatCollisions(IEnumerable<string> repoFilePaths)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var collisions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var path in repoFilePaths)
            {
                var name = Path.GetFileName(NormalizeRepoPath(path));
                if (!seen.Add(name))
                    collisions.Add(name);
            }
            return collisions;
        }

        /// <summary>
        /// Returns collision results for a set of repo-relative paths in flat mode.
        /// </summary>
        public static FlatCollisionResult CheckFlatCollisions(IEnumerable<string> repoFilePaths)
        {
            var paths = repoFilePaths.ToList();
            var collisions = DetectFlatCollisions(paths);
            return new FlatCollisionResult(collisions, paths);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        /// <summary>
        /// Normalizes a repo-relative path to use the OS path separator and strips leading slashes.
        /// Also validates that the path does not contain ".." traversal segments.
        /// </summary>
        public static string NormalizeRepoPath(string repoFilePath)
        {
            // Strip leading slashes
            var trimmed = repoFilePath.TrimStart('/', '\\');

            // Split and validate individual segments
            var segments = trimmed.Replace('\\', '/').Split('/');
            foreach (var segment in segments)
            {
                if (segment == ".." || segment == ".")
                    throw new ArgumentException(
                        $"Repo path '{repoFilePath}' contains traversal segments and cannot be resolved safely.",
                        nameof(repoFilePath));
            }

            // Rejoin using OS separator for local filesystem use
            return string.Join(Path.DirectorySeparatorChar, segments);
        }
    }

    /// <summary>Results from a flat-download collision check.</summary>
    public sealed class FlatCollisionResult
    {
        /// <summary>Bare filenames that appear more than once in the selected file list.</summary>
        public IReadOnlySet<string> CollidingFilenames { get; }

        /// <summary>All input paths, in their original order.</summary>
        public IReadOnlyList<string> AllPaths { get; }

        /// <summary>True when at least one collision exists.</summary>
        public bool HasCollisions => CollidingFilenames.Count > 0;

        public FlatCollisionResult(IReadOnlySet<string> collisions, IReadOnlyList<string> allPaths)
        {
            CollidingFilenames = collisions;
            AllPaths = allPaths;
        }

        /// <summary>
        /// Returns only the paths whose bare filename is in <see cref="CollidingFilenames"/>.
        /// </summary>
        public IEnumerable<string> CollidingPaths =>
            AllPaths.Where(p =>
                CollidingFilenames.Contains(Path.GetFileName(HuggingFacePathResolver.NormalizeRepoPath(p))));
    }
}
