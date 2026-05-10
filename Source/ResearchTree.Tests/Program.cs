using FluffyResearchTree;

var tests = new (string Name, Action Test)[]
{
    ("first run selects every valid tab", FirstRunSelectsEveryValidTab),
    ("stale saved tabs are pruned", StaleSavedTabsArePruned),
    ("empty initialized selection stays empty", EmptyInitializedSelectionStaysEmpty),
    ("tab names are compared case-insensitively", TabNamesAreComparedCaseInsensitively),
    ("null inputs normalize safely", NullInputsNormalizeSafely),
    ("legacy selection migrates to default profile", LegacySelectionMigratesToDefaultProfile),
    ("profile tabs are pruned independently", ProfileTabsArePrunedIndependently),
    ("missing active profile falls back to first profile", MissingActiveProfileFallsBackToFirstProfile),
    ("unique profile names are generated", UniqueProfileNamesAreGenerated),
    ("unique profile ids are generated", UniqueProfileIdsAreGenerated)
};

foreach (var (name, test) in tests)
{
    test();
    Console.WriteLine($"PASS {name}");
}

Console.WriteLine($"{tests.Length} tests passed.");

static void FirstRunSelectsEveryValidTab()
{
    var result = ResearchTabSelection.Normalize(["Core", "Ideology"], [], tabsInitialized: false);

    AssertTrue(result.TabsInitialized, "first run should initialize tab state");
    AssertTrue(result.Dirty, "first run should mark defaults as dirty");
    AssertSet(["Core", "Ideology"], result.IncludedTabs);
}

static void StaleSavedTabsArePruned()
{
    var result = ResearchTabSelection.Normalize(["Core", "Ideology"], ["Core", "RemovedByUninstalledMod"], true);

    AssertTrue(result.TabsInitialized, "existing initialized state should be preserved");
    AssertTrue(result.Dirty, "removing stale tabs should mark settings dirty");
    AssertSet(["Core"], result.IncludedTabs);
}

static void EmptyInitializedSelectionStaysEmpty()
{
    var result = ResearchTabSelection.Normalize(["Core", "Ideology"], [], tabsInitialized: true);

    AssertTrue(result.TabsInitialized, "initialized empty state should stay initialized");
    AssertFalse(result.Dirty, "deliberate empty selection should not be rewritten");
    AssertSet([], result.IncludedTabs);
}

static void TabNamesAreComparedCaseInsensitively()
{
    var result = ResearchTabSelection.Normalize(["Core"], ["core"], tabsInitialized: true);

    AssertFalse(result.Dirty, "case-only differences should not rewrite settings");
    AssertTrue(result.IncludedTabs.Contains("CORE"), "selection lookup should be case-insensitive");
}

static void NullInputsNormalizeSafely()
{
    var result = ResearchTabSelection.Normalize(null!, null!, tabsInitialized: false);

    AssertTrue(result.TabsInitialized, "null first-run state should still initialize");
    AssertTrue(result.Dirty, "normalizing uninitialized settings should be written once");
    AssertSet([], result.IncludedTabs);
}

static void LegacySelectionMigratesToDefaultProfile()
{
    var result = ResearchTabProfileSelection.Normalize(
        ["Core", "Ideology"],
        [],
        null!,
        ["Core"],
        tabsInitialized: true);

    AssertTrue(result.Dirty, "profile migration should be written once");
    AssertEqual(ResearchTabProfileSelection.DefaultProfileId, result.ActiveProfileId);
    AssertEqual(1, result.Profiles.Count);
    AssertSet(["Core"], result.Profiles[0].IncludedTabs);
}

static void ProfileTabsArePrunedIndependently()
{
    var result = ResearchTabProfileSelection.Normalize(
        ["Core", "Royalty"],
        [
            new ResearchTabProfileData("a", "A", ["Core", "Removed"]),
            new ResearchTabProfileData("b", "B", [])
        ],
        "b",
        [],
        tabsInitialized: true);

    AssertTrue(result.Dirty, "stale profile tabs should mark settings dirty");
    AssertEqual("b", result.ActiveProfileId);
    AssertSet(["Core"], result.Profiles[0].IncludedTabs);
    AssertSet([], result.Profiles[1].IncludedTabs);
}

static void MissingActiveProfileFallsBackToFirstProfile()
{
    var result = ResearchTabProfileSelection.Normalize(
        ["Core"],
        [
            new ResearchTabProfileData("a", "A", ["Core"]),
            new ResearchTabProfileData("b", "B", [])
        ],
        "missing",
        [],
        tabsInitialized: true);

    AssertTrue(result.Dirty, "missing active profile should be corrected");
    AssertEqual("a", result.ActiveProfileId);
}

static void UniqueProfileNamesAreGenerated()
{
    var profiles = new[]
    {
        new ResearchTabProfileData("a", "Default", []),
        new ResearchTabProfileData("b", "Default (2)", [])
    };

    AssertEqual("Default (3)", ResearchTabProfileSelection.CreateUniqueName(profiles, "Default"));
}

static void UniqueProfileIdsAreGenerated()
{
    var profiles = new[]
    {
        new ResearchTabProfileData("profile-1", "A", []),
        new ResearchTabProfileData("profile-2", "B", [])
    };

    AssertEqual("profile-3", ResearchTabProfileSelection.CreateUniqueId(profiles));
}

static void AssertSet(IEnumerable<string> expected, IEnumerable<string> actual)
{
    var expectedSet = new HashSet<string>(expected, StringComparer.OrdinalIgnoreCase);
    var actualSet = new HashSet<string>(actual, StringComparer.OrdinalIgnoreCase);

    if (!expectedSet.SetEquals(actualSet))
    {
        throw new InvalidOperationException(
            $"Expected [{string.Join(", ", expectedSet)}], got [{string.Join(", ", actualSet)}].");
    }
}

static void AssertTrue(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

static void AssertFalse(bool condition, string message)
{
    AssertTrue(!condition, message);
}

static void AssertEqual<T>(T expected, T actual)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException($"Expected {expected}, got {actual}.");
    }
}
