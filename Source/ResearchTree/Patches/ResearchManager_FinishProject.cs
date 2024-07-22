// Queue_HarmonyPatches.cs
// Copyright Karel Kroeze, 2020-2020

using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.FinishProject))]
public class ResearchManager_FinishProject
{
    private static void Prefix(ref bool doCompletionDialog)
    {
        if (doCompletionDialog)
        {
            doCompletionDialog = FluffyResearchTreeMod.instance.Settings.ShowCompletion;
        }
    }

    private static void Postfix(ResearchProjectDef proj)
    {
        if (proj == null || proj.IsAnomalyResearch())
        {
            return;
        }

        Queue.TryStartNext(proj);
    }
}