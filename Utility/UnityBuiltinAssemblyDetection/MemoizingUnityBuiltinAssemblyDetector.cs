using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    public sealed class MemoizingUnityBuiltinAssemblyDetector : IUnityBuiltinAssemblyDetector
    {
        private readonly IUnityBuiltinAssemblyDetector delegatee;

        private readonly ConcurrentDictionary<string, string[]> cache = new ConcurrentDictionary<string, string[]>(System.StringComparer.Ordinal);

        public MemoizingUnityBuiltinAssemblyDetector(IUnityBuiltinAssemblyDetector delegatee)
        {
            this.delegatee = delegatee;
        }

        public async Task<string[]> DetectAsync(string unityInstallationBasePath, string unityProjectFolder)
        {
            string cacheKey = $"{unityInstallationBasePath}{unityProjectFolder}";

            if (!cache.ContainsKey(cacheKey))
            {
                cache[cacheKey] = await delegatee.DetectAsync(unityInstallationBasePath, unityProjectFolder);
            }

            return cache[cacheKey];
        }
    }
}