using System.Collections.Generic;
using InformationBox.Config.Fixes;

namespace InformationBox.UI.ViewModels;

/// <summary>
/// Groups fix actions by category for display in the troubleshoot tab.
/// </summary>
public sealed record FixCategoryGroup(string Name, IReadOnlyList<FixAction> Actions);
