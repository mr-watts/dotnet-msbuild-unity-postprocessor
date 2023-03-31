using System.IO;
using System.Threading.Tasks;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    internal sealed class UnityMetaFileGenerator
    {
        private readonly UnityGuidGenerator unityGuidGenerator;

        internal UnityMetaFileGenerator(UnityGuidGenerator unityGuidGenerator)
        {
            this.unityGuidGenerator = unityGuidGenerator;
        }

        internal Task GenerateAsync(string path, string additionalContents)
        {
            return File.WriteAllTextAsync(
                path,
                "fileFormatVersion: 2\n" +
                $"guid: {unityGuidGenerator.Generate()}\n" +
                additionalContents + "\n"
            );
        }
    }
}