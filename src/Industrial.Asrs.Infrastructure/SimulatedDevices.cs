using Industrial.Asrs.Domain;

namespace Industrial.Asrs.Infrastructure;

public sealed class SimulatedShuttle : IShuttleDevice
{
    private readonly TimeProvider _clock; private readonly Random _random; private readonly double _faultRate; private readonly TimeSpan _stepDelay; private readonly SemaphoreSlim _gate = new(1, 1);
    private DeviceSnapshot _snapshot;
    public SimulatedShuttle(string id, Position initial, TimeProvider? clock = null, int seed = 1, double faultRate = .03, TimeSpan? stepDelay = null)
    { DeviceId = id; _clock = clock ?? TimeProvider.System; _random = new(seed); _faultRate = faultRate; _stepDelay = stepDelay ?? TimeSpan.FromMilliseconds(320); _snapshot = new(id, "Simulator", DeviceState.Offline, initial, null, null); }
    public string DeviceId { get; }
    public Task ConnectAsync(CancellationToken cancellationToken = default) { _snapshot = _snapshot with { State = DeviceState.Idle }; return Task.CompletedTask; }
    public Task<DeviceSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(_snapshot);
    public async Task MoveAsync(string orderId, IReadOnlyList<Position> path, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (_snapshot.State == DeviceState.Faulted) throw new InvalidOperationException($"{DeviceId} requires reset.");
            _snapshot = _snapshot with { State = DeviceState.Moving, ActiveOrderId = orderId, Error = null };
            foreach (Position step in path.Skip(1))
            {
                await Task.Delay(_stepDelay, _clock, cancellationToken);
                if (_random.NextDouble() < _faultRate) { string error = $"SIM-{_random.Next(100, 999)} drive fault"; _snapshot = _snapshot with { State = DeviceState.Faulted, Error = error }; throw new IOException(error); }
                _snapshot = _snapshot with { Position = step };
            }
            _snapshot = _snapshot with { State = DeviceState.Idle, ActiveOrderId = null };
        }
        finally { _gate.Release(); }
    }
    public Task ResetAsync(CancellationToken cancellationToken = default) { _snapshot = _snapshot with { State = DeviceState.Idle, ActiveOrderId = null, Error = null }; return Task.CompletedTask; }
}

public sealed class SimulatedConveyor(string id, TimeProvider? clock = null) : IConveyorDevice
{
    private readonly TimeProvider _clock = clock ?? TimeProvider.System; private DeviceSnapshot _snapshot = new(id, "Simulator", DeviceState.Offline, default, null, null);
    public string DeviceId { get; } = id;
    public Task ConnectAsync(CancellationToken cancellationToken = default) { _snapshot = _snapshot with { State = DeviceState.Idle }; return Task.CompletedTask; }
    public Task<DeviceSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default) => Task.FromResult(_snapshot);
    public async Task TransferAsync(string orderId, string destinationPort, CancellationToken cancellationToken = default) { _snapshot = _snapshot with { State = DeviceState.Moving, ActiveOrderId = orderId }; await Task.Delay(TimeSpan.FromMilliseconds(500), _clock, cancellationToken); _snapshot = _snapshot with { State = DeviceState.Idle, ActiveOrderId = null }; }
    public Task ResetAsync(CancellationToken cancellationToken = default) { _snapshot = _snapshot with { State = DeviceState.Idle, Error = null }; return Task.CompletedTask; }
}
