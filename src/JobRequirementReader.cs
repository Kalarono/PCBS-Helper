using System;
using System.Collections.Generic;
using System.Reflection;

namespace PCBSCalculatorHelper
{
    // Read the actual objective parameters. Email prose is localized and can
    // mention existing hardware; brand-name text searches would guess incorrectly.
    internal static class JobRequirementReader
    {
        internal static List<JobRefinement> Read(Job job, IEnumerable<RequirementPart> catalogue)
        {
            if (job == null) return new List<JobRefinement>();
            return Read(job.GetObjectives(), catalogue,
                delegate(string id) { return String.IsNullOrEmpty(id) ? null : FromPart(PartsDatabase.GetDesc(id, null)); },
                delegate(Objective goal) { return goal.GetDescription(job); });
        }

        internal static List<JobRefinement> Read(IEnumerable<Objective> objectives, IEnumerable<RequirementPart> catalogue,
            Func<string, RequirementPart> lookup, Func<Objective, string> describe)
        {
            List<JobRefinement> result = new List<JobRefinement>();
            foreach (Objective goal in objectives)
            {
                if (goal == null || goal.IsInvalid()) continue;
                JobRefinement rule = null;
                if (goal is UseBrandObjective || goal is FuturLab.EsportsAtLeastXBrandPartsObjective || goal is FuturLab.EsportsPartMustBeBrandObjective)
                {
                    string brand = Convert.ToString(Field(goal, "m_manufacturer"));
                    if (String.IsNullOrEmpty(brand)) continue;
                    bool scoped = goal is FuturLab.EsportsPartMustBeBrandObjective;
                    rule = new JobRefinement { Kind = JobRefinementKind.Brand, Brand = brand,
                        Type = scoped ? PartType(Convert.ToString(Field(goal, "m_partType"))) : RequirementClarifications.BrandType(brand, catalogue),
                        Count = goal is FuturLab.EsportsAtLeastXBrandPartsObjective ? IntField(goal, "m_numParts") :
                            goal is UseBrandObjective ? Math.Max(1, IntField(goal, "m_numberOfParts")) : 1 };
                }
                else if (goal is InstallPartObjective)
                {
                    RequirementPart part = lookup(Convert.ToString(Field(goal, "m_partId")));
                    rule = new JobRefinement { Kind = JobRefinementKind.Part, Part = part, Type = part == null ? "" : part.Type,
                        Count = Math.Max(1, IntField(goal, "m_numberToInstall")), Exact = goal is FuturLab.EsportsInstallSpecificPartObjective };
                }
                else if (goal is RamObjective || goal is StorageObjective)
                    rule = new JobRefinement { Kind = goal is RamObjective ? JobRefinementKind.Ram : JobRefinementKind.Storage,
                        Target = IntField(goal, "m_target") };
                else if (IsRefinement(goal.GetObjectiveType()))
                    rule = new JobRefinement { Kind = JobRefinementKind.Other, ManualCpuGpu = ManualRecommendation(goal.GetObjectiveType(), goal) };
                // Hidden brand/parts/appearance preferences are included: they are
                // the clarifications conveyed in the customer's email. Diagnosis,
                // delivery and boot objectives are not hardware refinements.
                if (rule == null) continue;
                rule.Description = describe(goal);
                result.Add(rule);
            }
            return result;
        }

