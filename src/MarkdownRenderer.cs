using System.Text;

namespace ChangeLogGenerator;

internal sealed class MarkdownRenderer
{
    public static string Render(
        SemanticVersion unreleasedVersion,
        string lastReleaseTag,
        string defaultBranch,
        IEnumerable<ChangelogEntry> entries,
        IEnumerable<string> contributors)
    {
        var allEntries = entries
            .OrderBy(entry => entry.Title, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var byModule = allEntries
            .GroupBy(entry => entry.Module)
            .ToDictionary(group => group.Key, group => group.ToList());

        var buffer = new StringBuilder();
        buffer.AppendLine("# Changelog");
        buffer.AppendLine();
        buffer.AppendLine($"## SFML {unreleasedVersion} (Unreleased)");
        buffer.AppendLine();
        buffer.AppendLine($"> Changes since `{lastReleaseTag}` on `{defaultBranch}`.");

        foreach (var module in LabelClassifier.ModuleOrder)
        {
            if (!byModule.TryGetValue(module, out var moduleEntries) || moduleEntries.Count == 0)
            {
                continue;
            }

            buffer.AppendLine();
            buffer.AppendLine($"### {module}");

            if (module == ChangelogModule.General)
            {
                // General section: flat list without Features/Bugfixes subheadings.
                buffer.AppendLine();
                AppendEntries(buffer, moduleEntries);
            }
            else
            {
                var features = moduleEntries.Where(e => e.EntryType == ChangelogEntryType.Feature).ToList();
                var bugfixes = moduleEntries.Where(e => e.EntryType == ChangelogEntryType.Bugfix).ToList();
                var unlabeled = moduleEntries.Where(e => e.EntryType == ChangelogEntryType.Unlabeled).ToList();

                if (features.Count > 0)
                {
                    buffer.AppendLine();
                    buffer.AppendLine("**Features**");
                    buffer.AppendLine();
                    AppendEntries(buffer, features);
                }

                if (bugfixes.Count > 0)
                {
                    buffer.AppendLine();
                    buffer.AppendLine("**Bugfixes**");
                    buffer.AppendLine();
                    AppendEntries(buffer, bugfixes);
                }

                if (unlabeled.Count > 0)
                {
                    buffer.AppendLine();
                    AppendEntries(buffer, unlabeled);
                }
            }
        }

        var contributorList = contributors
            .Where(contributor => !string.IsNullOrWhiteSpace(contributor))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(contributor => contributor, StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (contributorList.Count > 0)
        {
            buffer.AppendLine();
            buffer.AppendLine("### Contributors");
            buffer.AppendLine();

            foreach (var contributor in contributorList)
            {
                buffer.AppendLine($"-   @{contributor}");
            }
        }

        return buffer.ToString();
    }

    private static void AppendEntries(StringBuilder buffer, IEnumerable<ChangelogEntry> entries)
    {
        foreach (var entry in entries)
        {
            var osPrefix = entry.OsPrefixes.Count > 0
                ? string.Join(" ", entry.OsPrefixes) + " "
                : string.Empty;
            var references = entry.GetReferenceList();
            if (string.IsNullOrWhiteSpace(references))
            {
                buffer.AppendLine($"-   {osPrefix}{entry.Title}");
            }
            else
            {
                buffer.AppendLine($"-   {osPrefix}{entry.Title} ({references})");
            }
        }
    }
}
