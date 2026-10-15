using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using PCBSCalculatorHelper;

internal static class CalculatorJobTargetsEntry
{
    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs ev) {
            string path = Path.Combine(args[0], new AssemblyName(ev.Name).Name + ".dll");
            return File.Exists(path) ? Assembly.LoadFrom(path) : null;
        };
        Assembly.GetExecutingAssembly().GetType("CalculatorJobTargetsTests").GetMethod("Run").Invoke(null, null);
    }
}
internal static class CalculatorJobTargetsTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static void Set(object value, string name, object data)
    {
        for (Type type = value.GetType(); type != null; type = type.BaseType)
        {
            FieldInfo field = type.GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly);
            if (field != null) { field.SetValue(value, data); return; }
        }
        throw new Exception("Native objective field missing: " + name);
    }
    private static OverclockObjective Clock(Objective.Type type, int mhz)
    {
        OverclockObjective goal = (OverclockObjective)FormatterServices.GetUninitializedObject(typeof(OverclockObjective));
        Set(goal, "m_type", type); Set(goal, "m_targetClockspeed", mhz); return goal;
    }
    public static void Run()
    {
        OverclockObjective cpu = Clock(Objective.Type.OVERCLOCK_CPU, 4700), gpu = Clock(Objective.Type.OVERCLOCK_GPU, 2071);
        CalculatorJobTargets target = CalculatorJobTargets.Read(new Objective[] { cpu, gpu });
        Check(target.Supported && target.Score == 0, "An OC-only job is importable without inventing a score goal.");
        Check(target.CpuMhz == 4700 && target.GpuMhz == 2071, "CPU and GPU MHz are read separately from native objectives.");
        target = CalculatorJobTargets.Read(new Objective[] { cpu, Clock(Objective.Type.OVERCLOCK_CPU, 4800), gpu });
        Check(target.CpuMhz == 4800 && target.GpuMhz == 2071, "The strictest target wins independently per component.");
        target = CalculatorJobTargets.Read(new Objective[] { Clock(Objective.Type.ESPORTS_DLC_OVERCLOCK_GPU_MAX_PERCENT, 2100) });
        Check(target.GpuMhz == 2100 && target.CpuMhz == 0, "Finalized esports frequency goals are supported.");
        OverclockObjective percentage = Clock(Objective.Type.ESPORTS_DLC_OVERCLOCK_CPU_MAX_PERCENT, -10);
        Check(!CalculatorJobTargets.Read(new Objective[] { percentage }).Supported, "Unfinalized percentages are not treated as MHz.");
        Check((int)typeof(OverclockObjective).GetField("m_targetClockspeed", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(percentage) == -10,
            "Reading a job must not mutate/finalize its objectives.");
        Check(!CalculatorJobTargets.Read(null).Supported && !CalculatorJobTargets.Read(new Objective[0]).Supported, "Missing goals are not imported.");
        BenchmarkScoreObjective score = (BenchmarkScoreObjective)FormatterServices.GetUninitializedObject(typeof(BenchmarkScoreObjective));
        Set(score, "m_target", 6000);
        target = CalculatorJobTargets.Read(new Objective[] { score, cpu, gpu });
        Check(target.Score == 6000 && target.CpuMhz == 4700 && target.GpuMhz == 2071, "Mixed score and clock goals keep all targets.");
        Set(cpu, "m_invalid", true);
        Check(!CalculatorJobTargets.Read(new Objective[] { cpu }).Supported, "Invalid objectives do not enable the import button.");
        Set(cpu, "m_invalid", false);
        // Execute the game's actual goal check against an in-memory BIOS. No Unity or saves.
        ComputerSave pc = (ComputerSave)FormatterServices.GetUninitializedObject(typeof(ComputerSave));
        BiosConfig bios = (BiosConfig)FormatterServices.GetUninitializedObject(typeof(BiosConfig));
        pc.m_biosConfig = bios; bios.m_baseClock = 100; bios.m_cpuMultiplier = 47; bios.m_gpuClock = 2071;
        Check(cpu.TestSatisifiedBy(null, pc) && gpu.TestSatisifiedBy(null, pc), "Planned frequencies satisfy the native quest checks.");
        bios.m_cpuMultiplier = 46.5f; bios.m_gpuClock = 2070;
        Check(!cpu.TestSatisifiedBy(null, pc) && !gpu.TestSatisifiedBy(null, pc), "One step below either target fails the native quest check.");
        Console.WriteLine("Native calculator job targets: " + checks + " assertions passed.");
    }
}