        private static bool IsRefinement(Objective.Type type)
        {
            switch (type)
            {
                case Objective.Type.ALL_NEW_PARTS: case Objective.Type.DOUBLE_GPU: case Objective.Type.DOUBLE_GPU_READY:
                case Objective.Type.AIO_WATER_COOLED: case Objective.Type.CABLES: case Objective.Type.INSTALL_LIGHTS:
                case Objective.Type.CASE_TEMP: case Objective.Type.CWC_CPU: case Objective.Type.CWC_GPU:
                case Objective.Type.BENCHMARK_SCORE: case Objective.Type.STABLE: case Objective.Type.NO_THERMAL_THROTTLE:
                case Objective.Type.ESPORTS_RUN_OCCT_WITHOUT_THROTTLE:
                case Objective.Type.CWC_CPU_AND_GPU: case Objective.Type.LOOP_COLOUR: case Objective.Type.REPLACE_COOLANT:
                case Objective.Type.OVERCLOCK_CPU: case Objective.Type.OVERCLOCK_GPU: case Objective.Type.UPGRADE_PART:
                case Objective.Type.ESPORTS_DLC_NO_LIGHTS: case Objective.Type.ESPORTS_DLC_LIGHTING_EFFECT:
                case Objective.Type.ESPORTS_DLC_HAS_SPECIFIC_PERIPHERAL: case Objective.Type.ESPORTS_DLC_REPLACE_PERIPHERAL:
                case Objective.Type.ESPORTS_DLC_CHANGE_LIGHTS: case Objective.Type.ESPORTS_DLC_OVERCLOCK_CPU_MAX_PERCENT:
                case Objective.Type.ESPORTS_DLC_OVERCLOCK_GPU_MAX_PERCENT: case Objective.Type.ESPORTS_COOLANT_COLOUR:
                case Objective.Type.ESPORTS_INSTALL_PIPES: case Objective.Type.ESPORTS_AT_LEAST_ONE_COLOURED_CABLE:
                case Objective.Type.ITSUPPORT_INSTALL_PART_RGB: case Objective.Type.ITSUPPORT_SET_PART_RGB_VALUE:
                case Objective.Type.ITSUPPORT_INSTALL_PSU_WATTS_VALUE: case Objective.Type.ITSUPPORT_INSTALL_HDD_SPEED_VALUE:
                case Objective.Type.ITSUPPORT_INSTALL_RANDOM_PART_OF_TYPE:
                    return true;
                default: return false;
            }
        }

        private static string ManualRecommendation(Objective.Type type, Objective goal)
        {
            switch (type)
            {
                case Objective.Type.DOUBLE_GPU: case Objective.Type.CWC_GPU: case Objective.Type.OVERCLOCK_GPU:
                case Objective.Type.ESPORTS_DLC_OVERCLOCK_GPU_MAX_PERCENT: return "GPU";
                case Objective.Type.CWC_CPU: case Objective.Type.OVERCLOCK_CPU:
                case Objective.Type.ESPORTS_DLC_OVERCLOCK_CPU_MAX_PERCENT: return "CPU";
                case Objective.Type.CWC_CPU_AND_GPU: case Objective.Type.BENCHMARK_SCORE: return "Both";
                case Objective.Type.UPGRADE_PART: return PartType(Convert.ToString(Field(goal, "m_partType")));
                default: return "";
            }
        }

        internal static RequirementPart FromPart(PartDesc part)
        {
            if (part == null) return null;
            PartDescCPU cpu = part as PartDescCPU;
            PartDescGPU gpu = part as PartDescGPU;
            return new RequirementPart { Id = part.m_id, Name = part.m_uiName, Manufacturer = part.m_manufacturer,
                Type = PartType(part.m_type.ToString()), NativeType = part.m_type.ToString(), Price = part.m_price,
                Score = cpu != null ? cpu.Test() : gpu != null ? gpu.Test() : 0,
                Quality = cpu != null ? cpu.Test() : gpu != null ? gpu.Test() : 0,
                Vram = gpu == null ? 0 : gpu.m_vramGb, MultiGpu = gpu != null && gpu.m_doubleGPU,
                Socket = cpu == null ? "" : cpu.m_socket.ToString(), Unlocked = true };
        }

        private static string PartType(string type) { return type == "WATER_COOLED_GPU" ? "GPU" : type; }
        private static int IntField(object obj, string name) { object value = Field(obj, name); return value is int ? (int)value : 0; }
        private static object Field(object obj, string name)
        {
            for (Type type = obj.GetType(); type != null; type = type.BaseType)
            {
                FieldInfo field = type.GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
                if (field != null) return field.GetValue(obj);
            }
            return null;
        }
    }
}
