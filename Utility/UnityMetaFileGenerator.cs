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

        internal Task GenerateAsync(string path, string additionalContents, string? guid = null)
        {
            guid ??= unityGuidGenerator.Generate();

            return File.WriteAllTextAsync(
                path,
                "fileFormatVersion: 2\n" +
                $"guid: {guid}\n" +
                additionalContents + "\n"
            );
        }
    }
}