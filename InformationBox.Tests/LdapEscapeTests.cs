using InformationBox.Services;
using Xunit;

namespace InformationBox.Tests;

public class LdapEscapeTests
{
    [Theory]
    [InlineData("user*", "user\\2a")]
    [InlineData("us(er)", "us\\28er\\29")]
    [InlineData("slash\\", "slash\\5c")]
    public void EscapeLdap_EscapesSpecialCharacters(string input, string expected)
    {
        var escaped = typeof(GraphPasswordAgeProvider)
            .GetMethod("EscapeLdap", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)!
            .Invoke(null, new object[] { input }) as string;

        Assert.Equal(expected, escaped);
    }
}
