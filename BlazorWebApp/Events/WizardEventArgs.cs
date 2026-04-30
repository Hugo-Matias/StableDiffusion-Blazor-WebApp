using System;

namespace BlazorWebApp.Events
{
    /// <summary>
    /// Published when the Workshop wizard advances by one turn (intro choice, iteration choice,
    /// verb apply, more, or undo). UI subscribers refresh the panel from the persisted body.
    /// </summary>
    public class WizardTurnAdvancedEventArgs : EventArgs
    {
        /// <summary>
        /// Owning Workshop session, or <c>null</c> for the unbound slot used while no session is active.
        /// </summary>
        public int? SessionId { get; init; }

        public Models.WizardStage Stage { get; init; }

        public int TurnCount { get; init; }

        /// <summary>
        /// True the first time we cross the soft cap (30 turns). Surfaces a one-shot snackbar.
        /// </summary>
        public bool SoftCapReached { get; init; }

        /// <summary>
        /// True once the hard cap (50 turns) is reached. Action buttons should disable; only
        /// Commit / Reset / Undo remain available.
        /// </summary>
        public bool HardCapReached { get; init; }
    }

    /// <summary>
    /// Published when the user clicks "Commit -&gt; Composer". Subscribers (e.g. <c>WorkshopView</c>)
    /// fill the composer text box with <see cref="Draft"/>; the user still presses Send.
    /// </summary>
    public class WizardCommittedEventArgs : EventArgs
    {
        public int? SessionId { get; init; }
        public string Draft { get; init; } = string.Empty;
    }

    /// <summary>
    /// Published when the wizard is reset to a fresh intro state. The DB row is preserved.
    /// </summary>
    public class WizardResetEventArgs : EventArgs
    {
        public int? SessionId { get; init; }
    }
}
