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

    // ===== PATCH: Tooltip 全局开关（轻量无侵入） =====
    public static bool GloballyDisabled;

    public static void TipRegionIfEnabled(UnityEngine.Rect rect, string tip)
    {
        if (GloballyDisabled) return;
        TipRegion(rect, tip); // 复用你现在已有的 API
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
        if (!activeTips.ContainsKey(tip.uniqueId))
        {
            var activeTip = new ActiveTip(tip);
            activeTips.Add(tip.uniqueId, activeTip);
            activeTips[tip.uniqueId].firstTriggerTime = Time.realtimeSinceStartup;
        }

        activeTips[tip.uniqueId].lastTriggerFrame = (int)frameFieldInfo.GetValue(null);
        activeTips[tip.uniqueId].signal.text = tip.text;
        activeTips[tip.uniqueId].signal.textGetter = tip.textGetter;
        activeTipsFieldInfo.SetValue(null, activeTips);
    }
}