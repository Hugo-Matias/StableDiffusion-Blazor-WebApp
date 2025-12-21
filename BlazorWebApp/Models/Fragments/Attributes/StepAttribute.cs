namespace BlazorWebApp.Models.Fragments.Attributes
{
    /// <summary>
    /// Defines the step increment for numeric slider controls.
    /// Used in conjunction with [Range] to define complete slider behavior.
    /// </summary>
    /// <example>
    /// [Range(0, 1)]
    /// [Step(0.01)]
    /// public float Denoise { get; set; } = 1.0f;
    /// </example>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class StepAttribute : Attribute
    {
        /// <summary>
        /// The step increment value.
        /// </summary>
        public double Value { get; }

        /// <summary>
        /// Creates a Step attribute with the specified increment.
        /// </summary>
        /// <param name="value">The step increment (e.g., 0.01 for fine control, 8 for resolution snapping)</param>
        public StepAttribute(double value)
        {
            Value = value;
        }

        /// <summary>
        /// Gets the step value as the specified type.
        /// </summary>
        public T GetValue<T>() where T : struct, IConvertible
        {
            return (T)Convert.ChangeType(Value, typeof(T));
        }
    }
}
