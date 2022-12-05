namespace MrWatts.MSBuild.UnityPostProcessor
{
    public interface IUnityBuiltinAssemblyDetector
    {
        string[] Detect(string unityProjectFolder);
    }
}