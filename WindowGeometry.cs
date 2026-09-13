namespace FastWindowResizer;

internal static class WindowGeometry
{
    // Rectangles are physical pixels; only the small-window threshold is expressed in DIP.
    internal static Rectangle Fit(Rectangle bounds, Rectangle work, bool resizable, uint dpi)
    {
        int width = Math.Max(1, bounds.Width), height = Math.Max(1, bounds.Height);
        if (resizable)
        {
            double scale = Math.Max(96, dpi) / 96.0;
            if (width < 240 * scale || height < 160 * scale)
            {
                width = Math.Max(1, (int)(work.Width * .8));
                height = Math.Max(1, (int)(work.Height * .8));
            }
            width = Math.Min(width, Math.Max(1, work.Width));
            height = Math.Min(height, Math.Max(1, work.Height));
        }
        return new Rectangle(work.Left + Math.Max(0, (work.Width - width) / 2),
            work.Top + Math.Max(0, (work.Height - height) / 2), width, height);
    }
}
