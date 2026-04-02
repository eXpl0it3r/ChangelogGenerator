using Octokit;

namespace ChangeLogGenerator;

internal sealed class ChangelogGeneratorService(
    IGitHubDataSource gitHub,
    AppOptions options)
{
    public async Task<GenerationResult> GenerateAsync()
    {
        try
        {
            var tags = await gitHub.GetTagsAsync();
            var releaseTags = tags
                .Where(tag => tag.Version is not null)
                .OrderByDescending(tag => tag.Version)
                .ToList();

            if (releaseTags.Count == 0)
            {
                throw new InvalidOperationException("No semantic-version tags found.");
            }

            var lastRelease = releaseTags[0];
            var unreleasedVersion = lastRelease.Version!.Value.NextMinor();

            Console.WriteLine($"Comparing commits: {lastRelease.Name}...{options.DefaultBranch}");
            var commits = await gitHub.GetCompareCommitsAsync(lastRelease.Name, options.DefaultBranch);
            Console.WriteLine($"Found {commits.Count} commits to process.");

            var entriesByKey = new Dictionary<string, ChangelogEntry>(StringComparer.OrdinalIgnoreCase);
            var contributors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var processed = 0;

            foreach (var commit in commits)
            {
                processed++;
                var entry = await BuildEntryAsync(commit, processed, commits.Count);
                if (entry is null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(commit.AuthorLogin))
                {
                    contributors.Add(commit.AuthorLogin.Trim().TrimStart('@'));
                }

                if (entriesByKey.TryGetValue(entry.Key, out var existing))
                {
                    existing.Merge(entry);
                }
                else
                {
                    entriesByKey[entry.Key] = entry;
                }
            }

            Console.WriteLine();
            var markdown = MarkdownRenderer.Render(
                unreleasedVersion,
                lastRelease.Name,
                options.DefaultBranch,
                entriesByKey.Values,
                contributors);

            var outputPath = Path.Combine(Directory.GetCurrentDirectory(), options.OutputFileName);
            await File.WriteAllTextAsync(outputPath, markdown);

            return new GenerationResult(outputPath, entriesByKey.Count);
        }
        catch (RateLimitExceededException exception)
        {
            throw new InvalidOperationException(
                $"GitHub API rate-limited has been hit: {exception.Message}",
                exception);
        }
    }

    private async Task<ChangelogEntry?> BuildEntryAsync(CommitInfo commit, int index, int total)
    {
        var prs = await gitHub.GetPullRequestsForCommitAsync(commit.Sha);
        var prOnDefaultBranch = prs.FirstOrDefault(pr =>
            string.Equals(pr.BaseRef, options.DefaultBranch, StringComparison.OrdinalIgnoreCase));

        // Skip release-branch backports from other version lines.
        if (prOnDefaultBranch is null && prs.Count > 0)
        {
            return null;
        }

        var key = prOnDefaultBranch is not null ? $"pr:{prOnDefaultBranch.Number}" : $"commit:{commit.Sha}";
        var title = prOnDefaultBranch?.Title ?? ReferenceExtractor.NormalizeCommitSubject(commit.Subject);

        var links = new List<NumberedLink>();
        if (prOnDefaultBranch is not null)
        {
            links.Add(NumberedLink.FromPr(prOnDefaultBranch.Number));
        }

        // Closing references: "Closes/Fixes/Resolves #N" from commit message and PR body.
        var references = new HashSet<int>();
        foreach (var reference in ReferenceExtractor.ExtractClosingReferences(commit.Message))
        {
            references.Add(reference);
        }

        if (prOnDefaultBranch is not null)
        {
            // PR body: use closing-keyword extraction to avoid adding unrelated mentions.
            foreach (var reference in ReferenceExtractor.ExtractClosingReferences(prOnDefaultBranch.Body ?? string.Empty))
            {
                references.Add(reference);
            }

            // Also try the linked-issues API endpoint (returns empty on most repos but future-proof).
            var linkedIssues = await gitHub.GetLinkedIssueNumbersForPullRequestAsync(prOnDefaultBranch.Number);
            foreach (var linkedIssueNumber in linkedIssues)
            {
                references.Add(linkedIssueNumber);
            }

            references.Remove(prOnDefaultBranch.Number);
        }

        // Fetch labels from the PR and any linked issues/PRs (against the metadata/canonical repo).
        var allLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (prOnDefaultBranch is not null)
        {
            foreach (var label in await gitHub.GetLabelsAsync(prOnDefaultBranch.Number))
            {
                allLabels.Add(label);
            }
        }

        foreach (var reference in references.OrderBy(n => n))
        {
            var issue = await gitHub.GetIssueAsync(reference);
            if (issue is null)
            {
                continue;
            }

            links.Add(issue.IsPullRequest
                ? NumberedLink.FromPr(issue.Number)
                : NumberedLink.FromIssue(issue.Number));

            foreach (var label in await gitHub.GetLabelsAsync(issue.Number))
            {
                allLabels.Add(label);
            }

            if (prOnDefaultBranch is null && string.IsNullOrWhiteSpace(title))
            {
                title = issue.Title;
            }
        }

        if (string.IsNullOrWhiteSpace(title))
        {
            title = ReferenceExtractor.NormalizeCommitSubject(commit.Subject);
        }

        var prNum = prOnDefaultBranch?.Number.ToString() ?? commit.Sha[..7];
        var labelSummary = allLabels.Count > 0 ? string.Join(", ", allLabels) : "(none)";
        Console.Write($"\r[{index}/{total}] PR #{prNum} → module: {LabelClassifier.ClassifyModule(allLabels),-10} labels: {labelSummary}");

        return new ChangelogEntry(key, title, links, allLabels);
    }
}
