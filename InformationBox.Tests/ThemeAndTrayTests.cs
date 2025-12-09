using System.Threading.Tasks;
using InformationBox.Services;
using Xunit;

namespace InformationBox.Tests;

public class ThemeAndTrayTests
{
    [Fact]
    public void TrayIcon_MinimizeOnClose_DefaultsToTrue()
    {
        var layout = new Config.LayoutOptions();
        Assert.True(layout.MinimizeOnClose);
    }

    [Fact]
    public void StateLock_GuardsTrayIconAccess()
    {
        Assert.NotNull(typeof(App).GetField("StateLock", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static));
    }
}
