using System;
using System.Collections.Generic;
using System.Reflection;

namespace PCBSCalculatorHelper
{
    // Read native serialized goals, independently of email language and formatting.
    internal sealed class CalculatorJobTargets
    {
        internal int Score, CpuMhz, GpuMhz;
        internal bool Supported { get { return Score > 0 || CpuMhz > 0 || GpuMhz > 0; } }
        internal static CalculatorJobTargets Read(IEnumerable<Objective> objectives)
        {
            CalculatorJobTargets result = new CalculatorJobTargets();
            if (objectives == null) return result;
            foreach (Objective objective in objectives)
            {
                if (objective == null || objective.IsInvalid()) continue;
                BenchmarkScoreObjective benchmark = objective as BenchmarkScoreObjective;
                if (benchmark != null) result.Score = Math.Max(result.Score, benchmark.GetTargetScore());
                OverclockObjective overclock = objective as OverclockObjective;
                if (overclock == null) continue;
                FieldInfo field = typeof(OverclockObjective).GetField("m_targetClockspeed", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                int target = field == null ? 0 : (int)field.GetValue(overclock);
                // Esports percentages become MHz when the game finalizes the objective.
                // Reading must never finalize or otherwise change a customer's job.
                if (target <= 0) continue;
                Objective.Type type = objective.GetObjectiveType();
                if (type == Objective.Type.OVERCLOCK_CPU || type == Objective.Type.ESPORTS_DLC_OVERCLOCK_CPU_MAX_PERCENT)
                    result.CpuMhz = Math.Max(result.CpuMhz, target);
                if (type == Objective.Type.OVERCLOCK_GPU || type == Objective.Type.ESPORTS_DLC_OVERCLOCK_GPU_MAX_PERCENT)
                    result.GpuMhz = Math.Max(result.GpuMhz, target);
            }
            return result;
        }
    }
}
