# MSBuild Unity PostProcessor

Task for SDK-style .NET projects that post-processes installed dependencies so they work inside a Unity context.

## Usage

Tweak your `NuGetDependencies.csproj` .NET project to contain the following:

```xml
<Project Sdk="Microsoft.NET.Sdk">

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

    <UsingTask TaskName="UnityPostProcessor.PostProcessDotNetPackagesForUnity" AssemblyFile="$(PkgMrWatts_MSBuild_UnityPostProcessor)/lib/netstandard2.1/MSBuildUnityPostProcessor.dll" />

    <Target Name="PostProcessDotNetPackagesForUnity" AfterTargets="Restore">
        <Message Text="Running post-processing script for Unity..." Importance="high" />
        <PostProcessDotNetPackagesForUnity ProjectRoot="$(ProjectDir)" PackageRoot="$(NuGetPackageRoot)" />

        <!-- Delete the script itself so Unity itself doesn't pick it up. -->
        <Delete Files="$(PostProcessDotNetPackagesForUnityAssemblyFile)" />
    </Target>

</Project>
```

> Note that this package is published to our private registry, so you will need a `nuget.config` to tell .NET CLI to search there as well (see also [this example](https://gitlab.com/mr-watts/internal/dotnet-oasis/-/blob/develop/nuget.config)).

After that, each time you run `dotnet restore NuGetDependencies.csproj` on your project file, this task will be executed to post-process NuGet dependencies specifically for Unity. Output will be displayed as this happens.

After post-processing finishes, you can start or focus the Unity window of your project and let Unity import the dependencies.

## Development

Ensure all variables in `.env.dist` are loaded in your environment. There are a couple of ways to do that:

1. Copy `.env.dist` to `.env`, fill in the variables to your liking, and use something like [direnv](https://direnv.net/) to automatically load them into your environment. (That way you can use the same configuration for native and container builds.)
2. Export the variables in `.env.dist` manually using `export FOO=BAR`.
    - You can also put these in your `.bashrc` to not have to do this every time, if desired.
3. Prepend the variables in `.env.dist` to the `dotnet run` command below using `FOO=BAR BAZ=CUX dotnet watch`.

After the variables are in your environment:

1. Run `dotnet build`.
