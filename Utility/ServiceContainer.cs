namespace MrWatts.MSBuild.UnityPostProcessor
{
    public sealed class ServiceContainer
    {
        public MD5Hasher MD5Hasher { get; }
        public UnityGuidGenerator UnityGuidGenerator { get; }
        public UnityMetaFileGenerator UnityMetaFileGenerator { get; }
        public IUnityBuiltinAssemblyDetector UnityBuiltinAssemblyDetector { get; }

        public ServiceContainer()
        {
            MD5Hasher = new MD5Hasher();
            UnityGuidGenerator = new UnityGuidGenerator(MD5Hasher);
            UnityMetaFileGenerator = new UnityMetaFileGenerator(UnityGuidGenerator);
            UnityBuiltinAssemblyDetector = new MemoizingUnityBuiltinAssemblyDetector(new UnityBuiltinAssemblyDetector());
        }
    }
}