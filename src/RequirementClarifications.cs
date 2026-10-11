using System;
using System.Collections.Generic;

namespace PCBSCalculatorHelper
{
    internal sealed class RequirementPart
    {
        public string Id, Name, Type, NativeType, Manufacturer, Socket;
        public float Score, Quality;
        public int Price, Vram;
        public bool Unlocked, MultiGpu;
    }

    internal enum JobRefinementKind { Brand, Part, Ram, Storage, Other }

    internal sealed class JobRefinement
    {
        public JobRefinementKind Kind;
        // Empty Type means a general condition, not a condition on every CPU/GPU.
        public string Type, Brand, Description;
        public int Count = 1, Target;
        public bool Exact;
        public RequirementPart Part;
        // Complex goals remain visible without making an incomplete part recommendation.
        public string ManualCpuGpu;
    }

    internal sealed class RequirementProfile
    {
        public RequirementPart Cpu, Gpu;
        public int Ram, Storage;
        public RequirementPart Part(string type) { return type == "CPU" ? Cpu : type == "GPU" ? Gpu : null; }
    }

    internal sealed class BrandReplacementOptions
    {
        public JobRefinement Rule;
        public RequirementPart Cpu, Gpu;
        public bool HasCpu, HasGpu;
    }

    internal sealed class RequirementClarifications
    {
        public readonly List<JobRefinement> Notes = new List<JobRefinement>();
        public readonly List<BrandReplacementOptions> BrandOptions = new List<BrandReplacementOptions>();
        public RequirementPart Cpu, Gpu;
        public bool NeedsCpu, NeedsGpu, ManualCpu, ManualGpu;

        internal static RequirementClarifications Build(RequirementProfile profile, IEnumerable<JobRefinement> rules,
            IEnumerable<RequirementPart> parts, bool unlockedOnly)
        {
            RequirementClarifications result = new RequirementClarifications();
            if (profile == null || rules == null) return result;
            List<JobRefinement> all = new List<JobRefinement>();
            HashSet<string> descriptions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (JobRefinement rule in rules)
            {
                if (rule == null) continue;
                all.Add(rule);
                result.ManualCpu |= rule.ManualCpuGpu == "CPU" || rule.ManualCpuGpu == "Both";
                result.ManualGpu |= rule.ManualCpuGpu == "GPU" || rule.ManualCpuGpu == "Both";
                if (!AddsInformation(profile, rule)) continue;
                result.NeedsCpu |= rule.Type == "CPU" && (rule.Kind == JobRefinementKind.Brand || rule.Kind == JobRefinementKind.Part);
                result.NeedsGpu |= rule.Type == "GPU" && (rule.Kind == JobRefinementKind.Brand || rule.Kind == JobRefinementKind.Part);
                result.ManualCpu |= rule.Type == "CPU" && rule.Count > 1;
                string key = rule.Kind + "|" + rule.Type + "|" + rule.Brand + "|" + rule.Count + "|" + rule.Target + "|" + rule.Exact +
                    "|" + (rule.Part == null ? "" : rule.Part.Id) + "|" + rule.Description;
                if (descriptions.Add(key)) result.Notes.Add(rule);
            }
            if (parts != null)
            {
                List<RequirementPart> catalogue = new List<RequirementPart>(parts);
                if (result.NeedsCpu && !result.ManualCpu) result.Cpu = Cheapest("CPU", profile, all, catalogue, unlockedOnly);
                if (result.NeedsGpu && !result.ManualGpu) result.Gpu = Cheapest("GPU", profile, all, catalogue, unlockedOnly);
                foreach (JobRefinement rule in result.Notes)
                {
                    if (rule.Kind != JobRefinementKind.Brand || !String.IsNullOrEmpty(rule.Type) || rule.Count < 1) continue;
                    BrandReplacementOptions options = new BrandReplacementOptions { Rule = rule };
                    foreach (RequirementPart part in catalogue)
                    {
                        if (part == null || !String.Equals(part.Manufacturer, rule.Brand, StringComparison.OrdinalIgnoreCase)) continue;
                        options.HasCpu |= part.Type == "CPU";
                        options.HasGpu |= part.Type == "GPU";
                    }
                    if (!options.HasCpu && !options.HasGpu) continue;
                    // A general brand objective accepts a new part of either category.
                    // Show category alternatives instead of silently omitting them
                    // or claiming that BOTH the CPU and GPU must be replaced.
                    if (rule.Count == 1)
                    {
                        if (options.HasCpu && !result.ManualCpu)
                            options.Cpu = CheapestForBrand("CPU", rule.Brand, profile, all, catalogue, unlockedOnly);
                        if (options.HasGpu && !result.ManualGpu)
                            options.Gpu = CheapestForBrand("GPU", rule.Brand, profile, all, catalogue, unlockedOnly);
                        // AMD/Intel email preferences normally concern the CPU.
                        // Prefer that sufficient option; retain a GPU fallback
                        // if a CPU cannot be suggested under the job constraints.
                        if (options.Cpu != null && (String.Equals(rule.Brand, "AMD", StringComparison.OrdinalIgnoreCase) ||
                            String.Equals(rule.Brand, "Intel", StringComparison.OrdinalIgnoreCase)))
                        { options.HasGpu = false; options.Gpu = null; }
                    }
                    result.BrandOptions.Add(options);
                }
            }
            return result;
        }

