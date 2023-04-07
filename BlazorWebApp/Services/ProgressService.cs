using BlazorWebApp.Models;

namespace BlazorWebApp.Services
{
    public class ProgressService
    {
        public List<BaseProgress> Progresses { get; set; } = new();

        public event Action OnUpdate;
        public void Add(BaseProgress progress)
        {
            Progresses.Add(progress);
            Refresh();
        }

        public void Update(Guid id, float value)
        {
            var progress = Progresses.FirstOrDefault(p => p.Id == id);
            if (progress != null)
            {
                if (value < 0 || value > progress.MaxValue) { Remove(id); return; }
                else progress.Value = value;
            }
            Refresh();
        }

        public void Remove(Guid id)
        {
            var progress = Progresses?.FirstOrDefault(p => p.Id == id);
            if (progress != null) Progresses.Remove(progress);
            Refresh();
        }

        private void Refresh() => OnUpdate?.Invoke();
    }
}
