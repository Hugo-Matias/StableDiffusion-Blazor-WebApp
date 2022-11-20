using Microsoft.JSInterop;

namespace BlazorWebApp.Services
{
	public class JavascriptService
	{
		private readonly IJSRuntime jSRuntime;

		public JavascriptService(IJSRuntime jSRuntime)
		{
			this.jSRuntime = jSRuntime;
		}

		public async Task LogAsync(object message) => await this.jSRuntime.InvokeVoidAsync("console.log", message);

		public async Task RunScript(string script, object?[]? args) => await this.jSRuntime.InvokeAsync<object>(script, args);
	}
}
