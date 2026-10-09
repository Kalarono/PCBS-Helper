using System;
using System.Collections.Generic;
using PCBSCalculatorHelper;

internal static class CpuMatcherTests
{
    private static int assertions;
    private static void Check(bool pass, string message)
    { assertions++; if (!pass) throw new Exception(message); }
    private static CpuCandidate Cpu(string id, string brand, float score, int price)
    { return new CpuCandidate { Id = id, Name = id, Brand = brand, Score = score, Price = price, Socket = "test", Level = 1, Unlocked = true }; }
    public static int Main()
    {
        List<CpuCandidate> candidates = new List<CpuCandidate> {
            Cpu("same-brand", "AMD", 100, 10), Cpu("below", "Intel", 99.99f, 1),
            Cpu("closest-expensive", "Intel", 100, 250), Cpu("closest-cheap", "Intel", 100, 100),
            Cpu("stronger", "Intel", 110, 90), Cpu("nan", "Intel", Single.NaN, 1),
            Cpu("infinity", "Intel", Single.PositiveInfinity, 1), Cpu("zero", "Intel", 0, 1),
            Cpu("closest-cheap", "Intel", 100, 100)
        };
        List<CpuCandidate> result = CpuMatcher.Find(candidates, "AMD", 100, 3);
        Check(result.Count == 3, "Limit and distinct IDs must be applied.");
        Check(result[0].Id == "closest-cheap", "An equal-score lower-price CPU should rank first.");
        Check(result[1].Id == "closest-expensive", "Exact threshold is valid; brand and weaker CPUs are excluded.");
        Check(result[2].Id == "stronger", "Nearest stronger CPU should follow equal-score CPUs.");
        result = CpuMatcher.Find(candidates, "Intel", 100, 3);
        Check(result.Count == 1 && result[0].Brand == "AMD", "Intel -> AMD must work symmetrically.");
        Check(CpuMatcher.Find(candidates, "Other", 100, 3).Count == 0, "Unknown brand must not invent an alternative.");
        Check(CpuMatcher.Find(candidates, "AMD", Single.NaN, 3).Count == 0, "Invalid requirement score must be rejected.");
        Check(CpuMatcher.Find(candidates, "AMD", 100, 0).Count == 0, "Zero results requested.");
        Check(CpuMatcher.Brand("amd", "Ryzen") == "AMD", "Manufacturer matching is case insensitive.");
        Check(CpuMatcher.Brand("", "Intel Core i7") == "Intel", "Known CPU name may identify its brand.");
        candidates[2].Unlocked = false;
        candidates[3].Unlocked = false;
        candidates[8].Unlocked = false;
        result = CpuMatcher.Find(candidates, "AMD", 100, 3, true);
        Check(result.Count == 1 && result[0].Id == "stronger", "Classic career must exclude every locked CPU, even closer ones.");
        candidates[4].Unlocked = false;
        candidates[8].Unlocked = false;
        Check(CpuMatcher.Find(candidates, "AMD", 100, 3, true).Count == 0, "No locked fallback is allowed in classic career.");
        Check(CpuMatcher.Find(candidates, "AMD", 100, 3, false).Count == 3, "DLC and freebuild should ignore level availability.");
        List<CpuCandidate> pricing = new List<CpuCandidate> { Cpu("exact-expensive", "Intel", 100, 500),
            Cpu("fast-cheap", "Intel", 120, 50), Cpu("weak-cheapest", "Intel", 99, 1), Cpu("locked", "Intel", 150, 10) };
        pricing[3].Unlocked = false;
        Check(CpuMatcher.Cheapest(pricing, "AMD", 100, true).Id == "fast-cheap", "The cheapest qualifying unlocked CPU wins, not the closest score.");
        Check(CpuMatcher.Cheapest(pricing, "AMD", 100, false).Id == "locked", "DLC/freebuild pricing ignores career level.");
        Check(CpuMatcher.Cheapest(pricing, "AMD", 999, true) == null, "No weaker CPU may be offered as a cheap alternative.");
        Console.WriteLine("CPU matcher: " + assertions + " assertions passed.");
        return 0;
    }
}
