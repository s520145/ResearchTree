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
        // LeftCtrl is the way of Dubs Mint Menus mod
        if (newTab == MainButtonDefOf.Research && 
            FluffyResearchTreeMod.instance.Settings.OverrideResearch &&
            !Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.LeftControl) &&
            ((MainTabWindow_Research) MainButtonDefOf.Research.TabWindow).CurTab != ResearchTabDefOf.Anomaly)
        {
            newTab = Assets.MainButtonDefOf.FluffyResearchTree;
        }
        
        if (newTab == MainButtonDefOf.Research && 
            !FluffyResearchTreeMod.instance.Settings.OverrideResearch &&
            Input.GetKey(KeyCode.LeftShift) && !Input.GetKey(KeyCode.LeftControl) &&
            ((MainTabWindow_Research) MainButtonDefOf.Research.TabWindow).CurTab != ResearchTabDefOf.Anomaly)
        {
            newTab = Assets.MainButtonDefOf.FluffyResearchTree;
        }
    }
}