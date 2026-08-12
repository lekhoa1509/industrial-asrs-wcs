namespace Industrial.Asrs.Domain;

public enum DeviceState { Offline, Idle, Moving, Faulted }
public enum Zone { A, Charging, B }
public readonly record struct Position(Zone Zone, int Rail, int Level)
{
    public int DistanceTo(Position other) => Math.Abs(Rail - other.Rail) + Math.Abs(Level - other.Level);
}
public sealed record DeviceSnapshot(string DeviceId, string Driver, DeviceState State, Position Position, string? ActiveOrderId, string? Error);
public interface IAutomationDevice
{
    string DeviceId { get; }
    Task ConnectAsync(CancellationToken cancellationToken = default);
    Task<DeviceSnapshot> GetSnapshotAsync(CancellationToken cancellationToken = default);
    Task ResetAsync(CancellationToken cancellationToken = default);
}
public interface IShuttleDevice : IAutomationDevice
{
    Task MoveAsync(string orderId, IReadOnlyList<Position> path, CancellationToken cancellationToken = default);
}
public interface IConveyorDevice : IAutomationDevice
{
    Task TransferAsync(string orderId, string destinationPort, CancellationToken cancellationToken = default);
}
public sealed record TransportOrder(string OrderId, Position Source, Position Destination);
public sealed record DispatchResult(string OrderId, string ShuttleId, int EmptyTravelDistance, IReadOnlyList<Position> Path);
