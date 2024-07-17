// Queue_HarmonyPatches.cs
// Copyright Karel Kroeze, 2020-2020

using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(ResearchManager))]
public class ResearchManager_Patches
{
    [HarmonyPrefix]
    [HarmonyPatch(nameof(ResearchManager.FinishProject))]
    private static void FinishProjectPrefix(ref bool doCompletionDialog)
    {
        if (doCompletionDialog)
        {
            doCompletionDialog = FluffyResearchTreeMod.instance.Settings.ShowCompletion;
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(ResearchManager.FinishProject))]
    private static void FinishProjectPostfix(ResearchProjectDef proj)
    {
        if (proj == null || proj.IsAnomalyResearch())
        {
            return;
        }

        Queue.TryStartNext(proj);
    }

    [HarmonyPrefix]
    [HarmonyPatch(nameof(ResearchManager.StopProject))]
    private static void StopProjectPostfix(ResearchProjectDef proj)
    {
        if (proj == null || proj.IsAnomalyResearch())
        {
            return;
        }

        Queue.TryStartNext(proj);
    }
}