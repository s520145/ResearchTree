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
    /// TODO: should need a setting?
    /// </summary>
    /// <param name="label"></param>
    /// <param name="clickedAction"></param>
    [HarmonyPrefix]
    private static void ConstructorPrefix(string label, ref Action clickedAction)
    {
        if (label == ResearchTabDefOf.Anomaly.LabelCap.ToString() ||
            !FluffyResearchTreeMod.instance.Settings.OverrideResearch)
        {
            return;
        }

        clickedAction = () =>
        {
            ((MainTabWindow_Research)MainButtonDefOf.Research.TabWindow).CurTab = ResearchTabDefOf.Main;
            Find.WindowStack.TryRemove(MainButtonDefOf.Research.TabWindow, false);
            Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Research);
        };
    }
}