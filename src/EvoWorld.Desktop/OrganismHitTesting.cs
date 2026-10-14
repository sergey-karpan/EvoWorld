using EvoWorld.Core;

namespace EvoWorld.Desktop;

internal static class OrganismHitTesting
{
    // view.Scale is already in physical pixels per world unit. DIP-to-pixel DPI
    // conversion belongs only to the pointer; multiplying Scale by DPI again is incorrect.
    internal static int? Find(WorldState world, double pixelWidth, double pixelHeight,
        double dipX, double dipY, double dpiX = 1, double dpiY = 1)
    {
        if (!double.IsFinite(dipX) || !double.IsFinite(dipY) || !double.IsFinite(dpiX)
            || !double.IsFinite(dpiY) || dpiX <= 0 || dpiY <= 0
            || !ViewportTransform.TryCreate(world.WorldWidth, world.WorldHeight, pixelWidth, pixelHeight, out var view)) return null;
        var x = dipX * dpiX;
        var y = dipY * dpiY;
        if (!double.IsFinite(x) || !double.IsFinite(y) || x < view.OffsetX || y < view.OffsetY
            || x >= view.OffsetX + world.WorldWidth * view.Scale || y >= view.OffsetY + world.WorldHeight * view.Scale) return null;
        var radius = Math.Max(8, 5 * view.Scale);
        double closest = double.PositiveInfinity;
        int? selected = null;
        foreach (var organism in world.Organisms)
        {
            var dx = x - (view.OffsetX + organism.X * view.Scale);
            var dy = y - (view.OffsetY + organism.Y * view.Scale);
            var distance = Math.Sqrt(dx * dx + dy * dy);
            if (distance <= radius && (distance < closest || distance == closest && (selected is null || organism.Id < selected)))
            {
                closest = distance;
                selected = organism.Id;
            }
        }
        return selected;
    }
}
