namespace ChangeLogGenerator;

internal interface IGitHubDataSource
{
    Task<IReadOnlyList<TagInfo>> GetTagsAsync();

    Task<IReadOnlyList<CommitInfo>> GetCompareCommitsAsync(string baseTag, string headRef);

    Task<IReadOnlyList<PullRequestInfo>> GetPullRequestsForCommitAsync(string sha);

    Task<IReadOnlyList<int>> GetLinkedIssueNumbersForPullRequestAsync(int pullRequestNumber);

    Task<IssueInfo?> GetIssueAsync(int number);

    Task<IReadOnlyList<string>> GetLabelsAsync(int issueOrPrNumber);
}
