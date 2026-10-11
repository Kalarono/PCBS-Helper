using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using PCBSCalculatorHelper;

// Construct serialized objective data without game constructors, localization,
// Unity initialization, a running game process or any save-file access.
internal static class JobRequirementReaderEntry
{
    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs ev) {
            string path = Path.Combine(args[0], new AssemblyName(ev.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        // Defer resolving a test type with native Objective constraints until the
        // loader has the managed-only resolver. Nothing is copied into the game.
        Assembly.GetExecutingAssembly().GetType("JobRequirementReaderTests").GetMethod("Run").Invoke(null, null);
    }
}

internal static class JobRequirementReaderTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Set(object obj, string name, object value)
    {
        for (Type type = obj.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
            if (field == null) continue;
            field.SetValue(obj, value); return;
        }
        throw new Exception("Actual game objective field missing: " + name);
    }
    private static T Goal<T>(Objective.Type type) where T : Objective
    {
        T result = (T)FormatterServices.GetUninitializedObject(typeof(T));
        Set(result, "m_type", type); return result;
    }
    private static RequirementPart Part(string id, string type, string brand)
    { return new RequirementPart { Id = id, Name = id, Type = type, NativeType = type, Manufacturer = brand, Score = 100, Quality = 100, Unlocked = true, Socket = "test" }; }
    public static void Run()
    {
        RequirementPart intel = Part("intel", "CPU", "Intel"), nvidia = Part("nvidia", "GPU", "NVIDIA");
        List<RequirementPart> parts = new List<RequirementPart> { intel, nvidia,
            Part("team ram", "RAM", "Team Group"), Part("team SSD", "SSD", "Team Group"), Part("disk", "HDD", "Seagate") };
        Func<string, RequirementPart> lookup = delegate(string id) { return parts.Find(x => x.Id == id); };
        int descriptions = 0;
        Func<Objective, string> describe = delegate(Objective goal) { descriptions++; return goal.GetObjectiveType().ToString(); };
        UseBrandObjective cpu = Goal<UseBrandObjective>(Objective.Type.USE_BRAND);
        Set(cpu, "m_manufacturer", "Intel"); Set(cpu, "m_numberOfParts", 1); Set(cpu, "m_hidden", true);
        List<JobRefinement> rules = JobRequirementReader.Read(new Objective[] { cpu }, parts, lookup, describe);
        Check(rules.Count == 1 && rules[0].Type == "CPU" && rules[0].Brand == "Intel", "Hidden email brand preference is read from actual private fields.");
        Check(rules[0].Description == "USE_BRAND", "Display description comes from the native objective, independently of parsing.");
        Set(cpu, "m_manufacturer", "NVIDIA");
        rules = JobRequirementReader.Read(new Objective[] { cpu }, parts, lookup, describe);
        Check(rules[0].Type == "GPU", "NVIDIA preference is classified as GPU, not a CPU alternative.");
        Set(cpu, "m_manufacturer", "Team Group");
        rules = JobRequirementReader.Read(new Objective[] { cpu }, parts, lookup, describe);
        Check(rules[0].Type == "", "General Team Group goal must not be rewritten as a RAM-only objective.");
        Set(cpu, "m_numberOfParts", 3);
        Check(JobRequirementReader.Read(new Objective[] { cpu }, parts, lookup, describe)[0].Count == 3, "Brand count is preserved.");
        RequirementPart amdCpu = Part("AMD CPU option", "CPU", "AMD"), amdGpu = Part("AMD GPU option", "GPU", "AMD");
        parts.Add(amdCpu); parts.Add(amdGpu);
        UseBrandObjective amdPreference = Goal<UseBrandObjective>(Objective.Type.USE_BRAND);
        Set(amdPreference, "m_manufacturer", "AMD"); Set(amdPreference, "m_numberOfParts", 1);
        rules = JobRequirementReader.Read(new Objective[] { amdPreference }, parts, lookup, describe);
        Check(rules[0].Type == "" && rules[0].Count == 1, "Native general AMD goal preserves its actual scope and count.");
        RequirementProfile adobeLike = new RequirementProfile { Cpu = intel, Gpu = nvidia, Ram = 16, Storage = 7 };
        RequirementClarifications amdPlan = RequirementClarifications.Build(adobeLike, rules, parts, true);
        Check(amdPlan.BrandOptions.Count == 1 && amdPlan.BrandOptions[0].Cpu == amdCpu && amdPlan.BrandOptions[0].Gpu == null,
            "The actual UseBrandObjective -> reader -> planner path shows the missing AMD CPU analogue.");
        RequirementPart evgaGpu = Part("EVGA GPU option", "GPU", "EVGA");
        parts.Add(evgaGpu); parts.Add(Part("EVGA PSU", "PSU", "EVGA"));
        Set(amdPreference, "m_manufacturer", "EVGA");
        rules = JobRequirementReader.Read(new Objective[] { amdPreference }, parts, lookup, describe);
        Check(rules[0].Type == "", "Native EVGA goal also remains general because the brand makes GPUs and PSUs.");
        RequirementClarifications evgaPlan = RequirementClarifications.Build(adobeLike, rules, parts, true);
        Check(evgaPlan.BrandOptions.Count == 1 && evgaPlan.BrandOptions[0].Gpu == evgaGpu && !evgaPlan.BrandOptions[0].HasCpu,
            "The actual ambiguous GPU-brand goal produces a GPU model instead of only a note.");
        FuturLab.EsportsPartMustBeBrandObjective memory = Goal<FuturLab.EsportsPartMustBeBrandObjective>(Objective.Type.ESPORTS_DLC_PART_MUST_BE_BRAND);
        Set(memory, "m_manufacturer", "TEAM GROUP"); Set(memory, "m_partType", PartDesc.Type.RAM);
        rules = JobRequirementReader.Read(new Objective[] { memory }, parts, lookup, describe);
        Check(rules[0].Type == "RAM" && rules[0].Brand == "TEAM GROUP", "DLC RAM-specific brand objective stays specific.");
        Set(memory, "m_manufacturer", "Seagate"); Set(memory, "m_partType", PartDesc.Type.HDD);
        Check(JobRequirementReader.Read(new Objective[] { memory }, parts, lookup, describe)[0].Type == "HDD", "DLC HDD-specific brand is read correctly.");
        Set(memory, "m_partType", PartDesc.Type.WATER_COOLED_GPU);
        Check(JobRequirementReader.Read(new Objective[] { memory }, parts, lookup, describe)[0].Type == "GPU", "Water-cooled GPUs belong to the GPU group.");
        FuturLab.EsportsAtLeastXBrandPartsObjective exclude = Goal<FuturLab.EsportsAtLeastXBrandPartsObjective>(Objective.Type.ESPORTS_DLC_X_BRAND_PARTS);
        Set(exclude, "m_manufacturer", "NVIDIA"); Set(exclude, "m_numParts", 0);
        Check(JobRequirementReader.Read(new Objective[] { exclude }, parts, lookup, describe)[0].Count == 0, "Zero-brand DLC condition is an exclusion, never clamped to one.");
        InstallPartObjective install = Goal<InstallPartObjective>(Objective.Type.INSTALL_PART);
        Set(install, "m_partId", "intel"); Set(install, "m_numberToInstall", 1);
        rules = JobRequirementReader.Read(new Objective[] { install }, parts, lookup, describe);
        Check(rules[0].Part == intel && rules[0].Type == "CPU" && !rules[0].Exact, "Standard install permits native better-quality semantics.");
        FuturLab.EsportsInstallSpecificPartObjective exact = Goal<FuturLab.EsportsInstallSpecificPartObjective>(Objective.Type.ESPORTS_DLC_INSTALL_SPECIFIC_PART);
        Set(exact, "m_partId", "nvidia"); Set(exact, "m_numberToInstall", 2);
        rules = JobRequirementReader.Read(new Objective[] { exact }, parts, lookup, describe);
        Check(rules[0].Part == nvidia && rules[0].Exact && rules[0].Count == 2, "DLC subclass fields inherited from InstallPartObjective retain exact ID/count.");
        Set(install, "m_partId", "missing");
        Check(JobRequirementReader.Read(new Objective[] { install }, parts, lookup, describe)[0].Part == null, "A missing part stays explicit instead of guessing an ID.");
        RamObjective ram = Goal<RamObjective>(Objective.Type.RAM); Set(ram, "m_target", 16);
        StorageObjective disk = Goal<StorageObjective>(Objective.Type.STORAGE); Set(disk, "m_target", 2000);
        rules = JobRequirementReader.Read(new Objective[] { ram, disk }, parts, lookup, describe);
        Check(rules[0].Kind == JobRefinementKind.Ram && rules[0].Target == 16, "RAM capacity is read in game GB units.");
        Check(rules[1].Kind == JobRefinementKind.Storage && rules[1].Target == 2000, "Storage capacity is read without invented timestamps or units.");
        AllNewPartsObjective fresh = Goal<AllNewPartsObjective>(Objective.Type.ALL_NEW_PARTS);
        CablesObjective cables = Goal<CablesObjective>(Objective.Type.CABLES); Set(cables, "m_hidden", true);
        Check(JobRequirementReader.Read(new Objective[] { fresh, cables }, parts, lookup, describe).Count == 2, "New-part and cable preferences both appear, including hidden email goals.");
        OverclockObjective oc = Goal<OverclockObjective>(Objective.Type.OVERCLOCK_CPU);
        Check(JobRequirementReader.Read(new Objective[] { oc }, parts, lookup, describe)[0].ManualCpuGpu == "CPU", "CPU OC prevents incomplete CPU recommendations.");
        Set(oc, "m_type", Objective.Type.OVERCLOCK_GPU);
        Check(JobRequirementReader.Read(new Objective[] { oc }, parts, lookup, describe)[0].ManualCpuGpu == "GPU", "GPU OC prevents incomplete GPU recommendations.");
        DoubleGPUObjective dual = Goal<DoubleGPUObjective>(Objective.Type.DOUBLE_GPU);
        Check(JobRequirementReader.Read(new Objective[] { dual }, parts, lookup, describe)[0].ManualCpuGpu == "GPU", "Dual-GPU goal remains visible and needs manual compatibility selection.");
        UpgradePartObjective upgrade = Goal<UpgradePartObjective>(Objective.Type.UPGRADE_PART); Set(upgrade, "m_partType", PartDesc.Type.CPU);
        Check(JobRequirementReader.Read(new Objective[] { upgrade }, parts, lookup, describe)[0].ManualCpuGpu == "CPU", "A native upgrade goal is not replaced with the Will it Run? threshold.");
        BenchmarkScoreObjective benchmark = Goal<BenchmarkScoreObjective>(Objective.Type.BENCHMARK_SCORE);
        Set(benchmark, "m_target", 3000);
        Check(JobRequirementReader.Read(new Objective[] { benchmark }, parts, lookup, describe)[0].ManualCpuGpu == "Both", "A separate 3DMark target must not be silently replaced with Will it Run? scores.");
        BootToOSObjective boot = Goal<BootToOSObjective>(Objective.Type.BOOT_TO_OS);
        ReturnPCObjective delivery = Goal<ReturnPCObjective>(Objective.Type.RETURN_PC);
        DiagnoseObjective diagnosis = Goal<DiagnoseObjective>(Objective.Type.DIAGNOSE);
        CanPlayObjective play = Goal<CanPlayObjective>(Objective.Type.CAN_PLAY);
        Check(JobRequirementReader.Read(new Objective[] { boot, delivery, diagnosis, play }, parts, lookup, describe).Count == 0,
            "Boot, delivery, diagnosis and the game profile must not clutter hardware clarifications.");
        Set(cpu, "m_invalid", true); descriptions = 0;
        Check(JobRequirementReader.Read(new Objective[] { cpu, null }, parts, lookup, describe).Count == 0 && descriptions == 0,
            "Invalid and null goals must be skipped before description generation.");
        Set(cpu, "m_invalid", false); Set(cpu, "m_manufacturer", "");
        Check(JobRequirementReader.Read(new Objective[] { cpu }, parts, lookup, describe).Count == 0, "An empty brand must not produce an arbitrary recommendation.");
        RequirementProfile profile = new RequirementProfile { Cpu = intel, Gpu = nvidia, Ram = 8, Storage = 100 };
        Check(RequirementClarifications.Build(profile, new[] { new JobRefinement { Kind = JobRefinementKind.Ram, Target = 16 } }, parts, true).Notes.Count == 1,
            "Extracted data flows into the clarification planner without initializing Unity.");
        Console.WriteLine("Actual game job objective reader: " + checks + " assertions passed.");
    }
}
