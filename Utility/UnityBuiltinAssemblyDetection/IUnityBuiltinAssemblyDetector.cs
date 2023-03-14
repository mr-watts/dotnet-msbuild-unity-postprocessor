using System.Threading.Tasks;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    internal interface IUnityBuiltinAssemblyDetector
    {
        Task<string[]> DetectAsync(string unityInstallationBasePath, string unityProjectFolder);
    }
}