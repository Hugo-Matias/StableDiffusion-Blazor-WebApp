using BlazorWebApp.Models;
using Microsoft.AspNetCore.Components;

namespace BlazorWebApp.Components.Shared.Generation
{
    /// <summary>
    /// Base class for designed fragment form components.
    /// Provides access to schema constraints and typed value accessors.
    /// </summary>
    public abstract class FragmentFormBase : ComponentBase
    {
        /// <summary>
        /// The fragment schema with UI definition and parameter constraints.
        /// </summary>
        [Parameter]
        public FragmentSchema Schema { get; set; } = new();

        /// <summary>
        /// The fragment parameters containing values.
        /// </summary>
        [Parameter]
        public FragmentParameters Parameters { get; set; } = new();

        /// <summary>
        /// Callback when any value changes.
        /// </summary>
        [Parameter]
        public EventCallback<(string key, object? value)> OnValueChanged { get; set; }

        /// <summary>
        /// Gets the Values dictionary for direct access.
        /// </summary>
        protected Dictionary<string, object?> Values => Parameters.Values;

        #region Typed Value Accessors

        /// <summary>
        /// Gets an integer value with fallback.
        /// </summary>
        protected int GetInt(string key, int defaultValue = 0)
        {
            return Parameters.GetValueOrDefault(key, defaultValue);
        }

        /// <summary>
        /// Gets a long value with fallback.
        /// </summary>
        protected long GetLong(string key, long defaultValue = 0)
        {
            return Parameters.GetValueOrDefault(key, defaultValue);
        }

        /// <summary>
        /// Gets a double value with fallback.
        /// </summary>
        protected double GetDouble(string key, double defaultValue = 0)
        {
            return Parameters.GetValueOrDefault(key, defaultValue);
        }

        /// <summary>
        /// Gets a float value with fallback.
        /// </summary>
        protected float GetFloat(string key, float defaultValue = 0)
        {
            return Parameters.GetValueOrDefault(key, defaultValue);
        }

        /// <summary>
        /// Gets a string value with fallback.
        /// </summary>
        protected string GetString(string key, string defaultValue = "")
        {
            return Parameters.GetValueOrDefault(key, defaultValue) ?? defaultValue;
        }

        /// <summary>
        /// Gets a boolean value with fallback.
        /// </summary>
        protected bool GetBool(string key, bool defaultValue = false)
        {
            return Parameters.GetValueOrDefault(key, defaultValue);
        }

        #endregion

        #region Value Setters

        /// <summary>
        /// Sets a value and triggers the change callback.
        /// </summary>
        protected async Task SetValueAsync(string key, object? value)
        {
            Parameters.SetValue(key, value);
            await OnValueChanged.InvokeAsync((key, value));
        }

        /// <summary>
        /// Sets a value synchronously (for binding).
        /// </summary>
        protected void SetValue(string key, object? value)
        {
            Parameters.SetValue(key, value);
            _ = OnValueChanged.InvokeAsync((key, value));
        }

        #endregion

        #region Constraint Accessors

        /// <summary>
        /// Gets constraints for a parameter.
        /// </summary>
        protected ParameterConstraints GetConstraints(string parameterName)
        {
            return Schema.GetConstraints(parameterName);
        }

        /// <summary>
        /// Gets the minimum value for a parameter as integer.
        /// </summary>
        protected int GetMinInt(string parameterName, int defaultValue = 0)
        {
            return GetConstraints(parameterName).GetMin(defaultValue);
        }

        /// <summary>
        /// Gets the maximum value for a parameter as integer.
        /// </summary>
        protected int GetMaxInt(string parameterName, int defaultValue = 100)
        {
            return GetConstraints(parameterName).GetMax(defaultValue);
        }

        /// <summary>
        /// Gets the step value for a parameter as integer.
        /// </summary>
        protected int GetStepInt(string parameterName, int defaultValue = 1)
        {
            return GetConstraints(parameterName).GetStep(defaultValue);
        }

        /// <summary>
        /// Gets the minimum value for a parameter as double.
        /// </summary>
        protected double GetMinDouble(string parameterName, double defaultValue = 0)
        {
            return GetConstraints(parameterName).GetMin(defaultValue);
        }

        /// <summary>
        /// Gets the maximum value for a parameter as double.
        /// </summary>
        protected double GetMaxDouble(string parameterName, double defaultValue = 100)
        {
            return GetConstraints(parameterName).GetMax(defaultValue);
        }

        /// <summary>
        /// Gets the step value for a parameter as double.
        /// </summary>
        protected double GetStepDouble(string parameterName, double defaultValue = 1)
        {
            return GetConstraints(parameterName).GetStep(defaultValue);
        }

        /// <summary>
        /// Gets the options array for a select field.
        /// </summary>
        protected List<string> GetOptions(string parameterName)
        {
            return GetConstraints(parameterName).Options ?? new List<string>();
        }

        #endregion
    }
}
