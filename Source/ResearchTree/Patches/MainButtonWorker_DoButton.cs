using HarmonyLib;
using RimWorld;
using UnityEngine;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(MainButtonWorker), nameof(MainButtonWorker.DoButton))]
public class MainButtonWorker_DoButton
{
    private static void Postfix(Rect rect, MainButtonDef ___def)
    {
        if (___def != MainButtonDefOf.Research)
        {
            return;
        }

        Queue.DrawLabelForMainButton(rect);
    }
}