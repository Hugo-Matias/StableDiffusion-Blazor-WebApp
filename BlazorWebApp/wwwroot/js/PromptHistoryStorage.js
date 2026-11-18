// Prompt History LocalStorage Management
window.PromptHistoryStorage = {
    storageKey: 'llm-prompt-history',
    maxEntries: 20,

    // Get all history entries
    getHistory: function () {
        try {
            const data = localStorage.getItem(this.storageKey);
            if (!data) return [];
            return JSON.parse(data);
        } catch (error) {
            console.error('Error reading prompt history from localStorage:', error);
            return [];
        }
    },

    // Save history entries
    saveHistory: function (historyArray) {
        try {
            // Limit to max entries
            if (historyArray.length > this.maxEntries) {
                historyArray = historyArray.slice(0, this.maxEntries);
            }
            localStorage.setItem(this.storageKey, JSON.stringify(historyArray));
            return true;
        } catch (error) {
            console.error('Error saving prompt history to localStorage:', error);
            return false;
        }
    },

    // Add a new entry
    addEntry: function (entry) {
        const history = this.getHistory();
        history.unshift(entry); // Add to beginning
        return this.saveHistory(history);
    },

    // Clear all history
    clearHistory: function () {
        try {
            localStorage.removeItem(this.storageKey);
            return true;
        } catch (error) {
            console.error('Error clearing prompt history:', error);
            return false;
        }
    },

    // Get storage size
    getStorageSize: function () {
        const data = localStorage.getItem(this.storageKey);
        return data ? data.length : 0;
    }
};

// File download helper
window.downloadFile = function (filename, contentType, content) {
    const blob = new Blob([content], { type: contentType });
    const url = window.URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url;
    link.download = filename;
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
    window.URL.revokeObjectURL(url);
};
