using System;
using PCBSCalculatorHelper;

internal static class EmailButtonLayoutTests
{
    private static int checks;
    private static void Check(bool value, string message)
    { checks++; if (!value) throw new Exception(message); }
    private static void CheckBounds(EmailButtonLayout plan, int count, float left, float right)
    {
        Check(plan.Width > 0, "Every button has positive width.");
        Check(plan.Left >= left, "The row stays within its left boundary.");
        Check(plan.Left + count * plan.Width + (count - 1) * EmailButtonLayout.Gap <= right + 0.001f,
            "Buttons and gaps fit within the footer.");
    }
    private static void Main()
    {
        EmailButtonLayout single = EmailButtonLayout.Calculate(0, 600, 480, 120, 128, 2, 1);
        Check(!single.CompactNative && single.Width == 120 && single.Left == 352,
            "One helper keeps the existing position beside the positive action.");
        CheckBounds(single, 1, 128, 472);

        EmailButtonLayout both = EmailButtonLayout.Calculate(0, 600, 480, 120, 128, 2, 2);
        Check(!both.CompactNative && both.Width == 120 && both.Left == 224,
            "Two helpers occupy separate slots before the positive action.");
        CheckBounds(both, 2, 128, 472);

        EmailButtonLayout tight = EmailButtonLayout.Calculate(0, 400, 280, 120, 128, 2, 2);
        Check(tight.CompactNative, "A crowded footer shares space with native actions.");
        CheckBounds(tight, 4, 0, 400);
        Check(tight.Width >= 90, "The regular compact row keeps readable button widths.");

        EmailButtonLayout dlc = EmailButtonLayout.Calculate(-300, 300, 180, 120, 172, 3, 2);
        Check(dlc.CompactNative, "Both helpers coexist with all three DLC actions.");
        CheckBounds(dlc, 5, -300, 300);
        Check(dlc.Width >= 90, "DLC labels have enough room without covering the status row.");

        EmailButtonLayout hiddenPositive = EmailButtonLayout.Calculate(0, 400, 280, 120, 272, 1, 2);
        Check(hiddenPositive.CompactNative, "A hidden positive action need not consume a slot.");
        CheckBounds(hiddenPositive, 3, 0, 400);

        foreach (int nativeCount in new int[] { 0, 1, 2, 3 })
            foreach (int helperCount in new int[] { 1, 2 })
            {
                EmailButtonLayout compact = EmailButtonLayout.Calculate(-250, 250, 130, 120, 122,
                    nativeCount, helperCount);
                Check(compact.CompactNative, "Crowded layouts use one shared row for any action count.");
                CheckBounds(compact, nativeCount + helperCount, -250, 250);
            }
        Console.WriteLine("Email button layout: " + checks + " assertions passed.");
    }
}
