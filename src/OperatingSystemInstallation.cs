using System;
using System.Collections.Generic;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;

namespace PCBSCalculatorHelper
{
    internal static class OperatingSystemInstallation
    {
        internal static bool Enabled;

        internal static void Install(Harmony harmony)
        {
            MethodInfo install = AccessTools.Method(typeof(VirtualComputer), "OSInstallProcess");
            MethodInfo moveNext = AccessTools.EnumeratorMoveNext(install);
            if (moveNext == null) throw new MissingMethodException("VirtualComputer.OSInstallProcess.MoveNext");
            harmony.Patch(moveNext, null, null, new HarmonyMethod(typeof(OperatingSystemInstallation), "ShortenDuration"));
            harmony.Patch(AccessTools.Method(typeof(VirtualComputer), "CheckBiosError"), null, null,
                new HarmonyMethod(typeof(OperatingSystemInstallation), "AllowInstallerWithoutUsb"));
        }

        internal static float Duration(float original) { return Enabled ? 0f : original; }
        internal static bool InstallerAvailable(ComputerSave computer)
        { return Enabled || computer.IsUSBDriveInserted(); }

        internal static IEnumerable<CodeInstruction> AllowInstallerWithoutUsb(IEnumerable<CodeInstruction> instructions)
        {
            MethodInfo inserted = AccessTools.Method(typeof(ComputerSave), "IsUSBDriveInserted");
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                if (instruction.Calls(inserted))
                {
                    // This changes only the installer's USB prerequisite. CPU,
                    // storage, RAM and keyboard checks keep their native branches.
                    instruction.opcode = OpCodes.Call;
                    instruction.operand = AccessTools.Method(typeof(OperatingSystemInstallation), "InstallerAvailable");
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1) throw new InvalidOperationException("Expected exactly one OS installer USB check.");
        }

        internal static IEnumerable<CodeInstruction> ShortenDuration(IEnumerable<CodeInstruction> instructions)
        {
            int replaced = 0;
            foreach (CodeInstruction instruction in instructions)
            {
                FieldInfo field = instruction.operand as FieldInfo;
                if (instruction.opcode == OpCodes.Stfld && field != null && field.FieldType == typeof(float) &&
                    field.Name.StartsWith("<duration>", StringComparison.Ordinal))
                {
                    // Keep the native installer and its completion callback. Only
                    // the duration changes, after the hardware checks have passed.
                    CodeInstruction duration = new CodeInstruction(OpCodes.Call,
                        AccessTools.Method(typeof(OperatingSystemInstallation), "Duration"));
                    duration.labels.AddRange(instruction.labels); instruction.labels.Clear();
                    duration.blocks.AddRange(instruction.blocks); instruction.blocks.Clear();
                    yield return duration;
                    replaced++;
                }
                yield return instruction;
            }
            if (replaced != 1) throw new InvalidOperationException("Expected exactly one OS installation duration.");
        }
    }
}
