using EvoWorld.Core;
using SkiaSharp;

namespace EvoWorld.Desktop;

internal sealed class WorldRenderer : IDisposable
{
    private readonly SKPaint _world = new() { Color = new SKColor(32, 43, 56) };
    private readonly SKPaint _border = new() { Color = new SKColor(104, 125, 148), Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
    private readonly SKPaint _food = new() { Color = new SKColor(115, 219, 121), IsAntialias = true };
    private readonly SKPaint _organism = new() { Color = new SKColor(65, 201, 235), IsAntialias = true };
    private readonly SKPaint _heading = new() { Color = new SKColor(222, 247, 255), StrokeWidth = 1.5f, IsAntialias = true };
    private readonly SKPaint _predatorOutline = new() { Color = new SKColor(213, 104, 255), Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _selection = new() { Color = new SKColor(255, 209, 102), Style = SKPaintStyle.Stroke, IsAntialias = true };
    private readonly SKPaint _trail = new() { Color = new SKColor(255, 209, 102, 100), Style = SKPaintStyle.Stroke, IsAntialias = true };

    public bool Draw(SKCanvas canvas, int width, int height, WorldState world,
        PopulationStatistics statistics = default, ColorTrait trait = ColorTrait.None, OrganismInspection? inspection = null)
    {
        canvas.Clear(new SKColor(21, 27, 36));
        if (!ViewportTransform.TryCreate(world.WorldWidth, world.WorldHeight, width, height, out var view))
            return false;
        var originalMatrix = canvas.TotalMatrix;
        canvas.Save();
        try
        {
            canvas.Translate(view.OffsetX, view.OffsetY);
            canvas.Scale(view.Scale);
            var bounds = SKRect.Create((float)world.WorldWidth, (float)world.WorldHeight);
            canvas.DrawRect(bounds, _world);
            canvas.DrawRect(bounds, _border);
            canvas.ClipRect(bounds);
            if (inspection is { Count: > 1 })
            {
                // Keep strokes in pixels without forming an overflowing inverse scale.
                // The existing world clip remains in device space when the matrix changes.
                canvas.Save();
                try
                {
                    canvas.SetMatrix(originalMatrix);
                    _trail.StrokeWidth = 1.5f;
                    for (var i = 1; i < inspection.Count; i++)
                        canvas.DrawLine((float)(view.OffsetX + inspection[i - 1].X * view.Scale),
                            (float)(view.OffsetY + inspection[i - 1].Y * view.Scale),
                            (float)(view.OffsetX + inspection[i].X * view.Scale),
                            (float)(view.OffsetY + inspection[i].Y * view.Scale), _trail);
                }
                finally { canvas.Restore(); }
            }
            // Stable draw order: background, boundary, food, organisms, headings.
            foreach (var food in world.FoodItems)
                canvas.DrawCircle((float)food.X, (float)food.Y, 3, _food);
            var (min, max) = OrganismColoring.Bounds(world, statistics, trait);
            foreach (var organism in world.Organisms)
            {
                _organism.Color = OrganismColoring.Color(OrganismColoring.Value(organism, trait), min, max, trait);
                canvas.DrawCircle((float)organism.X, (float)organism.Y, 5, _organism);
            }
            // Physical-pixel strokes avoid inverse-scale overflow. Circle geometry,
            // clip and hit testing stay unchanged; species is independent of trait fill.
            canvas.Save();
            try
            {
                canvas.SetMatrix(originalMatrix);
                _predatorOutline.StrokeWidth = Math.Clamp(1.5f * view.Scale, 1.5f, 3f);
                foreach (var organism in world.Organisms)
                    if (organism.Species == Species.Predator)
                        canvas.DrawCircle((float)(view.OffsetX + organism.X * view.Scale),
                            (float)(view.OffsetY + organism.Y * view.Scale), 5 * view.Scale, _predatorOutline);
            }
            finally { canvas.Restore(); }
            foreach (var organism in world.Organisms)
                canvas.DrawLine((float)organism.X, (float)organism.Y,
                    (float)(organism.X + 10 * Math.Cos(organism.HeadingRadians)),
                    (float)(organism.Y + 10 * Math.Sin(organism.HeadingRadians)), _heading);
            if (inspection is { IsAlive: true, SelectedId: int selected })
            {
                var organism = world.Organisms.FirstOrDefault(o => o.Id == selected);
                if (organism is not null)
                {
                    canvas.Save();
                    try
                    {
                        canvas.SetMatrix(originalMatrix);
                        _selection.StrokeWidth = 2;
                        canvas.DrawCircle((float)(view.OffsetX + organism.X * view.Scale),
                            (float)(view.OffsetY + organism.Y * view.Scale), 5 * view.Scale + 4, _selection);
                    }
                    finally { canvas.Restore(); }
                }
            }
            return true;
        }
        finally { canvas.Restore(); }
    }

    public void Dispose()
    {
        _world.Dispose();
        _border.Dispose();
        _food.Dispose();
        _organism.Dispose();
        _heading.Dispose();
        _predatorOutline.Dispose();
        _selection.Dispose();
        _trail.Dispose();
    }
}
