// Queue_HarmonyPatches.cs
// Copyright Karel Kroeze, 2020-2020

using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

public class ResearchManager_FinishProject
{
    [HarmonyPatch(typeof(ResearchManager), "FinishProject")]
    public class DoCompletionDialog
    {
        private static void Prefix(ref bool doCompletionDialog)
        {
            doCompletionDialog = false;
        }

        private static void Postfix(ResearchProjectDef proj)
        {
            if (proj == null)
            {
                return;
            }

            Queue.TryStartNext(proj);
        }
    }
}