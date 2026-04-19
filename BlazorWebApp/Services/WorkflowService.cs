using BlazorWebApp.Models;
using BlazorWebApp.Workflows.Models;
using System.Reflection;
using System.Text.Json;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    /// <summary>
    /// Service for loading, composing, and managing workflow templates.
    /// Uses C# IWorkflowBuilder implementations exclusively.
    /// </summary>
    public class WorkflowService : IWorkflowService
    {
        private readonly ILogger<WorkflowService> _logger;

        // Cache for discovered C# workflow builders
        private readonly Dictionary<Guid, IWorkflowBuilder> _workflowBuilders = new();
        private readonly object _workflowBuildersLock = new();
        private bool _workflowBuildersDiscovered = false;

        public WorkflowService(ILogger<WorkflowService> logger)
        {
            _logger = logger;
        }

        #region Workflow Builder Discovery

        /// <summary>
        /// Discovers all IWorkflowBuilder implementations in the current assembly.
        /// Called lazily on first access. Results are cached.
        /// </summary>
        private void DiscoverWorkflowBuilders()
        {
            lock (_workflowBuildersLock)
            {
                if (_workflowBuildersDiscovered) return;

                _workflowBuilders.Clear();

                try
                {
                    var assembly = Assembly.GetExecutingAssembly();
                    var workflowBuilderType = typeof(IWorkflowBuilder);

                    var builderTypes = assembly.GetTypes()
                        .Where(t => !t.IsAbstract && !t.IsInterface && workflowBuilderType.IsAssignableFrom(t));

                    foreach (var type in builderTypes)
                    {
                        try
                        {
                            var instance = (IWorkflowBuilder)Activator.CreateInstance(type)!;
                            var id = instance.Metadata.Id;

                            if (_workflowBuilders.TryGetValue(id, out var existing))
                            {
                                _logger.LogError(
                                    "Duplicate workflow ID detected! {NewType} ({NewBase}/{NewMode}/{NewTitle}) " +
                                    "collides with {ExistingType} ({ExistingBase}/{ExistingMode}/{ExistingTitle}). " +
                                    "ID: {Id}. The new workflow will be skipped.",
                                    type.FullName, instance.Metadata.Base, instance.Metadata.Mode, instance.Metadata.Title,
                                    existing.GetType().FullName, existing.Metadata.Base, existing.Metadata.Mode, existing.Metadata.Title,
                                    id);
                                continue;
                            }

                            _workflowBuilders[id] = instance;
                            _logger.LogDebug("Discovered C# workflow: {Title} ({Base}/{Mode}) [{Id}]",
                                instance.Metadata.Title, instance.Metadata.Base, instance.Metadata.Mode, id);
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Failed to instantiate workflow builder: {Type}", type.FullName);
                        }
                    }

                    _workflowBuildersDiscovered = true;
                    _logger.LogInformation("Discovered {Count} C# workflow builder(s)", _workflowBuilders.Count);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error discovering workflow builders");
                }
            }
        }

        /// <inheritdoc />
        public IWorkflowBuilder? GetWorkflowBuilder(Guid workflowId)
        {
            DiscoverWorkflowBuilders();
            lock (_workflowBuildersLock)
            {
                return _workflowBuilders.GetValueOrDefault(workflowId);
            }
        }

        /// <inheritdoc />
        public IReadOnlyDictionary<Guid, IWorkflowBuilder> GetWorkflowBuilders()
        {
            DiscoverWorkflowBuilders();
            lock (_workflowBuildersLock)
            {
                return _workflowBuilders.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
            }
        }

        /// <inheritdoc />
        public bool HasWorkflowBuilder(Guid workflowId)
        {
            DiscoverWorkflowBuilders();
            lock (_workflowBuildersLock)
            {
                return _workflowBuilders.ContainsKey(workflowId);
            }
        }

        #endregion

        #region Workflow Discovery

        /// <inheritdoc />
        public List<Workflow> GetWorkflows()
        {
            DiscoverWorkflowBuilders();

            var workflows = new List<Workflow>();

            lock (_workflowBuildersLock)
            {
                foreach (var builder in _workflowBuilders.Values)
                {
                    var workflow = ConvertBuilderToWorkflow(builder);
                    workflows.Add(workflow);
                }
            }

            _logger.LogDebug("GetWorkflows: {Count} C# workflows", workflows.Count);
            return workflows;
        }

        /// <summary>
        /// Converts an IWorkflowBuilder to the Workflow model for UI compatibility.
        /// </summary>
        private Workflow ConvertBuilderToWorkflow(IWorkflowBuilder builder)
        {
            var metadata = builder.Metadata;
            return new Workflow
            {
                Id = metadata.Id,
                Title = metadata.Title,
                Base = metadata.Base,
                Mode = metadata.Mode,
                Assets = metadata.Assets?.Select(a => new Models.WorkflowAsset
                {
                    Parameter = a.Parameter,
                    Label = a.Label,
                    Type = ConvertAssetType(a.Type),
                    DefaultValue = a.DefaultValue,
                    Order = a.Order,
                    ColumnSize = a.ColumnSize
                }).ToList(),
                Sources = metadata.Sources?.Select(s => new Models.WorkflowSource
                {
                    Id = s.Id,
                    Label = s.Label,
                    Type = s.Type.ToString().ToLower(),
                    Required = s.Required,
                    AllowMultiple = s.AllowMultiple,
                    Parameter = s.Parameter
                }).ToList()
            };
        }

        /// <summary>
        /// Converts fluent API AssetType to Models.AssetType.
        /// </summary>
        private static Models.AssetType ConvertAssetType(Workflows.Models.AssetType type)
        {
            return type switch
            {
                Workflows.Models.AssetType.DiffusionModel => Models.AssetType.DiffusionModel,
                Workflows.Models.AssetType.Clip => Models.AssetType.Clip,
                Workflows.Models.AssetType.Vae => Models.AssetType.Vae,
                Workflows.Models.AssetType.CheckpointModel => Models.AssetType.CheckpointModel,
                Workflows.Models.AssetType.ClipVision => Models.AssetType.ClipVision,
                Workflows.Models.AssetType.Lora => Models.AssetType.Lora,
                _ => Models.AssetType.DiffusionModel
            };
        }

        /// <inheritdoc />
        public Workflow? GetWorkflowById(Guid workflowId)
        {
            if (HasWorkflowBuilder(workflowId))
            {
                var builder = GetWorkflowBuilder(workflowId)!;
                return ConvertBuilderToWorkflow(builder);
            }
            return null;
        }

        /// <inheritdoc />
        public (List<Workflow> workflows, ModelBase? suggestedBase, Guid? suggestedId) RefreshWorkflows(
            ModelBase? currentWorkflowBase = null,
            Guid? currentWorkflowId = null)
        {
            // Reset workflow discovery to pick up any changes
            lock (_workflowBuildersLock)
            {
                _workflowBuildersDiscovered = false;
            }

            try
            {
                var workflows = GetWorkflows();

                if (workflows.Count == 0)
                {
                    _logger.LogWarning("No workflows found");
                    return (new List<Workflow>(), null, null);
                }

                // Try to preserve current selection
                if (currentWorkflowId.HasValue)
                {
                    var matchById = workflows.FirstOrDefault(w => w.Id == currentWorkflowId.Value);
                    if (matchById != null)
                    {
                        return (workflows, matchById.Base, matchById.Id);
                    }
                }

                if (currentWorkflowBase.HasValue && currentWorkflowBase.Value != default)
                {
                    var matchByBase = workflows.FirstOrDefault(w => w.Base == currentWorkflowBase.Value);
                    if (matchByBase != null)
                    {
                        return (workflows, matchByBase.Base, matchByBase.Id);
                    }
                }

                // Default to first workflow
                var first = workflows.First();
                return (workflows, first.Base, first.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error refreshing workflows");
                return (new List<Workflow>(), null, null);
            }
        }

        #endregion

        #region Workflow Composition

        /// <inheritdoc />
        public string ComposeWorkflowFromGenerationParameters(Workflow template, GenerationParameters parameters)
        {
            var builder = GetWorkflowBuilder(template.Id);
            if (builder == null)
            {
                throw new InvalidOperationException($"No C# workflow builder found for workflow '{template.Title}' (ID: {template.Id})");
            }

            var comfyWorkflow = builder.Build(parameters);
            _logger.LogDebug("Composed workflow: {Title}", template.Title);
            return comfyWorkflow.Json;
        }

        #endregion

        #region Schema Access

        /// <inheritdoc />
        public Dictionary<string, FragmentSchema> GetWorkflowFragmentSchemas(Workflow workflow)
        {
            // For C# workflows, build schemas from fragment metadata
            var builder = GetWorkflowBuilder(workflow.Id);
            if (builder == null)
            {
                return new Dictionary<string, FragmentSchema>();
            }

            var schemas = new Dictionary<string, FragmentSchema>(StringComparer.OrdinalIgnoreCase);
            foreach (var fragment in builder.GetFragments())
            {
                var metadata = fragment.Metadata;
                if (metadata.IsHidden) continue;

                var schema = BuildSchemaFromMetadata(metadata);
                schemas[metadata.Id] = schema;
            }

            return schemas;
        }

        /// <summary>
        /// Builds a FragmentSchema from C# FragmentMetadata.
        /// </summary>
        private static FragmentSchema BuildSchemaFromMetadata(FragmentMetadata metadata)
        {
            var schema = new FragmentSchema
            {
                Type = metadata.Type,
                Title = metadata.Title,
                Icon = metadata.Icon,
                Order = metadata.Order,
                DefaultCollapsed = metadata.Collapsible && metadata.DefaultCollapsed,
                Collapsible = metadata.Collapsible,
                Component = metadata.Component
            };

            if (metadata.Parameters != null)
            {
                schema.Parameters = new Dictionary<string, ParameterConstraints>(StringComparer.OrdinalIgnoreCase);
                foreach (var param in metadata.Parameters)
                {
                    schema.Parameters[param.Name] = new ParameterConstraints
                    {
                        Default = param.DefaultValue,
                        Min = param.Min,
                        Max = param.Max,
                        Step = param.Step,
                        Options = param.Options?.ToList(),
                        Source = param.Source?.NodeType,
                        InputName = param.Source?.InputName
                    };
                }
            }

            return schema;
        }

        #endregion
    }
}
