namespace BlazorWebApp.Attributes
{
    /// <summary>
    /// Attribute to mark a Blazor component as a fragment form component.
    /// Used for automatic discovery and registration in ComponentRegistry.
    /// </summary>
    /// <remarks>
    /// Apply this attribute to fragment form components:
    /// <code>
    /// [FragmentComponent("SamplerForm")]
    /// public partial class SamplerForm : ComponentBase { }
    /// </code>
    /// 
    /// The component will be automatically registered during startup
    /// and can be rendered dynamically based on fragment schema.
    /// </remarks>
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
    public class FragmentComponentAttribute : Attribute
    {
        /// <summary>
        /// The component name used in fragment schemas.
        /// Should match the "component" property in fragment #meta.ui blocks.
        /// </summary>
        public string ComponentName { get; }

        /// <summary>
        /// Creates a new FragmentComponentAttribute.
        /// </summary>
        /// <param name="componentName">The component name for schema matching.</param>
        public FragmentComponentAttribute(string componentName)
        {
            ComponentName = componentName ?? throw new ArgumentNullException(nameof(componentName));
        }
    }
}
