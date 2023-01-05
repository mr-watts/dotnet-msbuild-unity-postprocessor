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
    </Target>

</Project>
```

> Note that this package is published to our private registry, so you will need a `nuget.config` to tell .NET CLI to search there as well (see also [this example](https://gitlab.com/mr-watts/medenvision/live-surgery/unity-live-surgery/-/blob/d55ed4e4ba7dea5887f4ebf9c1b148e104d0936c/nuget.config)).

After that, run:

```sh
dotnet restore NuGetDependencies.csproj
```

On your project file, this task will be executed to post-process NuGet dependencies specifically for Unity. Output will be displayed as this happens.

After post-processing finishes, you can start or focus the Unity window of your project and let Unity import the dependencies.

### Error `task could not be loaded`

If you see something like this the first time you restore:

```sh
/path/to/project/NuGetDependencies.csproj(43,9): error MSB4062: The "UnityPostProcessor.PostProcessDotNetPackagesForUnity" task could not be loaded from the assembly /lib/netstandard2.1/MSBuildUnityPostProcessor.dll. Could not load file or assembly '/lib/netstandard2.1/MSBuildUnityPostProcessor.dll'. The system cannot find the file specified.

/path/to/project/NuGetDependencies.csproj(43,9): error MSB4062:  Confirm that the <UsingTask> declaration is correct, that the assembly and all its dependencies are available, and that the task contains a public class that implements Microsoft.Build.Framework.ITask.
```

Just run the restore a second time. There seems to be a bug in MSBuild where `PkgMrWatts_MSBuild_UnityPostProcessor` isn't filled in yet if the package isn't in the NuGet cache yet, so this might also happen every time you switch to a new version and it's not in your package cache yet.

## Development

Ensure all variables in `.env.dist` are loaded in your environment. There are a couple of ways to do that:

1. Copy `.env.dist` to `.env`, fill in the variables to your liking, and use something like [direnv](https://direnv.net/) to automatically load them into your environment. (That way you can use the same configuration for native and container builds.)
2. Export the variables in `.env.dist` manually using `export FOO=BAR`.
    - You can also put these in your `.bashrc` to not have to do this every time, if desired.
3. Prepend the variables in `.env.dist` to the `dotnet run` command below using `FOO=BAR BAZ=CUX dotnet watch`.

After the variables are in your environment:

1. Run `dotnet build`.

### Testing In Project

To test inside a project, run e.g. `dotnet build -c Release` and replace the path of `PostProcessDotNetPackagesForUnityAssemblyFile` from _Usage_ above with the full path to the DLL in `bin/Release/netstandard2.1/MSBuildUnityPostProcessor.dll`.
