using System.Threading.Tasks;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    public interface IUnityBuiltinAssemblyDetector
    {
        Task<string[]> DetectAsync(string unityInstallationBasePath, string unityProjectFolder);
    }
}