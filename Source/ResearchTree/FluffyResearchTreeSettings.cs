using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

/// <summary>
///     Definition of the settings for the mod
/// </summary>
internal class FluffyResearchTreeSettings : ModSettings
{
    public Color BackgroundColor = new(0f, 0f, 0f, 0.1f);
    public bool CtrlFunction = true;
    public bool HideNodesBlockedByTechLevel;
    public int LoadType = Constants.LoadTypeLoadInBackground;
    public bool NoIdeologyPopup;
    public bool OverrideResearch = true;
    public bool PauseOnOpen = true;
    public bool ShowResearchLines = true;

    public bool ShowCompletion;
    public bool ReverseShift;

    public bool VerboseLogging;

    // Saved ResearchTabDef names included in the generated tree.
    public HashSet<string> IncludedTabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    public List<ResearchTabProfile> TabProfiles = new();

    public string ActiveTabProfileId;

    // Distinguishes first-run defaults from a deliberate empty tab selection.
    public bool TabsInitialized;

    public bool SkipCompleted = true;

    public bool VisualGroupByTab = true;

    // Runtime cache of selectable research tabs.
    [Unsaved(false)] public List<ResearchTabDef> AllTabsCache;

    private static readonly PropertyInfo ResearchTabProperty =
        AccessTools.Property(typeof(ResearchProjectDef), "tab")
        ?? AccessTools.Property(typeof(ResearchProjectDef), "researchTab")
        ?? AccessTools.Property(typeof(ResearchProjectDef), "researchTabDef");

    private static readonly FieldInfo ResearchTabField =
        AccessTools.Field(typeof(ResearchProjectDef), "tab")
        ?? AccessTools.Field(typeof(ResearchProjectDef), "researchTab")
        ?? AccessTools.Field(typeof(ResearchProjectDef), "researchTabDef");

