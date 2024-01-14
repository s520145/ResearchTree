using HarmonyLib;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(GenUI), "ClearLabelWidthCache")]
public class GenUI_ClearLabelWidthCache
{
    private static void Postfix()
    {
        if (!Tree.Initialized)
        {
            return;
        }

        Tree.Reset();
    }
}