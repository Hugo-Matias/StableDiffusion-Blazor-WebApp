using BlazorWebApp.Models;
using System.Buffers.Binary;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace BlazorWebApp.Services
{
    public class ComfyUIWebsocketService : IHostedService
    {
        private const int ComfyBinaryHeaderLength = 8;
        private const uint PreviewImageEventType = 1;
        private const uint JpegImageType = 1;
        private const uint PngImageType = 2;

        private readonly ILogger<ComfyUIWebsocketService> _logger;
        private readonly IBackendService _backend;
        private readonly IImageService _imageService;
        private readonly IProgressService _progressService;
        private readonly ComfyUIEventBus _bus;
        private ClientWebSocket? _currentWs;
        private readonly object _lock = new();
        private Guid _promptId;
        private string _currentNodeId = string.Empty;

        public ComfyUIWebsocketService(ILogger<ComfyUIWebsocketService> logger, IBackendService backend, IImageService imageService, IProgressService progressService, ComfyUIEventBus bus)
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

                        if (type == "status")
                        {
                            // ComfyUI sends: { type: "status", data: { status: { exec_info: { queue_remaining: N } } } }
                            if (doc.RootElement.TryGetProperty("data", out var statusData)
                                && statusData.TryGetProperty("status", out var statusObj)
                                && statusObj.TryGetProperty("exec_info", out var execInfo)
                                && execInfo.TryGetProperty("queue_remaining", out var queueRemaining)
                                && queueRemaining.TryGetInt32(out var remaining))
                            {
                                _progressService.QueueRemaining = remaining;
                            }
                        }

                        if (type == "execution_start")
                        {
                            _currentNodeId = string.Empty;
                            _imageService.Progress = new() { State = new() { Job = "Execution Started" } };
                            _progressService.NotifyProgressChanged();
                        }

                        if (type == "executing")
                        {
                            var data = doc.RootElement.GetProperty("data");
                            if (data.TryGetProperty("node", out var nodeElement) && nodeElement.ValueKind != JsonValueKind.Null)
                            {
                                _currentNodeId = nodeElement.GetString() ?? string.Empty;
                                _imageService.Progress.CurrentNodeId = _currentNodeId;
                                _imageService.Progress.State.Job = FormatNodeLabel(_currentNodeId);
                                _progressService.NotifyProgressChanged();
                            }
                        }

                        if (type == "progress")
                        {
                            var data = doc.RootElement.GetProperty("data");
                            if (!Guid.TryParse(data.GetProperty("prompt_id").GetString(), out var id))
                            {
                                _logger.LogWarning("ComfyUI progress event did not include a valid prompt_id");
                                continue;
                            }

                            var value = data.GetProperty("value").GetInt32();
                            var max = data.GetProperty("max").GetInt32();
                            var node = doc.RootElement.GetProperty("data").GetProperty("node").GetString() ?? string.Empty;
                            _currentNodeId = node;

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
                            _imageService.Progress.CurrentNodeId = node;
                            _imageService.Progress.State.Job = FormatNodeLabel(node);
                            _progressService.Update(id, value);
                            _progressService.NotifyProgressChanged();
                        }

                        if (type == "execution_success" || type == "execution_error" || type == "execution_interrupted")
                        {
                            if (!Guid.TryParse(doc.RootElement.GetProperty("data").GetProperty("prompt_id").GetString(), out _promptId))
                            {
                                _logger.LogWarning("ComfyUI {EventType} event did not include a valid prompt_id", type);
                                continue;
                            }

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
                            _currentNodeId = string.Empty;
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
                    if (!TryReadPreviewImage(fullBytes, out var imageBytes, out var mimeType))
                    {
                        _logger.LogDebug("Ignoring unsupported ComfyUI binary websocket message ({ByteCount} bytes)", fullBytes.Length);
                        continue;
                    }

                    var base64 = Convert.ToBase64String(imageBytes);
                    _imageService.Progress.CurrentImage = base64;
                    _imageService.Progress.CurrentImageMimeType = mimeType;
                    _imageService.Progress.CurrentPreviewNodeId = _currentNodeId;
                    _progressService.NotifyProgressChanged();
                }
            }
        }

        private static string FormatNodeLabel(string nodeId)
        {
            if (string.IsNullOrWhiteSpace(nodeId))
            {
                return "Running";
            }

            return nodeId switch
            {
                "stage1_sampler" => "Running: Stage 1 sampler",
                "stage2_sampler" => "Running: Stage 2 sampler",
                _ => $"Running node: {nodeId}"
            };
        }

        private static bool TryReadPreviewImage(byte[] bytes, out byte[] imageBytes, out string mimeType)
        {
            imageBytes = Array.Empty<byte>();
            mimeType = "image/png";

            if (bytes.Length <= ComfyBinaryHeaderLength)
            {
                return false;
            }

            var eventType = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(0, 4));
            if (eventType != PreviewImageEventType)
            {
                return false;
            }

            var imageType = BinaryPrimitives.ReadUInt32BigEndian(bytes.AsSpan(4, 4));
            mimeType = imageType switch
            {
                JpegImageType => "image/jpeg",
                PngImageType => "image/png",
                _ => DetectImageMimeType(bytes.AsSpan(ComfyBinaryHeaderLength))
            };

            imageBytes = bytes[ComfyBinaryHeaderLength..];
            return imageBytes.Length > 0;
        }

        private static string DetectImageMimeType(ReadOnlySpan<byte> imageBytes)
        {
            if (imageBytes.Length >= 3
                && imageBytes[0] == 0xFF
                && imageBytes[1] == 0xD8
                && imageBytes[2] == 0xFF)
            {
                return "image/jpeg";
            }

            if (imageBytes.Length >= 8
                && imageBytes[0] == 0x89
                && imageBytes[1] == 0x50
                && imageBytes[2] == 0x4E
                && imageBytes[3] == 0x47)
            {
                return "image/png";
            }

            return "image/png";
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
