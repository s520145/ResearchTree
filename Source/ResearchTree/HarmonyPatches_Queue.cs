// Queue_HarmonyPatches.cs
// Copyright Karel Kroeze, 2020-2020

using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

public class HarmonyPatches_Queue
{
    [HarmonyPatch(typeof(ResearchManager), "ResearchPerformed", typeof(float), typeof(Pawn))]
    public class ResearchPerformed
    {
        private static void Prefix(ResearchManager __instance, ref ResearchProjectDef __state)
        {
            __state = __instance.currentProj;
        }

        private static void Postfix(ResearchProjectDef __state)
        {
            if (__state is { IsFinished: true })
            {
                Queue.TryStartNext(__state);
            }
        }
    }

    [HarmonyPatch(typeof(ResearchManager), "FinishProject")]
    public class DoCompletionDialog
    {
        private static void Prefix(ref bool doCompletionDialog)
        {
            doCompletionDialog = false;
        }
    }
}