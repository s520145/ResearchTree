using System.Reflection;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch]
public class UINotIncluded_Button_Worker_OnRepaint
{
    private static bool Prepare()
    {
        return LoadedModManager.RunningModsListForReading
            .Any(m => m.PackageId == "gondragon.uinotincluded".ToLowerInvariant());
    }

    private static MethodBase TargetMethod()
    {
        return AccessTools.Method("UINotIncluded.Widget.Workers.Button_Worker:_OnRepaint");
    }

    private static void Postfix(Rect rect, MainButtonDef ___def)
    {
        if (___def != MainButtonDefOf.Research)
        {
            return;
        }

        Queue.DrawLabelForMainButton(rect);
    }
}