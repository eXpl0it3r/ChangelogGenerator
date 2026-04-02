using System.Text.RegularExpressions;

namespace ChangeLogGenerator;

internal static partial class ReferenceExtractor
{
    [GeneratedRegex(@"\s*\(#\d+\)\s*$")]
    private static partial Regex CommitSuffixRegex();

    // Matches GitHub auto-close keywords: "Closes #123", "Fixes #123", "Resolves #123"
    [GeneratedRegex(@"\b(?:close[sd]?|fix(?:e[sd])?|resolve[sd]?)\s+#(\d+)", RegexOptions.IgnoreCase)]
    private static partial Regex ClosingReferenceRegex();

    public static string NormalizeCommitSubject(string subject)
    {
        var normalized = CommitSuffixRegex().Replace(subject, string.Empty).Trim();
        return string.IsNullOrWhiteSpace(normalized) ? subject.Trim() : normalized;
    }

    public static IEnumerable<int> ExtractClosingReferences(string text)
    {
        var matches = ClosingReferenceRegex().Matches(text);
        foreach (Match match in matches)
        {
            if (int.TryParse(match.Groups[1].Value, out var number))
            {
                yield return number;
            }
        }
    }
}
