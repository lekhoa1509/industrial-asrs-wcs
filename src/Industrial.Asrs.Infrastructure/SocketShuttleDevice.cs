using System.Net.Sockets;
using System.Text.Json;
using Industrial.Asrs.Domain;

namespace Industrial.Asrs.Infrastructure;

/// <summary>Real-device adapter for a line-delimited JSON PLC/device gateway.</summary>
public sealed class SocketShuttleDevice(string id, string host, int port) : IShuttleDevice, IAsyncDisposable
{
    private TcpClient? _client; private StreamReader? _reader; private StreamWriter? _writer;
    public string DeviceId { get; } = id;
    public async Task ConnectAsync(CancellationToken cancellationToken = default) { _client = new TcpClient(); await _client.ConnectAsync(host, port, cancellationToken); NetworkStream stream = _client.GetStream(); _reader = new(stream); _writer = new(stream) { AutoFlush = true }; }
    public Task<DeviceSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => RequestAsync<DeviceSnapshot>(new { command = "snapshot", deviceId = DeviceId }, cancellationToken);
    public Task MoveAsync(string orderId, IReadOnlyList<Position> path, CancellationToken cancellationToken = default) => RequestAsync<object>(new { command = "move", deviceId = DeviceId, orderId, path }, cancellationToken);
    public Task ResetAsync(CancellationToken cancellationToken = default) => RequestAsync<object>(new { command = "reset", deviceId = DeviceId }, cancellationToken);
    private async Task<T> RequestAsync<T>(object request, CancellationToken token) { if (_reader is null || _writer is null) throw new InvalidOperationException("Device is not connected."); await _writer.WriteLineAsync(JsonSerializer.Serialize(request)); string json = await _reader.ReadLineAsync(token) ?? throw new IOException("Gateway disconnected."); return JsonSerializer.Deserialize<T>(json, new JsonSerializerOptions(JsonSerializerDefaults.Web))!; }
    public async ValueTask DisposeAsync() { if (_writer is not null) await _writer.DisposeAsync(); _reader?.Dispose(); _client?.Dispose(); }
}