    /// <summary>
    ///     Saving and loading the values
    /// </summary>
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref PauseOnOpen, "PauseOnOpen", true);
        Scribe_Values.Look(ref ShowResearchLines, "ShowResearchLines", true);
        Scribe_Values.Look(ref CtrlFunction, "CtrlFunction", true);
        Scribe_Values.Look(ref OverrideResearch, "OverrideResearch", true);
        Scribe_Values.Look(ref ShowCompletion, "ShowCompletion");
        Scribe_Values.Look(ref ReverseShift, "ReverseShift");
        Scribe_Values.Look(ref NoIdeologyPopup, "NoIdeologyPopup");
        Scribe_Values.Look(ref HideNodesBlockedByTechLevel, "HideNodesBlockedByTechLevel");
        Scribe_Values.Look(ref VerboseLogging, "VerboseLogging");
        Scribe_Values.Look(ref LoadType, "LoadType", 1);
        Scribe_Values.Look(ref BackgroundColor, "BackgroundColor", new Color(0f, 0f, 0f, 0.1f));
        Scribe_Values.Look(ref SkipCompleted, "SkipCompleted", false);
        Scribe_Values.Look(ref VisualGroupByTab, "VisualGroupByTab", true);
        Scribe_Values.Look(ref TabsInitialized, "TabsInitialized");
        Scribe_Collections.Look(ref IncludedTabs, "IncludedTabs", LookMode.Value);
        Scribe_Collections.Look(ref TabProfiles, "TabProfiles", LookMode.Deep);
        Scribe_Values.Look(ref ActiveTabProfileId, "ActiveTabProfileId");
        IncludedTabs ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        TabProfiles ??= new List<ResearchTabProfile>();

        if (!TabsInitialized && IncludedTabs.Count > 0)
        {
            TabsInitialized = true;
        }
    }

    public void Reset()
    {
        PauseOnOpen = true;
        ShowResearchLines = true;
        CtrlFunction = true;
        OverrideResearch = true;
        ShowCompletion = false;
        ReverseShift = false;
        NoIdeologyPopup = false;
        HideNodesBlockedByTechLevel = false;
        VerboseLogging = false;
        LoadType = Constants.LoadTypeLoadInBackground;
        BackgroundColor = new Color(0f, 0f, 0f, 0.1f);
        SkipCompleted = false;
        VisualGroupByTab = true;
        EnsureTabCache();

        if (AllTabsCache != null && AllTabsCache.Count > 0)
        {
            IncludedTabs = new HashSet<string>(
                AllTabsCache.Where(tab => tab != null).Select(tab => tab.defName),
                StringComparer.OrdinalIgnoreCase);
        }
        else
        {
            IncludedTabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }

        TabsInitialized = true;
        TabProfiles =
        [
            new ResearchTabProfile(
                ResearchTabProfileSelection.DefaultProfileId,
                "Fluffy.ResearchTree.ProfileDefault".Translate(),
                IncludedTabs)
        ];
        ActiveTabProfileId = TabProfiles[0].Id;
    }

    public void EnsureTabCache()
    {
        if (AllTabsCache != null) return;

        AllTabsCache = DefDatabase<ResearchTabDef>.AllDefsListForReading
            .Where(t => !string.Equals(t.tutorTag, "Research-Tab-Anomaly", StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.defName)
            .ToList();

        var tabsWithResearch = new HashSet<ResearchTabDef>(
            DefDatabase<ResearchProjectDef>.AllDefsListForReading
                .Select(TryResolveProjectTab)
                .Where(tab => tab != null));

        AllTabsCache = AllTabsCache
            .Where(tab => tab != null && tabsWithResearch.Contains(tab))
            .ToList();

        if (NormalizeProfiles())
        {
            FluffyResearchTreeMod.instance?.WriteSettings();
        }
    }

    private static ResearchTabDef TryResolveProjectTab(ResearchProjectDef def)
    {
        if (def == null)
        {
            return null;
        }

        if (ResearchTabProperty != null)
        {
            var value = ResearchTabProperty.GetValue(def) as ResearchTabDef;
            if (value != null)
            {
                return value;
            }
        }

        if (ResearchTabField != null)
        {
            return ResearchTabField.GetValue(def) as ResearchTabDef;
        }

        return null;
    }

    public bool TabIncluded(ResearchTabDef def)
    {
        if (IncludedTabs == null || IncludedTabs.Count == 0)
        {
            return !TabsInitialized;
        }

        return IncludedTabs.Contains(def.defName);
    }

    public ResearchTabProfile ActiveProfile
    {
        get
        {
            EnsureTabCache();
            return ActiveProfileWithoutCacheRefresh();
        }
    }

    public IEnumerable<ResearchTabProfile> Profiles
    {
        get
        {
            EnsureTabCache();
            return TabProfiles;
        }
    }

    public void SetActiveProfile(string profileId)
    {
        EnsureTabCache();
        var profile = FindProfile(profileId);
        if (profile == null)
        {
            return;
        }

        ActiveTabProfileId = profile.Id;
        SyncLegacySelectionFromActive();
    }

    public bool SetActiveProfileTabIncluded(string defName, bool included)
    {
        EnsureTabCache();
        var profile = ActiveProfileWithoutCacheRefresh();
        if (profile == null || string.IsNullOrWhiteSpace(defName))
        {
            return false;
        }

        var changed = included
            ? profile.IncludedTabs.Add(defName)
            : profile.IncludedTabs.Remove(defName);

        if (changed)
        {
            SyncLegacySelectionFromActive();
        }

        return changed;
    }

    public void ReplaceProfiles(IEnumerable<ResearchTabProfileData> profiles, string activeProfileId)
    {
        EnsureTabCache();

        TabProfiles = (profiles ?? Enumerable.Empty<ResearchTabProfileData>())
            .Select(ResearchTabProfile.FromData)
            .ToList();
        ActiveTabProfileId = activeProfileId;
        NormalizeProfiles();
    }

    public string ProfileSignature(ResearchTabProfile profile)
    {
        if (profile == null)
        {
            return string.Empty;
        }

        var tabs = string.Join("|", profile.IncludedTabs.OrderBy(tab => tab, StringComparer.OrdinalIgnoreCase));
        return $"{profile.Id}:{SkipCompleted}:{HideNodesBlockedByTechLevel}:{VisualGroupByTab}:{tabs}";
    }

    private bool NormalizeProfiles()
    {
        var result = ResearchTabProfileSelection.Normalize(
            AllTabsCache?.Where(tab => tab != null).Select(tab => tab.defName),
            TabProfiles?.Select(profile => profile.ToData()),
            ActiveTabProfileId,
            IncludedTabs,
            TabsInitialized);

        var dirty = result.Dirty;
        foreach (var profile in result.Profiles)
        {
            if (string.Equals(profile.Id, ResearchTabProfileSelection.DefaultProfileId,
                    StringComparison.OrdinalIgnoreCase) &&
                string.Equals(profile.Name, "Default", StringComparison.Ordinal))
            {
                profile.Name = "Fluffy.ResearchTree.ProfileDefault".Translate();
                dirty = true;
            }
        }

        TabProfiles = result.Profiles.Select(ResearchTabProfile.FromData).ToList();
        ActiveTabProfileId = result.ActiveProfileId;
        TabsInitialized = result.TabsInitialized;
        dirty |= SyncLegacySelectionFromActive();

        return dirty;
    }

    private ResearchTabProfile ActiveProfileWithoutCacheRefresh()
    {
        if (TabProfiles == null || TabProfiles.Count == 0)
        {
            return null;
        }

        return FindProfile(ActiveTabProfileId) ?? TabProfiles[0];
    }

    private ResearchTabProfile FindProfile(string profileId)
    {
        if (string.IsNullOrWhiteSpace(profileId) || TabProfiles == null)
        {
            return null;
        }

        return TabProfiles.FirstOrDefault(
            profile => string.Equals(profile.Id, profileId, StringComparison.OrdinalIgnoreCase));
    }

    private bool SyncLegacySelectionFromActive()
    {
        var profile = ActiveProfileWithoutCacheRefresh();
        var activeTabs = profile?.IncludedTabs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var current = IncludedTabs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (current.SetEquals(activeTabs))
        {
            IncludedTabs = new HashSet<string>(current, StringComparer.OrdinalIgnoreCase);
            return false;
        }

        IncludedTabs = new HashSet<string>(activeTabs, StringComparer.OrdinalIgnoreCase);
        return true;
    }
}
