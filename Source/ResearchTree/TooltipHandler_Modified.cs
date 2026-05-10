using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using Verse;
using Verse.Steam;

namespace FluffyResearchTree;

public static class TooltipHandler_Modified
{
    private static readonly FieldInfo activeTipsFieldInfo = AccessTools.Field(typeof(TooltipHandler), "activeTips");
    private static readonly FieldInfo frameFieldInfo = AccessTools.Field(typeof(TooltipHandler), "frame");

    public static bool GloballyDisabled;

    public static void TipRegionIfEnabled(UnityEngine.Rect rect, string tip)
    {
        if (GloballyDisabled) return;
        TipRegion(rect, tip);
    }

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

        var activeTips = (Dictionary<int, ActiveTip>)activeTipsFieldInfo.GetValue(null);
        if (!activeTips.TryGetValue(tip.uniqueId, out var activeTip))
        {
            activeTip = new ActiveTip(tip);
            activeTips.Add(tip.uniqueId, activeTip);
            activeTip.firstTriggerTime = Time.realtimeSinceStartup;
        }

        activeTip.lastTriggerFrame = (int)frameFieldInfo.GetValue(null);
        activeTip.signal.text = tip.text;
        activeTip.signal.textGetter = tip.textGetter;
        activeTipsFieldInfo.SetValue(null, activeTips);
    }
}
