using System.Reflection;

namespace SolarisBot.ConfigFiles
{
    internal static class ConfigFileProvider
    {
        internal static void LoadConfigFiles(Assembly assembly)
        {
            if (!Directory.Exists(Utils.PathConfigDirectory))
            {
                Directory.CreateDirectory(Utils.PathConfigDirectory);
            }

            var thisType = typeof(ConfigFileProvider);
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