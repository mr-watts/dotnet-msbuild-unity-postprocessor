using System.IO;
using System.Threading.Tasks;

namespace MrWatts.MSBuild.UnityPostProcessor
{
    public sealed class UnityMetaFileGenerator
    {
        private readonly UnityGuidGenerator unityGuidGenerator;

        public UnityMetaFileGenerator(UnityGuidGenerator unityGuidGenerator)
        {
            this.unityGuidGenerator = unityGuidGenerator;
        }

        public Task GenerateAsync(string path, string additionalContents)
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