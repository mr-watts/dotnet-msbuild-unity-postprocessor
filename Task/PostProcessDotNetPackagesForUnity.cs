using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using MrWatts.Internal.Extensions;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    public sealed class PostProcessDotNetPackagesForUnity : Microsoft.Build.Utilities.Task
    {
        private UnityMetaFileGenerator unityMetaFileGenerator;
        private IUnityBuiltinAssemblyDetector unityBuiltinAssemblyDetector;

        [Required]
        public string ProjectRoot { get; set; } = default!;

        [Required]
        public string PackageRoot { get; set; } = default!;

        [Required]
        public string UnityInstallationBasePath { get; set; } = default!;

        public PostProcessDotNetPackagesForUnity()
        {
            ServiceContainer serviceContainer = new ServiceContainer();
            unityMetaFileGenerator = serviceContainer.UnityMetaFileGenerator;
            unityBuiltinAssemblyDetector = serviceContainer.UnityBuiltinAssemblyDetector;
        }

        public override bool Execute()
        {
            try
            {
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

        private async Task ProcessPackagesAsync()
        {
            await Directory.GetDirectories(PackageRoot).ForEachAsync(ProcessPackageAsync);
        }

        private async Task ProcessPackageAsync(string packageDirectory)
        {
            string packageName = Path.GetRelativePath(PackageRoot, packageDirectory);

            Log.LogMessage(MessageImportance.High, $"  - {packageName}");

            if (await IsPackageShippedByUnityAsync(packageName))
            {
                /*
                    We don't want to drop all files as tools - such as OmniSharp - might still want to scan some files
                    (e.g. ref folders and XML index files for assemblies) for IntelliSense.

                    Ideally we would not drop the libraries and assemblies themselves and ignore them instead, but Unity
                    (at least 2021.3.8) insists on *still* using these in its reference rewriter on UWP builds, even
                    when they are excluded from all platforms, are not referenced automatically, are set to not
                    validate, and have define constraints. The *only* way to ensure Unity doesn't do so is to fully
                    delete them.
                */
                Log.LogMessage(MessageImportance.High, "    - Dropping all libraries because Unity already ships its own version of these and they cannot be properly excluded through meta files in UWP builds.");

                FindLibrariesInFolder(packageDirectory)
                    .ForEach(x => File.Delete(x.FullName));
            }
            else if (IsThisPackage(packageName))
            {
                // We can't just remove this because on Windows, a "file is in use" error will be generated because
                // MSBuild is still running and using the assembly.
                Log.LogMessage(MessageImportance.High, "    - Marking assemblies as ignored so Unity doesn't pick it up (it's this script).");

                await MarkAllLibrariesInFolderAsIgnoredByUnityAsync(packageDirectory);
            }
            else
            {
                await Directory.GetDirectories(packageDirectory).ForEachAsync(ProcessPackageVersionAsync);
            }
        }

        private async Task ProcessPackageVersionAsync(string packageVersionFolder)
        {
            DropConflictingNativeLibraries(packageVersionFolder);
            DropUnsupportedNativeRuntimeLibraries(packageVersionFolder);

            await FilterMatchingDotNetVersionFoldersAsync(packageVersionFolder);
            await GenerateRoslynAnalyzerUnityMetaFilesAsync(packageVersionFolder);
        }

        /// <summary>
        /// Drop conflicting DLLs in the lib folder from libraries (such as System.Security.Cryptography.OpenSsl).
        /// </summary>
        /// <param name="packageVersionFolder"></param>
        private void DropConflictingNativeLibraries(string packageVersionFolder)
        {
            FindLibrariesInFolder(Path.Combine(packageVersionFolder, "ref"))
                .ForEach(x =>
                {
                    Log.LogMessage(MessageImportance.High, $"    - WARNING: Dropping unsupported ref assembly '{Path.GetRelativePath(packageVersionFolder, x.FullName)}'.");
                    File.Delete(x.FullName);
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
            FindLibrariesInFolder(Path.Combine(packageVersionFolder, "runtimes"))
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
        private async Task FilterMatchingDotNetVersionFoldersAsync(string packageVersionFolder)
        {
            await Directory.GetDirectories(packageVersionFolder, "lib", SearchOption.AllDirectories)
                .ToList()
                .ForEachAsync(async x =>
                {
                    Log.LogMessage(MessageImportance.High, $"    - Detected library folder with assemblies '{Path.GetRelativePath(packageVersionFolder, x)}'.");

                    await FilterMatchingDotNetVersionFolderAsync(x);
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
        private async Task FilterMatchingDotNetVersionFolderAsync(string libraryFolder)
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
                Log.LogMessage(MessageImportance.High, "      - WARNING: No compatible assemblies found, none will be usable by Unity.");
                return;
            }

            Log.LogMessage(MessageImportance.High, $"      - Best compatible version is '{highestCompatibleDotNetVersion}', marking assemblies in other folders so Unity ignores them.");

            await Directory.GetDirectories(libraryFolder)
                .Select(x => new DirectoryInfo(x))
                .Where(x => x.Name != highestCompatibleDotNetVersion)
                .ToList()
                .ForEachAsync(x => MarkAllLibrariesInFolderAsIgnoredByUnityAsync(x.FullName));
        }

        private async Task MarkAllLibrariesInFolderAsIgnoredByUnityAsync(string folder)
        {
            await FindLibrariesInFolder(folder)
                .ForEachAsync(x => unityMetaFileGenerator.GenerateAsync(
                    $"{x.FullName}.meta",
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
                    "      settings: {}\n" +
                    "  - first:\n" +
                    "      Any:\n" +
                    "    second:\n" +
                    "      enabled: 0\n" +
                    "      settings: {}\n" +
                    "  userData:\n" +
                    "  assetBundleName:\n" +
                    "  assetBundleVariant:\n"
                )
            );
        }

        private async Task GenerateRoslynAnalyzerUnityMetaFilesAsync(string packageVersionFolder)
        {
            await FindLibrariesInFolder(Path.Combine(packageVersionFolder, "analyzers"))
                .ForEachAsync(async x =>
                {
                    Log.LogMessage(MessageImportance.High, $"    - Processing Roslyn analyzer assembly '{Path.GetRelativePath(packageVersionFolder, x.FullName)}'.");

                    await unityMetaFileGenerator.GenerateAsync(
                        $"{x.FullName}.meta",
                        "labels:\n" +
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
            );
        }

        private List<FileInfo> FindLibrariesInFolder(string folder)
        {
            DirectoryInfo directoryInfo = new DirectoryInfo(folder);

            if (!directoryInfo.Exists)
            {
                return new List<FileInfo>();
            }

            return directoryInfo.GetFiles("*.dll", SearchOption.AllDirectories).ToList();
        }

        private async Task<bool> IsPackageShippedByUnityAsync(string packageName)
        {
            string[] builtinUnityDotNetAssemblies = await unityBuiltinAssemblyDetector.DetectAsync(UnityInstallationBasePath, ProjectRoot);

            if (builtinUnityDotNetAssemblies.Length == 0)
            {
                throw new Exception("Could not scan your project Library folder for Unity assemblies to filter them out. Have you started Unity with your project at least once so it is generated?");
            }

            return builtinUnityDotNetAssemblies.Contains(packageName, StringComparer.OrdinalIgnoreCase);
        }

        private bool IsThisPackage(string packageName)
        {
            return string.Equals(packageName, "mrwatts.msbuild.unitypostprocessor", StringComparison.OrdinalIgnoreCase);
        }
    }
}