using Microsoft.JSInterop;

namespace BlazorWebApp.Services
{
	public class JSConsole
	{
		private readonly IJSRuntime jSRuntime;

		public JSConsole(IJSRuntime jSRuntime)
		{
			this.jSRuntime = jSRuntime;
		}

		public async Task LogAsync(object message)
		{
			await this.jSRuntime.InvokeVoidAsync("console.log", message);
		}
	}
}
