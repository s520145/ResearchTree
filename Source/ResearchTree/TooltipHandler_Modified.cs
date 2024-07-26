using UnityEngine;
using Verse;
using Verse.Steam;

namespace FluffyResearchTree;

public static class TooltipHandler_Modified
{
    public static void TipRegion(Rect rect, TipSignal tip)
    {
        if (Event.current.type != EventType.Repaint || !rect.Contains(Event.current.mousePosition) &&
                                                    !DebugViewSettings.drawTooltipEdges
                                                    || tip.textGetter == null && tip.text.NullOrEmpty() ||
                                                    SteamDeck.KeyboardShowing)
        {
            return;
        }

        if (DebugViewSettings.drawTooltipEdges)
        {
            Widgets.DrawBox(rect);
        }

        if (!TooltipHandler.activeTips.ContainsKey(tip.uniqueId))
        {
            var activeTip = new ActiveTip(tip);
            TooltipHandler.activeTips.Add(tip.uniqueId, activeTip);
            TooltipHandler.activeTips[tip.uniqueId].firstTriggerTime = Time.realtimeSinceStartup;
        }

        TooltipHandler.activeTips[tip.uniqueId].lastTriggerFrame = TooltipHandler.frame;
        TooltipHandler.activeTips[tip.uniqueId].signal.text = tip.text;
        TooltipHandler.activeTips[tip.uniqueId].signal.textGetter = tip.textGetter;
    }
}