using TbotUltra.Worker;
using Xunit;

namespace TbotUltra.Worker.Tests;

public sealed class ProjectRootLocatorTests
{
    [Fact]
    public void FindProjectRoot_UsesSolutionFile_WhenRuntimeConfigIsAbsent()
    {
        var root = Path.Combine(Path.GetTempPath(), $"tbot-root-locator-{Guid.NewGuid():N}");
        var nestedDirectory = Path.Combine(root, "src", "test", "bin");
        Directory.CreateDirectory(nestedDirectory);

        try
        {
            File.WriteAllText(Path.Combine(root, "TbotUltra.sln"), string.Empty);

            var actual = ProjectRootLocator.FindProjectRoot(nestedDirectory);

            Assert.Equal(root, actual);
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
