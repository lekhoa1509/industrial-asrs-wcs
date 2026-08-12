namespace Industrial.Asrs.Domain;

public sealed class GridPathPlanner
{
    public IReadOnlyList<Position> FindShortestPath(Position start, Position destination)
    {
        List<Position> path = [start];
        Position current = start;
        while (current != destination)
        {
            current = current.Zone != destination.Zone
                ? current with { Zone = destination.Zone }
                : current.Aisle != destination.Aisle
                    ? current with { Aisle = current.Aisle + Math.Sign(destination.Aisle - current.Aisle) }
                    : current with { Level = current.Level + Math.Sign(destination.Level - current.Level) };
            path.Add(current);
        }
        return path;
    }
}
