using System.Threading.Tasks;
using InformationBox.Services;
using Xunit;

namespace InformationBox.Tests;

public class CommandRunnerSmokeTests
{
    [Fact]
    public async Task RunAsync_EchoesOutput_Succeeds()
    {
        var result = await CommandRunner.RunAsync("Write-Output \"hello\"");

        Assert.True(result.Success);
        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.Output);
    }
}
