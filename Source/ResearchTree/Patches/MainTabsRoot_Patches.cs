using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(MainTabsRoot))]
public class MainTabsRoot_Patches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(MainTabsRoot.ToggleTab))]
    public static void ToggleTabPrefix(ref MainButtonDef newTab)
    {
        if (newTab == MainButtonDefOf.Research && 
            FluffyResearchTreeMod.instance.Settings.OverrideResearch &&
            !Input.GetKey(KeyCode.LeftShift) &&
            ((MainTabWindow_Research) MainButtonDefOf.Research.TabWindow).CurTab != ResearchTabDefOf.Anomaly)
        {
            newTab = Assets.MainButtonDefOf.FluffyResearchTree;
        }
        
        if (newTab == MainButtonDefOf.Research && 
            !FluffyResearchTreeMod.instance.Settings.OverrideResearch &&
            Input.GetKey(KeyCode.LeftShift) &&
            ((MainTabWindow_Research) MainButtonDefOf.Research.TabWindow).CurTab != ResearchTabDefOf.Anomaly)
        {
            newTab = Assets.MainButtonDefOf.FluffyResearchTree;
        }
    }
}