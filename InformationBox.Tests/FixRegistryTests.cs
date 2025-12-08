using System;
using System.Collections.Generic;
using System.Linq;
using InformationBox.Config;
using InformationBox.Config.Fixes;
using Xunit;

namespace InformationBox.Tests;

public class FixRegistryTests
{
    [Fact]
    public void BuiltIns_ArePresent_AndIdsUnique()
    {
        var fixes = FixRegistry.BuildFixes(Array.Empty<FixAction>());
        Assert.True(fixes.Count >= 6);

        var ids = fixes.Select(f => f.Id).Where(id => !string.IsNullOrWhiteSpace(id));
        Assert.Equal(ids.Count(), ids.Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    [Fact]
    public void Override_WithEmptyCommand_ReusesBuiltInCommand()
    {
        var baseList = FixRegistry.BuildFixes(Array.Empty<FixAction>());
        var builtIn = baseList.First(f => string.Equals(f.Id, "restart-onedrive", StringComparison.OrdinalIgnoreCase));

        var overrideConfig = new List<FixAction>
        {
            new()
            {
                Id = "restart-onedrive",
                Name = "Custom name",
                Command = string.Empty,
                Visible = true
            }
        };

        var merged = FixRegistry.BuildFixes(overrideConfig);
        var mergedFix = merged.First(f => string.Equals(f.Id, "restart-onedrive", StringComparison.OrdinalIgnoreCase));

        Assert.Equal(builtIn.Command, mergedFix.Command); // reused built-in command
        Assert.Equal("Custom name", mergedFix.Name);       // override applied
    }

    [Fact]
    public void Override_CanHideBuiltIn()
    {
        var overrideConfig = new List<FixAction>
        {
            new()
            {
                Id = "reset-teams-cache",
                Visible = false
            }
        };

        var merged = FixRegistry.BuildFixes(overrideConfig);
        Assert.DoesNotContain(merged, f => string.Equals(f.Id, "reset-teams-cache", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Custom_ActionWithoutId_IsIncluded()
    {
        var custom = new FixAction
        {
            Name = "Custom script",
            Command = "Write-Output 'hi'",
            Category = FixCategory.Custom,
            Visible = true,
            Order = 99
        };

        var merged = FixRegistry.BuildFixes(new[] { custom });
        Assert.Contains(merged, f => string.Equals(f.Name, "Custom script", StringComparison.OrdinalIgnoreCase));
    }
}
