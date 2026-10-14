namespace EvoWorld.Core;

internal static class FoodSeeking
{
    internal static double Steer(Organism organism, IReadOnlyList<Food> foodItems, bool[] consumed, double timestep)
    {
        if (organism.SensingRadius == 0 || organism.SteeringRate == 0) return organism.HeadingRadians;
        var index = FindNearest(organism.X, organism.Y, foodItems, consumed, FoodDistance.FromLength(organism.SensingRadius));
        if (index < 0) return organism.HeadingRadians;
        var food = foodItems[index];
        return Steer(organism, new SensedTarget(TargetKind.Food, food.X, food.Y), timestep);
    }

    internal static double Steer(Organism organism, SensedTarget sensedTarget, double timestep)
    {
        if (organism.SensingRadius == 0 || organism.SteeringRate == 0 || sensedTarget.Kind == TargetKind.None)
            return organism.HeadingRadians;
        var dx = sensedTarget.X - organism.X;
        var dy = sensedTarget.Y - organism.Y;
        if (dx == 0 && dy == 0) return organism.HeadingRadians;
        var target = Normalize(Math.Atan2(dy, dx));
        var delta = target - organism.HeadingRadians;
        if (delta > Math.PI) delta -= 2 * Math.PI;
        if (delta <= -Math.PI) delta += 2 * Math.PI;
        if (delta == 0) return organism.HeadingRadians;
        // For timestep <=1 multiplication cannot overflow. For larger steps,
        // compare against pi/timestep first; the quotient is finite and positive.
        var limit = timestep > 1 && organism.SteeringRate > Math.PI / timestep
            ? Math.PI : Math.Min(organism.SteeringRate * timestep, Math.PI);
        var rotation = Math.Clamp(delta, -limit, limit);
        return rotation == 0 ? organism.HeadingRadians : Normalize(organism.HeadingRadians + rotation);
    }

    internal static int FindNearest(double x, double y, IReadOnlyList<Food> foodItems, bool[] consumed, FoodDistance radius)
    {
        var selected = -1;
        var closest = default(FoodDistance);
        for (var index = 0; index < foodItems.Count; index++)
        {
            if (consumed[index]) continue;
            var food = foodItems[index];
            var distance = FoodDistance.Between(x, y, food.X, food.Y);
            if (distance.CompareTo(radius) > 0) continue;
            if (selected < 0 || distance.CompareTo(closest) < 0
                || (distance.CompareTo(closest) == 0 && food.Id < foodItems[selected].Id))
            {
                selected = index;
                closest = distance;
            }
        }
        return selected;
    }

    internal static double Normalize(double heading)
    {
        var fullTurn = 2.0 * Math.PI;
        var result = heading % fullTurn;
        if (result < 0.0) result += fullTurn;
        return result < fullTurn ? result : Math.BitDecrement(fullTurn);
    }
}
