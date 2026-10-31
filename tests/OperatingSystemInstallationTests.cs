using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.Serialization;
using HarmonyLib;
using PCBSCalculatorHelper;

internal static class OperatingSystemInstallationEntry
{
    private static void Main(string[] args)
    {
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs ev) {
            string filename = new AssemblyName(ev.Name).Name + ".dll";
            foreach (string directory in args)
            {
                string path = Path.Combine(directory, filename);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        Assembly.GetExecutingAssembly().GetType("OperatingSystemInstallationTests").GetMethod("Run").Invoke(null, null);
    }
}

internal static class OperatingSystemInstallationTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    public static void Run()
    {
        OperatingSystemInstallation.Enabled = false;
        Check(OperatingSystemInstallation.Duration(60) == 60, "Disabled option keeps the native installation duration.");
        Check(OperatingSystemInstallation.Duration(13) == 13, "Disabled option does not hard-code a replacement duration.");
        OperatingSystemInstallation.Enabled = true;
        Check(OperatingSystemInstallation.Duration(60) == 0, "The shared application option removes the installation wait.");
        OperatingSystemInstallation.Enabled = false;
        Check(OperatingSystemInstallation.Duration(60) == 60, "Turning the option off restores normal installation for the next boot.");
        ComputerSave pc = (ComputerSave)FormatterServices.GetUninitializedObject(typeof(ComputerSave));
        pc.usbSlots = new PartInstance[0];
        Check(!OperatingSystemInstallation.InstallerAvailable(pc), "With the option off, a PC without USB keeps the native missing-installer result.");
        OperatingSystemInstallation.Enabled = true;
        Check(OperatingSystemInstallation.InstallerAvailable(pc), "With the option on, the installer is available without inserting USB hardware.");
        Check(pc.usbSlots.Length == 0 && !pc.IsUSBDriveInserted(), "The feature never adds a fake USB drive or changes the physical USB state.");
        OperatingSystemInstallation.Enabled = false;

        MethodInfo usb = AccessTools.Method(typeof(ComputerSave), "IsUSBDriveInserted");
        MethodInfo other = AccessTools.Method(typeof(ComputerSave), "GetBiosConfig");
        CodeInstruction usbCall = new CodeInstruction(OpCodes.Callvirt, usb), otherCall = new CodeInstruction(OpCodes.Callvirt, other);
        CodeInstruction[] boot = OperatingSystemInstallation.AllowInstallerWithoutUsb(new[] { usbCall, otherCall }).ToArray();
        Check(boot.Length == 2 && boot[0].opcode == OpCodes.Call && ((MethodInfo)boot[0].operand).Name == "InstallerAvailable",
            "Only the installer's USB check is rewritten to consult the shared toggle.");
        Check(Object.ReferenceEquals(boot[1].operand, other), "Other BIOS and hardware calls are retained.");

        MethodInfo native = AccessTools.EnumeratorMoveNext(AccessTools.Method(typeof(VirtualComputer), "OSInstallProcess"));
        Check(native != null && native.Name == "MoveNext", "Harmony resolves the actual game's installer coroutine.");
        FieldInfo duration = native.DeclaringType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Single(f => f.Name.StartsWith("<duration>") && f.FieldType == typeof(float));
        CodeInstruction store = new CodeInstruction(OpCodes.Stfld, duration);
        Label entry = new DynamicMethod("OSInstallLabel", typeof(void), Type.EmptyTypes).GetILGenerator().DefineLabel();
        store.labels.Add(entry);
        CodeInstruction untouched = new CodeInstruction(OpCodes.Ret);
        CodeInstruction[] original = { new CodeInstruction(OpCodes.Ldarg_0), new CodeInstruction(OpCodes.Ldc_R4, 60f), store, untouched };
        CodeInstruction[] changed = OperatingSystemInstallation.ShortenDuration(original).ToArray();
        Check(changed.Length == 5 && changed[2].opcode == OpCodes.Call, "Exactly one call is inserted before the native duration store.");
        Check(((MethodInfo)changed[2].operand).Name == "Duration", "The inserted call uses the shared option's policy.");
        Check(changed[2].labels.Contains(entry) && store.labels.Count == 0, "A branch to the store also goes through the policy.");
        Check(Object.ReferenceEquals(changed[3], store) && Object.ReferenceEquals(changed[4], untouched), "Native storage and completion instructions remain intact.");
        bool refused = false;
        try { OperatingSystemInstallation.ShortenDuration(new[] { untouched }).ToArray(); }
        catch (InvalidOperationException) { refused = true; }
        Check(refused, "An incompatible installer is rejected instead of silently patching another stage.");
        Console.WriteLine("Fast OS installation: " + checks + " assertions passed.");
    }
}
