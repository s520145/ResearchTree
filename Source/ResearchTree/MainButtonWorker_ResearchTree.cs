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
        currentStart -= Constants.SmallQueueLabelSize - Constants.Margin;
    }

    public override void Activate()
    {
        if (!Event.current.shift && !Tree.Initialized && FluffyResearchTreeMod.instance.Settings.LoadType != 2)
        {
            Messages.Message("Fluffy.ResearchTree.StillLoading".Translate(), MessageTypeDefOf.RejectInput);
            return;
        }

        Find.MainTabsRoot.ToggleTab(Event.current.shift ? Assets.MainButtonDefOf.ResearchOriginal : def);
    }
}