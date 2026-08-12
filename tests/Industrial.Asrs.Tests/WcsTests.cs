using Industrial.Asrs.Domain;
using Industrial.Asrs.Infrastructure;

namespace Industrial.Asrs.Tests;

public sealed class WcsTests
{
    [Fact]
    public void PathPlanner_ReturnsShortestGridPath()
    {
        GridPathPlanner planner = new();
        IReadOnlyList<Position> path = planner.FindShortestPath(new(Zone.A, 1, 1), new(Zone.A, 3, 2));
        Assert.Equal(6, path.Count);
        Assert.Equal(new Position(Zone.A, 3, 2), path[^1]);
        Assert.Contains(new Position(Zone.Charging, 6, 0), planner.FindShortestPath(new(Zone.A, 5, 2), new(Zone.B, 7, 2)));
    }

    [Fact]
    public async Task Wcs_SelectsNearestIdleShuttle()
    {
        SimulatedShuttle near = new("SH-NEAR", new(Zone.A, 1, 0), faultRate: 0, stepDelay: TimeSpan.Zero);
        SimulatedShuttle far = new("SH-FAR", new(Zone.B, 12, 0), faultRate: 0, stepDelay: TimeSpan.Zero);
        WarehouseControlSystem wcs = new([near, far], new GridPathPlanner()); await wcs.StartAsync();
        wcs.Enqueue(new("ORDER-1", new(Zone.A, 2, 1), new(Zone.A, 5, 2)));
        await wcs.ProcessNextAsync();
        WcsSnapshot snapshot = await wcs.SnapshotAsync();
        Assert.Equal(1, snapshot.Completed);
        Assert.Contains(snapshot.Events, x => x.Message.Contains("SH-NEAR selected"));
    }
}
