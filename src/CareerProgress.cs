using System;

namespace PCBSCalculatorHelper
{
    internal struct CareerProgressValue
    {
        public int Level;
        public double Percent;
    }

    internal static class CareerProgress
    {
        // Same boundaries as LevelProgression.GetZeroIndexedLevel and PlayerStatus.Update.
        public static CareerProgressValue Calculate(int kudos, int[] thresholds)
        {
            if (thresholds == null || thresholds.Length == 0) return new CareerProgressValue();
            kudos = Math.Max(0, kudos);
            int index = 0;
            while (index < thresholds.Length - 1 && kudos >= thresholds[index]) index++;
            int lower = index > 0 ? thresholds[index - 1] : 0;
            int upper = thresholds[index];
            double percent = upper <= lower ? 0 : (kudos - (double)lower) * 100 / (upper - (double)lower);
            return new CareerProgressValue { Level = index + 1, Percent = Math.Max(0, Math.Min(100, percent)) };
        }
    }
}
