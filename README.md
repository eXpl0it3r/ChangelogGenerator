# Changelog Generator (for SFML)

Small CLI tool to generate changelog entries for an unreleased version of [SFML](https://github.com/SFML/SFML).

In theory, this could be used for other GitHub repositories as well, but it's been created for SFML's specific changelog layout without templating support.

## What It Does

- Finds the latest semantic-version tag
- Compares that tag against `master` branch
- Skips commits that only belong to non-`master` PRs (release-branch backports)
- Groups commits by PR when possible (using PR title)
- Links related PR and issue references
- Groups changes by type and module (using PR labels)
- Prefixes issues/PR entries with OS tags when platform labels are present
- Adds a Contributions section with commit authors (@usernamen)
- Writes changelog file as Markdown

## Built With

- .NET 10
- [Octokit](https://github.com/octokit/octokit.net)

## License

ChangelogGenerator is distributed under the zlib license. See LICENSE.
