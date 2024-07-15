// Queue_HarmonyPatches.cs
// Copyright Karel Kroeze, 2020-2020

using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(ResearchManager), nameof(ResearchManager.StopProject))]
public class ResearchManager_StopProject
{
    private static void Postfix(ResearchProjectDef proj)
    {
        if (proj == null)
        {
            return;
        }

        Queue.TryDequeue(proj.ResearchNode());
    }
}