using BlazorWebApp.Data.Entities;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Interface for database operations used by StateService.
    /// This is a minimal interface extracted for testing purposes.
    /// Full IDatabaseService will be created in later phases.
    /// </summary>
    public interface IStateDatabaseService
    {
        Task<State?> GetState(int id);
        Task<State> UpdateState(State state);
    }
}
