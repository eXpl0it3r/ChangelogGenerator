namespace ChangeLogGenerator;

internal sealed record AppOptions(
    string Owner,
    string Repository,
    string DefaultBranch,
    string OutputFileName)
{
    public static AppOptions Default { get; } = new(
        Owner: "SFML",
        Repository: "SFML",
        DefaultBranch: "master",
        OutputFileName: "changelog.generated.md");
}
