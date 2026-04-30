using System;
using BlazorWebApp.Models;

namespace BlazorWebApp.Data.Entities
{
    /// <summary>
    /// Per-session (and one unbound) restorable state for the Workshop Wizard.
    /// Body is JSON-serialized via <see cref="WizardJsonOptions.Compact"/> through a
    /// <c>ValueConverter</c> registered in <see cref="AppDbContext"/>.
    /// </summary>
    /// <remarks>
    /// A filtered unique index on <see cref="SessionId"/> ensures at most one wizard row
    /// per Workshop session, while still permitting one row with <c>SessionId IS NULL</c>
    /// (the unbound slot used when no session is active).
    /// </remarks>
    public class WorkshopWizardSession
    {
        public int Id { get; set; }

        public int? SessionId { get; set; }
        public PromptWorkshopSession? Session { get; set; }

        public WizardBody Body { get; set; } = new();

        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }
    }
}
