using System;
using BlazorWebApp.Models;

namespace BlazorWebApp.Data.Entities
{
    /// <summary>
    /// Per-session (and one unbound) restorable state for Odditarium.
    /// Body is JSON-serialized via <see cref="OdditariumJsonOptions.Compact"/> through a
    /// <c>ValueConverter</c> registered in <see cref="AppDbContext"/>.
    /// </summary>
    /// <remarks>
    /// A filtered unique index on <see cref="SessionId"/> ensures at most one Odditarium row
    /// per Workshop session, while still permitting one row with <c>SessionId IS NULL</c>
    /// (the unbound slot used when no session is active).
    /// </remarks>
    public class OdditariumSession
    {
        public int Id { get; set; }

        /// <summary>
        /// Optional reference to a PromptWorkshopSession. Stored as nullable int without FK constraint
        /// (SQLite migration limitations). Null when the game runs standalone.
        /// </summary>
        public int? SessionId { get; set; }

        public OdditariumBody Body { get; set; } = new();

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
