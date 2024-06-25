using System.Threading.Tasks;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    internal interface IUnityBuiltinAssemblyDetector
    {
        /// <param name="unityInstallationBasePath"></param>
        /// <param name="unityProjectFolder"></param>
        /// <exception cref="UnityVersionNotFoundException">
        Task<string[]> DetectAsync(string unityInstallationBasePath, string unityProjectFolder);
    }
}