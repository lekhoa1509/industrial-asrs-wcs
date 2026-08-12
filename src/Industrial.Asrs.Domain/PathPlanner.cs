namespace Industrial.Asrs.Domain;

public sealed class GridPathPlanner
{
    public IReadOnlyList<Position> FindShortestPath(Position start, Position destination)
    {
        List<Position> path = [start];
        Position current = start;

        while (current.Level > 0 && current.Rail != destination.Rail)
        {
            current = current with { Level = current.Level - 1 };
            path.Add(current);
        }

        while (current.Rail != destination.Rail)
        {
            int rail = current.Rail + Math.Sign(destination.Rail - current.Rail);
            current = new Position(ZoneForRail(rail), rail, 0);
            path.Add(current);
        }

        while (current != destination)
        {
            current = current with
            {
                Zone = destination.Zone,
                Level = current.Level + Math.Sign(destination.Level - current.Level)
            };
            path.Add(current);
        }
        return path;
    }

    public static Zone ZoneForRail(int rail) => rail switch
    {
        >= 1 and <= 5 => Zone.A,
        6 => Zone.Charging,
        >= 7 and <= 12 => Zone.B,
        _ => throw new ArgumentOutOfRangeException(nameof(rail), "Rail must be between 1 and 12.")
    };
}
