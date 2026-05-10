using System;
using System.Collections.Generic;
using System.Linq;

namespace FluffyResearchTree;

internal sealed class ResearchTabProfileData
{
    public ResearchTabProfileData(string id, string name, IEnumerable<string> includedTabs)
    {
        Id = id;
        Name = name;
        IncludedTabs = new HashSet<string>(includedTabs ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
    }

    public string Id { get; set; }

    public string Name { get; set; }

    public HashSet<string> IncludedTabs { get; }

    public ResearchTabProfileData Clone()
    {
        return new ResearchTabProfileData(Id, Name, IncludedTabs);
    }
}

internal static class ResearchTabProfileSelection
{
    public const string DefaultProfileId = "default";

    private const string DefaultProfileName = "Default";

    internal readonly struct Result
    {
        public Result(List<ResearchTabProfileData> profiles, string activeProfileId, bool tabsInitialized, bool dirty)
        {
            Profiles = profiles;
            ActiveProfileId = activeProfileId;
            TabsInitialized = tabsInitialized;
            Dirty = dirty;
        }

        public List<ResearchTabProfileData> Profiles { get; }

        public string ActiveProfileId { get; }

        public bool TabsInitialized { get; }

        public bool Dirty { get; }
    }

    public static Result Normalize(
        IEnumerable<string> availableTabNames,
        IEnumerable<ResearchTabProfileData> savedProfiles,
        string activeProfileId,
        IEnumerable<string> legacyIncludedTabNames,
        bool tabsInitialized)
    {
        var validTabNames = new HashSet<string>(
            (availableTabNames ?? Enumerable.Empty<string>())
            .Where(name => !string.IsNullOrWhiteSpace(name)),
            StringComparer.OrdinalIgnoreCase);

        var profiles = (savedProfiles ?? Enumerable.Empty<ResearchTabProfileData>())
            .Where(profile => profile != null)
            .Select(profile => profile.Clone())
            .ToList();

        var dirty = false;
        if (profiles.Count == 0)
        {
            var normalizedLegacy = ResearchTabSelection.Normalize(
                validTabNames,
                legacyIncludedTabNames,
                tabsInitialized);

            profiles.Add(new ResearchTabProfileData(
                DefaultProfileId,
                DefaultProfileName,
                normalizedLegacy.IncludedTabs));

            tabsInitialized = normalizedLegacy.TabsInitialized;
            dirty = true;
        }

        dirty |= EnsureStableProfileIds(profiles);
        dirty |= EnsureProfileNames(profiles);
        dirty |= PruneUnavailableTabs(profiles, validTabNames);

        if (string.IsNullOrWhiteSpace(activeProfileId) ||
            profiles.All(profile => !string.Equals(profile.Id, activeProfileId, StringComparison.OrdinalIgnoreCase)))
        {
            activeProfileId = profiles[0].Id;
            dirty = true;
        }

        if (!tabsInitialized)
        {
            tabsInitialized = true;
            dirty = true;
        }

        return new Result(profiles, activeProfileId, tabsInitialized, dirty);
    }

    public static string CreateUniqueId(IEnumerable<ResearchTabProfileData> profiles)
    {
        var existing = new HashSet<string>(
            (profiles ?? Enumerable.Empty<ResearchTabProfileData>())
            .Where(profile => profile != null && !string.IsNullOrWhiteSpace(profile.Id))
            .Select(profile => profile.Id),
            StringComparer.OrdinalIgnoreCase);

        var index = 1;
        string id;
        do
        {
            id = $"profile-{index++}";
        } while (existing.Contains(id));

        return id;
    }

    public static string CreateUniqueName(IEnumerable<ResearchTabProfileData> profiles, string baseName)
    {
        baseName = string.IsNullOrWhiteSpace(baseName) ? DefaultProfileName : baseName.Trim();

        var existing = new HashSet<string>(
            (profiles ?? Enumerable.Empty<ResearchTabProfileData>())
            .Where(profile => profile != null && !string.IsNullOrWhiteSpace(profile.Name))
            .Select(profile => profile.Name),
            StringComparer.OrdinalIgnoreCase);

        if (!existing.Contains(baseName))
        {
            return baseName;
        }

        var index = 2;
        string candidate;
        do
        {
            candidate = $"{baseName} ({index++})";
        } while (existing.Contains(candidate));

        return candidate;
    }

    private static bool EnsureStableProfileIds(List<ResearchTabProfileData> profiles)
    {
        var dirty = false;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            if (string.IsNullOrWhiteSpace(profile.Id) || !used.Add(profile.Id))
            {
                profile.Id = i == 0 && !used.Contains(DefaultProfileId)
                    ? DefaultProfileId
                    : CreateUniqueId(profiles.Take(i));
                used.Add(profile.Id);
                dirty = true;
            }
        }

        return dirty;
    }

    private static bool EnsureProfileNames(IEnumerable<ResearchTabProfileData> profiles)
    {
        var dirty = false;
        var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var profile in profiles)
        {
            var name = string.IsNullOrWhiteSpace(profile.Name) ? DefaultProfileName : profile.Name.Trim();
            if (!used.Add(name))
            {
                name = CreateUniqueName(
                    used.Select(existing => new ResearchTabProfileData(existing, existing, Enumerable.Empty<string>())),
                    name);
                used.Add(name);
            }

            if (profile.Name != name)
            {
                profile.Name = name;
                dirty = true;
            }
        }

        return dirty;
    }

    private static bool PruneUnavailableTabs(IEnumerable<ResearchTabProfileData> profiles, HashSet<string> validTabNames)
    {
        var dirty = false;

        foreach (var profile in profiles)
        {
            var before = profile.IncludedTabs.ToList();
            profile.IncludedTabs.RemoveWhere(name => !validTabNames.Contains(name));
            dirty |= !new HashSet<string>(before, StringComparer.OrdinalIgnoreCase).SetEquals(profile.IncludedTabs);
        }

        return dirty;
    }
}
