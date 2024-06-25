using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    internal sealed class UnityBuiltinAssemblyDetector : IUnityBuiltinAssemblyDetector
    {
        public async Task<string[]> DetectAsync(string unityInstallationBasePath, string unityProjectFolder)
        {
            string projectVersionFilePath = Path.Combine(unityProjectFolder, "ProjectSettings", "ProjectVersion.txt");
            string projectVersionFileContents = await File.ReadAllTextAsync(projectVersionFilePath);

            Match? match = Regex.Match(projectVersionFileContents, "(?<=m_EditorVersion:\\s)[f\\d\\.]+", RegexOptions.Compiled, TimeSpan.FromMinutes(1));

            if (match is null)
            {
                throw new Exception($"Could not detect Unity project version, does '{projectVersionFilePath}' exist?");
            }

            string projectUnityVersion = match.Value;
            string unityVersionFolder = Path.Combine(unityInstallationBasePath, projectUnityVersion);

            if (!Directory.Exists(unityVersionFolder))
            {
                throw new UnityVersionNotFoundException($"Could not find Unity version {projectUnityVersion} at path '{unityVersionFolder}'");
            }

            string unityDotNetAssemblyFolder = Path.Combine(unityVersionFolder, "Editor", "Data", "NetStandard", "compat", "2.1.0", "shims", "netstandard");

            if (!Directory.Exists(unityDotNetAssemblyFolder))
            {
                throw new Exception(
                    $"Unity version {projectUnityVersion} exists but it does not have a folder with .NET asseblies " +
                    $"at '{unityDotNetAssemblyFolder}', which either implies a bug in the postprocessor or your " +
                    "Unity installation is corrupt"
                );
            }

            return new DirectoryInfo(unityDotNetAssemblyFolder)
                .GetFiles("*.dll")
                .Select(x => Path.GetFileNameWithoutExtension(x.FullName))
                .ToArray();
        }
    }
}