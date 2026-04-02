namespace ChangeLogGenerator;

internal sealed record TagInfo(string Name, SemanticVersion? Version);
internal sealed record CommitInfo(string Sha, string Subject, string Message, string? AuthorLogin);
internal sealed record PullRequestInfo(int Number, string Title, string HtmlUrl, string BaseRef, string? Body);
internal sealed record IssueInfo(int Number, string Title, string HtmlUrl, bool IsPullRequest);
internal sealed record GenerationResult(string OutputPath, int EntryCount);

internal enum LinkKind
{
    PullRequest,
    Issue
}

internal sealed record NumberedLink(int Number, LinkKind Kind)
{
    public static NumberedLink FromPr(int number) => new(number, LinkKind.PullRequest);

    public static NumberedLink FromIssue(int number) => new(number, LinkKind.Issue);

    public string ToDisplay() => $"#{Number}";
}

internal sealed class ChangelogEntry
{
    private readonly Dictionary<int, NumberedLink> _linksByNumber = new();
    private readonly HashSet<string> _labels = new(StringComparer.OrdinalIgnoreCase);

    public ChangelogEntry(string key, string title, IEnumerable<NumberedLink> links, IEnumerable<string> entryLabels)
    {
        Key = key;
        Title = title;

        foreach (var link in links)
        {
            _linksByNumber[link.Number] = link;
        }

        foreach (var label in entryLabels)
        {
            _labels.Add(label);
        }
    }

    public string Key { get; }

    public string Title { get; private set; }

    public ChangelogModule Module => LabelClassifier.ClassifyModule(_labels);

    public ChangelogEntryType EntryType => LabelClassifier.ClassifyType(_labels);

    public IReadOnlyList<string> OsPrefixes => LabelClassifier.GetOsPrefixes(_labels);

    public void Merge(ChangelogEntry other)
    {
        foreach (var link in other._linksByNumber.Values)
        {
            _linksByNumber[link.Number] = link;
        }

        foreach (var label in other._labels)
        {
            _labels.Add(label);
        }

        if (Title.StartsWith("Merge", StringComparison.OrdinalIgnoreCase) &&
            !other.Title.StartsWith("Merge", StringComparison.OrdinalIgnoreCase))
        {
            Title = other.Title;
        }
    }

    public string GetReferenceList()
    {
        return string.Join(", ", _linksByNumber.Values
            .OrderBy(link => link.Kind)
            .ThenBy(link => link.Number)
            .Select(link => link.ToDisplay()));
    }
}

