// MainButtonWorker_ResearchTree.cs
// Copyright Karel Kroeze, 2018-2020

using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class MainButtonWorker_ResearchTree : MainButtonWorker_ToggleResearchTab
{
    public override void DoButton(Rect rect)
    {
        base.DoButton(rect);
        var currentStart = rect.xMax - Constants.SmallQueueLabelSize - Constants.Margin;
        if (!Tree.Initialized && FluffyResearchTreeMod.instance.Settings.LoadType != 2)
        {
            Queue.DrawLabel(
                new Rect(currentStart, 0f, Constants.SmallQueueLabelSize, Constants.SmallQueueLabelSize)
                    .CenteredOnYIn(rect), Color.yellow,
                Color.grey, "..", "Fluffy.ResearchTree.StillLoading".Translate());
            return;
        }

        if (Queue.NumQueued <= 0)
        {
            return;
        }

        Queue.DrawLabel(
            new Rect(currentStart, 0f, Constants.SmallQueueLabelSize, Constants.SmallQueueLabelSize)
                .CenteredOnYIn(rect), Color.white,
            Color.grey, Queue.NumQueued.ToString());
    }

    public override void Activate()
    {
        Assets.OpenResearchWindow();
    }
}