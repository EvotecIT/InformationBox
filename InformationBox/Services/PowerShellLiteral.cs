namespace InformationBox.Services;

/// <summary>Encodes data as a single-quoted PowerShell literal, including typographic quote delimiters.</summary>
internal static class PowerShellLiteral
{
    internal static string Quote(string? value) => "'" + (value ?? string.Empty)
        .Replace("'", "''")
        .Replace("\u2018", "\u2018\u2018")
        .Replace("\u2019", "\u2019\u2019")
        .Replace("\u201A", "\u201A\u201A")
        .Replace("\u201B", "\u201B\u201B") + "'";
}
