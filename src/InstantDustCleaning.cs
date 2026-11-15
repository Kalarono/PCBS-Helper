using System;
using System.Reflection;
using HarmonyLib;

namespace PCBSCalculatorHelper
{
    internal static class ComponentDustInstantCleanPatch
    {
        internal static void Install(Harmony harmony)
        {
            MethodInfo target = AccessTools.Method(typeof(ComponentDust), "ReducePartInstanceDust", new Type[] { typeof(float) });
            if (target == null) throw new MissingMethodException(typeof(ComponentDust).FullName, "ReducePartInstanceDust(float)");
            harmony.Patch(target, new HarmonyMethod(typeof(ComponentDustInstantCleanPatch), "Prefix"));
        }

        private static void Prefix(ref float __0)
        {
            // The native method subtracts this amount from filth, clamps to zero,
            // and runs its normal one-time completion path.
            __0 = float.MaxValue;
        }
    }
}
