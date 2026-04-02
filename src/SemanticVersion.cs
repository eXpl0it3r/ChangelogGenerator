using System.Text.RegularExpressions;

namespace ChangeLogGenerator;

internal readonly record struct SemanticVersion(int Major, int Minor, int Patch) : IComparable<SemanticVersion>
{
    public SemanticVersion NextMinor() => new(Major, Minor + 1, 0);

    public int CompareTo(SemanticVersion other)
    {
        var majorComparison = Major.CompareTo(other.Major);
        if (majorComparison != 0)
        {
            return majorComparison;
        }

        var minorComparison = Minor.CompareTo(other.Minor);
        if (minorComparison != 0)
        {
            return minorComparison;
        }

        return Patch.CompareTo(other.Patch);
    }

    public override string ToString() => $"{Major}.{Minor}.{Patch}";

    public static SemanticVersion? TryParseFromTag(string tag)
    {
        var match = Regex.Match(tag, @"(\d+)\.(\d+)\.(\d+)");
        if (!match.Success)
        {
            return null;
        }

        return new SemanticVersion(
            int.Parse(match.Groups[1].Value),
            int.Parse(match.Groups[2].Value),
            int.Parse(match.Groups[3].Value));
    }
}

