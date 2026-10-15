using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Mono.Cecil;
using Mono.Cecil.Cil;

// Inspect the installed game's metadata and IL without initializing Unity or a save.
internal static class WorkbenchGameContractTests
{
    private static int checks;
    private static void Check(bool value, string message) { checks++; if (!value) throw new Exception(message); }
    private static TypeDefinition Type(ModuleDefinition module, string name)
    { TypeDefinition type = module.Types.FirstOrDefault(x => x.Name == name); Check(type != null, "Missing game type: " + name); return type; }
    private static MethodDefinition Method(TypeDefinition type, string name, params string[] args)
    {
        MethodDefinition method = type.Methods.FirstOrDefault(x => x.Name == name && x.Parameters.Select(p => p.ParameterType.FullName).SequenceEqual(args));
        Check(method != null, "Missing game method/signature: " + type.Name + "." + name); return method;
    }
    private static void Field(TypeDefinition type, string name, string fieldType)
    { Check(type.Fields.Any(x => x.Name == name && x.FieldType.FullName == fieldType), "Missing game field: " + type.Name + "." + name); }
    private static void Main(string[] args)
    {
        using (ModuleDefinition game = ModuleDefinition.ReadModule(Path.Combine(args[0], "Assembly-CSharp-firstpass.dll")))
        {
            TypeDefinition work = Type(game, "WorkingOnComputerState");
            Method(work, "HandleInput"); Method(work, "DisassemblyTick"); Method(work, "SetDisassemblyMode"); Method(work, "HandleExit");
            Method(work, "AssemblyTick"); Method(work, "OnStateEnter"); Method(work, "ClearPlaceholders");
            Method(work, "SetAssemblyMode"); Method(work, "PopulatePlaceholders", "Slot[]"); Method(work, "TickPopulatePlaceholders");
            Field(work, "m_pendingSlotsForPlaceholders", "System.Collections.Generic.List`1<Slot>");
            Field(work, "m_mode", "WorkingOnComputerState/Mode");
            TypeDefinition basis = Type(game, "WorkingOnComputerStateBase");
            Field(basis, "m_case", "Case"); Field(basis, "m_camera", "UnityEngine.Camera"); Field(basis, "m_selected", "ISelectable");
            TypeDefinition install = Type(game, "InstallingPartState");
            Method(install, "Tick"); Method(install, "OnStateEnter"); Method(install, "HandleExit");
            Method(install, "DoScrewsStep", "System.Collections.Generic.List`1<Screw>", "System.Collections.Generic.List`1<Screw>");
            Field(install, "m_dir", "System.Single"); Field(install, "m_part", "PartInstance"); Field(install, "m_slot", "Slot");
            Method(install, "OnStateDestroy");
            Field(install, "m_auto", "System.Boolean"); Field(install, "m_autoStandoff", "System.Boolean"); Field(install, "m_oneClickScrew", "System.Boolean");
            Method(install, "UninstallState", "Case", "UnityEngine.Camera", "ComponentPC");
            TypeDefinition controller = Type(game, "GameController");
            Field(controller, "m_currentState", "State"); Method(controller, "SetCableType", "PartDesc");
            Method(controller, "get_selectedItem");
            MethodDefinition workEnter = Method(work, "OnStateEnter");
            Check(workEnter.Body.Instructions.Any(i => i.Operand is MethodReference &&
                ((MethodReference)i.Operand).Name == "SetCableType"), "A selected inventory cable is processed on workbench entry.");
            MethodDefinition cableMode = Method(work, "SetCablingMode");
            Check(cableMode.Body.Instructions.Any(i => i.Operand is MethodReference &&
                ((MethodReference)i.Operand).Name == "set_selectedItem"), "Capture cable selection before native cabling mode clears it.");
            TypeDefinition cable = Type(game, "CableInstance");
            Field(cable, "m_playerChoice", "System.Boolean"); Method(cable, "Update"); Method(cable, "Uninstall");
            Method(cable, "SnapToInstalled"); Method(cable, "GetCableEnd");
            TypeDefinition mesh = Type(game, "CableMesh");
            Field(mesh, "m_desc", "CableDesc"); Field(mesh, "m_startRigidity", "System.Single"); Field(mesh, "m_endRigidity", "System.Single");
            Field(mesh, "m_subTransforms", "UnityEngine.Transform[]"); Field(mesh, "m_meshFilter", "UnityEngine.MeshFilter"); Field(mesh, "m_mesh", "UnityEngine.Mesh");
            Field(mesh, "m_boxColliders", "UnityEngine.BoxCollider[]");
            Method(mesh, "Init", "CableDesc", "CableVisuals", "UnityEngine.Transform[]", "System.Boolean", "System.Single", "System.Single", "System.Boolean");
            Method(mesh, "RegenerateMesh");
            Check(!mesh.Methods.Any(x => x.Name == "OnDestroy"), "Staging mesh destruction must not destroy the transferred mesh.");
            Field(Type(game, "PowerConnector"), "m_overrideCableVisuals", "System.String");
            TypeDefinition outline = game.Types.SelectMany(x => x.NestedTypes.Concat(new[] { x })).First(x => x.FullName == "cakeslice.CustomOutline");
            Field(outline, "originalMaterials", "UnityEngine.Material[]");
            TypeDefinition component = Type(game, "ComponentPC");
            Field(component, "m_proxy", "ComponentPC"); Field(component, "subslots", "Slot[]"); Method(component, "CanBeUninstalled");
            TypeDefinition slot = Type(game, "Slot");
            Field(slot, "m_dynamicBlockedBy", "System.Collections.Generic.List`1<ComponentPC>");
            Field(slot, "m_dynamicComponentCollidesWith", "System.Collections.Generic.List`1<ComponentPC>");
            Method(slot, "CanFillWithCurrentItem"); Method(slot, "get_blocked");
            Method(slot, "CurrentBlockers", "System.Boolean", "System.Boolean");
            MethodDefinition aioPlacement = Method(slot, "HandleRadiator", "ComponentPC", "Radiator", "System.Boolean");
            Check(aioPlacement.Body.Instructions.Any(x => x.Operand is MethodReference && ((MethodReference)x.Operand).Name == "SetProxy"),
                "AIO installation uses a proxy in the CPU cooler slot, rather than installing in that slot.");
            Method(Type(game, "Motherboard"), "get_cpuSlot");
            TypeDefinition pin = Type(game, "Pin");
            Method(pin, "IsBlocked"); Method(pin, "PinAnimation", "System.Boolean"); Field(pin, "m_BlockedByClosedPins", "CBlocker[]");
            foreach (string socketType in new[] { "AM3Slot", "LGASlot" })
            {
                TypeDefinition socket = Type(game, socketType);
                Check(socket.BaseType.FullName == "CPin", socketType + " uses a separate native CPin implementation.");
                Field(socket, "m_blockedBy", "CBlocker[]");
                MethodDefinition blocked = Method(socket, "IsBlocked");
                Check(blocked.Body.Instructions.Any(x => x.Operand is FieldReference && ((FieldReference)x.Operand).Name == "m_blockedBy") &&
                    blocked.Body.Instructions.Any(x => x.Operand is MethodReference && ((MethodReference)x.Operand).Name == "IsBlocking"),
                    socketType + " refuses to toggle while an installed cooler blocks it.");
                Method(socket, "TogglePin"); Method(socket, "IsAnimating");
                if (socketType == "AM3Slot") { Method(socket, "OpenSocket"); Method(socket, "CloseSocket"); }
                else Method(socket, "Update");
            }
            TypeDefinition ram = Type(game, "RAMSlot");
            Check(ram.BaseType.FullName == "CBlocker", "RAM blockers are compound CBlockers, not CPin or Slot.");
            Field(ram, "pin1", "UnityEngine.GameObject"); Field(ram, "pin2", "UnityEngine.GameObject");
            MethodDefinition ramBlocking = Method(ram, "IsBlocking", "Slot");
            foreach (string pinField in new[] { "pin1", "pin2" })
                Check(ramBlocking.Body.Instructions.Any(x => x.Operand is FieldReference &&
                    ((FieldReference)x.Operand).DeclaringType.Name == "RAMSlot" && ((FieldReference)x.Operand).Name == pinField),
                    "Native RAM blocking checks " + pinField + ".");
            TypeDefinition inventory = Type(game, "Inventory");
            Field(inventory, "m_itemsPendingCreation", "System.Collections.Generic.List`1<PartInstance>");
            Method(inventory, "UpdateInventory", "PartDesc/ShopCategory"); Method(inventory, "ShowInventory", "Case", "PeripheralSlot");
            Method(Type(game, "CPU"), "SetThermalPasteState", "System.Boolean");
            Method(Type(game, "PartInstance"), "GetResaleValue");
            MethodDefinition append = Method(Type(game, "CareerStatus"), "AddPartToInventory", "PartInstance");
            Check(append.Body.Instructions.Any(x => x.OpCode == OpCodes.Callvirt && x.Operand is MethodReference &&
                ((MethodReference)x.Operand).Name == "Add" && ((MethodReference)x.Operand).DeclaringType.FullName == "System.Collections.Generic.List`1<PartInstance>"),
                "Native inventory appends arrivals; price sorting must not mutate that list.");
        }
        using (ModuleDefinition helper = ModuleDefinition.ReadModule(args[1]))
        {
            TypeDefinition automation = Type(helper, "WorkbenchAutomation");
            Method(automation, "BeforeWorkEntered", "WorkingOnComputerState", "PartDesc&");
            Method(automation, "AfterWorkEntered", "WorkingOnComputerState", "PartDesc");
            MethodDefinition recolour = Method(Type(helper, "CableColourReplacement"), "Apply", "WorkingOnComputerState", "PartDesc");
            Check(!recolour.Body.Instructions.Any(i => i.OpCode == OpCodes.Ldfld && i.Operand is FieldReference &&
                ((FieldReference)i.Operand).Name == "m_playerChoice"), "Autoconnected cables must not be excluded by their creation history.");
            TypeDefinition removal = helper.Types.First(x => x.Name == "DependencyRemoval");
            MethodDefinition fromBlocker = Method(removal, "FromBlocker", "IBlocker", "Slot");
            Check(fromBlocker.Body.Instructions.Any(x => x.OpCode == OpCodes.Isinst && x.Operand is TypeReference &&
                ((TypeReference)x.Operand).Name == "RAMSlot"), "The compiled helper must recognize the compound RAM blocker.");
            foreach (string pinField in new[] { "pin1", "pin2" })
                Check(removal.NestedTypes.SelectMany(x => x.Methods).Where(x => x.HasBody).SelectMany(x => x.Body.Instructions).Any(x =>
                    x.Operand is FieldReference && ((FieldReference)x.Operand).DeclaringType.Name == "RAMSlot" &&
                    ((FieldReference)x.Operand).Name == pinField), "The compiled planner must expand " + pinField + ".");
            MethodDefinition pinBlocked = Method(removal, "IsPinBlocked", "CPin");
            MethodDefinition pinBlockers = Method(removal, "PinBlockers", "CPin");
            foreach (string socketType in new[] { "AM3Slot", "LGASlot" })
            {
                Check(pinBlocked.Body.Instructions.Any(x => x.OpCode == OpCodes.Isinst && x.Operand is TypeReference &&
                    ((TypeReference)x.Operand).Name == socketType), "Native blocker checks must cover " + socketType + ".");
                Check(pinBlockers.Body.Instructions.Any(x => x.Operand is FieldReference &&
                    ((FieldReference)x.Operand).DeclaringType.Name == socketType && ((FieldReference)x.Operand).Name == "m_blockedBy"),
                    "Cooler dependencies must be traversed for " + socketType + ".");
            }
            MethodDefinition ready = removal.Methods.First(x => x.Name == "Ready");
            Check(ready.Body.Instructions.Any(x => x.Operand is MethodReference && ((MethodReference)x.Operand).Name == "IsPinBlocked"),
                "Latch readiness must respect the native socket blocker check before toggling.");
            Method(automation, "BeforeAssembly", "WorkingOnComputerState");
            Method(automation, "AfterPinAnimation", "CPin", "System.Collections.IEnumerator&");
            Method(automation, "BeforeSocketAnimation", "LGASlot");
            MethodDefinition paste = Method(automation, "OnInstallEntered", "InstallingPartState");
            Check(paste.Body.Instructions.Any(x => x.Operand is MethodReference && ((MethodReference)x.Operand).Name == "get_cpuSlot"),
                "Thermal paste must target the installed CPU from its actual socket.");
            Check(paste.Body.Instructions.Any(x => x.Operand is MethodReference && ((MethodReference)x.Operand).Name == "IsThermalPastePart"),
                "The installation hook must use the part eligibility policy for CPU and AIO installs.");
        }
        // Exercise the packaged hook's part policy with the game's real enum,
        // without constructing Unity objects or touching the running game/save.
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs ev) {
            string filename = new AssemblyName(ev.Name).Name + ".dll";
            foreach (string directory in new[] { args[0], Path.GetFullPath(Path.Combine(args[0], "..", "..", "BepInEx", "core")) })
            {
                string path = Path.Combine(directory, filename);
                if (File.Exists(path)) return Assembly.LoadFrom(path);
            }
            return null;
        };
        System.Type automationType = Assembly.LoadFrom(args[1]).GetType("PCBSCalculatorHelper.WorkbenchAutomation");
        MethodInfo pasteEligibility = automationType.GetMethod("IsThermalPastePart", BindingFlags.Static | BindingFlags.NonPublic);
        System.Type nativePartType = pasteEligibility.GetParameters()[0].ParameterType;
        foreach (string cooling in new[] { "CPU", "AIR_COOLER", "LIQUID_COOLER", "CPU_BLOCK" })
            Check((bool)pasteEligibility.Invoke(null, new[] { Enum.Parse(nativePartType, cooling) }),
                "Paste is triggered by " + cooling + " regardless of its installation slot.");
        foreach (string unrelated in new[] { "CASE_FAN", "RADIATOR", "RESERVOIR", "GPU", "WATER_COOLED_GPU", "MOTHERBOARD", "None" })
            Check(!(bool)pasteEligibility.Invoke(null, new[] { Enum.Parse(nativePartType, unrelated) }),
                "Unrelated installation must not apply paste: " + unrelated + ".");
        Console.WriteLine("Installed game workbench API: " + checks + " assertions passed.");
    }
}
