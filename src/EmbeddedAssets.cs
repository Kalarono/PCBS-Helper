using System;
using System.IO;
using System.Reflection;

namespace PCBSCalculatorHelper
{
    internal static class EmbeddedAssets
    {
        internal static string Read(string name, Assembly assembly, string externalDirectory)
        {
            using (Stream stream = assembly.GetManifestResourceStream("PCBSHelper.Assets." + name))
                if (stream != null) using (var reader = new StreamReader(stream)) return reader.ReadToEnd();
            // Compatibility with old development builds; packaged resources always take priority.
            string path = Path.Combine(externalDirectory, name);
            if (File.Exists(path)) return File.ReadAllText(path);
            throw new FileNotFoundException("Calculator asset is missing: " + name, path);
        }
    }
}
