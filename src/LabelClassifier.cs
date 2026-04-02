namespace ChangeLogGenerator;

internal enum ChangelogModule
{
    General,
    System,
    Window,
    Graphics,
    Audio,
    Network,
    Unittests
}

internal enum ChangelogEntryType
{
    Unlabeled,
    Feature,
    Bugfix
}

internal static class LabelClassifier
{
    private static readonly Dictionary<string, ChangelogModule> ModuleLabelMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["m:sfml-audio"] = ChangelogModule.Audio,
        ["m:sfml-graphics"] = ChangelogModule.Graphics,
        ["m:sfml-network"] = ChangelogModule.Network,
        ["m:sfml-system"] = ChangelogModule.System,
        ["m:sfml-window"] = ChangelogModule.Window,
        ["m:unittest"] = ChangelogModule.Unittests
    };

    private static readonly Dictionary<string, ChangelogEntryType> TypeLabelMap = new(StringComparer.OrdinalIgnoreCase)
    {
        ["bug"] = ChangelogEntryType.Bugfix,
        ["feature"] = ChangelogEntryType.Feature
    };

    private static readonly (string Label, string Prefix)[] OsLabelMap =
    {
        ("p:android", "[Android]"),
        ("p:ios", "[iOS]"),
        ("p:linux", "[Linux]"),
        ("p:macos", "[macOS]"),
        ("p:windows", "[Windows]")
    };

    // Explicit display order for module sections in the rendered output.
    public static readonly IReadOnlyList<ChangelogModule> ModuleOrder =
    [
        ChangelogModule.General,
        ChangelogModule.System,
        ChangelogModule.Window,
        ChangelogModule.Graphics,
        ChangelogModule.Audio,
        ChangelogModule.Network,
        ChangelogModule.Unittests
    ];

    public static ChangelogModule ClassifyModule(IEnumerable<string> labels)
    {
        foreach (var label in labels)
        {
            if (ModuleLabelMap.TryGetValue(label, out var module))
            {
                return module;
            }
        }

        return ChangelogModule.General;
    }

    public static ChangelogEntryType ClassifyType(IEnumerable<string> labels)
    {
        foreach (var label in labels)
        {
            if (TypeLabelMap.TryGetValue(label, out var type))
            {
                return type;
            }
        }

        return ChangelogEntryType.Unlabeled;
    }

    public static IReadOnlyList<string> GetOsPrefixes(IEnumerable<string> labels)
    {
        var labelSet = new HashSet<string>(labels, StringComparer.OrdinalIgnoreCase);
        var prefixes = new List<string>();

        foreach (var (label, prefix) in OsLabelMap)
        {
            if (labelSet.Contains(label))
            {
                prefixes.Add(prefix);
            }
        }

        return prefixes;
    }
}

