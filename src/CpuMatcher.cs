using System;
using System.Collections.Generic;

namespace PCBSCalculatorHelper
{
    internal sealed class CpuCandidate
    {
        public string Id, Name, Brand, Socket;
        public float Score;
        public int Price, Level;
        public bool Unlocked;
    }

    internal static class CpuMatcher
    {
        // Will it Run? compares PartDescCPU.Test(), including the CPU's own default
        // memory speed and maximum channel count. Never substitute cores or GHz.
        public static List<CpuCandidate> Find(IEnumerable<CpuCandidate> candidates,
            string requiredBrand, float requiredScore, int count, bool unlockedOnly = false)
        {
            List<CpuCandidate> matches = new List<CpuCandidate>();
            string otherBrand = requiredBrand == "AMD" ? "Intel" : requiredBrand == "Intel" ? "AMD" : null;
            if (otherBrand == null || !ValidScore(requiredScore) || count <= 0) return matches;
            HashSet<string> ids = new HashSet<string>();
            foreach (CpuCandidate cpu in candidates)
            {
                if ((!unlockedOnly || cpu.Unlocked) && cpu.Brand == otherBrand && ValidScore(cpu.Score) && cpu.Score >= requiredScore && ids.Add(cpu.Id))
                    matches.Add(cpu);
            }
            matches.Sort(delegate(CpuCandidate a, CpuCandidate b)
            {
                int byScore = a.Score.CompareTo(b.Score);
                if (byScore != 0) return byScore;
                int byPrice = a.Price.CompareTo(b.Price);
                return byPrice != 0 ? byPrice : String.CompareOrdinal(a.Id, b.Id);
            });
            if (matches.Count > count) matches.RemoveRange(count, matches.Count - count);
            return matches;
        }

        private static bool ValidScore(float score)
        { return score > 0 && !Single.IsNaN(score) && !Single.IsInfinity(score); }

        public static CpuCandidate Cheapest(IEnumerable<CpuCandidate> candidates, string requiredBrand,
            float requiredScore, bool unlockedOnly)
        {
            List<CpuCandidate> matches = Find(candidates, requiredBrand, requiredScore, Int32.MaxValue, unlockedOnly);
            matches.Sort(delegate(CpuCandidate a, CpuCandidate b)
            {
                int price = a.Price.CompareTo(b.Price);
                if (price != 0) return price;
                int score = a.Score.CompareTo(b.Score);
                return score != 0 ? score : String.CompareOrdinal(a.Id, b.Id);
            });
            return matches.Count == 0 ? null : matches[0];
        }

        public static string Brand(string manufacturer, string name)
        {
            if (String.Equals(manufacturer, "AMD", StringComparison.OrdinalIgnoreCase) ||
                (name ?? "").StartsWith("AMD ", StringComparison.OrdinalIgnoreCase)) return "AMD";
            if (String.Equals(manufacturer, "Intel", StringComparison.OrdinalIgnoreCase) ||
                (name ?? "").StartsWith("Intel ", StringComparison.OrdinalIgnoreCase)) return "Intel";
            return "";
        }
    }
}
