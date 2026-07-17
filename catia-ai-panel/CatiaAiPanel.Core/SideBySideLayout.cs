namespace CatiaAiPanel.Core;

public readonly record struct PixelRectangle(int Left, int Top, int Right, int Bottom)
{
    public int Width => Math.Max(0, Right - Left);
    public int Height => Math.Max(0, Bottom - Top);
}

public readonly record struct SideBySideWindowLayout(PixelRectangle Catia, PixelRectangle Panel);

public static class SideBySideLayout
{
    public static SideBySideWindowLayout Calculate(
        PixelRectangle workArea,
        int desiredPanelWidth,
        int minimumPanelWidth,
        int maximumPanelWidth,
        int minimumCatiaWidth)
    {
        if (workArea.Width <= 1 || workArea.Height <= 1)
            return new(workArea, workArea);

        var reservedCatiaWidth = Math.Min(minimumCatiaWidth, workArea.Width * 2 / 3);
        var availableForPanel = Math.Max(1, workArea.Width - reservedCatiaWidth);
        var maximum = Math.Min(maximumPanelWidth, availableForPanel);
        var minimum = Math.Min(minimumPanelWidth, maximum);
        var panelWidth = Math.Clamp(desiredPanelWidth, minimum, maximum);
        var split = workArea.Right - panelWidth;
        return new(
            new(workArea.Left, workArea.Top, split, workArea.Bottom),
            new(split, workArea.Top, workArea.Right, workArea.Bottom));
    }
}
