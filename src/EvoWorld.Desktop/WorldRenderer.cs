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

    public bool Draw(SKCanvas canvas, int width, int height, WorldState world)
    {
        canvas.Clear(new SKColor(21, 27, 36));
        if (!ViewportTransform.TryCreate(world.WorldWidth, world.WorldHeight, width, height, out var view))
            return false;
        canvas.Save();
        try
        {
            canvas.Translate(view.OffsetX, view.OffsetY);
            canvas.Scale(view.Scale);
            var bounds = SKRect.Create((float)world.WorldWidth, (float)world.WorldHeight);
            canvas.DrawRect(bounds, _world);
            canvas.DrawRect(bounds, _border);
            canvas.ClipRect(bounds);
            // Stable draw order: background, boundary, food, organisms, headings.
            foreach (var food in world.FoodItems)
                canvas.DrawCircle((float)food.X, (float)food.Y, 3, _food);
            foreach (var organism in world.Organisms)
                canvas.DrawCircle((float)organism.X, (float)organism.Y, 5, _organism);
            foreach (var organism in world.Organisms)
                canvas.DrawLine((float)organism.X, (float)organism.Y,
                    (float)(organism.X + 10 * Math.Cos(organism.HeadingRadians)),
                    (float)(organism.Y + 10 * Math.Sin(organism.HeadingRadians)), _heading);
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
    }
}
