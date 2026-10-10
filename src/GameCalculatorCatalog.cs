using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Collections;
using System.Text.RegularExpressions;
using System.IO;
using BepInEx;
using UnityEngine;
using ZenFulcrum.EmbeddedBrowser;

namespace PCBSCalculatorHelper
{
    // Catalog is read from the user's loaded game. No third-party calculator data is shipped.
    internal static class GameCalculatorCatalog
    {
        private static readonly Dictionary<string, JSONNode> slotNodes = new Dictionary<string, JSONNode>();
        private static readonly List<PartDesc> slotParts = new List<PartDesc>();
        private static string CachePath { get { return Path.Combine(Path.Combine(Paths.ConfigPath, "PCBSHelper"), "game-geometry-cache.json"); } }
        private static string Fingerprint()
        {
            string key = "geometry-v2";
            foreach (string path in new string[] { typeof(PartDesc).Assembly.Location, Path.Combine(Application.dataPath, "sharedassets1.assets"), Path.Combine(Application.dataPath, "resources.assets") })
            { FileInfo file = new FileInfo(path); if (file.Exists) key += "|" + file.Length + ":" + file.LastWriteTimeUtc.Ticks; }
            return key;
        }
        private static JSONNode Obj() { return new JSONNode(JSONNode.NodeType.Object); }
        private static JSONNode Arr() { return new JSONNode(JSONNode.NodeType.Array); }
        private static object Field(object value, string name)
        { return value.GetType().GetField(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic).GetValue(value); }
        private static int Int(object value, string name) { return Convert.ToInt32(Field(value, name), CultureInfo.InvariantCulture); }
        private static float Float(object value, string name) { return Convert.ToSingle(Field(value, name), CultureInfo.InvariantCulture); }
        private static bool Bool(object value, string name) { return (bool)Field(value, name); }
        internal static string Name(PartDesc part)
        {
            string name = (part.m_manufacturer ?? "") + " " + (part.m_partName ?? "");
            PartDescRAM ram = part as PartDescRAM;
            if (ram != null) name += " " + ram.m_sizeGb + " GB " + ram.m_speedMhz + " MHz";
            return Regex.Replace(name, "\\s+", " ").Trim();
        }
        internal static string Socket(CpuSocket socket)
        { return socket == CpuSocket.LGA_1151_KABY || socket == CpuSocket.LGA_1151_SKY ? "LGA_1151_SKY_KABY" : socket.ToString(); }
        internal static JSONNode Read()
        {
            if (!PartsDatabase.IsReady) throw new InvalidOperationException("Load the workshop before opening the calculator.");
            JSONNode catalog = Obj(), constants = Obj();
            slotNodes.Clear(); slotParts.Clear();
            Dictionary<string, JSONNode> cached = new Dictionary<string, JSONNode>();
            try
            {
                JSONNode cache = JSONNode.Parse(File.ReadAllText(CachePath));
                if ((string)cache["fingerprint"] == Fingerprint() && cache["slots"].Type == JSONNode.NodeType.Object)
                    cached = (Dictionary<string, JSONNode>)cache["slots"];
            }
            catch (Exception) { }
            catalog["constants"] = constants;
            constants["minCpu"] = OverclockConstants.s_min3DMarkCPU;
            constants["minGpu"] = OverclockConstants.s_min3DMarkGPU;
            constants["minFps"] = OverclockConstants.s_min3DMarkFPS;
            foreach (string group in new string[] { "cpus", "gpus", "mobos", "pcCases", "rams", "storages" }) catalog[group] = Obj();
            foreach (PartDesc part in PartsDatabase.GetAllParts())
            {
                if (part == null || part.m_class == PartDesc.Class.DUMMY || part.m_isPlatformLocked || (part.m_isDLC && !part.m_isDLCInstalled)) continue;
                PartDescCPU cpu = part as PartDescCPU; PartDescGPU gpu = part as PartDescGPU;
                PartDesc mobo = part.GetType().Name == "PartDescMotherboard" ? part : null;
                PartDesc pcCase = part.GetType().Name == "PartDescCase" ? part : null;
                PartDescRAM ram = part as PartDescRAM;
                PartDesc disk = part.GetType().Name == "PartDescStorage" ? part : null;
                string group = cpu != null ? "cpus" : gpu != null ? "gpus" : mobo != null ? "mobos" : pcCase != null ? "pcCases" : ram != null ? "rams" : disk != null ? "storages" : null;
                if (group == null) continue;
                JSONNode item = Obj(); string name = Name(part);
                item["id"] = part.m_id; item["fullName"] = name; item["manufacturer"] = part.m_manufacturer ?? "";
                item["displayName"] = Regex.Replace(part.m_uiName ?? name, "<[^>]*>", "");
                item["price"] = part.m_price; item["sellPrice"] = part.m_sellPrice;
                item["level"] = part.m_levelUnlock; item["kudos"] = part.m_kudosUnlock;
                item["inShop"] = part.m_inShop ? "Yes" : "No";
                // Availability comes from CareerStatus, including XP within a level and DLC ownership.
                CareerStatus career = CareerStatus.Get();
                item["unlocked"] = career == null || career.IsPartUnlocked(part.m_id);
                if (cpu != null)
                {
                    item["socket"] = Socket(cpu.m_socket); item["baseMhz"] = cpu.m_freqMhz;
                    item["maxChannels"] = cpu.m_maxMemoryChannels; item["defaultRamMhz"] = cpu.m_defaultMemorySpeed;
                    item["watts"] = cpu.m_wattage; item["canOverclock"] = cpu.m_canOverclock ? "Yes" : "No";
                    JSONNode coefficients = Arr(); coefficients.Add(Float(cpu, "m_coefCoreClock")); coefficients.Add(Float(cpu, "m_coefMemChannel"));
                    coefficients.Add(Float(cpu, "m_coefMemClock")); coefficients.Add(Float(cpu, "m_adjustment")); item["coefficients"] = coefficients;
                }
                if (gpu != null)
                {
                    item["chipset"] = gpu.m_chipSet; item["vendor"] = gpu.m_chipSetBrand; item["vram"] = gpu.m_vramGb;
                    item["baseMhz"] = gpu.m_coreClockFreq; item["memoryMhz"] = gpu.m_memClockFreq;
                    item["watts"] = gpu.m_wattage; item["length"] = gpu.m_length; item["slots"] = gpu.m_slotSize;
                    item["multiGpu"] = gpu.m_doubleGPU; item["sli"] = gpu.m_useSLI;
                    item["gpuType"] = part.m_type == PartDesc.Type.WATER_COOLED_GPU ? "Water" : "Air";
                    JSONNode tests = Arr();
                    foreach (object coef in (IEnumerable)Field(gpu, "m_testScores"))
                    { JSONNode c = Arr(); c.Add(Float(coef, "m_coreClock")); c.Add(Float(coef, "m_memClock")); c.Add(Float(coef, "m_adjustment")); tests.Add(c); }
                    item["tests"] = tests;
                }
                if (mobo != null)
                {
                    item["socket"] = Socket((CpuSocket)Field(mobo, "m_socket")); item["size"] = Field(mobo, "m_size").ToString(); item["ramType"] = Field(mobo, "m_ramType").ToString();
                    item["canOverclock"] = Bool(mobo, "m_canOverclock") ? "Yes" : "No"; item["sli"] = Bool(mobo, "m_supportSLI"); item["crossfire"] = Bool(mobo, "m_supportCrossfire");
                    JSONNode speeds = Arr(); foreach (int speed in (IEnumerable)Field(mobo, "m_memorySpeeds")) speeds.Add(speed); item["memorySpeeds"] = speeds;
                    AddSlots(part, item, cached);
                }
                if (pcCase != null)
                {
                    JSONNode sizes = Arr(); foreach (object size in (IEnumerable)Field(pcCase, "m_motherboardTypes")) sizes.Add(size.ToString());
                    item["sizes"] = sizes; item["maxGpuLength"] = Int(pcCase, "m_maxGPULength"); AddSlots(part, item, cached);
                }
                if (ram != null) { item["sizeGb"] = ram.m_sizeGb; item["speedMhz"] = ram.m_speedMhz; item["ramType"] = ram.m_ramType.ToString(); }
                if (disk != null) { item["sizeGb"] = Int(disk, "m_sizeGb"); item["heatsink"] = Float(disk, "m_heatsinkThickness"); item["storageType"] = part.m_type.ToString(); }
                // IDs disambiguate otherwise identical names; do not silently overwrite a model.
                if (((Dictionary<string, JSONNode>)catalog[group]).ContainsKey(name)) name += " [" + part.m_id + "]";
                item["fullName"] = name; catalog[group][name] = item;
            }
            catalog["geometryReady"] = slotParts.Count == 0;
            return catalog;
        }
        private static readonly string[] GeometryFields = { "ramSlots", "gpuSlots", "m2Slots", "gpuPositions", "m2Heatsinks", "pciSlots", "useCaseGpuSlots" };
        private static void AddSlots(PartDesc part, JSONNode item, Dictionary<string, JSONNode> cached)
        {
            JSONNode geometry;
            if (cached.TryGetValue(part.m_id, out geometry))
            { foreach (string field in GeometryFields) item[field] = geometry[field]; }
            else
            {
                item["ramSlots"] = 0; item["gpuSlots"] = 0; item["m2Slots"] = 0; item["pciSlots"] = 0;
                item["gpuPositions"] = Arr(); item["m2Heatsinks"] = Arr(); item["useCaseGpuSlots"] = false;
                slotParts.Add(part);
            }
            slotNodes[part.m_id] = item;
        }
        internal static IEnumerator LoadGeometry(JSONNode catalog)
        {
            foreach (PartDesc part in slotParts) { ReadSlots(part, slotNodes[part.m_id]); yield return null; }
            catalog["geometryReady"] = true;
            JSONNode cache = Obj(), slots = Obj(); cache["fingerprint"] = Fingerprint(); cache["slots"] = slots;
            foreach (KeyValuePair<string, JSONNode> entry in slotNodes)
            { JSONNode g = Obj(); foreach (string field in GeometryFields) g[field] = entry.Value[field]; slots[entry.Key] = g; }
            try { Directory.CreateDirectory(Path.GetDirectoryName(CachePath)); File.WriteAllText(CachePath, cache.AsJSON); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        internal static JSONNode Availability()
        {
            JSONNode result = Obj(); CareerStatus career = CareerStatus.Get();
            foreach (PartDesc part in PartsDatabase.GetAllParts()) result[part.m_id] = career == null || career.IsPartUnlocked(part.m_id);
            return result;
        }
        private static void ReadSlots(PartDesc part, JSONNode item)
        {
            int ram = 0, gpu = 0, m2 = 0, pci = 0, covers = 0;
            JSONNode gpuSlots = Arr(), m2Heatsinks = Arr();
            foreach (Slot slot in PartsDatabase.GetSlots(part))
            {
                if (slot.m_slotType == Slot.SlotType.RAM) ram++;
                if (slot.m_slotType == Slot.SlotType.PCI_COVER) covers++;
                if ((int)slot.m_slotType == 9)
                { gpu++; gpuSlots.Add(slot.firstSlotOccupied); pci = Math.Max(pci, slot.firstSlotOccupied + Math.Max(1, slot.m_pciSlotsCovered)); }
                if (slot.m_slotType == Slot.SlotType.M2) { m2++; m2Heatsinks.Add(slot.maxPartThicknessAllowed); }
            }
            item["ramSlots"] = ram; item["gpuSlots"] = gpu; item["m2Slots"] = m2;
            item["gpuPositions"] = gpuSlots; item["m2Heatsinks"] = m2Heatsinks; item["pciSlots"] = covers > 0 ? covers : pci;
            if (part.GetType().Name == "PartDescCase")
            {
                Case shape = PartsDatabase.GetPrefab(part).GetComponent<Case>();
                item["useCaseGpuSlots"] = shape != null && Bool(shape, "m_disallowMotherboardGPUSlots");
            }
        }
    }
}
