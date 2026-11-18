// Workshop composer Enter-to-send helper.
// Prevents the default newline insertion on Enter (without modifiers) so the
// MudTextField's bound value never picks up a stray "\n" before/after the send.
// Shift/Ctrl/Alt + Enter still inserts a newline as expected.
window.workshopComposer = {
  attach: function (rootEl) {
    if (!rootEl) return;
    const ta = rootEl.querySelector("textarea");
    if (!ta || ta.dataset.workshopComposerAttached === "1") return;
    ta.dataset.workshopComposerAttached = "1";
    ta.addEventListener("keydown", function (e) {
      if (e.key === "Enter" && !e.shiftKey && !e.ctrlKey && !e.altKey) {
        e.preventDefault();
      }
    });
  },
};
