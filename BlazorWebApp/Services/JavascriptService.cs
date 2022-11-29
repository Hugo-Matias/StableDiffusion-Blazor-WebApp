using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorWebApp.Services
{
	public class JavascriptService
	{
		private readonly IJSRuntime _jSRuntime;

		public JavascriptService(IJSRuntime jSRuntime)
		{
			_jSRuntime = jSRuntime;
		}

		public async Task LogAsync(object message) => await _jSRuntime.InvokeVoidAsync("console.log", message);

		public async Task RunScript(string script) => await _jSRuntime.InvokeVoidAsync($"{script}");

		public async Task RunScript(string script, object?[]? args) => await _jSRuntime.InvokeAsync<object>(script, args);

		public async Task RunScript(string script, string arg) => await _jSRuntime.InvokeAsync<string>(script, arg);

		public async Task<string> GetSelectionFromInput(ElementReference element) => await _jSRuntime.InvokeAsync<string>("getSelectedText", element);
	}
}
