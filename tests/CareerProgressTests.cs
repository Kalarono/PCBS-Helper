using System;
using PCBSCalculatorHelper;

internal static class CareerProgressTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static int Main()
    {
        int[] thresholds = { 100, 250, 1000 };
        CareerProgressValue first = CareerProgress.Calculate(50, thresholds);
        Check(first.Level == 1 && first.Percent == 50, "First-level XP progress.");
        CareerProgressValue boundary = CareerProgress.Calculate(100, thresholds);
        Check(boundary.Level == 2 && boundary.Percent == 0, "Exact XP boundary begins the next level.");
        CareerProgressValue middle = CareerProgress.Calculate(175, thresholds);
        Check(middle.Level == 2 && middle.Percent == 50, "Progress uses the current game's level thresholds.");
        CareerProgressValue max = CareerProgress.Calculate(Int32.MaxValue, thresholds);
        Check(max.Level == 3 && max.Percent == 100, "Cheated XP saturates at the final level and 100 percent.");
        Check(CareerProgress.Calculate(-100, thresholds).Percent == 0, "Negative XP is clamped safely.");
        Check(CareerProgress.Calculate(20, new int[0]).Level == 0, "Unavailable progression data has no invented level.");
        Check(CareerProgress.Calculate(100, new int[] { 100, 100 }).Percent == 0, "Duplicate thresholds must not divide by zero.");
        Console.WriteLine("Career progress: " + checks + " assertions passed.");
        return 0;
    }
}
