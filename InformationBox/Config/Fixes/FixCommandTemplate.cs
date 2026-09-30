using System.Text.RegularExpressions;
using InformationBox.Services;

namespace InformationBox.Config.Fixes;

/// <summary>
/// Expands built-in command tokens once, keeping branding values as PowerShell literal data.
/// Tokens must appear as standalone expressions in the original command template.
/// </summary>
internal static class FixCommandTemplate
{
    internal static string Expand(string command, Branding branding)
    {
        return Regex.Replace(command, @"\{\{(?:SUPPORT_EMAIL|COMPANY_NAME|PRODUCT_NAME)\}\}", match =>
        {
            var value = match.Value switch
            {
                "{{SUPPORT_EMAIL}}" => branding.SupportEmail,
                "{{COMPANY_NAME}}" => branding.CompanyName,
                "{{PRODUCT_NAME}}" => branding.ProductName,
                _ => null
            };
            return PowerShellLiteral.Quote(value);
        });
    }
}
