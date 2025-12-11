using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Adapter to allow DatabaseService to be used as IStateDatabaseService.
    /// This is a temporary solution until full database service interfaces are created.
    /// </summary>
    public class StateDatabaseServiceAdapter : IStateDatabaseService
    {
        private readonly DatabaseService _databaseService;

        public StateDatabaseServiceAdapter(DatabaseService databaseService)
        {
            _databaseService = databaseService;
        }

        public Task<State?> GetState(int id) => _databaseService.GetState(id);

        public Task<State> UpdateState(State state) => _databaseService.UpdateState(state);
    }
}
