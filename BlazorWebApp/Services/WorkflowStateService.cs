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
            catch (System.Text.Json.JsonException jsonEx)
            {
                // JSON deserialization failed - likely old data without type discriminators
                _logger.LogWarning("Workflow state for {WorkflowId} has incompatible format, deleting stale data: {Message}", 
                    workflowId, jsonEx.Message);
                await DeleteWorkflowStateAsync(workflowId);
                return null;
            }
            catch (NotSupportedException nsEx) when (nsEx.Message.Contains("type discriminator"))
            {
                // Polymorphic deserialization failed - old data without $type property
                _logger.LogWarning("Workflow state for {WorkflowId} missing type discriminators, deleting stale data", workflowId);
                await DeleteWorkflowStateAsync(workflowId);
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

                // First, check if we need to delete incompatible old data
                bool deletedOldData = false;
                try
                {
                    // Try to load existing state - this may fail if old format
                    var existingState = await context.WorkflowStates
                        .FirstOrDefaultAsync(ws => ws.WorkflowId == workflowId);

                    if (existingState != null)
                    {
                        // Update existing state
                        existingState.Parameters = parameters.Clone();
                        existingState.LastModified = DateTime.UtcNow;
                        _logger.LogDebug("Updating existing state for workflow {WorkflowId}", workflowId);
                        await context.SaveChangesAsync();
                        _logger.LogDebug("Saved workflow state for {WorkflowId}", workflowId);
                        return;
                    }
                }
                catch (NotSupportedException nsEx) when (nsEx.Message.Contains("type discriminator"))
                {
                    // Can't load existing state due to old format - delete it
                    _logger.LogWarning("Existing workflow state for {WorkflowId} has incompatible format, deleting and recreating", workflowId);
                    await DeleteWorkflowStateInternalAsync(context, workflowId);
                    deletedOldData = true;
                }
                catch (System.Text.Json.JsonException)
                {
                    // JSON format issue - delete and recreate
                    _logger.LogWarning("Existing workflow state for {WorkflowId} has invalid JSON, deleting and recreating", workflowId);
                    await DeleteWorkflowStateInternalAsync(context, workflowId);
                    deletedOldData = true;
                }

                // Create new state (either no existing state, or we just deleted incompatible data)
                var newState = new WorkflowState
                {
                    WorkflowId = workflowId,
                    Parameters = parameters.Clone(),
                    LastModified = DateTime.UtcNow
                };
                context.WorkflowStates.Add(newState);
                _logger.LogDebug("Creating new state for workflow {WorkflowId}{Reason}", 
                    workflowId, 
                    deletedOldData ? " (replaced incompatible data)" : "");

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
                await DeleteWorkflowStateInternalAsync(context, workflowId);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting workflow state for {WorkflowId}", workflowId);
            }
        }

        private async Task DeleteWorkflowStateInternalAsync(AppDbContext context, Guid workflowId)
        {
            // Use raw SQL to delete without loading the entity (avoids deserialization issues)
            await context.Database.ExecuteSqlInterpolatedAsync(
                $"DELETE FROM WorkflowStates WHERE WorkflowId = {workflowId.ToString()}");
            _logger.LogDebug("Deleted workflow state for {WorkflowId}", workflowId);
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
