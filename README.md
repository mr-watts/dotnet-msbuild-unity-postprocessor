# MSBuild Unity PostProcessor

Task for SDK-style .NET projects that post-processes installed dependencies so they work inside a Unity context.

This is a replacement and different approach to tools such as [NuGetForUnity](https://github.com/GlitchEnzo/NuGetForUnity). Instead of partially reimplementing what `dotnet restore` and standard .NET tooling do, this approach builds upon that by post-processing installed NuGet packages to make them suitable to Unity.

Next to eliminating duplicate efforts, this also yields free support for private registries, lock files, and other advanced .NET CLI features that work out of the box for standard .NET projects as well.

## Usage

Tweak your `NuGetDependencies.csproj` .NET project to contain the following:

```xml
<Project Sdk="Microsoft.NET.Sdk">

    <PropertyGroup>
        <PostProcessDotNetPackagesForUnityAssemblyFile>$(PkgMrWatts_MSBuild_UnityPostProcessor)/lib/netstandard2.1/MSBuildUnityPostProcessor.dll</PostProcessDotNetPackagesForUnityAssemblyFile>
    </PropertyGroup>

    <ItemGroup>

        <!-- Production dependencies. -->
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

> Note that this package is published to our private registry, so you will need a `nuget.config` to tell .NET CLI to search there as well (see also [this example](https://gitlab.com/mr-watts/medenvision/live-surgery/unity-live-surgery/-/blob/d55ed4e4ba7dea5887f4ebf9c1b148e104d0936c/nuget.config)).

After that, **set `UNITY_INSTALLATION_BASE_PATH` as environment variable** (e.g. the same way you set `MRWATTS_PRIVATE_PACKAGE_REGISTRY_USERNAME` for `nuget.config`), and run:

```sh
dotnet msbuild -target:PostProcessDotNetPackagesForUnity -restore NuGetDependencies.csproj
```

On your project file, this task will be executed to post-process NuGet dependencies specifically for Unity. Output will be displayed as this happens.

After post-processing finishes, you can start or focus the Unity window of your project and let Unity import the dependencies.

> Note that `AfterTargets="Restore"` to automatically run after restore is not used because it will not work [due to the way NuGet operates internally](https://github.com/NuGet/Home/issues/13513).

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
