using System;

namespace BlazorWebApp.Events
{
    /// <summary>
    /// Published when Odditarium advances by one round (option choice, more, skip, or undo).
    /// UI subscribers refresh the panel from the persisted body.
    /// </summary>
    public class OdditariumTurnAdvancedEventArgs : EventArgs
    {
        /// <summary>
        /// Owning Workshop session, or <c>null</c> for the unbound slot used while no session is active.
        /// </summary>
        public int? SessionId { get; init; }

        public int RoundCount { get; init; }

        /// <summary>The descriptive layer just added by this turn.</summary>
        public string LayerAdded { get; init; } = string.Empty;

        /// <summary>Total layers collected so far in this session.</summary>
        public int LayersCollected { get; init; }
    }

    /// <summary>
    /// Published when the user clicks "Commit". The LLM assembles all collected layers
    /// into a coherent image prompt. Subscribers fill their surfaces with <see cref="Draft"/>.
    /// </summary>
    public class OdditariumCommittedEventArgs : EventArgs
    {
        public int? SessionId { get; init; }
        public string Draft { get; init; } = string.Empty;
    }

    /// <summary>
    /// Published when the game is reset to a fresh state. The DB row is preserved.
    /// </summary>
    public class OdditariumResetEventArgs : EventArgs
    {
        public int? SessionId { get; init; }
    }
}
