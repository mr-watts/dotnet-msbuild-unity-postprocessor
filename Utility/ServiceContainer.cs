namespace MrWatts.MSBuild.UnityPostProcessor
{
    internal sealed class ServiceContainer
    {
        internal MD5Hasher MD5Hasher { get; }
        internal UnityGuidGenerator UnityGuidGenerator { get; }
        internal UnityMetaFileGenerator UnityMetaFileGenerator { get; }
        internal IUnityBuiltinAssemblyDetector UnityBuiltinAssemblyDetector { get; }

        internal ServiceContainer()
        {
            MD5Hasher = new MD5Hasher();
            UnityGuidGenerator = new UnityGuidGenerator(MD5Hasher);
            UnityMetaFileGenerator = new UnityMetaFileGenerator(UnityGuidGenerator);
            UnityBuiltinAssemblyDetector = new MemoizingUnityBuiltinAssemblyDetector(new UnityBuiltinAssemblyDetector());
        }
    }
}