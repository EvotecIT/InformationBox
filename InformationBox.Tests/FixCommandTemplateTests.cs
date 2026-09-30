using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using InformationBox.Config;
using InformationBox.Config.Fixes;
using InformationBox.Services;
using Xunit;

namespace InformationBox.Tests;

public class FixCommandTemplateTests
{
    [Fact]
    public async Task PowerShellQuoteVariants_InBrandingRemainLiteral()
    {
        var values = new[] { '\'', '\u2018', '\u2019', '\u201A', '\u201B' }
            .Select(quote => $"value {quote}; Write-Output UNEXPECTED; #")
            .ToArray();
        var command = string.Join("; ", values.Select(value => FixCommandTemplate.Expand(
            "Write-Output ([Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes({{SUPPORT_EMAIL}})))",
            new Branding { SupportEmail = value })));

        var result = await CommandRunner.RunAsync(command);

        Assert.True(result.Success);
        Assert.Equal(values.Select(value => Convert.ToBase64String(Encoding.Unicode.GetBytes(value))),
            result.Output.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }

    [Fact]
    public void EmailLogs_TokensInsideBrandingAreNotExpandedAgain()
    {
        var action = Assert.Single(FixRegistry.BuildFixes(Array.Empty<FixAction>()), fix => fix.Id == "email-logs");
        var branding = new Branding
        {
            SupportEmail = "{{COMPANY_NAME}}",
            CompanyName = "'); Write-Output UNEXPECTED; #"
        };

        var command = FixCommandTemplate.Expand(action.Command, branding);

        Assert.Contains("'{{COMPANY_NAME}}'", command);
        Assert.DoesNotContain("UNEXPECTED", command);
    }

    [Fact]
    public async Task BrandingWithTokensAndPowerShellSyntax_IsEmittedOnlyAsData()
    {
        var branding = new Branding
        {
            SupportEmail = "{{COMPANY_NAME}}",
            CompanyName = "'); Write-Output UNEXPECTED; # {{PRODUCT_NAME}}",
            ProductName = "Product 'quoted' $(Write-Output UNEXPECTED)"
        };
        var command = FixCommandTemplate.Expand(
            "Write-Output {{SUPPORT_EMAIL}}; Write-Output {{COMPANY_NAME}}; Write-Output {{PRODUCT_NAME}}", branding);

        var result = await CommandRunner.RunAsync(command);

        Assert.True(result.Success);
        Assert.Equal(new[] { branding.SupportEmail, branding.CompanyName, branding.ProductName },
            result.Output.Replace("\r\n", "\n").Split('\n', StringSplitOptions.RemoveEmptyEntries));
    }
}
