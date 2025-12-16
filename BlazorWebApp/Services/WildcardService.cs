using System.Text.RegularExpressions;
using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for wildcard business logic operations.
    /// Handles wildcard parsing, random selection, and collection management.
    /// </summary>
    public class WildcardService : IWildcardService
    {
        private readonly IDatabaseService _database;
        private readonly ILogger<WildcardService> _logger;
        private readonly Random _random;

        // Regex pattern to match __wildcard__ syntax
        // __ - literal double underscore start
        // ([a-zA-Z0-9][a-zA-Z0-9_\-./]*[a-zA-Z0-9]) - capture group: 
        //    - starts with alphanumeric
        //    - can contain alphanumeric, underscore, hyphen, dot, or slash in the middle
        //    - ends with alphanumeric
        // __ - literal double underscore end
        // This allows underscores in the middle but not at start/end, preventing issues with triple underscores
        private static readonly Regex WildcardPattern = new Regex(@"__([a-zA-Z0-9](?:[a-zA-Z0-9_\-./]*[a-zA-Z0-9])?)__", RegexOptions.Compiled);

        public WildcardService(IDatabaseService database, ILogger<WildcardService> logger)
        {
            _database = database;
            _logger = logger;
            _random = new Random();
            
            // Seeding is now manual - users can click "Load Sample Data" button in the UI
        }

        public async Task<string?> GetRandomEntry(string collectionName)
        {
            try
            {
                var collection = await _database.GetWildcardCollectionByName(collectionName);
                if (collection == null || !collection.Entries.Any())
                {
                    _logger.LogWarning("Collection '{CollectionName}' not found or has no entries", collectionName);
                    return null;
                }

                // Update usage count
                await _database.UpdateWildcardCollectionUsage(collection.Id);

                // Equal probability selection
                var index = _random.Next(collection.Entries.Count);
                return collection.Entries.ElementAt(index).Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting random entry from collection '{CollectionName}'", collectionName);
                return null;
            }
        }

        public async Task<string?> GetRandomEntryWeighted(string collectionName)
        {
            try
            {
                var collection = await _database.GetWildcardCollectionByName(collectionName);
                if (collection == null || !collection.Entries.Any())
                {
                    _logger.LogWarning("Collection '{CollectionName}' not found or has no entries", collectionName);
                    return null;
                }

                // Update usage count
                await _database.UpdateWildcardCollectionUsage(collection.Id);

                // Calculate total weight
                var totalWeight = collection.Entries.Sum(e => e.Weight);
                if (totalWeight <= 0)
                {
                    // Fallback to equal probability if all weights are zero or negative
                    return await GetRandomEntry(collectionName);
                }

                // Weighted random selection
                var randomValue = _random.NextDouble() * totalWeight;
                var cumulativeWeight = 0.0;

                foreach (var entry in collection.Entries)
                {
                    cumulativeWeight += entry.Weight;
                    if (randomValue <= cumulativeWeight)
                    {
                        return entry.Value;
                    }
                }

                // Fallback (should not reach here, but safety)
                return collection.Entries.Last().Value;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting weighted random entry from collection '{CollectionName}'", collectionName);
                return null;
            }
        }

        public async Task<List<string>> GetAllEntryValues(string collectionName)
        {
            try
            {
                var collection = await _database.GetWildcardCollectionByName(collectionName);
                if (collection == null || !collection.Entries.Any())
                {
                    return new List<string>();
                }

                return collection.Entries
                    .OrderBy(e => e.SortOrder)
                    .Select(e => e.Value)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting entry values from collection '{CollectionName}'", collectionName);
                return new List<string>();
            }
        }

        public async Task<bool> CollectionExists(string collectionName)
        {
            try
            {
                var collection = await _database.GetWildcardCollectionByName(collectionName);
                return collection != null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if collection '{CollectionName}' exists", collectionName);
                return false;
            }
        }

        public async Task<string> ParseWildcards(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input;

            try
            {
                var result = input;
                var matches = WildcardPattern.Matches(input);

                foreach (Match match in matches)
                {
                    var wildcardName = match.Groups[1].Value;
                    var replacement = await GetRandomEntryWeighted(wildcardName);

                    if (replacement != null)
                    {
                        result = result.Replace(match.Value, replacement);
                        _logger.LogDebug("Replaced wildcard '{Wildcard}' with '{Replacement}'", match.Value, replacement);
                    }
                    else
                    {
                        _logger.LogWarning("No replacement found for wildcard '{Wildcard}'", match.Value);
                    }
                }

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing wildcards in input: {Input}", input);
                return input;
            }
        }

        public List<string> DetectWildcards(string input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return new List<string>();

            try
            {
                var matches = WildcardPattern.Matches(input);
                return matches
                    .Select(m => m.Groups[1].Value)
                    .Distinct()
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error detecting wildcards in input: {Input}", input);
                return new List<string>();
            }
        }

        public async Task SeedCollections()
        {
            try
            {
                // Check if wildcards already exist
                var existingCollections = await _database.GetAllWildcardCollections();
                if (existingCollections.Any())
                {
                    _logger.LogInformation("Wildcard collections already exist, skipping seed.");
                    return;
                }

                _logger.LogInformation("Seeding sample wildcard collections...");

                var collections = new List<WildcardCollection>
                {
                    // Clothing - Tops
                    new WildcardCollection
                    {
                        Name = "clothing/tops",
                        Description = "Various upper body clothing items",
                        Category = "Clothing",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "white t-shirt", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "black hoodie", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "red dress shirt", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "blue sweater", Weight = 1.0f, SortOrder = 3 },
                            new() { Value = "green tank top", Weight = 1.0f, SortOrder = 4 },
                            new() { Value = "gray cardigan", Weight = 1.0f, SortOrder = 5 },
                            new() { Value = "leather jacket", Weight = 0.8f, SortOrder = 6 },
                            new() { Value = "denim jacket", Weight = 0.8f, SortOrder = 7 }
                        }
                    },
                    // Clothing - Bottoms
                    new WildcardCollection
                    {
                        Name = "clothing/bottoms",
                        Description = "Various lower body clothing items",
                        Category = "Clothing",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "blue jeans", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "black pants", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "khaki shorts", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "pleated skirt", Weight = 1.0f, SortOrder = 3 },
                            new() { Value = "black leggings", Weight = 1.0f, SortOrder = 4 },
                            new() { Value = "cargo pants", Weight = 0.8f, SortOrder = 5 }
                        }
                    },
                    // Clothing - Shoes
                    new WildcardCollection
                    {
                        Name = "clothing/shoes",
                        Description = "Various footwear options",
                        Category = "Clothing",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "white sneakers", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "black boots", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "brown sandals", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "high heels", Weight = 1.0f, SortOrder = 3 },
                            new() { Value = "leather loafers", Weight = 0.8f, SortOrder = 4 }
                        }
                    },
                    // Locations - Indoor
                    new WildcardCollection
                    {
                        Name = "locations/indoor",
                        Description = "Indoor location settings",
                        Category = "Locations",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "cozy living room", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "modern bedroom", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "spacious kitchen", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "home office", Weight = 1.0f, SortOrder = 3 },
                            new() { Value = "grand library", Weight = 0.8f, SortOrder = 4 },
                            new() { Value = "art studio", Weight = 0.8f, SortOrder = 5 }
                        }
                    },
                    // Locations - Outdoor
                    new WildcardCollection
                    {
                        Name = "locations/outdoor",
                        Description = "Outdoor location settings",
                        Category = "Locations",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "mystical forest", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "sandy beach", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "mountain peak", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "busy city street", Weight = 1.0f, SortOrder = 3 },
                            new() { Value = "peaceful park", Weight = 1.0f, SortOrder = 4 },
                            new() { Value = "rural countryside", Weight = 0.8f, SortOrder = 5 }
                        }
                    },
                    // Locations - Fantasy
                    new WildcardCollection
                    {
                        Name = "locations/fantasy",
                        Description = "Fantasy and sci-fi locations",
                        Category = "Locations",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "ancient castle", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "dark dungeon", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "floating island", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "crystal cave", Weight = 1.0f, SortOrder = 3 },
                            new() { Value = "enchanted forest", Weight = 1.0f, SortOrder = 4 },
                            new() { Value = "cyberpunk cityscape", Weight = 0.8f, SortOrder = 5 }
                        }
                    },
                    // Styles - Art Medium
                    new WildcardCollection
                    {
                        Name = "styles/art-medium",
                        Description = "Different artistic mediums and techniques",
                        Category = "Styles",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "oil painting", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "watercolor", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "digital art", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "pencil sketch", Weight = 1.0f, SortOrder = 3 },
                            new() { Value = "acrylic painting", Weight = 1.0f, SortOrder = 4 },
                            new() { Value = "ink drawing", Weight = 0.8f, SortOrder = 5 }
                        }
                    },
                    // Styles - Lighting
                    new WildcardCollection
                    {
                        Name = "styles/lighting",
                        Description = "Different lighting conditions and moods",
                        Category = "Styles",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "natural daylight", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "studio lighting", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "dramatic lighting", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "soft diffused light", Weight = 1.0f, SortOrder = 3 },
                            new() { Value = "golden hour", Weight = 1.2f, SortOrder = 4 },
                            new() { Value = "volumetric lighting", Weight = 0.8f, SortOrder = 5 }
                        }
                    },
                    // Styles - Mood
                    new WildcardCollection
                    {
                        Name = "styles/mood",
                        Description = "Emotional atmosphere and mood",
                        Category = "Styles",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "peaceful and serene", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "energetic and vibrant", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "mysterious and dark", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "romantic and dreamy", Weight = 1.0f, SortOrder = 3 },
                            new() { Value = "melancholic and somber", Weight = 0.8f, SortOrder = 4 }
                        }
                    },
                    // Characters - Hair Color
                    new WildcardCollection
                    {
                        Name = "characters/hair-color",
                        Description = "Hair color options for characters",
                        Category = "Characters",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "blonde hair", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "brunette hair", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "black hair", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "red hair", Weight = 1.0f, SortOrder = 3 },
                            new() { Value = "silver hair", Weight = 0.8f, SortOrder = 4 },
                            new() { Value = "pink hair", Weight = 0.7f, SortOrder = 5 }
                        }
                    },
                    // Characters - Hair Style
                    new WildcardCollection
                    {
                        Name = "characters/hair-style",
                        Description = "Hair style variations",
                        Category = "Characters",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "long flowing hair", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "short hair", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "ponytail", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "braided hair", Weight = 1.0f, SortOrder = 3 },
                            new() { Value = "messy hair", Weight = 0.8f, SortOrder = 4 },
                            new() { Value = "bun hairstyle", Weight = 0.8f, SortOrder = 5 }
                        }
                    },
                    // Characters - Eye Color
                    new WildcardCollection
                    {
                        Name = "characters/eye-color",
                        Description = "Eye color options for characters",
                        Category = "Characters",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "blue eyes", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "green eyes", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "brown eyes", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "amber eyes", Weight = 0.8f, SortOrder = 3 },
                            new() { Value = "violet eyes", Weight = 0.7f, SortOrder = 4 }
                        }
                    },
                    // Actions - Poses
                    new WildcardCollection
                    {
                        Name = "actions/poses",
                        Description = "Character poses and positions",
                        Category = "Actions",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "standing confidently", Weight = 1.0f, SortOrder = 0 },
                            new() { Value = "sitting casually", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "lying down relaxed", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "kneeling", Weight = 0.8f, SortOrder = 3 },
                            new() { Value = "crouching", Weight = 0.8f, SortOrder = 4 }
                        }
                    },
                    // Actions - Expressions
                    new WildcardCollection
                    {
                        Name = "actions/expressions",
                        Description = "Facial expressions",
                        Category = "Actions",
                        Entries = new List<WildcardEntry>
                        {
                            new() { Value = "gentle smile", Weight = 1.2f, SortOrder = 0 },
                            new() { Value = "laughing happily", Weight = 1.0f, SortOrder = 1 },
                            new() { Value = "serious expression", Weight = 1.0f, SortOrder = 2 },
                            new() { Value = "surprised look", Weight = 0.8f, SortOrder = 3 },
                            new() { Value = "sad expression", Weight = 0.7f, SortOrder = 4 }
                        }
                    }
                };

                // Create all collections
                foreach (var collection in collections)
                {
                    await _database.CreateWildcardCollection(collection);
                }

                _logger.LogInformation($"Seeded {collections.Count} wildcard collections with {collections.Sum(c => c.Entries.Count)} total entries");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error seeding wildcard collections");
            }
        }

        #region Phase 4: Autocomplete Support

        public async Task<List<string>> GetCategories()
        {
            try
            {
                return await _database.GetAllWildcardCategories();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting wildcard categories");
                return new List<string>();
            }
        }

        public async Task<List<WildcardCollection>> GetCollectionsByCategory(string category)
        {
            try
            {
                return await _database.GetWildcardCollectionsByCategory(category);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting collections for category '{Category}'", category);
                return new List<WildcardCollection>();
            }
        }

        public async Task<List<WildcardCollection>> SearchCollections(string searchQuery, int maxResults = 10)
        {
            try
            {
                var allCollections = await _database.GetAllWildcardCollections();
                
                if (string.IsNullOrWhiteSpace(searchQuery))
                {
                    return allCollections.Take(maxResults).ToList();
                }
                
                return allCollections
                    .Where(c => c.Name.Contains(searchQuery, StringComparison.OrdinalIgnoreCase) ||
                               (c.Category != null && c.Category.Contains(searchQuery, StringComparison.OrdinalIgnoreCase)))
                    .Take(maxResults)
                    .ToList();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error searching collections with query '{Query}'", searchQuery);
                return new List<WildcardCollection>();
            }
        }

        #endregion
    }
}
