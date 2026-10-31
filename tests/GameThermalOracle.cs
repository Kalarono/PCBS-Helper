using System;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Globalization;
using System.Collections.Generic;
using ZenFulcrum.EmbeddedBrowser;

// Exercise PCBS's own ComputerSave heat calculation, without starting Unity.
internal static class GameThermalOracle
{
    private const BindingFlags Flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
    private static string managed;
    private static float F(JSONNode p) { return Convert.ToSingle(p.Value, CultureInfo.InvariantCulture); }
    private static int N(JSONNode p) { return (int)F(p); }
    private static void Set(object target, string field, object value) { target.GetType().GetField(field, Flags).SetValue(target, value); }
    private static object Empty(Type type) { return FormatterServices.GetUninitializedObject(type); }
    private static PartInstance Instance(PartDesc part)
    {
        PartInstance instance = (PartInstance)Empty(typeof(PartInstance));
        Set(instance,"m_partDesc",part); Set(instance,"m_partId","thermal-fixture");
        Set(instance,"m_connected",true); Set(instance,"m_powered",true); Set(instance,"m_siliconLottery",1f);
        return instance;
    }
    private static void Main(string[] args)
    {
        managed=args[0];
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender, ResolveEventArgs e) {
            string path=Path.Combine(managed,new AssemblyName(e.Name).Name+".dll");
            return File.Exists(path)?Assembly.LoadFrom(path):null;
        };
        Run(args[1],args[2]);
    }
    private static void Run(string input, string output)
    {
        JSONNode db=JSONNode.Parse(File.ReadAllText(input)), result=new JSONNode(JSONNode.NodeType.Array);
        foreach (Type type in new Type[]{typeof(HeatConstants),typeof(OverclockConstants)})
            foreach(FieldInfo field in type.GetFields(BindingFlags.Static|BindingFlags.Public|BindingFlags.NonPublic))
            {
                JSONNode value=db["constants"]["heat"]["native"][field.Name];
                if(value.Type==JSONNode.NodeType.Number) field.SetValue(null,Convert.ChangeType(value.Value,field.FieldType,CultureInfo.InvariantCulture));
            }
        foreach(string cpuName in new string[]{"Intel Core i3-8350K","Intel Core i5-8600K","Intel Core i7-8700K","AMD Ryzen 7 Eight Core 1700","AMD Ryzen Threadripper Sixteen Core 1950X","Intel Core i7-7740X"})
        foreach(string coolerName in new string[]{"Mortoni Thermal 100","NZXT Kraken Z63","NZXT Kraken X72"})
        foreach(string gpuName in new string[]{"MSI Radeon R9 390 GAMING 8G","EVGA GeForce GTX 1070 FTW2 GAMING"})
        foreach(int count in new int[]{1,2})
        foreach(int delta in new int[]{0,50,274})
        {
            string caseName="SilverStone PS14 (Black + Window)";
            JSONNode c=db["cpus"][cpuName],g=db["gpus"][gpuName],k=db["coolers"][coolerName];
            if(k.Type!=JSONNode.NodeType.Object) throw new Exception("Missing cooler: "+coolerName);
            var cpu=new PartDescCPU(); cpu.m_freqMhz=N(c["baseMhz"]); cpu.m_maxFreqMhz=N(c["maxMhz"]);
            cpu.m_wattage=N(c["watts"]); cpu.m_numberDies=F(c["dies"]); cpu.m_thermalLimit=N(c["thermalLimit"]);
            cpu.m_voltage=F(c["voltage"]); cpu.m_maxVoltage=F(c["maxVoltage"]);
            var gpu=new PartDescGPU(); gpu.m_coreClockFreq=N(g["baseMhz"]); gpu.m_maxCoreClockFreq=N(g["maxMhz"]);
            gpu.m_memClockFreq=N(g["memoryMhz"]); gpu.m_maxMemClockFreq=N(g["maxMemoryMhz"]);
            gpu.m_wattage=N(g["watts"]); gpu.m_thermalLimit=N(g["thermalLimit"]);
            gpu.m_maxVoltageIncrease=N(g["maxVoltageIncrease"]);
            PartDesc cooler=(PartDesc)Empty(typeof(PartDesc).Assembly.GetType("PartDescCooler"));
            cooler.m_type=(bool)k["water"]?PartDesc.Type.LIQUID_COOLER:PartDesc.Type.AIR_COOLER;
            Set(cooler,"m_airflow",F(k["airflow"]));
            PartDesc pcCase=(PartDesc)Empty(typeof(PartDesc).Assembly.GetType("PartDescCase")); Set(pcCase,"m_cooling",F(db["pcCases"][caseName]["cooling"]));
            PartDesc psu=(PartDesc)Empty(typeof(PartDesc).Assembly.GetType("PartDescPSU"));
            ComputerSave pc=(ComputerSave)Empty(typeof(ComputerSave));
            pc.cpuID=Instance(cpu); pc.cpuCoolerID=Instance(cooler); pc.caseID=Instance(pcCase); pc.psuID=Instance(psu);
            pc.pciSlots=count==1?new PartInstance[]{Instance(gpu)}:new PartInstance[]{Instance(gpu),Instance(gpu)};
            pc.caseFanSlots=(bool)k["water"]?new PartInstance[]{pc.cpuCoolerID}:new PartInstance[0]; pc.radiatorSlots=new PartInstance[0];
            pc.thermalPasteApplied=true; pc.m_biosConfig=(BiosConfig)Empty(typeof(BiosConfig));
            int mhz=Math.Min(4800,N(c["maxMhz"]));
            pc.m_biosConfig.m_baseClock=100; pc.m_biosConfig.m_cpuMultiplier=mhz/100f;
            pc.m_biosConfig.m_gpuClock=gpu.m_coreClockFreq+delta; pc.m_biosConfig.m_gpuMemClock=gpu.m_memClockFreq+delta;
            Set(pc,"m_biosUpdating",true);
            JSONNode sample=new JSONNode(JSONNode.NodeType.Object);
            sample["cpu"]=cpuName; sample["gpu"]=gpuName; sample["cooler"]=coolerName; sample["pcCase"]=caseName;
            sample["count"]=count; sample["cpuMhz"]=mhz; sample["gpuMhz"]=pc.m_biosConfig.m_gpuClock; sample["memoryMhz"]=pc.m_biosConfig.m_gpuMemClock;
            sample["temperature"]=pc.GetCPULoadInternalTemp();
            sample["gpuTemperature"]=pc.GetGPULoadInternalTemp(0);
            sample["gpuVoltage"]=pc.GetGPUVoltageBoostRequired(pc.pciSlots[0]);
            sample["watts"]=pc.GetPeakConsumption(); result.Add(sample);
        }
        File.WriteAllText(output,result.AsJSON); Console.WriteLine("Game thermal oracle: "+result.Count+" native full-load cases.");
    }
}
