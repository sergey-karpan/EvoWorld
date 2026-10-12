namespace EvoWorld.Desktop;

// Pure canvas math; linked into Tests without a WPF dependency.
internal readonly record struct ViewportTransform(float Scale, float OffsetX, float OffsetY)
{
    public static bool TryCreate(double width, double height, double viewportWidth,
        double viewportHeight, out ViewportTransform transform)
    {
        transform = default;
        if (!double.IsFinite(width) || width <= 0 || !double.IsFinite(height) || height <= 0
            || !double.IsFinite(viewportWidth) || viewportWidth <= 0
            || !double.IsFinite(viewportHeight) || viewportHeight <= 0)
            return false;
        var scale = Math.Min(viewportWidth / width, viewportHeight / height);
        var offsetX = (viewportWidth - width * scale) / 2;
        var offsetY = (viewportHeight - height * scale) / 2;
        // Skia uses floats: reject unrepresentable inputs or a collapsed axis.
        if (!double.IsFinite(scale) || (float)scale <= 0 || !float.IsFinite((float)(scale * 10))
            || !float.IsFinite((float)width) || !float.IsFinite((float)height)
            || (float)width <= 0 || (float)height <= 0
            || width * scale < 1 || height * scale < 1
            || !float.IsFinite((float)offsetX) || !float.IsFinite((float)offsetY))
            return false;
        transform = new((float)scale, (float)offsetX, (float)offsetY);
        return true;
    }
}
