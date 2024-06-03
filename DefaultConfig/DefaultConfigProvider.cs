using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace SolarisBot.DefaultConfig
{
    internal static class DefaultConfigProvider
    {
        internal static void PrepareDefaultConfig(Assembly assembly)
        {
            if (!Directory.Exists(Utils.PathConfigDirectory))
            {
                Directory.CreateDirectory(Utils.PathConfigDirectory);
            }

            var thisType = typeof(DefaultConfigProvider);
            var thisNamespaceSearch = thisType.Namespace + ".";

            foreach (var name in assembly.GetManifestResourceNames())
            {
                if (!name.StartsWith(thisNamespaceSearch) || name == thisType.FullName)
                {
                    return;
                }

                var fileName = name.Remove(0, thisNamespaceSearch.Length);
                var path = Path.Combine(Utils.PathConfigDirectory, fileName);
                using var stream = assembly.GetManifestResourceStream(name)!;
                using var fStream = new FileStream(path, FileMode.OpenOrCreate);
                stream.Seek(0, SeekOrigin.Begin);
                stream.CopyTo(fStream);
            }
        }
    }
}