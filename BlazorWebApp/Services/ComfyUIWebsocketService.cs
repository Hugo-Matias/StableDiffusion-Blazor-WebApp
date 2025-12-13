using BlazorWebApp.Models;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BlazorWebApp.Services
{
    public class ComfyUIWebsocketService : IHostedService
    {
        private readonly ILogger<ComfyUIWebsocketService> _logger;
        private readonly IBackendService _backend;
        private readonly ImageService _imageService;
        private readonly IProgressService _progressService;
        private readonly ComfyUIEventBus _bus;
        private ClientWebSocket? _currentWs;
        private readonly object _lock = new();
        private Guid _promptId;

        public ComfyUIWebsocketService(ILogger<ComfyUIWebsocketService> logger, IBackendService backend, ImageService imageService, IProgressService progressService, ComfyUIEventBus bus)
        {
            _logger = logger;
            _backend = backend;
            _imageService = imageService;
            _progressService = progressService;
            _bus = bus;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _ = Task.Run(() => ConnectLoop(cancellationToken), cancellationToken);
            return Task.CompletedTask;
        }

        private async Task ConnectLoop(CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                using var ws = new ClientWebSocket();
                try
                {
                    _backend.ComfyWSClientId = Guid.NewGuid().ToString();
                    await ws.ConnectAsync(
                        new Uri($"ws://localhost:8188/ws?clientId={_backend.ComfyWSClientId}"),
                        cancellationToken);

                    lock (_lock)
                    {
                        _currentWs = ws;
                    }

                    _logger.LogInformation($"WS connected | ClientID: {_backend.ComfyWSClientId}");
                    await ListenLoop(ws, cancellationToken);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "WS connect failed, retrying in 5s...");
                    await Task.Delay(TimeSpan.FromSeconds(5), cancellationToken);
                }
                finally
                {
                    lock (_lock)
                    {
                        _currentWs = null;
                    }
                }
            }
        }

        private async Task ListenLoop(ClientWebSocket ws, CancellationToken cancellationToken)
        {
            var buffer = new byte[1024 * 64];

            while (ws.State == WebSocketState.Open && !cancellationToken.IsCancellationRequested)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await ws.ReceiveAsync(buffer, cancellationToken);
                    ms.Write(buffer, 0, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(ms.ToArray());
                    //_logger.LogInformation("WS JSON: {json}", json);

                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        var type = doc.RootElement.GetProperty("type").GetString();

                        if (type == "execution_start")
                        {
                            _imageService.Progress = new() { State = new() { Job = "Execution Started" } };
                            _progressService.NotifyProgressChanged();
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

                            _imageService.Progress.Value = (float)value / max;
                            _imageService.Progress.State.Job = $"Running node: {node}";
                            _progressService.Update(id, value);
                            _progressService.NotifyProgressChanged();
                        }

                        if (type == "execution_success" || type == "execution_error" || type == "execution_interrupted")
                        {
                            _promptId = Guid.Parse(doc.RootElement.GetProperty("data").GetProperty("prompt_id").GetString());

                            if (type == "execution_success")
                            {
                                _bus.PublishExecutionSucceeded(_promptId);
                            }
                            else if (type == "execution_interrupted")
                            {
                                _bus.PublishExecutionFailed(_promptId, "Execution Interrupted");
                            }
                            else if (type == "execution_error")
                            {
                                var data = doc.RootElement.GetProperty("data");
                                var nodeId = data.GetProperty("node_id").GetString();
                                var nodeType = data.GetProperty("node_type").GetString();
                                var errorType = data.GetProperty("exception_type").GetString();
                                var error = data.GetProperty("exception_message").GetString();
                                _bus.PublishExecutionFailed(_promptId, $"{errorType} | Node: {nodeId} - {nodeType}: {error}");
                            }
                            _progressService.Remove(_promptId);
                            _imageService.Progress = new();
                            _progressService.NotifyProgressChanged();
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Failed to parse WS JSON");
                    }


                }

                if (result.MessageType == WebSocketMessageType.Binary)
                {
                    var fullBytes = ms.ToArray();
                    // ComfyUI frames have an 8-byte header we can skip
                    var imageBytes = fullBytes.Skip(8).ToArray();

                    var base64 = Convert.ToBase64String(imageBytes);
                    _imageService.Progress.CurrentImage = base64;
                }
            }
        }

        public async Task StopAsync(CancellationToken cancellationToken)
        {
            ClientWebSocket? ws;
            lock (_lock)
            {
                ws = _currentWs;
            }

            if (ws != null && ws.State == WebSocketState.Open)
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "Stopping", cancellationToken);
            }
        }
    }
}
