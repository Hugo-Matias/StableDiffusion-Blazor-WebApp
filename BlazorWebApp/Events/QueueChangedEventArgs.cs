namespace BlazorWebApp.Events
{
    /// <summary>
    /// Event arguments for when the ComfyUI queue size changes.
    /// QueueRemaining reflects the number of pending prompts reported by the backend's
    /// "status" WebSocket message (exec_info.queue_remaining), including any currently
    /// executing prompt.
    /// </summary>
    public class QueueChangedEventArgs : EventArgs
    {
        /// <summary>
        /// Total number of prompts known to the backend queue (running + pending).
        /// </summary>
        public int QueueRemaining { get; set; }

        public QueueChangedEventArgs(int queueRemaining)
        {
            QueueRemaining = queueRemaining;
        }
    }
}
