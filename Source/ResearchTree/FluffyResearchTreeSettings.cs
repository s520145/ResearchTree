using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
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

    public bool ShowCompletion;

    public bool VerboseLogging;

    // 保存：选择的 Tab defName 列表（原版科研页签）
    public HashSet<string> IncludedTabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    // 选项：跳过已完成
    public bool SkipCompleted = true;

    // 选项：启用按 Tab 的可视化分组
    public bool VisualGroupByTab = true;

    // UI 缓存：所有可选 Tab（运行时收集）
    [Unsaved(false)] public List<ResearchTabDef> AllTabsCache;

    /// <summary>
    ///     Saving and loading the values
    /// </summary>
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref PauseOnOpen, "PauseOnOpen", true);
        Scribe_Values.Look(ref CtrlFunction, "CtrlFunction", true);
        Scribe_Values.Look(ref OverrideResearch, "OverrideResearch", true);
        Scribe_Values.Look(ref ShowCompletion, "ShowCompletion");
        Scribe_Values.Look(ref NoIdeologyPopup, "NoIdeologyPopup");
        Scribe_Values.Look(ref HideNodesBlockedByTechLevel, "HideNodesBlockedByTechLevel");
        Scribe_Values.Look(ref VerboseLogging, "VerboseLogging");
        Scribe_Values.Look(ref LoadType, "LoadType", 1);
        Scribe_Values.Look(ref BackgroundColor, "BackgroundColor", new Color(0f, 0f, 0f, 0.1f));
        Scribe_Values.Look(ref SkipCompleted, "SkipCompleted", false);
        Scribe_Values.Look(ref VisualGroupByTab, "VisualGroupByTab", true);
        Scribe_Collections.Look(ref IncludedTabs, "IncludedTabs", LookMode.Value);
        IncludedTabs ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public void Reset()
    {
        PauseOnOpen = true;
        CtrlFunction = true;
        OverrideResearch = true;
        ShowCompletion = false;
        NoIdeologyPopup = false;
        HideNodesBlockedByTechLevel = false;
        VerboseLogging = false;
        LoadType = Constants.LoadTypeLoadInBackground;
        BackgroundColor = new Color(0f, 0f, 0f, 0.1f);
        SkipCompleted = false;
        VisualGroupByTab = true;
        IncludedTabs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public void EnsureTabCache()
    {
        Logging.Message("[ResearchTree]:EnsureTabCache");
        if (AllTabsCache != null) return;

        // 从所有研究项目收集“出现过的 tab”，但排除 Anomaly
        AllTabsCache = DefDatabase<ResearchTabDef>.AllDefsListForReading
            .Where(t => !string.Equals(t.tutorTag, "Research-Tab-Anomaly", StringComparison.OrdinalIgnoreCase))
            .OrderBy(t => t.defName)
            .ToList();

        foreach (var tab in AllTabsCache)
        {
            IncludedTabs.Add(tab.defName);
            Logging.Message($"[ResearchTree]:{tab.defName} / {tab.LabelCap}");
        }
    }

    public bool TabIncluded(ResearchTabDef def)
    {
        if (IncludedTabs == null || IncludedTabs.Count == 0) return true; // 未配置=不过滤
        return IncludedTabs.Contains(def.defName);
    }

}