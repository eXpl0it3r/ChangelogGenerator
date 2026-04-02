using ChangeLogGenerator;

var options = AppOptions.Default;

var token = Environment.GetEnvironmentVariable("GITHUB_TOKEN");

Console.WriteLine($"Commit source : {options.Owner}/{options.Repository}");
Console.WriteLine($"Commit token  : {(string.IsNullOrEmpty(token) ? "none (unauthenticated)" : "set")}");
Console.WriteLine();

try
{
    Console.WriteLine($"Loading release tags for {options.Owner}/{options.Repository}...");

    var gitHub = new OctokitGitHubDataSource(options, token);
    var generator = new ChangelogGeneratorService(gitHub, options);

    var result = await generator.GenerateAsync();
    Console.WriteLine($"Generated {result.OutputPath} with {result.EntryCount} grouped entries.");
}
catch (Exception ex)
{
    Console.Error.WriteLine($"\nGeneration failed: {ex.Message}");
    Environment.ExitCode = 1;
}