        private static bool AddsInformation(RequirementProfile profile, JobRefinement rule)
        {
            if (rule.Kind == JobRefinementKind.Ram) return rule.Target > profile.Ram;
            if (rule.Kind == JobRefinementKind.Storage) return rule.Target > profile.Storage;
            if (rule.Kind == JobRefinementKind.Other) return true;
            if (rule.Kind == JobRefinementKind.Brand && String.IsNullOrEmpty(rule.Type) && rule.Count > 0)
            {
                int covered = 0;
                foreach (RequirementPart part in new[] { profile.Cpu, profile.Gpu })
                    if (part != null && String.Equals(part.Manufacturer, rule.Brand, StringComparison.OrdinalIgnoreCase)) covered++;
                if (covered >= rule.Count) return false;
            }
            RequirementPart reference = profile.Part(rule.Type);
            if (reference == null) return true;
            // Will it Run? describes one CPU/GPU; a count condition is additional information.
            if (rule.Count > 1) return true;
            return !Allows(reference, rule);
        }

        private static bool Allows(RequirementPart part, JobRefinement rule)
        {
            if (rule.Kind == JobRefinementKind.Brand)
            {
                if (!String.IsNullOrEmpty(rule.Type) && rule.Type != part.Type) return true;
                // General goals are handled by category options, each explicitly
                // filtered to the requested brand in CheapestForBrand.
                if (String.IsNullOrEmpty(rule.Type) && rule.Count > 0) return true;
                if (rule.Count > 1 && part.Type == "GPU" && !part.MultiGpu) return false;
                bool same = String.Equals(part.Manufacturer, rule.Brand, StringComparison.OrdinalIgnoreCase);
                return rule.Count == 0 ? !same : same;
            }
            if (rule.Kind != JobRefinementKind.Part || rule.Type != part.Type) return true;
            if (rule.Part == null) return false;
            if (rule.Count > 1 && part.Type == "GPU" && !part.MultiGpu) return false;
            // Standard INSTALL_PART accepts the native part or better quality;
            // the Esports specific-part objective requires the exact ID.
            return rule.Exact ? part.Id == rule.Part.Id : (part.NativeType == rule.Part.NativeType || part.Type == "GPU") &&
                ValidScore(part.Quality) && ValidScore(rule.Part.Quality) && part.Quality >= rule.Part.Quality;
        }

        private static RequirementPart Cheapest(string type, RequirementProfile profile, List<JobRefinement> rules,
            IEnumerable<RequirementPart> parts, bool unlockedOnly)
        {
            RequirementPart reference = profile.Part(type), cheapest = null;
            if (reference == null || !ValidScore(reference.Score)) return null;
            foreach (RequirementPart part in parts)
            {
                if (part == null || part.Type != type || (unlockedOnly && !part.Unlocked) ||
                    !ValidScore(part.Score) || part.Score < reference.Score ||
                    (type == "GPU" && part.Vram < reference.Vram)) continue;
                bool allowed = true;
                foreach (JobRefinement rule in rules) if (!Allows(part, rule)) { allowed = false; break; }
                if (!allowed) continue;
                if (cheapest == null || part.Price < cheapest.Price || (part.Price == cheapest.Price &&
                    (part.Score < cheapest.Score || (part.Score == cheapest.Score && String.CompareOrdinal(part.Id, cheapest.Id) < 0))))
                    cheapest = part;
            }
            return cheapest;
        }

        private static RequirementPart CheapestForBrand(string type, string brand, RequirementProfile profile,
            List<JobRefinement> rules, List<RequirementPart> parts, bool unlockedOnly)
        {
            List<JobRefinement> scoped = new List<JobRefinement>(rules);
            scoped.Add(new JobRefinement { Kind = JobRefinementKind.Brand, Type = type, Brand = brand, Count = 1 });
            return Cheapest(type, profile, scoped, parts, unlockedOnly);
        }

        private static bool ValidScore(float score) { return score > 0 && !Single.IsNaN(score) && !Single.IsInfinity(score); }

        internal static string BrandType(string brand, IEnumerable<RequirementPart> parts)
        {
            // Infer a category only when the game's catalogue makes it unambiguous.
            // Team Group makes RAM and drives; MSI makes GPUs and other hardware.
            string type = null;
            foreach (RequirementPart part in parts)
                if (String.Equals(part.Manufacturer, brand, StringComparison.OrdinalIgnoreCase))
                {
                    if (type != null && type != part.Type) return "";
                    type = part.Type;
                }
            return type ?? "";
        }
    }
}
