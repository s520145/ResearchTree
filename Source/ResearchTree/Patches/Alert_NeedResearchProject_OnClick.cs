using HarmonyLib;
using RimWorld;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(Alert_NeedResearchProject), nameof(Alert_NeedResearchProject.OnClick))]
public class Alert_NeedResearchProject_OnClick
{
    public static bool Prefix()
    {
        Assets.OpenResearchWindow();
        return false;
    }
}