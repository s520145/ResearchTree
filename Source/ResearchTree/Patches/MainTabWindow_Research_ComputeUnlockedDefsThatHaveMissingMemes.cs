using System.Collections.Generic;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(MainTabWindow_Research), nameof(MainTabWindow_Research.ComputeUnlockedDefsThatHaveMissingMemes))]
public class MainTabWindow_Research_ComputeUnlockedDefsThatHaveMissingMemes
{
    private static bool Prefix(ref List<(BuildableDef, List<string>)> __result)
    {
        __result = [];
        return !FluffyResearchTreeMod.instance.Settings.NoIdeologyPopup;
    }
}