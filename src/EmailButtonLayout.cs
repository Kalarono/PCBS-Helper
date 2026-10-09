using System;

namespace PCBSCalculatorHelper
{
    internal sealed class EmailButtonLayout
    {
        internal const float Gap = 8;
        internal float Left, Width;
        internal bool CompactNative;

        internal static EmailButtonLayout Calculate(float rowLeft, float rowRight, float positiveLeft,
            float positiveWidth, float minimumLeft, int nativeCount, int helperCount)
        {
            if (helperCount < 1 || nativeCount < 0) throw new ArgumentOutOfRangeException();
            float width = Math.Min(positiveWidth,
                (positiveLeft - Gap - minimumLeft - Gap * (helperCount - 1)) / helperCount);
            if (width >= 90)
                return new EmailButtonLayout { Left = positiveLeft - helperCount * (width + Gap), Width = width };
            // Share the existing footer row, including DLC actions, without covering the status row.
            int count = nativeCount + helperCount;
            return new EmailButtonLayout {
                Left = rowLeft, Width = Math.Max(1, (rowRight - rowLeft - Gap * (count - 1)) / count),
                CompactNative = true
            };
        }
    }
}
