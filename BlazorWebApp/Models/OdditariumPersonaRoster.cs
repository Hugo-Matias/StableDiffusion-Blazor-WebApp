using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace BlazorWebApp.Models
{
    /// <summary>
    /// Loads Odditarium personas from JSON files at startup.
    /// Personas are stored in Data/Odditarium/Personas/{id}.json.
    /// Image asset paths are auto-resolved from the persona Id.
    /// </summary>
    public static class OdditariumPersonaRoster
    {
        private static readonly List<OdditariumPersona> _personas = LoadPersonas();

        private static List<OdditariumPersona> LoadPersonas()
        {
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            var personaDir = Path.Combine(baseDir, "Data", "Odditarium", "Personas");

            if (!Directory.Exists(personaDir))
            {
                Console.WriteLine($"[OdditariumPersonaRoster] Persona directory not found: {personaDir}");
                return new List<OdditariumPersona>();
            }

            var personas = new List<OdditariumPersona>();

            foreach (var filePath in Directory.GetFiles(personaDir, "*.json"))
            {
                try
                {
                    var json = File.ReadAllText(filePath);
                    var persona = JsonSerializer.Deserialize<OdditariumPersona>(json, OdditariumJsonOptions.Compact);

                    if (persona is null)
                    {
                        Console.WriteLine($"[OdditariumPersonaRoster] Skipped {Path.GetFileName(filePath)}: deserialization returned null");
                        continue;
                    }

                    // Validate required fields
                    if (string.IsNullOrWhiteSpace(persona.Id))
                    {
                        persona.Id = Path.GetFileNameWithoutExtension(filePath).ToLowerInvariant();
                    }
                    if (string.IsNullOrWhiteSpace(persona.Name) || string.IsNullOrWhiteSpace(persona.Tagline))
                    {
                        Console.WriteLine($"[OdditariumPersonaRoster] Skipped {Path.GetFileName(filePath)}: missing Name or Tagline");
                        continue;
                    }

                    // Auto-resolve image asset paths from persona Id
                    var id = persona.Id.ToLowerInvariant().Replace("_", "-");
                    persona.ImageAssetIdle = $"/odditarium/personas/{id}/idle.png";
                    persona.ImageAssetHover = $"/odditarium/personas/{id}/hover.png";
                    persona.ImageAssetActive = $"/odditarium/personas/{id}/active.png";
                    persona.ImageAssetThinking = $"/odditarium/personas/{id}/thinking-1.png";

                    personas.Add(persona);
                }
                catch (JsonException ex)
                {
                    Console.WriteLine($"[OdditariumPersonaRoster] Skipped {Path.GetFileName(filePath)}: JSON parse error — {ex.Message}");
                }
                catch (IOException ex)
                {
                    Console.WriteLine($"[OdditariumPersonaRoster] Skipped {Path.GetFileName(filePath)}: IO error — {ex.Message}");
                }
            }

            return personas;
        }

        /// <summary>Return all available personas.</summary>
        public static IReadOnlyList<OdditariumPersona> All => _personas.AsReadOnly();

        /// <summary>Find a persona by its unique key.</summary>
        public static OdditariumPersona? FindById(string id) =>
            _personas.FirstOrDefault(p => p.Id == id);
    }
}
