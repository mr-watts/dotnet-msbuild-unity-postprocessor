using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    public sealed class UnityBuiltinAssemblyDetector : IUnityBuiltinAssemblyDetector
    {
        public string[] Detect(string unityProjectFolder)
        {
            return new DirectoryInfo(Path.Combine(unityProjectFolder, "Library", "Bee"))
                .GetFiles("*.json")
                .Select(x => File.ReadAllText(x.FullName))
                .SelectMany(x => Regex.Matches(x, "(?<=/shims/netstandard/)[^\"]+(?=.dll)", RegexOptions.Compiled, TimeSpan.FromMinutes(1)))
                .Select(x => x.Value)
                .ToHashSet(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
    }
}