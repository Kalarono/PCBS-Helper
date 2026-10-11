using System;
using System.Collections.Generic;
using PCBSCalculatorHelper;

internal static class RequirementClarificationsTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static RequirementPart Part(string id, string type, string brand, float score, int price, int vram = 0)
    { return new RequirementPart { Id = id, Name = id, Type = type, NativeType = type, Manufacturer = brand,
        Score = score, Quality = score, Price = price, Vram = vram, Unlocked = true, MultiGpu = true, Socket = "test" }; }
    private static JobRefinement Brand(string type, string brand, int count = 1)
    { return new JobRefinement { Kind = JobRefinementKind.Brand, Type = type, Brand = brand, Count = count, Description = "Use " + brand }; }
    private static RequirementClarifications Plan(RequirementProfile profile, List<RequirementPart> parts, params JobRefinement[] rules)
    { return RequirementClarifications.Build(profile, rules, parts, true); }
    private static void Main()
    {
        RequirementProfile profile = new RequirementProfile { Cpu = Part("AMD reference", "CPU", "AMD", 100, 150),
            Gpu = Part("AMD Radeon reference", "GPU", "AMD", 200, 200, 8), Ram = 8, Storage = 100 };
        List<RequirementPart> catalogue = new List<RequirementPart> {
            Part("weak Intel", "CPU", "Intel", 99.99f, 1), Part("exact Intel", "CPU", "Intel", 100, 100),
            Part("cheap Intel", "CPU", "Intel", 110, 90), Part("locked Intel", "CPU", "Intel", 120, 10),
            Part("NVIDIA insufficient VRAM", "GPU", "NVIDIA", 210, 1, 6),
            Part("weak NVIDIA", "GPU", "NVIDIA", 199.99f, 2, 8), Part("NVIDIA exact", "GPU", "NVIDIA", 200, 120, 8),
            Part("NVIDIA cheapest", "GPU", "NVIDIA", 210, 100, 8),
            Part("EVGA GeForce", "GPU", "EVGA", 240, 20, 8),
            Part("locked NVIDIA", "GPU", "NVIDIA", 240, 10, 8),
            Part("RAM", "RAM", "Team Group", 0, 20), Part("SSD", "SSD", "Team Group", 0, 30),
            Part("Seagate disk", "HDD", "Seagate", 0, 40), Part("MSI board", "MOTHERBOARD", "MSI", 0, 50),
            Part("MSI Radeon", "GPU", "MSI", 230, 45, 8), profile.Cpu, profile.Gpu
        };
        catalogue[3].Unlocked = catalogue[9].Unlocked = false;
        RequirementClarifications plan = Plan(profile, catalogue);
        Check(plan.Notes.Count == 0 && plan.Cpu == null && plan.Gpu == null, "A plain Will it Run? job must not invent brand alternatives.");
        plan = Plan(profile, catalogue, Brand("CPU", "Intel"), Brand("GPU", "NVIDIA"));
        Check(plan.Notes.Count == 2 && plan.NeedsCpu && plan.NeedsGpu, "Both CPU and GPU brand conflicts must be shown.");
        Check(plan.Cpu.Id == "cheap Intel", "Pick the cheapest sufficient unlocked CPU, not merely the nearest score.");
        Check(plan.Gpu.Id == "NVIDIA cheapest", "GPU alternatives must meet the game's score AND VRAM threshold.");
        Check(plan.Gpu.Manufacturer == "NVIDIA", "A NVIDIA manufacturer objective must not be satisfied by an EVGA GeForce.");
        plan = RequirementClarifications.Build(profile, new[] { Brand("CPU", "Intel"), Brand("GPU", "NVIDIA") }, catalogue, false);
        Check(plan.Cpu.Id == "locked Intel" && plan.Gpu.Id == "locked NVIDIA", "Freebuild/DLC ignore classic career level restrictions.");
        plan = Plan(profile, catalogue, Brand("CPU", "amd"), Brand("GPU", "AMD"));
        Check(plan.Notes.Count == 0 && !plan.NeedsCpu && !plan.NeedsGpu, "Already matching brand information must not add an Analog block.");
        Check(Plan(profile, catalogue, Brand("", "AMD")).Notes.Count == 0, "A general brand condition already covered by reference hardware adds no information.");
        RequirementProfile different = new RequirementProfile { Cpu = catalogue[1], Gpu = catalogue[6], Ram = 8, Storage = 100 };
        plan = Plan(different, catalogue, Brand("", "AMD"));
        Check(plan.Notes.Count == 1 && !plan.NeedsCpu && plan.Cpu == null && plan.BrandOptions.Count == 1,
            "General AMD preference must show alternatives without requiring a particular category.");
        Check(plan.BrandOptions[0].Cpu == profile.Cpu && plan.BrandOptions[0].Gpu == null && !plan.BrandOptions[0].HasGpu,
            "Intel/NVIDIA reference plus a general AMD goal prioritizes an actual sufficient AMD CPU model.");
        Check(plan.BrandOptions[0].Rule.Count == 1, "CPU and GPU options are alternatives for ONE new matching part.");
        Check(Plan(different, catalogue, Brand("", "amd")).BrandOptions[0].Cpu == profile.Cpu,
            "AMD CPU preference is case-insensitive.");
        plan = Plan(different, catalogue, Brand("GPU", "AMD"));
        Check(plan.Gpu == profile.Gpu && plan.Cpu == null && plan.BrandOptions.Count == 0,
            "An explicitly GPU-scoped AMD condition still selects the GPU, never the CPU.");
        plan = Plan(profile, catalogue, Brand("", "EVGA"));
        Check(plan.BrandOptions.Count == 1 && !plan.BrandOptions[0].HasCpu && plan.BrandOptions[0].Gpu.Id == "EVGA GeForce",
            "A general EVGA preference can suggest its GPU without falsely forcing a CPU.");
        Check(Plan(different, catalogue, Brand("", "Team Group")).BrandOptions.Count == 0,
            "General RAM/SSD brands do not generate unrelated CPU/GPU alternatives.");
        plan = Plan(different, catalogue, Brand("", "AMD"), Brand("CPU", "Intel"));
        Check(plan.BrandOptions[0].Cpu == null && plan.BrandOptions[0].Gpu == profile.Gpu,
            "A general AMD GPU option respects the simultaneous Intel CPU condition.");
        plan = Plan(different, catalogue, Brand("", "AMD"), Brand("GPU", "NVIDIA"));
        Check(plan.BrandOptions[0].Cpu == profile.Cpu && plan.BrandOptions[0].Gpu == null,
            "A general AMD CPU option respects the simultaneous NVIDIA GPU condition.");
        plan = Plan(different, catalogue, Brand("", "AMD"), Brand("", "AMD", 0));
        Check(plan.BrandOptions[0].Cpu == null && plan.BrandOptions[0].Gpu == null,
            "Contradictory general-brand requirements must not suggest a partial match.");
        profile.Cpu.Unlocked = profile.Gpu.Unlocked = false;
        plan = Plan(different, catalogue, Brand("", "AMD"));
        Check(plan.BrandOptions[0].Cpu == null && plan.BrandOptions[0].Gpu == null,
            "General-brand alternatives apply current career availability to both categories.");
        plan = RequirementClarifications.Build(different, new[] { Brand("", "AMD") }, catalogue, false);
        Check(plan.BrandOptions[0].Cpu != null && plan.BrandOptions[0].Gpu == null,
            "Other modes ignore career locks and still prioritize AMD CPUs.");
        profile.Gpu.Unlocked = true;
        plan = Plan(different, catalogue, Brand("", "AMD"));
        Check(plan.BrandOptions[0].Cpu == null && plan.BrandOptions[0].Gpu == profile.Gpu,
            "An available sufficient AMD GPU remains a valid fallback if the CPU is career-locked.");
        profile.Cpu.Unlocked = profile.Gpu.Unlocked = true;
        profile.Gpu.Vram = 4;
        Check(Plan(different, catalogue, Brand("", "AMD"), Brand("CPU", "Intel")).BrandOptions[0].Gpu == null,
            "General-brand GPU alternatives must still meet the reference VRAM requirement.");
        profile.Gpu.Vram = 8;
        plan = Plan(different, catalogue, Brand("", "AMD"), new JobRefinement { Kind = JobRefinementKind.Other, ManualCpuGpu = "CPU" });
        Check(plan.BrandOptions[0].Cpu == null && plan.BrandOptions[0].Gpu != null,
            "A complex CPU goal blocks incomplete CPU matching without hiding a GPU brand option.");
        plan = Plan(different, catalogue, Brand("", "AMD"), new JobRefinement { Kind = JobRefinementKind.Other, ManualCpuGpu = "Both" });
        Check(plan.BrandOptions[0].Cpu == null && plan.BrandOptions[0].Gpu == null && plan.ManualCpu && plan.ManualGpu,
            "A separate benchmark target keeps general-brand CPU/GPU selection manual.");
        plan = Plan(different, catalogue, Brand("", "AMD", 2));
        Check(plan.BrandOptions[0].Rule.Count == 2 && plan.BrandOptions[0].Cpu == null && plan.BrandOptions[0].Gpu == null,
            "A count of two must not advertise either single-part alternative as sufficient.");
        plan = Plan(profile, catalogue, Brand("RAM", "Team Group"), Brand("HDD", "Seagate"));
        Check(plan.Notes.Count == 2 && plan.Cpu == null && plan.Gpu == null, "Memory and disk preferences stay visible without irrelevant CPU suggestions.");
        Check(RequirementClarifications.BrandType("intel", catalogue) == "CPU", "Infer Intel's unambiguous category case-insensitively.");
        Check(RequirementClarifications.BrandType("NVIDIA", catalogue) == "GPU", "Infer GPU-only brand from actual catalogue entries.");
        Check(RequirementClarifications.BrandType("Team Group", catalogue) == "", "Team Group RAM/SSD preference stays general.");
        Check(RequirementClarifications.BrandType("MSI", catalogue) == "", "MSI GPU/motherboard preference stays general.");
        Check(RequirementClarifications.BrandType("Seagate", catalogue) == "HDD", "A disk-only brand can be classified.");
        Check(RequirementClarifications.BrandType("Unknown", catalogue) == "", "Unknown brand must not be guessed.");
        plan = Plan(profile, catalogue, new JobRefinement { Kind = JobRefinementKind.Ram, Target = 8 },
            new JobRefinement { Kind = JobRefinementKind.Storage, Target = 100 });
        Check(plan.Notes.Count == 0, "RAM/storage requirements already covered by Will it Run? add no notes.");
        plan = Plan(profile, catalogue, new JobRefinement { Kind = JobRefinementKind.Ram, Target = 16 },
            new JobRefinement { Kind = JobRefinementKind.Storage, Target = 200 });
        Check(plan.Notes.Count == 2, "Higher RAM/storage goals must be visible.");
        plan = Plan(profile, catalogue, Brand("GPU", "AMD", 2));
        Check(plan.Notes.Count == 1 && plan.NeedsGpu, "A two-part goal is additional even when its manufacturer matches.");
        profile.Gpu.MultiGpu = false;
        Check(Plan(profile, catalogue, Brand("GPU", "AMD", 2)).Gpu == null, "A brand count of two must respect native multi-GPU support.");
        profile.Gpu.MultiGpu = true;
        Check(Plan(profile, catalogue, Brand("CPU", "AMD", 2)).Cpu == null, "Do not recommend one CPU for a two-CPU count condition.");
        plan = Plan(profile, catalogue, Brand("GPU", "AMD", 0));
        Check(plan.Notes.Count == 1 && plan.Gpu.Manufacturer != "AMD", "Excluding the reference GPU manufacturer requires a different card.");
        plan = Plan(profile, catalogue, Brand("GPU", "NVIDIA", 0));
        Check(plan.Notes.Count == 0, "An exclusion already covered by the reference GPU should not duplicate information.");
        plan = Plan(profile, catalogue, Brand("GPU", "NVIDIA"), Brand("", "NVIDIA", 0));
        Check(plan.Gpu == null && plan.Notes.Count == 2, "Conflicting positive/exclusion requirements must not yield a misleading card.");
        plan = Plan(profile, catalogue, Brand("CPU", "Intel"), Brand("CPU", "AMD"));
        Check(plan.Cpu == null, "Even a redundant matching rule must be respected when filtering recommendations.");
        JobRefinement exact = new JobRefinement { Kind = JobRefinementKind.Part, Type = "GPU", Part = catalogue[6], Exact = true, Description = "Install specific GPU" };
        plan = Plan(profile, catalogue, exact);
        Check(plan.Notes.Count == 1 && plan.Gpu.Id == "NVIDIA exact", "DLC exact-part goals require the requested ID, not a faster substitute.");
        catalogue[6].Unlocked = false;
        plan = Plan(profile, catalogue, exact);
        Check(plan.Gpu == null, "An exact locked part must not fall back to a different card.");
        catalogue[6].Unlocked = true;
        exact.Part = catalogue[4];
        Check(Plan(profile, catalogue, exact).Gpu == null, "An exact GPU with too little VRAM must not be advertised as meeting Will it Run?.");
        exact.Part = profile.Gpu;
        Check(Plan(profile, catalogue, exact).Notes.Count == 0, "An identical exact-part requirement is already covered.");
        exact.Count = 2;
        profile.Gpu.MultiGpu = false;
        Check(Plan(profile, catalogue, exact).Gpu == null, "A two-card install must not recommend a card without multi-GPU support.");
        profile.Gpu.MultiGpu = true;
        JobRefinement better = new JobRefinement { Kind = JobRefinementKind.Part, Type = "CPU", Part = catalogue[2], Description = "Install CPU or better" };
        plan = Plan(profile, catalogue, better, Brand("CPU", "Intel"));
        Check(plan.Cpu.Id == "cheap Intel", "Standard install accepts the threshold quality instead of forcing an exact model.");
        better.Part = catalogue[0];
        Check(Plan(profile, catalogue, better).Notes.Count == 0, "An already sufficient reference CPU covers a weaker quality objective.");
        plan = Plan(profile, catalogue, Brand("CPU", "Intel"), new JobRefinement { Kind = JobRefinementKind.Other, Description = "Overclock CPU", ManualCpuGpu = "CPU" });
        Check(plan.Notes.Count == 2 && plan.ManualCpu && plan.Cpu == null, "Do not auto-recommend a CPU without checking a separate overclock condition.");
        plan = Plan(profile, catalogue, Brand("GPU", "NVIDIA"), new JobRefinement { Kind = JobRefinementKind.Other, Description = "Two GPUs", ManualCpuGpu = "GPU" });
        Check(plan.ManualGpu && plan.Gpu == null, "Multi-GPU job context must not silently use single-card matching.");
        JobRefinement team = Brand("RAM", "Team Group");
        Check(Plan(profile, catalogue, team, team).Notes.Count == 1, "Repeated identical notes should not clutter F3.");
        Check(Plan(profile, catalogue, Brand("GPU", "NVIDIA"), Brand("GPU", "NVIDIA", 2)).Notes.Count == 2, "Different counts are separate constraints.");
        catalogue[2].Score = Single.NaN;
        Check(Plan(profile, catalogue, Brand("CPU", "Intel")).Cpu.Id == "exact Intel", "Reject NaN score candidates.");
        catalogue[1].Score = Single.PositiveInfinity;
        Check(Plan(profile, catalogue, Brand("CPU", "Intel")).Cpu == null, "Reject infinite scores and locked/weak fallbacks.");
        profile.Cpu.Score = 0;
        Check(Plan(profile, catalogue, Brand("CPU", "Intel")).Cpu == null, "Reject invalid reference scores.");
        profile.Cpu = null;
        Check(Plan(profile, catalogue, Brand("CPU", "Intel")).Notes.Count == 1, "Missing profile CPU must not erase the job's brand requirement.");
        Check(RequirementClarifications.Build(null, new[] { team }, catalogue, true).Notes.Count == 0, "No manual-game job context means no clarifications.");
        Check(RequirementClarifications.Build(profile, null, catalogue, true).Notes.Count == 0, "Missing rules are safe.");
        Check(RequirementClarifications.Build(profile, new[] { team }, null, true).Notes.Count == 1, "Notes do not depend on recommendation availability.");
        Check(Plan(profile, catalogue, new JobRefinement { Kind = JobRefinementKind.Part, Type = "GPU", Part = null, Description = "Unavailable exact part" }).Gpu == null,
            "An unresolved objective must not silently admit any substitute.");
        Console.WriteLine("F3 job clarifications: " + checks + " assertions passed.");
    }
}
