# MSBuild Unity PostProcessor

Task for SDK-style .NET projects that post-processes installed dependencies so they work inside a Unity context.

This is a replacement and different approach to tools such as [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity). Instead of partially reimplementing what `dotnet restore` and standard .NET tooling do, this approach builds upon them by post-processing installed NuGet packages to make them suitable to Unity.

Next to eliminating duplicate efforts, this also yields free support for private registries, lock files, and other advanced .NET CLI features that work out of the box for standard .NET projects as well.

## Usage

Create `NuGetDependencies/NuGetDependencies.csproj` in your Unity project with the following contents:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <TargetFramework>netstandard2.1</TargetFramework>
        <EnableDefaultCompileItems>false</EnableDefaultCompileItems>
        <PostProcessDotNetPackagesForUnityAssemblyFile>$(PkgMrWatts_MSBuild_UnityPostProcessor)/lib/netstandard2.1/MSBuildUnityPostProcessor.dll</PostProcessDotNetPackagesForUnityAssemblyFile>
    </PropertyGroup>

    <ItemGroup>

        <!-- Production dependencies. -->
        <!-- <PackageReference Include="System.Text.Json" Version="7.0.2" /> -->
        <!-- ... -->

        <!-- Development dependencies. -->
        <PackageReference Include="MrWatts.MSBuild.UnityPostProcessor" Version="x.y.z" GeneratePathProperty="true" PrivateAssets="all" />

    </ItemGroup>

    <!-- NOTE: Necessary because a `dotnet restore` will not prune packages you remove automatically. -->
    <Target Name="CleanUpUnityPackageFolder" BeforeTargets="Restore">
        <Message Text="Cleaning up installed packages to start from a clean slate..." Importance="high" />
        <RemoveDir Directories="$(NuGetPackageRoot)" />
    </Target>

    <UsingTask TaskName="UnityPostProcessor.PostProcessDotNetPackagesForUnity" AssemblyFile="$(PostProcessDotNetPackagesForUnityAssemblyFile)" />

    <Target Name="PostProcessDotNetPackagesForUnity">
        <Message Text="Running post-processing script for Unity..." Importance="high" />
        <PostProcessDotNetPackagesForUnity ProjectRoot="$(ProjectDir)" PackageRoot="$(NuGetPackageRoot)" UnityInstallationBasePath="$(UNITY_INSTALLATION_BASE_PATH)" />
    </Target>

</Project>
```

Next, create `NuGetDependencies/nuget.config` with these contents:

```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <config>
        <add key="globalPackagesFolder" value="../Assets/Packages/NuGet" />
        <add key="dependencyversion" value="Highest" />
    </config>
</configuration>
```

This will ensure that installed dependencies end up in your Unity project and that Unity can scan them to pick them up.

> If you stumbled upon this public repository, know that that this package is currently only published to our private registry, so you will need a `nuget.config` to tell .NET CLI to search there and get access from us, or build this yourself and update `UsingTask` to reference the built DLL.

After that, **set `UNITY_INSTALLATION_BASE_PATH` as environment variable** (e.g. the same way you set `MRWATTS_PRIVATE_PACKAGE_REGISTRY_USERNAME` for `nuget.config`), and run:

```sh
dotnet msbuild -target:PostProcessDotNetPackagesForUnity -restore NuGetDependencies
```

On your project file, this task will be executed to post-process NuGet dependencies specifically for Unity. Output will be displayed as this happens.

After post-processing finishes, you can start or focus the Unity window of your project and let Unity import the dependencies.

> Note that `AfterTargets="Restore"` to automatically run after restore is not used because it will not work [due to the way NuGet operates internally](https://github.com/NuGet/Home/issues/13513).

### Roslyn Analyzers / Source Generators

The task will mark Roslyn analyzers such as Roslynator and source generators with the `RoslynAnalyzer` tag for Unity automatically. Note that for at least Unity 2022 and up this means that these will automatically apply to every other assembly definition [due to the way scoping in Unity works](https://docs.unity3d.com/2023.2/Documentation/Manual/roslyn-analyzers.html#analyser-scope-anchor-link).

In older Unity versions you could turn off 'Enable Roslyn Analyzers' in the player settings but this option no longer exists since Unity 2022, so if you don't want this, you can create e.g. `Assets/Packages/RoslynAnalyzerWrapper.asmdef` with the following contents:

```json
{
    "name": "RoslynAnalyzerWrapper",
    "rootNamespace": "",
    "references": [],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [],
    "autoReferenced": false,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}
```

This will make them only apply to code inside this assembly definition. As mentioned in the documentation, you can then reference this wrapper in your other assembly definitions to make the analyzers 'leak through' and also apply to them.

> Referencing some analyzers or source generators but not others and cherry-picking is currently not supported. This might be implemented in the future by creating an asmdef automatically per NuGet package in this task during post-processing.

#### Disabling Only In Unity Editor

If you just want Roslyn analyzers in your IDE but not in the Unity Editor because your rules and analyzers block play mode, you can use the following trick (only tested with C# projects generated by `com.unity.ide.visualstudio`):

* Split off your Roslyn analyzer NuGet dependencies from `NuGetDependencies.csproj` into a `.targets` file.
    * Import that `.targets` file back into `NuGetDependencies.csproj`.
* Create a `.csproj.user` next to the generated `.csproj` that matches your custom asmdef.
* Also import the `.targets` file here.

This ensures only your Roslyn analyzer packages are pulled in by the IDE, which processes the `.csproj` files separately.

> Inside the `.targets` file you can also use e.g. `<CodeAnalysisRuleSet>` to reference a custom XML ruleset.

## Development

Ensure all variables in `.env.dist` are loaded in your environment. There are a couple of ways to do that:

1. Export the variables in `.env.dist` manually:
    1. [Using `$env:FOO = 'Bar'`](https://stackoverflow.com/a/714918) (PowerShell only)
    1. Using `SET FOO=Bar` (Windows Command Prompt only)
    1. Using `export FOO=BAR` (Bash and compatible shells only).
        - With Bash, you can also put these in your `.bashrc` to not have to do this every time, if desired.
    1. Prepend the variables in `.env.dist` to the command using `FOO=BAR BAZ=CUX dotnet ...` (Bash and compatible shells only).
1. Copy `.env.dist` to `.env`, fill in the variables to your liking, and use something like [direnv](https://direnv.net/) to automatically load them into your environment. (That way you can use the same configuration for native and container builds.)

After the variables are in your environment:

1. Run `dotnet build`.

### Testing In Project

To test inside a project, run e.g. `dotnet build -c Release` and replace the path of `PostProcessDotNetPackagesForUnityAssemblyFile` from _Usage_ above with the full path to the DLL in `bin/Release/netstandard2.1/MSBuildUnityPostProcessor.dll`.