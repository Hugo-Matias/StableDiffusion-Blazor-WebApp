using BlazorWebApp.Models;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BlazorWebApp.Services
{
    public class ComfyUIWebsocketService : IHostedService
    {
        private readonly ILogger<ComfyUIWebsocketService> _logger;
        private readonly ManagerService _m;
        private readonly ProgressService _progressService;
        private readonly ClientWebSocket _ws = new();

        public ComfyUIWebsocketService(ILogger<ComfyUIWebsocketService> logger, ManagerService m, ProgressService progressService)
        {
            _logger = logger;
            _m = m;
            _progressService = progressService;
        }

        public async Task StartAsync(CancellationToken cancellationToken)
        {
            if (_m.ComfyWSClientId == null || string.IsNullOrWhiteSpace(_m.ComfyWSClientId)) await _m.GetComfyWSClientId();
            await _ws.ConnectAsync(new Uri($"ws://localhost:8188/ws?clientId={_m.ComfyWSClientId}"), cancellationToken);
            _logger.LogInformation($"WS connection created | ClientID: {_m.ComfyWSClientId}");

            _ = Task.Run(() => ListenLoop(cancellationToken));
        }

        private async Task ListenLoop(CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 64];

            while (_ws.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _ws.ReceiveAsync(buffer, cancellationToken);
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var fullBytes = ms.ToArray();
                    // ComfyUI frames have an 8-byte header we can skip
                    var imageBytes = fullBytes.Skip(8).ToArray();
                    var base64 = Convert.ToBase64String(imageBytes);

                    _m.Progress.CurrentImage = base64;
                }

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    //_logger.LogInformation("WS JSON: {json}", json);

                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        var type = doc.RootElement.GetProperty("type").GetString();

                        if (type == "execution_start")
                        {
                            _m.Progress = new() { State = new() { Job = "Execution Started" } };
                            _m.InvokeProgressChanged();
                        }

                        if (type == "progress")
                        {
                            var data = doc.RootElement.GetProperty("data");
                            var id = Guid.Parse(data.GetProperty("prompt_id").ToString());
                            var value = data.GetProperty("value").GetInt32();
                            var max = data.GetProperty("max").GetInt32();
                            var node = doc.RootElement.GetProperty("data").GetProperty("node");

                            // If not already tracked, add it
                            var existing = _progressService.Progresses.FirstOrDefault(p => p.Id == id);
                            if (existing == null)
                            {
                                var progress = new BaseProgress
                                {
                                    Id = id,
                                    Label = string.Empty,
                                    MaxValue = max,
                                    Value = value,
                                    BarColor = MudBlazor.Color.Primary,
                                };
                                _progressService.Progresses.Add(progress);
                            }

                            _m.Progress.Value = (float)value / max;
                            _m.Progress.State.Job = $"Running node: {node}";
                            _progressService.Update(id, value);
                            _m.InvokeProgressChanged();
                        }

                        if (type == "execution_success" || type == "execution_error")
                        {
                            var id = Guid.Parse(doc.RootElement.GetProperty("data").GetProperty("prompt_id").GetString());
                            _progressService.Remove(id);
                            _m.Progress = new();
                            _m.InvokeProgressChanged();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse WS JSON");
                    }
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
            => _ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stopping", cancellationToken);
    }
}
