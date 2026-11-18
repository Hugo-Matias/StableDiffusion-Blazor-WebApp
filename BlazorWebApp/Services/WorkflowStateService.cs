using BlazorWebApp.Data;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using Microsoft.EntityFrameworkCore;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for managing per-workflow generation parameter state.
    /// Persists workflow-specific parameters to the database so users' customizations
    /// are remembered when switching between workflows.
    /// </summary>
    public class WorkflowStateService : IWorkflowStateService
    {
        private readonly IDbContextFactory<AppDbContext> _contextFactory;
        private readonly ILogger<WorkflowStateService> _logger;

        public WorkflowStateService(
            IDbContextFactory<AppDbContext> contextFactory,
            ILogger<WorkflowStateService> logger)
        {
            _contextFactory = contextFactory;
            _logger = logger;
        }

        /// <inheritdoc />
        public async Task<GenerationParameters?> LoadWorkflowStateAsync(Guid workflowId)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                
                var state = await context.WorkflowStates
                    .AsNoTracking()
                    .FirstOrDefaultAsync(ws => ws.WorkflowId == workflowId);

                if (state?.Parameters != null)
                {
                    _logger.LogDebug("Loaded saved state for workflow {WorkflowId}, last modified {LastModified}", 
                        workflowId, state.LastModified);
                    return state.Parameters;
                }

                _logger.LogDebug("No saved state found for workflow {WorkflowId}", workflowId);
                return null;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading workflow state for {WorkflowId}", workflowId);
                return null;
            }
        }

        /// <inheritdoc />
        public async Task SaveWorkflowStateAsync(Guid workflowId, GenerationParameters parameters)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();

                var existingState = await context.WorkflowStates
                    .FirstOrDefaultAsync(ws => ws.WorkflowId == workflowId);

                if (existingState != null)
                {
                    // Update existing state
                    existingState.Parameters = parameters.Clone();
                    existingState.LastModified = DateTime.UtcNow;
                    _logger.LogDebug("Updating existing state for workflow {WorkflowId}", workflowId);
                }
                else
                {
                    // Create new state
                    var newState = new WorkflowState
                    {
                        WorkflowId = workflowId,
                        Parameters = parameters.Clone(),
                        LastModified = DateTime.UtcNow
                    };
                    context.WorkflowStates.Add(newState);
                    _logger.LogDebug("Creating new state for workflow {WorkflowId}", workflowId);
                }

                await context.SaveChangesAsync();
                _logger.LogDebug("Saved workflow state for {WorkflowId}", workflowId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error saving workflow state for {WorkflowId}", workflowId);
            }
        }

        /// <inheritdoc />
        public async Task DeleteWorkflowStateAsync(Guid workflowId)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();

                var state = await context.WorkflowStates
                    .FirstOrDefaultAsync(ws => ws.WorkflowId == workflowId);

                if (state != null)
                {
                    context.WorkflowStates.Remove(state);
                    await context.SaveChangesAsync();
                    _logger.LogDebug("Deleted workflow state for {WorkflowId}", workflowId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting workflow state for {WorkflowId}", workflowId);
            }
        }

        /// <inheritdoc />
        public async Task<List<WorkflowState>> GetAllWorkflowStatesAsync()
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                
                return await context.WorkflowStates
                    .OrderByDescending(ws => ws.LastModified)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all workflow states");
                return new List<WorkflowState>();
            }
        }

        /// <inheritdoc />
        public async Task<List<WorkflowState>> GetRecentWorkflowStatesAsync(int count = 10)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                
                return await context.WorkflowStates
                    .OrderByDescending(ws => ws.LastModified)
                    .Take(count)
                    .AsNoTracking()
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting recent workflow states");
                return new List<WorkflowState>();
            }
        }

        /// <inheritdoc />
        public async Task<bool> HasSavedStateAsync(Guid workflowId)
        {
            try
            {
                await using var context = await _contextFactory.CreateDbContextAsync();
                return await context.WorkflowStates.AnyAsync(ws => ws.WorkflowId == workflowId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking workflow state for {WorkflowId}", workflowId);
                return false;
            }
        }
    }
}
