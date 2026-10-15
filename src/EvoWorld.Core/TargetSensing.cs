namespace EvoWorld.Core;

// Tick-local value only: no retained identity, history, or heap allocation.
internal readonly record struct SensedTarget(TargetKind Kind, double X, double Y);

internal static class TargetSensing
{
    internal static SensedTarget Resolve(Organism organism, IReadOnlyList<Food> food, bool[] consumed,
        IReadOnlyList<Organism> previousOrganisms)
        => Resolve(organism, food, consumed, previousOrganisms, out _);

    internal static SensedTarget Resolve(Organism organism, IReadOnlyList<Food> food, bool[] consumed,
        IReadOnlyList<Organism> previousOrganisms, out int? targetId)
    {
        targetId = null;
        if (organism.SensingRadius == 0) return default;
        var radius = FoodDistance.FromLength(organism.SensingRadius);
        if (organism.Species == Species.Herbivore)
        {
            var selected = FoodSeeking.FindNearest(organism.X, organism.Y, food, consumed, radius);
            if (selected < 0) return default;
            targetId = food[selected].Id;
            return new(TargetKind.Food, food[selected].X, food[selected].Y);
        }

        var nearest = -1;
        FoodDistance nearestDistance = default;
        for (var index = 0; index < previousOrganisms.Count; index++)
        {
            var prey = previousOrganisms[index];
            if (prey.Species != Species.Herbivore) continue;
            // Snapshot membership is eligibility. Do not inspect current-tick drain,
            // updated positions, or survivors, even if this prey dies before acting.
            var distance = FoodDistance.Between(organism.X, organism.Y, prey.X, prey.Y);
            if (distance.CompareTo(radius) > 0) continue;
            if (nearest < 0 || distance.CompareTo(nearestDistance) < 0
                || distance.CompareTo(nearestDistance) == 0 && prey.Id < previousOrganisms[nearest].Id)
            {
                nearest = index;
                nearestDistance = distance;
            }
        }
        if (nearest < 0) return default;
        targetId = previousOrganisms[nearest].Id;
        return new(TargetKind.Prey, previousOrganisms[nearest].X, previousOrganisms[nearest].Y);
    }
}
