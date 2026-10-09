using System;
using System.IO;
using System.Reflection;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using ZenFulcrum.EmbeddedBrowser;

namespace PCBSCalculatorHelper
{
    internal sealed class CalculatorSession
    {
        private readonly string statePath;
        private readonly ManualLogSource log;
        private string lastSaved;
        private const int MaxStateSize = 8 * 1024 * 1024;
        private static readonly FieldInfo ModeField = AccessTools.Field(typeof(GameController), "m_gameMode");

        public CalculatorSession(string configPath, ManualLogSource log)
        { statePath = Path.Combine(Path.Combine(configPath, "PCBSHelper"), "calculator-state.json"); this.log = log; }

        public string Load()
        {
            foreach (string path in new string[] { statePath, statePath + ".bak" })
            {
                try
                {
                    if (!File.Exists(path)) continue;
                    string json = File.ReadAllText(path, Encoding.UTF8);
                    Validate(json);
                    lastSaved = path == statePath ? json : null;
                    return json;
                }
                catch (Exception ex) { log.LogWarning("Could not read calculator state: " + ex.Message); }
            }
            return "null";
        }

        private static void Validate(string json)
        {
            if (json.Length > MaxStateSize) throw new InvalidDataException("Calculator state exceeds the size limit.");
            JSONNode node = JSONNode.Parse(json);
            if ((int)node["version"] != 1) throw new InvalidDataException("Unsupported calculator state version.");
        }

        public void Save(string json)
        {
            if (json == lastSaved) return;
            try
            {
                Validate(json);
                Directory.CreateDirectory(Path.GetDirectoryName(statePath));
                string temp = statePath + ".tmp";
                File.WriteAllText(temp, json, new UTF8Encoding(false));
                if (File.Exists(statePath)) File.Replace(temp, statePath, statePath + ".bak");
                else File.Move(temp, statePath);
                lastSaved = json;
            }
            catch (Exception ex) { log.LogError("Could not save calculator state: " + ex.Message); }
        }

        public static string CareerJson()
        {
            GameController controller = GameController.Get();
            if (controller == null) return "{\"valid\":false}";
            GameMode mode = (GameMode)ModeField.GetValue(controller);
            if (mode == GameMode.NOT_SET) return "{\"valid\":false}";
            bool careerMode = mode == GameMode.CAREER;
            JSONNode result = new JSONNode(JSONNode.NodeType.Object);
            result["valid"] = true;
            result["applicable"] = careerMode;
            if (careerMode)
            {
                CareerStatus career = CareerStatus.Get();
                int[] thresholds = LevelProgression.s_levelKudosThreshold;
                if (career == null || thresholds == null || thresholds.Length == 0) return "{\"valid\":false}";
                CareerProgressValue progress = CareerProgress.Calculate(career.GetKudos(), thresholds);
                result["level"] = progress.Level;
                result["percent"] = progress.Percent;
            }
            return result.AsJSON;
        }

        public static string ScriptString(string value)
        {
            return new JSONNode(value).AsJSON.Replace("<", "\\u003c").Replace("\u2028", "\\u2028").Replace("\u2029", "\\u2029");
        }
    }
}
