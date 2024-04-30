using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(Alert_NeedAnomalyProject), nameof(Alert_NeedAnomalyProject.OnClick))]
public static class Alert_NeedAnomalyProject_OnClick
{
    public static bool Prefix()
    {
        Find.MainTabsRoot.ToggleTab(Assets.MainButtonDefOf.ResearchOriginal);
        ((MainTabWindow_Research)Assets.MainButtonDefOf.ResearchOriginal.TabWindow).CurTab =
            ResearchTabDefOf.Anomaly;
        return false;
    }
}