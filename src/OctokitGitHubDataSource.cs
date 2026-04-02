using Octokit;

namespace ChangeLogGenerator;

internal sealed class OctokitGitHubDataSource(AppOptions options, string? commitToken) : IGitHubDataSource
{
    private readonly GitHubClient _client = BuildClient(commitToken);   // hits Owner/Repository

    private readonly Dictionary<int, IssueInfo?> _issueCache = new();
    private readonly Dictionary<int, IReadOnlyList<string>> _labelCache = new();
    private readonly Dictionary<string, IReadOnlyList<PullRequestInfo>> _commitPullRequestCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<int, IReadOnlyList<int>> _linkedIssueNumbersByPrCache = new();

    private static GitHubClient BuildClient(string? token)
    {
        var client = new GitHubClient(new ProductHeaderValue("ChangeLogGenerator"));
        if (!string.IsNullOrWhiteSpace(token))
        {
            client.Credentials = new Credentials(token);
        }

        return client;
    }

    private async Task<T> ExecuteMetadataAsync<T>(Func<GitHubClient, Task<T>> operation)
    {
        return await operation(_client);
    }

    public async Task<IReadOnlyList<TagInfo>> GetTagsAsync()
    {
        var tags = new List<TagInfo>();
        var page = 1;

        while (true)
        {
            var apiOptions = new ApiOptions { StartPage = page, PageSize = 100, PageCount = 1 };
            var pageTags = await _client.Repository.GetAllTags(options.Owner, options.Repository, apiOptions);
            if (pageTags.Count == 0)
            {
                break;
            }

            tags.AddRange(pageTags.Select(tag => new TagInfo(
                Name: tag.Name,
                Version: SemanticVersion.TryParseFromTag(tag.Name))));

            page++;
        }

        return tags;
    }

    public async Task<IReadOnlyList<CommitInfo>> GetCompareCommitsAsync(string baseTag, string headRef)
    {
        var compare = await _client.Repository.Commit.Compare(options.Owner, options.Repository, baseTag, headRef);

        return compare.Commits
            .Select(commit =>
            {
                var message = commit.Commit.Message ?? string.Empty;
                var subject = message.Split('\n', 2)[0].Trim();
                var authorLogin = commit.Author?.Login;
                return new CommitInfo(commit.Sha, subject, message, authorLogin);
            })
            .ToList();
    }

    public async Task<IReadOnlyList<PullRequestInfo>> GetPullRequestsForCommitAsync(string sha)
    {
        if (_commitPullRequestCache.TryGetValue(sha, out var cached))
        {
            return cached;
        }

        var endpoint = new Uri(
            $"repos/{options.Owner}/{options.Repository}/commits/{sha}/pulls",
            UriKind.Relative);

        var pullsResponse = await _client.Connection.Get<IReadOnlyList<CommitPullRequestDto>>(
            endpoint,
            new Dictionary<string, string> { ["per_page"] = "100" },
            "application/vnd.github+json");

        var mapped = pullsResponse.Body
            .Select(pr => new PullRequestInfo(
                Number: pr.Number,
                Title: pr.Title,
                HtmlUrl: pr.HtmlUrl,
                BaseRef: pr.Base.Ref,
                Body: pr.Body))
            .ToList();

        _commitPullRequestCache[sha] = mapped;
        return mapped;
    }

    public async Task<IReadOnlyList<int>> GetLinkedIssueNumbersForPullRequestAsync(int pullRequestNumber)
    {
        if (_linkedIssueNumbersByPrCache.TryGetValue(pullRequestNumber, out var cached))
        {
            return cached;
        }

        var endpoint = new Uri(
            $"repos/{options.Owner}/{options.Repository}/pulls/{pullRequestNumber}/issues",
            UriKind.Relative);

        try
        {
            var response = await ExecuteMetadataAsync(client => client.Connection.Get<IReadOnlyList<LinkedIssueDto>>(
                endpoint,
                new Dictionary<string, string> { ["per_page"] = "100" },
                "application/vnd.github+json"));

            var numbers = response.Body
                .Select(issue => issue.Number)
                .Distinct()
                .OrderBy(n => n)
                .ToList();

            _linkedIssueNumbersByPrCache[pullRequestNumber] = numbers;
            return numbers;
        }
        catch (Exception)
        {
            // Endpoint may return 404 on repos that don't support it; fall back gracefully.
            _linkedIssueNumbersByPrCache[pullRequestNumber] = Array.Empty<int>();
            return Array.Empty<int>();
        }
    }

    public async Task<IssueInfo?> GetIssueAsync(int number)
    {
        if (_issueCache.TryGetValue(number, out var cached))
        {
            return cached;
        }

        try
        {
            var issue = await ExecuteMetadataAsync(client =>
                client.Issue.Get(options.Owner, options.Repository, number));

            var mapped = new IssueInfo(
                Number: issue.Number,
                Title: issue.Title,
                HtmlUrl: issue.HtmlUrl,
                IsPullRequest: issue.PullRequest is not null);

            _issueCache[number] = mapped;
            return mapped;
        }
        catch (Exception)
        {
            _issueCache[number] = null;
            return null;
        }
    }

    public async Task<IReadOnlyList<string>> GetLabelsAsync(int issueOrPrNumber)
    {
        if (_labelCache.TryGetValue(issueOrPrNumber, out var cached))
        {
            return cached;
        }

        try
        {
            // Issue.Labels.GetAllForIssue works for both issues and pull requests.
            var ghLabels = await ExecuteMetadataAsync(client =>
                client.Issue.Labels.GetAllForIssue(
                    options.Owner, options.Repository, issueOrPrNumber));

            var names = ghLabels.Select(l => l.Name).ToList();
            _labelCache[issueOrPrNumber] = names;
            return names;
        }
        catch (Exception)
        {
            _labelCache[issueOrPrNumber] = Array.Empty<string>();
            return Array.Empty<string>();
        }
    }
}

internal sealed class CommitPullRequestDto
{
    public int Number { get; set; }
    public string Title { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;

    public string? Body { get; set; }

    public CommitPullRequestBaseDto Base { get; set; } = new();
}

internal sealed class CommitPullRequestBaseDto
{
    public string Ref { get; set; } = string.Empty;
}

internal sealed class LinkedIssueDto
{
    public int Number { get; set; }
}
