using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    public sealed class PostProcessDotNetPackagesForUnity : Microsoft.Build.Utilities.Task
    {
        private UnityMetaFileGenerator unityMetaFileGenerator;
        private IUnityBuiltinAssemblyDetector unityBuiltinAssemblyDetector;

        [Required]
        public string ProjectRoot { get; set; }

        [Required]
        public string PackageRoot { get; set; }

        public override bool Execute()
        {
            try
            {
                InitializeDependencies();

                // There is no other option here but to block, see https://github.com/dotnet/msbuild/issues/4174.
                ProcessPackagesAsync().Wait();
                return true;
            }
            catch (Exception exception)
            {
                Log.LogErrorFromException(exception, showStackTrace: true);
                return false;
            }
        }

        private void InitializeDependencies()
        {
            ServiceContainer serviceContainer = new ServiceContainer();
            unityMetaFileGenerator = serviceContainer.UnityMetaFileGenerator;
            unityBuiltinAssemblyDetector = serviceContainer.UnityBuiltinAssemblyDetector;
        }

        private async Task ProcessPackagesAsync()
        {
            await Task.WhenAll(Directory.GetDirectories(PackageRoot).Select(async x => await ProcessPackageAsync(x)));
        }

        private async Task ProcessPackageAsync(string packageDirectory)
        {
            string packageName = Path.GetRelativePath(PackageRoot, packageDirectory);

            Log.LogMessage(MessageImportance.High, $"  - {packageName}");

            if (IsPackageShippedByUnity(packageDirectory))
            {
                Log.LogMessage(MessageImportance.High, "    - Dropping because Unity already ships its own version of this assembly.");

                Directory.Delete(packageDirectory, true);

                return;
            }

            await Task.WhenAll(Directory.GetDirectories(packageDirectory).Select(async x => await ProcessPackageVersionAsync(x)));
        }

        private async Task ProcessPackageVersionAsync(string packageVersionFolder)
        {
            DropConflictingNativeLibraries(packageVersionFolder);
            DropUnsupportedNativeRuntimeLibraries(packageVersionFolder);
            FilterMatchingDotNetVersionFolders(packageVersionFolder);

            await GenerateRoslynAnalyzerUnityMetaFilesAsync(packageVersionFolder);
        }

        /// <summary>
        /// Drop conflicting DLLs in the lib folder from libraries (such as System.Security.Cryptography.OpenSsl).
        /// </summary>
        /// <param name="packageVersionFolder"></param>
        private void DropConflictingNativeLibraries(string packageVersionFolder)
        {
            Directory.GetDirectories(packageVersionFolder, "ref", SearchOption.AllDirectories)
                .ToList()
                .ForEach(x =>
                {
                    Log.LogMessage(MessageImportance.High, $"    - WARNING: Dropping unsupported ref folder '{Path.GetRelativePath(packageVersionFolder, x)}'.");
                    Directory.Delete(x, true);
                });
        }

        /// <summary>
        /// <para>Drop unsupported runtime assemblies.</para>
        /// <para>
        /// Packages such as the Microsoft QR package have a separate "Unity" folder *also* containing these DLLs,
        /// so if these are supported by generating the appropriate meta files, duplicates also have to be taken into
        /// account.
        ///
        /// NuGetForUnity also seems to ignore these.
        /// </para>
        /// </summary>
        /// <param name="packageVersionFolder"></param>
        private void DropUnsupportedNativeRuntimeLibraries(string packageVersionFolder)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(packageVersionFolder, "runtimes"));

            if (!directoryInfo.Exists)
            {
                return;
            }

            directoryInfo
                .GetFiles("*.dll", SearchOption.AllDirectories)
                .ToList()
                .ForEach(x =>
                {
                    Log.LogMessage(MessageImportance.High, $"    - WARNING: Dropping currently unsupported native library '{Path.GetRelativePath(packageVersionFolder, x.FullName)}'.");
                    File.Delete(x.FullName);
                });
        }

        /// <summary>
        /// <para>Filters out assemblies for unsupported or older .NET API versions from all "lib" folders.</para>
        /// </summary>
        /// <param name="packageVersionFolder"></param>
        private void FilterMatchingDotNetVersionFolders(string packageVersionFolder)
        {
            Directory.GetDirectories(packageVersionFolder, "lib", SearchOption.AllDirectories)
                .ToList()
                .ForEach(x =>
                {
                    Log.LogMessage(MessageImportance.High, $"    - Detected library folder with assemblies '{Path.GetRelativePath(packageVersionFolder, x)}'.");

                    FilterMatchingDotNetVersionFolder(x);
                });
        }

        /// <summary>
        /// <para>Filters out assemblies for unsupported or older .NET API versions from the specified "lib" folder.</para>
        /// <para>
        /// The implementation is not great, and only works for .NET Standard 2.1 projects, but hopefully once Unity
        /// moves to .NET >= 5 we have official NuGet support in place. Otherwise this will need to be updated to try
        /// newer .NET versions first.
        /// </para>
        /// </summary>
        /// <param name="libraryFolder"></param>
        private void FilterMatchingDotNetVersionFolder(string libraryFolder)
        {
            string[] apiFolderNames = new string[] {
                "netstandard2.1",
                "netstandard2.0",
                "netstandard1.6",
                "netstandard1.5",
                "netstandard1.4",
                "netstandard1.3",
                "netstandard1.2",
                "netstandard1.1",
                "netstandard1.0",
            };

            string highestCompatibleDotNetVersion;

            try
            {
                highestCompatibleDotNetVersion = Array.Find(apiFolderNames, x => Directory.Exists(Path.Combine(libraryFolder, x)));
            }
            catch (ArgumentNullException)
            {
                Log.LogMessage(MessageImportance.High, "      - WARNING: No compatible assemblies found.");
                return;
            }

            Log.LogMessage(MessageImportance.High, $"      - Maintaining best compatible version '{highestCompatibleDotNetVersion}'.");

            Directory.GetDirectories(libraryFolder)
                .Select(x => new DirectoryInfo(x))
                .Where(x => x.Name != highestCompatibleDotNetVersion)
                .ToList()
                .ForEach(x =>
                {
                    Directory.Delete(x.FullName, true);
                });
        }

        private async Task GenerateRoslynAnalyzerUnityMetaFilesAsync(string packageVersionFolder)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(Path.Combine(packageVersionFolder, "analyzers"));

            if (!directoryInfo.Exists)
            {
                return;
            }

            await Task.WhenAll(
                directoryInfo
                    .GetFiles("*.dll", SearchOption.AllDirectories)
                    .Select(async x =>
                    {
                        Log.LogMessage(MessageImportance.High, $"    - Processing Roslyn analyzer assembly '{Path.GetRelativePath(packageVersionFolder, x.FullName)}'.");

                        await unityMetaFileGenerator.GenerateAsync(
                            $"{x.FullName}.meta",
                            @"labels:\n" +
                            "- RoslynAnalyzer\n" +
                            "PluginImporter:\n" +
                            "  externalObjects: {}\n" +
                            "  serializedVersion: 2\n" +
                            "  iconMap: {}\n" +
                            "  executionOrder: {}\n" +
                            "  defineConstraints: []\n" +
                            "  isPreloaded: 0\n" +
                            "  isOverridable: 0\n" +
                            "  isExplicitlyReferenced: 0\n" +
                            "  validateReferences: 1\n" +
                            "  platformData:\n" +
                            "  - first:\n" +
                            "      : Any\n" +
                            "    second:\n" +
                            "      enabled: 0\n" +
                            "      settings:\n" +
                            "        Exclude Editor: 1\n" +
                            "        Exclude Linux64: 1\n" +
                            "        Exclude OSXUniversal: 1\n" +
                            "        Exclude Win: 1\n" +
                            "        Exclude Win64: 1\n" +
                            "  - first:\n" +
                            "      Any:\n" +
                            "    second:\n" +
                            "      enabled: 0\n" +
                            "      settings: {}\n" +
                            "  - first:\n" +
                            "      Editor: Editor\n" +
                            "    second:\n" +
                            "      enabled: 0\n" +
                            "      settings:\n" +
                            "        DefaultValueInitialized: true\n" +
                            "  - first:\n" +
                            "      Standalone: Win\n" +
                            "    second:\n" +
                            "      enabled: 0\n" +
                            "      settings:\n" +
                            "        CPU: None\n" +
                            "  - first:\n" +
                            "      Standalone: Win64\n" +
                            "    second:\n" +
                            "      enabled: 0\n" +
                            "      settings:\n" +
                            "        CPU: None\n" +
                            "  - first:\n" +
                            "      Windows Store Apps: WindowsStoreApps\n" +
                            "    second:\n" +
                            "      enabled: 0\n" +
                            "      settings:\n" +
                            "        CPU: AnyCPU\n" +
                            "  userData:\n" +
                            "  assetBundleName:\n" +
                            "  assetBundleVariant:\n"
                                .Trim()
                        );
                    }
                )
            );
        }

        private bool IsPackageShippedByUnity(string packageName)
        {
            string[] builtinUnityDotNetAssemblies = unityBuiltinAssemblyDetector.Detect(ProjectRoot);

            if (builtinUnityDotNetAssemblies.Length == 0)
            {
                throw new Exception("Could not scan your project Library folder for Unity assemblies to filter them out. Have you started Unity with your project at least once so it is generated?");
            }

            return builtinUnityDotNetAssemblies.Contains(packageName, StringComparer.OrdinalIgnoreCase);
        }
    }
}