// Queue_HarmonyPatches.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using HarmonyLib;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

[HarmonyPatch(typeof(MainTabWindow_Research))]
public class MainTabWindow_Research_Patches
{
    private static readonly MethodInfo CustomButtonTextMethodInfo =
        AccessTools.Method(typeof(Widgets), nameof(Widgets.CustomButtonText));

    private static readonly MethodInfo Dialog_MessageBox_CreateConfirmationMethodInfo =
        AccessTools.Method(typeof(Dialog_MessageBox), nameof(Dialog_MessageBox.CreateConfirmation),
            [typeof(TaggedString), typeof(Action), typeof(bool), typeof(string), typeof(WindowLayer)]);

    private static readonly MethodInfo Queue_CreateConfirmationMethodInfo =
        AccessTools.Method(typeof(Queue), nameof(Queue.CreateConfirmation),
            [typeof(ResearchProjectDef), typeof(TaggedString), typeof(Action), typeof(bool), typeof(string), typeof(WindowLayer)]);

    [HarmonyPrefix]
    [HarmonyPatch(nameof(MainTabWindow_Research.DoBeginResearch))]
    private static void DoBeginResearchPrefix(ResearchProjectDef projectToStart)
    {
        if (projectToStart.IsAnomalyResearch())
        {
            return;
        }

        var researchNode = projectToStart.ResearchNode();
        var researchNodes = researchNode.GetMissingRequired();
        // check is same order
        if (Queue.IsEnqueueRangeFirstSameOrder(researchNodes, false, false))
        {
            return;
        }

        Queue.EnqueueFirst(researchNodes);
    }

    /// <summary>
    /// Determine the index by viewing the IL code of the MainTabWindow_Research.ListProjects method through dnSpy.
    /// </summary>
    [HarmonyTranspiler]
    [HarmonyPatch(nameof(MainTabWindow_Research.ListProjects))]
    private static IEnumerable<CodeInstruction> ListProjectsTranspiler(IEnumerable<CodeInstruction> instr, ILGenerator generator)
    {
        var instructions = instr.ToList();

        var startIndex = -1;
        var codes = new List<CodeInstruction>(instructions);
        for (var i = 0; i < codes.Count - 18; i++)
        {
            if (
                // int borderSize = flag4 ? 2 : 1;
                codes[i].opcode == OpCodes.Ldc_I4_1 &&
                codes[i + 1].opcode == OpCodes.Br_S &&
                codes[i + 2].opcode == OpCodes.Ldc_I4_2 &&
                codes[i + 3].opcode == OpCodes.Stloc_S &&
                codes[i + 4].opcode == OpCodes.Ldloca_S &&
                // if (Widgets.CustomButtonText...
                codes[i + 18].Calls(CustomButtonTextMethodInfo)
            )
            {
                startIndex = i + 4;
            }
        }

        if (startIndex <= -1)
        {
            return codes.AsEnumerable();
        }

        CodeInstruction codeInstruction0 = null;
        CodeInstruction codeInstruction1 = null;
        CodeInstruction codeInstruction2 = null;
        // ... && !project.IsHidden
        if (codes[startIndex - 4 + 18 + 3].opcode == OpCodes.Ldfld &&
            codes[startIndex - 4 + 18 + 2].opcode == OpCodes.Ldloc_S)
        {
            codeInstruction0 = new CodeInstruction(codes[startIndex]);
            codeInstruction1 = new CodeInstruction(codes[startIndex - 4 + 18 + 2]);
            codeInstruction2 = new CodeInstruction(codes[startIndex - 4 + 18 + 3]);
        }

        if (codeInstruction1 == null || codes.Count < 30)
        {
            Log.Error("Can not find code instruction position. Maybe RimWorld tweaked the source code.", true);
            return codes.AsEnumerable();
        }

        var opts = new List<CodeInstruction>();
        // if (Widgets.CustomButtonText(ref rect1,...     ref: Ldloca_S change to Ldloc_S
        codeInstruction0.opcode = OpCodes.Ldloc_S;
        opts.Add(codeInstruction0); // ldloc.s 
        opts.Add(codeInstruction1); // ldloc.s
        opts.Add(codeInstruction2); // ldfld
        opts.Add(new CodeInstruction(OpCodes.Call, // call
            AccessTools.Method(typeof(Queue), nameof(Queue.DrawLabelForVanillaWindow))));
        opts.Add(new CodeInstruction(OpCodes.Nop)); // nop

        // Color color7 = GUI.color; -> later
        var insertIndex = startIndex - 4 + 18 + 15;
        codes.InsertRange(insertIndex, opts);

        // SoundDefOf.Click.PlayOneShotOnCamera(); -> before
        insertIndex = startIndex - 4 + 18 + 3 + 3;

        var soundDefOf_Click_PlayOneShotOnCamera_Label = generator.DefineLabel();
        codes[insertIndex].labels.Add(soundDefOf_Click_PlayOneShotOnCamera_Label);

        var handleVanillaNodeClickEvent_Label = generator.DefineLabel();
        var handleVanillaNodeClickEvent = new CodeInstruction(OpCodes.Call, // call
            AccessTools.Method(typeof(ResearchNode), nameof(ResearchNode.HandleVanillaNodeClickEvent)));
        handleVanillaNodeClickEvent.labels.Add(handleVanillaNodeClickEvent_Label);

        opts.Clear();
        opts.Add(codeInstruction1); // ldloc.s
        opts.Add(codeInstruction2); // ldfld
        opts.Add(new CodeInstruction(OpCodes.Call, // call
            AccessTools.Method(typeof(ResearchProjectDef_Extensions), nameof(ResearchProjectDef_Extensions.ResearchNode))));
        opts.Add(new CodeInstruction(OpCodes.Dup)); // dup
        opts.Add(new CodeInstruction(OpCodes.Brtrue_S, handleVanillaNodeClickEvent_Label)); // brtrue.s label
        opts.Add(new CodeInstruction(OpCodes.Pop)); // pop
        opts.Add(new CodeInstruction(OpCodes.Br_S, soundDefOf_Click_PlayOneShotOnCamera_Label));
        opts.Add(handleVanillaNodeClickEvent);
        opts.Add(new CodeInstruction(OpCodes.Nop)); // nop

        codes.InsertRange(insertIndex, opts);

        return codes.AsEnumerable();
    }

    [HarmonyTranspiler]
    [HarmonyPatch(nameof(MainTabWindow_Research.AttemptBeginResearch))]
    [HarmonyPatch([typeof(ResearchProjectDef)])]
    private static IEnumerable<CodeInstruction> AttemptBeginResearchTranspiler(IEnumerable<CodeInstruction> instr)
    {
        var instructions = instr.ToList();

        var startIndex = -1;
        var codes = new List<CodeInstruction>(instructions);
        for (var i = 0; i < codes.Count; i++)
        {
            if (codes[i].Calls(Dialog_MessageBox_CreateConfirmationMethodInfo))
            {
                startIndex = i;
            }
        }

        if (startIndex <= -1)
        {
            return codes.AsEnumerable();
        }

        var opts = new List<CodeInstruction>();
        // Dialog_MessageBox.CreateConfirmation(...
        opts.Add(new CodeInstruction(codes[9])); // ldloc.0
        opts.Add(new CodeInstruction(codes[10])); // ldfld   projectToStart

        codes.InsertRange(startIndex - 9, opts);
        codes[startIndex + 2].operand = Queue_CreateConfirmationMethodInfo;

        return codes.AsEnumerable();
    }
}