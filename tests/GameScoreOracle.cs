using System;
using System.IO;
using System.Reflection;
using System.Globalization;
using ZenFulcrum.EmbeddedBrowser;

// Calls PCBS's actual managed CPU/GPU test methods; never reads a third-party calculator.
internal static class GameScoreOracle
{
    private static string managed;
    private static void Set(object target, string name, object value)
    { target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public).SetValue(target,value); }
    private static float F(JSONNode node) { return Convert.ToSingle(node.Value,CultureInfo.InvariantCulture); }
    private static int N(JSONNode node) { return Convert.ToInt32(node.Value,CultureInfo.InvariantCulture); }
    private static void Main(string[] args)
    {
        managed=args[0];
        AppDomain.CurrentDomain.AssemblyResolve += delegate(object sender,ResolveEventArgs e){string p=Path.Combine(managed,new AssemblyName(e.Name).Name+".dll");return File.Exists(p)?Assembly.LoadFrom(p):null;};
        Run(args[1],args[2]);
    }
    private static void Run(string input,string output)
    {
        JSONNode db=JSONNode.Parse(File.ReadAllText(input)), examples=new JSONNode(JSONNode.NodeType.Array);
        OverclockConstants.s_min3DMarkFPS=F(db["constants"]["minFps"]);
        OverclockConstants.s_min3DMarkCPU=N(db["constants"]["minCpu"]);
        OverclockConstants.s_min3DMarkGPU=N(db["constants"]["minGpu"]);
        foreach (System.Collections.Generic.KeyValuePair<string,JSONNode> entry in (System.Collections.Generic.Dictionary<string,JSONNode>)db["cpus"])
        {
            JSONNode p=entry.Value;var cpu=new PartDescCPU();cpu.m_freqMhz=N(p["baseMhz"]);cpu.m_maxMemoryChannels=N(p["maxChannels"]);cpu.m_defaultMemorySpeed=N(p["defaultRamMhz"]);
            string[] fields={"m_coefCoreClock","m_coefMemChannel","m_coefMemClock","m_adjustment"};
            for(int i=0;i<fields.Length;i++)Set(cpu,fields[i],F(p["coefficients"][i]));
            foreach(int speed in new int[]{1866,2133,2400,2666,3200,4400}) for(int channel=1;channel<=cpu.m_maxMemoryChannels;channel++)
                foreach(int mhz in new int[]{cpu.m_freqMhz,cpu.m_freqMhz+125})
                {JSONNode item=new JSONNode(JSONNode.NodeType.Object);item["kind"]="cpu";item["name"]=entry.Key;item["speed"]=speed;item["channels"]=channel;item["mhz"]=mhz;item["score"]=Math.Max((int)(298f*cpu.Test(mhz,channel,speed)),OverclockConstants.s_min3DMarkCPU);examples.Add(item);}
        }
        foreach(System.Collections.Generic.KeyValuePair<string,JSONNode> entry in (System.Collections.Generic.Dictionary<string,JSONNode>)db["gpus"])
        {
            JSONNode p=entry.Value;var gpu=new PartDescGPU();gpu.m_coreClockFreq=N(p["baseMhz"]);gpu.m_memClockFreq=N(p["memoryMhz"]);
            FieldInfo scores=typeof(PartDescGPU).GetField("m_testScores",BindingFlags.Instance|BindingFlags.NonPublic|BindingFlags.Public);
            Type element=scores.FieldType.GetElementType();Array coeffs=Array.CreateInstance(element,4);
            for(int i=0;i<4;i++){object c=Activator.CreateInstance(element);string[] fields={"m_coreClock","m_memClock","m_adjustment"};for(int j=0;j<3;j++)Set(c,fields[j],F(p["tests"][i][j]));coeffs.SetValue(c,i);}scores.SetValue(gpu,coeffs);
            for(int count=1;count<=((bool)p["multiGpu"]?2:1);count++) foreach(int delta in new int[]{0,50})
            {int mhz=gpu.m_coreClockFreq+delta,mem=gpu.m_memClockFreq+delta;float fps1=gpu.Test(mhz,mem,1,count),fps2=gpu.Test(mhz,mem,2,count);if(fps1<=0||fps2<=0)continue;JSONNode item=new JSONNode(JSONNode.NodeType.Object);item["kind"]="gpu";item["name"]=entry.Key;item["count"]=count;item["mhz"]=mhz;item["memory"]=mem;item["score"]=Math.Max((int)(328f/(1f/fps1+1f/fps2)),OverclockConstants.s_min3DMarkGPU);examples.Add(item);}
        }
        File.WriteAllText(output,examples.AsJSON);Console.WriteLine("Game score oracle: "+examples.Count+" cases.");
    }
}
