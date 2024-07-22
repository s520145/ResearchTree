using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(DiaOption), nameof(DiaOption.Activate))]
public class DiaOption_Activate
{
    private static void Prefix(string ___text)
    {
        if (!"VoidMonolithViewResearch".Translate().ToString().Equals(___text))
        {
            return;
        }

        // fixed Void Monolith view jump to Anomaly research window wrong(jumped to research tree view)
        // Because in the original code, the research window is jumped first and then the tab of the window is changed.
        // This patch just changes the tab in advance, and subsequent patch files will be able to correctly judge
        // see: MainTabsRoot_ToggleTab
        ((MainTabWindow_Research)MainButtonDefOf.Research.TabWindow).CurTab = ResearchTabDefOf.Anomaly;
    }
}