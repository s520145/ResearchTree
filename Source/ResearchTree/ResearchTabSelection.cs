using System;
using System.Collections.Generic;
using System.Linq;

namespace FluffyResearchTree;

internal static class ResearchTabSelection
{
    internal readonly struct Result
    {
        public Result(HashSet<string> includedTabs, bool tabsInitialized, bool dirty)
        {
            IncludedTabs = includedTabs;
            TabsInitialized = tabsInitialized;
            Dirty = dirty;
        }

        public HashSet<string> IncludedTabs { get; }

        public bool TabsInitialized { get; }

        public bool Dirty { get; }
    }

    public static Result Normalize(
        IEnumerable<string> availableTabNames,
        IEnumerable<string> includedTabNames,
        bool tabsInitialized)
    {
        var validTabNames = new HashSet<string>(
            (availableTabNames ?? Enumerable.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.OrdinalIgnoreCase);

        var includedTabs = new HashSet<string>(
            (includedTabNames ?? Enumerable.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.OrdinalIgnoreCase);

        var before = includedTabs.ToList();
        includedTabs.RemoveWhere(name => !validTabNames.Contains(name));
        var dirty = !SameSet(before, includedTabs);

        if (!tabsInitialized)
        {
            includedTabs = new HashSet<string>(validTabNames, StringComparer.OrdinalIgnoreCase);
            tabsInitialized = true;
            dirty = true;
        }

        return new Result(includedTabs, tabsInitialized, dirty);
    }

    private static bool SameSet(IEnumerable<string> before, HashSet<string> after)
    {
        var previous = new HashSet<string>(before, StringComparer.OrdinalIgnoreCase);
        return previous.SetEquals(after);
    }
}
