using System.Collections.Concurrent;

namespace Industrial.Asrs.Domain;

public sealed record WcsEvent(DateTimeOffset Timestamp, string Message);
public sealed record WcsSnapshot(IReadOnlyList<DeviceSnapshot> Devices, IReadOnlyList<TransportOrder> Queue, IReadOnlyList<WcsEvent> Events, int Completed, int Failed, IReadOnlyList<Position> ActivePath);

public sealed class WarehouseControlSystem(IEnumerable<IShuttleDevice> shuttles, GridPathPlanner planner)
{
    private readonly IReadOnlyList<IShuttleDevice> _shuttles = shuttles.ToArray();
    private readonly ConcurrentQueue<TransportOrder> _queue = new();
    private readonly ConcurrentQueue<WcsEvent> _events = new();
    private readonly ConcurrentDictionary<Zone, SemaphoreSlim> _zoneReservations = new();
    private IReadOnlyList<Position> _activePath = [];
    private int _completed; private int _failed;

    public async Task StartAsync(CancellationToken token = default)
    { await Task.WhenAll(_shuttles.Select(x => x.ConnectAsync(token))); Log("WCS online. Simulator mode selected."); }

    public void Enqueue(TransportOrder order) { _queue.Enqueue(order); Log($"Order {order.OrderId} queued."); }

    public async Task ProcessNextAsync(CancellationToken token = default)
    {
        if (!_queue.TryDequeue(out TransportOrder? order)) return;
        DeviceSnapshot[] snapshots = await Task.WhenAll(_shuttles.Select(x => x.GetSnapshotAsync(token)));
        DeviceSnapshot selected = snapshots.Where(x => x.State == DeviceState.Idle).OrderBy(x => x.Position.DistanceTo(order.Source)).ThenBy(x => x.DeviceId).FirstOrDefault()
            ?? throw new InvalidOperationException("No idle shuttle available.");
        IShuttleDevice shuttle = _shuttles.Single(x => x.DeviceId == selected.DeviceId);
        IReadOnlyList<Position> pickup = planner.FindShortestPath(selected.Position, order.Source);
        IReadOnlyList<Position> delivery = planner.FindShortestPath(order.Source, order.Destination);
        _activePath = pickup.Concat(delivery.Skip(1)).ToArray();
        Log($"{selected.DeviceId} selected for {order.OrderId}; empty travel {selected.Position.DistanceTo(order.Source)}.");
        SemaphoreSlim reservation = _zoneReservations.GetOrAdd(order.Destination.Zone, static _ => new(1, 1));
        await reservation.WaitAsync(token);
        try
        {
            await shuttle.MoveAsync(order.OrderId, _activePath, token); Interlocked.Increment(ref _completed); Log($"Order {order.OrderId} completed by {shuttle.DeviceId}.");
        }
        catch (Exception ex) { Interlocked.Increment(ref _failed); Log($"Order {order.OrderId} failed: {ex.Message}"); }
        finally { _activePath = []; reservation.Release(); }
    }

    public async Task ResetAsync(CancellationToken token = default) { await Task.WhenAll(_shuttles.Select(x => x.ResetAsync(token))); Log("Device faults reset."); }
    public async Task<WcsSnapshot> SnapshotAsync(CancellationToken token = default) => new(await Task.WhenAll(_shuttles.Select(x => x.GetSnapshotAsync(token))), _queue.ToArray(), _events.Reverse().Take(12).ToArray(), _completed, _failed, _activePath);
    private void Log(string message) => _events.Enqueue(new(DateTimeOffset.UtcNow, message));
}
