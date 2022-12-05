using System.Collections.Generic;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    public sealed class MemoizingUnityBuiltinAssemblyDetector : IUnityBuiltinAssemblyDetector
    {
        private readonly IUnityBuiltinAssemblyDetector delegatee;

        private readonly Dictionary<string, string[]> cache = new Dictionary<string, string[]>();

        public MemoizingUnityBuiltinAssemblyDetector(IUnityBuiltinAssemblyDetector delegatee)
        {
            this.delegatee = delegatee;
        }

        public string[] Detect(string unityProjectFolder)
        {
            if (!cache.ContainsKey(unityProjectFolder))
            {
                cache[unityProjectFolder] = delegatee.Detect(unityProjectFolder);
            }

            return cache[unityProjectFolder];
        }
    }
}