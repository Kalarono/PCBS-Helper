using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using ZenFulcrum.EmbeddedBrowser;

namespace PCBSCalculatorHelper
{
    internal static class JobCalculatorImport
    {
        internal static bool Russian
        { get { return HelperLocalization.Russian; } }
        private static string Clean(string text) { return Regex.Replace(text ?? "", "<[^>]*>", ""); }
        private static object Field(object obj, string name)
        { FieldInfo field = AccessTools.Field(obj.GetType(), name); return field == null ? null : field.GetValue(obj); }
        private static int IntField(object obj, string name)
        { object value = Field(obj, name); return value is int ? (int)value : 0; }
        private static string Name(PartInstance instance)
        { return instance == null || instance.IsProxy() ? "" : Clean(instance.GetPart().m_uiName); }
        private static JSONNode Obj() { return new JSONNode(JSONNode.NodeType.Object); }
        private static JSONNode Arr() { return new JSONNode(JSONNode.NodeType.Array); }
        private static void Add(JSONNode array, string value) { array.Add(value); }
        public static int Target(Job job)
        {
            int target = 0;
            if (job != null) foreach (Objective objective in job.GetObjectives())
            {
                BenchmarkScoreObjective score = objective as BenchmarkScoreObjective;
                if (score != null && !score.IsInvalid()) target = Math.Max(target, score.GetTargetScore());
            }
            return target;
        }
        public static JSONNode Read(Job job)
        {
            if (job == null || Target(job) <= 0) throw new InvalidOperationException("This job has no 3DMark score objective.");
            bool ru = Russian;
            bool build = job.GetJobType() == JobDesc.Type.BUILD || job.GetJobType() == JobDesc.Type.DLC1_FAKE_BUILD;
            ComputerSave pc = build ? null : job.GetComputer();
            JSONNode result = Obj(), system = Obj(), notes = Arr(), requirements = Arr(), brands = Arr(), fields = Obj();
            result["tab"] = build ? "divBuildMaker" : "divBuildUpgrader";
            result["jobId"] = job.GetId(); result["title"] = Clean(job.GetSubject()); result["ru"] = ru;
            result["budget"] = job.GetBudget(); result["target"] = Target(job);
            result["system"] = system; result["notes"] = notes; result["requirements"] = requirements;
            result["brands"] = brands; result["fields"] = fields;
            fields["cpuBrand"] = "Any"; fields["gpuBrand"] = "Any";
            fields["gpuCount"] = "Any"; fields["gpuType"] = "Any";
            fields["cpuOverclock"] = false;
            if (!build && pc == null)
                Add(notes, ru ? "Исходный ПК сейчас недоступен: заполните его характеристики вручную." : "The current PC is unavailable; enter its hardware manually.");
            if (pc != null)
            {
                system["pcCase"] = Name(pc.caseID); system["cpu"] = Name(pc.cpuID); system["mobo"] = Name(pc.motherboardID);
                system["cooler"] = Name(pc.cpuCoolerID); system["psu"] = Name(pc.psuID);
                JSONNode gpus = Arr(), m2s = Arr(), ram = Arr(), disks = Arr();
                int sticks = 0, baseRamSpeed = 0;
                foreach (PartInstance part in pc.pciSlots ?? new PartInstance[0])
                    if (part != null && !part.IsProxy() && part.GetPart() is PartDescGPU) Add(gpus, Name(part));
                foreach (PartInstance part in pc.m2Slots ?? new PartInstance[0]) if (part != null && !part.IsProxy()) Add(m2s, Name(part));
                foreach (PartInstance part in pc.storageSlots ?? new PartInstance[0]) if (part != null && !part.IsProxy()) Add(disks, Name(part));
                foreach (PartInstance part in pc.ramSlots ?? new PartInstance[0])
                    if (part != null && !part.IsProxy())
                    {
                        PartDescRAM desc = part.GetPart() as PartDescRAM;
                        if (desc == null) continue;
                        ++sticks; Add(ram, Name(part));
                        baseRamSpeed = baseRamSpeed == 0 ? desc.m_speedMhz : Math.Min(baseRamSpeed, desc.m_speedMhz);
                    }
                system["gpus"] = gpus; system["m2s"] = m2s; system["ram"] = ram; system["disks"] = disks;
                system["ramSticks"] = sticks;
                system["ramSpeed"] = pc.m_biosConfig == null ? baseRamSpeed : pc.GetMemoryClock();
                if (pc.m_biosConfig != null)
                { system["cpuMhz"] = pc.m_biosConfig.GetCPUSpeed(); system["gpuMhz"] = pc.m_biosConfig.m_gpuClock; system["gpuMemoryMhz"] = pc.m_biosConfig.m_gpuMemClock; }
                system["ramGb"] = pc.GetRAM();
                JSONNode otherParts = Arr();
                foreach (PartInstance instance in pc.GetAllParts())
                {
                    if (instance == null || instance.IsProxy()) continue;
                    PartDesc part = instance.GetPart();
                    if (part is PartDescCPU || part is PartDescGPU || part.m_type == PartDesc.Type.CASE || part.m_type == PartDesc.Type.MOTHERBOARD) continue;
                    JSONNode item = Obj(); item["manufacturer"] = part.m_manufacturer; item["fullName"] = Clean(part.m_uiName);
                    otherParts.Add(item);
                }
                system["otherParts"] = otherParts;
                if (gpus.Count > 1 && (string)gpus[0] != (string)gpus[1])
                    Add(notes, ru ? "Две разные модели GPU: Build Upgrader умеет считать только одинаковые карты. Проверьте исходные GPU вручную." : "Different GPU models: Build Upgrader models identical cards only. Review the original GPUs manually.");
                if (m2s.Count > 1)
                    Add(notes, ru ? "В ПК несколько M.2: калькулятор учитывает первый, совместимость остальных проверьте отдельно." : "Several M.2 drives are installed; the calculator checks the first. Review compatibility of the others.");
                if (baseRamSpeed > 0 && pc.m_biosConfig != null && pc.GetMemoryClock() != baseRamSpeed)
                    Add(notes, ru ? "Частоты перенесены из текущего BIOS. Расчёт предполагает стабильную работу без перегрева; стабильность разгона проверьте в игре." : "Clocks were imported from the current BIOS. Scores assume stable hardware without thermal throttling; verify overclock stability in the game.");
            }
            foreach (Objective objective in job.GetObjectives())
            {
                if (objective.IsInvalid()) continue;
                if (objective.IsHiddenByDiagnose() && !DebugVars.s_showDiagnoseObjectives && !DebugVars.s_showHiddenObjectives) continue;
                Add(requirements, Clean(objective.GetDescription(job)));
                if (objective is DoubleGPUObjective) fields["gpuCount"] = "2";
                Objective.Type typeOfGoal = objective.GetObjectiveType();
                if (typeOfGoal == Objective.Type.CWC_GPU || typeOfGoal == Objective.Type.CWC_CPU_AND_GPU) fields["gpuType"] = "Water";
                if (typeOfGoal == Objective.Type.OVERCLOCK_CPU) fields["cpuOverclock"] = true;
                if (objective is InstallPartObjective)
                {
                    string id = Field(objective, "m_partId") as string;
                    PartDesc part = String.IsNullOrEmpty(id) ? null : PartsDatabase.GetDesc(id, null);
                    if (part != null)
                    {
                        string key = part is PartDescCPU ? "fixedCpu" : part is PartDescGPU ? "fixedGpu" :
                            part.m_type == PartDesc.Type.CASE ? "fixedCase" : part.m_type == PartDesc.Type.MOTHERBOARD ? "fixedMobo" :
                            part is PartDescRAM ? "fixedRam" : "";
                        if (key != "") fields[key] = Clean(part.m_uiName);
                        if (part is PartDescGPU && IntField(objective, "m_numberToInstall") == 2) fields["gpuCount"] = "2";
                    }
                }
                if (objective is UseBrandObjective || objective is FuturLab.EsportsAtLeastXBrandPartsObjective)
                {
                    string brand = Field(objective, "m_manufacturer") as string;
                    JSONNode requirement = Obj(); requirement["brand"] = brand ?? "";
                    requirement["count"] = Math.Max(1, IntField(objective, objective is UseBrandObjective ? "m_numberOfParts" : "m_numParts"));
                    requirement["newOnly"] = objective is UseBrandObjective;
                    brands.Add(requirement);
                    if (String.Equals(brand, "Intel", StringComparison.OrdinalIgnoreCase)) fields["cpuBrand"] = "Intel";
                }
                if (objective is FuturLab.EsportsPartMustBeBrandObjective)
                {
                    string type = Convert.ToString(Field(objective, "m_partType"), CultureInfo.InvariantCulture);
                    string brand = Field(objective, "m_manufacturer") as string;
                    if (type == "CPU") fields["cpuBrand"] = brand ?? "Any";
                    if (type == "GPU") fields["gpuBrand"] = brand ?? "Any";
                }
            }
            Add(notes, ru
                ? "Перенесены данные задания. Нажмите Get Builds / Get Upgrades, когда будете готовы. Калькулятор подбирает CPU/GPU/плату, но не выполняет задания по ремонту, накопителям, охлаждению или разгону — все условия перечислены ниже."
                : "Job data imported. Run Get Builds / Get Upgrades when ready. The calculator selects CPU/GPU/motherboard combinations; repair, storage, cooling and overclock objectives still need your review. All objectives are listed below.");
            if (build) Add(notes, ru ? "Reserved Budget = 400 — стандартный резерв калькулятора на БП, охлаждение и накопители. Измените его под выбранные детали." : "Reserved Budget = 400 is the calculator's default allowance for PSU, cooling and storage. Adjust it for your parts.");
            return result;
        }
    }
}
