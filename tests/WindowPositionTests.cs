using System;
using System.Globalization;
using System.Threading;
using PCBSCalculatorHelper;

internal static class WindowPositionTests
{
    private static int assertions;
    private static void Check(bool pass, string message)
    { assertions++; if (!pass) throw new Exception(message); }
    private static bool Near(float a, float b) { return Math.Abs(a - b) < 0.02f; }

    public static int Main()
    {
        WindowPoint normalized = WindowPositionMath.Normalize(new WindowPoint(700, 220), 1920, 1080);
        string saved = WindowPositionMath.Serialize(normalized);
        WindowPoint reloaded;
        Check(WindowPositionMath.TryParse(saved, out reloaded), "Saved position must load after restart.");
        WindowPoint same = WindowPositionMath.Resolve(reloaded, 1920, 1080, 560, 650);
        Check(Near(same.X, 700) && Near(same.Y, 220), "Restart must preserve the position on the same screen.");
        WindowPoint resized = WindowPositionMath.Resolve(reloaded, 1280, 720, 373, 434);
        Check(Near(resized.X, 700 * 1280f / 1920) && Near(resized.Y, 220 * 720f / 1080), "Resolution change should retain relative placement.");
        WindowPoint taller = WindowPositionMath.Resolve(reloaded, 1920, 1080, 560, 800);
        Check(Near(taller.X, 700) && Near(taller.Y, 220), "Changing panel size must not shift a position that still fits.");
        WindowPoint edge = WindowPositionMath.Resolve(new WindowPoint(1, 1), 1280, 720, 424, 650);
        Check(edge.X == 856 && edge.Y == 70, "A saved edge position must remain entirely on screen.");
        WindowPoint negative = WindowPositionMath.Clamp(new WindowPoint(-30, -50), 1920, 1080, 560, 650);
        Check(negative.X == 0 && negative.Y == 0, "Dragging past the top/left edges must clamp.");
        WindowPoint oversized = WindowPositionMath.Clamp(new WindowPoint(500, 300), 320, 200, 560, 650);
        Check(oversized.X == 0 && oversized.Y == 0, "Oversized windows must keep their header on screen.");
        Check(!WindowPositionMath.TryParse("default", out reloaded), "Default placement is distinct from a saved position.");
        Check(!WindowPositionMath.TryParse("NaN,0.5", out reloaded), "NaN cannot be loaded.");
        Check(!WindowPositionMath.TryParse("Infinity,0.5", out reloaded), "Infinity cannot be loaded.");
        Check(!WindowPositionMath.TryParse("-1,2", out reloaded), "Invalid normalized positions cannot be loaded.");
        Thread.CurrentThread.CurrentCulture = new CultureInfo("ru-RU");
        Check(WindowPositionMath.Serialize(normalized) == saved && WindowPositionMath.TryParse(saved, out reloaded),
            "Positions must survive locale changes without comma-decimal ambiguity.");
        Console.WriteLine("Window positions: " + assertions + " assertions passed.");
        return 0;
    }
}
