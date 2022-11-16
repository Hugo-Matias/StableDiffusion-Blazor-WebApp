namespace BlazorWebApp.Services
{
    public class RandomService
    {
        public List<int> Ids { get; set; } = new();

        private int _maxValue = 9999;

        public int GetRandomId()
        {
            int id = 0;

            while (Ids.Contains(id))
            {
                id = new Random().Next(_maxValue);
            }

            Ids.Add(id);

            return id;
        }
    }
}
