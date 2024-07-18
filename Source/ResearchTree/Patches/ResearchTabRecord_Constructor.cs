using System;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(TabRecord), MethodType.Constructor,
    [typeof(string), typeof(Action), typeof(Func<bool>)])]
public class ResearchTabRecord_Constructor
{
    /// <summary>
    /// Patch vanilla research window Main tab click action to open research tree.
    /// TODO: should need a setting? Comment it out first, looking for a better way
    /// </summary>
    /// <param name="label"></param>
    /// <param name="clickedAction"></param>
    [HarmonyPrefix]
    private static void ConstructorPrefix(string label, ref Action clickedAction)
    {
        // if (ModsConfig.AnomalyActive && label == ResearchTabDefOf.Anomaly.LabelCap.ToString())
        // {
        //     return;
        // }
        //
        // if (!FluffyResearchTreeMod.instance.Settings.OverrideResearch)
        // {
        //     return;
        // }
        // // Otherwise, open the research tree directly
        // clickedAction = () =>
        // {
        //     ((MainTabWindow_Research)MainButtonDefOf.Research.TabWindow).CurTab = ResearchTabDefOf.Main;
        //     // try close vanilla
        //     Find.WindowStack.TryRemove(MainButtonDefOf.Research.TabWindow, false);
        //     // open research tree
        //     Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Research);
        // };
    }
}