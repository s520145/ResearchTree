// ResearchNode.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections.Generic;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class ResearchNode : Node
{
    private static readonly Dictionary<ResearchProjectDef, bool> _buildingPresentCache = [];

    private static readonly Dictionary<ResearchProjectDef, List<ThingDef>> _missingFacilitiesCache = [];

    private readonly int cacheOrder;

    public readonly ResearchProjectDef Research;

    private bool availableCache;

    private int currentCacheOrder;

    private bool hasRefreshedAvailability;
    private bool hasRefreshedBuildings;
    private bool hasRefreshedFacilities;

    // --- 研究台/设施短期缓存（60 ticks 刷新一次） ---
    private static int _benchesCacheTick = -1;
    private static List<Building_ResearchBench> _benchesCached;
    private static HashSet<ThingDef> _benchDefsCached;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void RefreshBenchCacheIfNeeded()
    {
        int now = Find.TickManager?.TicksGame ?? 0;
        if (_benchesCacheTick != -1 && now - _benchesCacheTick < 60 &&
            _benchesCached != null && _benchDefsCached != null)
            return;

        _benchesCacheTick = now;

        // 收集所有殖民者研究台
        var benches = new List<Building_ResearchBench>(64);
        var maps = Find.Maps;
        for (int mi = 0; mi < maps.Count; mi++)
        {
            var list = maps[mi].listerBuildings.allBuildingsColonist;
            for (int bi = 0; bi < list.Count; bi++)
                if (list[bi] is Building_ResearchBench rb)
                    benches.Add(rb);
        }
        _benchesCached = benches;

        // 研究台 def 去重
        var defs = new HashSet<ThingDef>();
        for (int i = 0; i < benches.Count; i++)
            defs.Add(benches[i].def);
        _benchDefsCached = defs;
    }

    public ResearchNode(ResearchProjectDef research, int order)
    {
        Research = research;
        _pos = new Vector2(0f, research.researchViewY + 1f);
        cacheOrder = order;
        currentCacheOrder = Assets.TotalAmountOfResearch;
    }

    public List<ResearchNode> Parents
    {
        get
        {
            var enumerable = InNodes.OfType<ResearchNode>();
            enumerable = enumerable.Concat(from dn in InNodes.OfType<DummyNode>()
                select dn.Parent);
            return enumerable.ToList();
        }
    }

    public override Color Color
    {
        get
        {
            if (Highlighted)
            {
                return GenUI.MouseoverColor;
            }

            if (Completed)
            {
                return Assets.ColorCompleted.TryGetValue(Research.techLevel);
            }

            return Available
                ? Assets.ColorCompleted.TryGetValue(Research.techLevel)
                : Assets.ColorUnavailable.TryGetValue(Research.techLevel);
        }
    }

    public override Color EdgeColor
    {
        get
        {
            if (Highlighted)
            {
                return GenUI.MouseoverColor;
            }

            if (Completed)
            {
                return Assets.ColorCompleted.TryGetValue(Research.techLevel);
            }

            return Available
                ? Assets.ColorAvailable.TryGetValue(Research.techLevel)
                : Assets.ColorUnavailable.TryGetValue(Research.techLevel);
        }
    }

    public List<ResearchNode> Children
    {
        get
        {
            var enumerable = OutNodes.OfType<ResearchNode>();
            enumerable = enumerable.Concat(from dn in OutNodes.OfType<DummyNode>()
                select dn.Child);
            return enumerable.ToList();
        }
    }

    public override string Label => Research.LabelCap;

    public override bool Completed => Research.IsFinished;

    public override bool Available
    {
        get
        {
            if (Research.IsFinished || Research.IsAnomalyResearch())
            {
                return false;
            }

            return FluffyResearchTreeMod.instance.Settings.LoadType == Constants.LoadTypeDoNotGenerateResearchTree
                   || DebugSettings.godMode || getCacheValue();
        }
    }

    public override bool IsVisible =>
        !Assets.IsBlockedBySOS2(Research) && !Assets.IsHiddenByTechLevelRestrictions(Research);

    public void ClearInstanceCaches()
    {
        hasRefreshedAvailability = false;
        hasRefreshedBuildings = false;
        hasRefreshedFacilities = false;
        currentCacheOrder = cacheOrder;
    }

    private bool getCacheValue()
    {
        if (!Assets.RefreshResearch || hasRefreshedAvailability)
        {
            return availableCache;
        }

        if (currentCacheOrder < Assets.TotalAmountOfResearch)
        {
            currentCacheOrder++;
            return availableCache;
        }

        hasRefreshedAvailability = true;

        if (missingFacilities(Research).Any() || Assets.SemiRandomResearchLoaded && Assets.SemiResearchEnabled ||
            Assets.UsingRimedieval && !Assets.RimedievalAllowedResearchDefs.Contains(Research) ||
            !Research.TechprintRequirementMet || !Research.InspectionRequirementsMet ||
            Research.requiredResearchBuilding != null && !Research.PlayerHasAnyAppropriateResearchBench ||
            !Research.PlayerMechanitorRequirementMet || !Research.AnalyzedThingsRequirementsMet)
        {
            availableCache = false;
            return availableCache;
        }

        object[] parameters = [Research, null];
        if (Assets.UsingVanillaVehiclesExpanded)
        {
            var boolResult = (bool)Assets.IsDisabledMethod.Invoke(null, parameters);
            if (boolResult)
            {
                availableCache = false;
                return availableCache;
            }
        }

        if (Assets.IsBlockedByGrimworld(Research) || Assets.IsBlockedByWorldTechLevel(Research) ||
            Assets.IsBlockedByMedievalOverhaul(Research))
        {
            availableCache = false;
            return availableCache;
        }

        if (Research.prerequisites?.Any() != true && Research.hiddenPrerequisites?.Any() != true)
        {
            availableCache = true;
            return availableCache;
        }

        if (Research.prerequisites?.Any() == true)
        {
            foreach (var researchPrerequisite in Research.prerequisites)
            {
                if (researchPrerequisite.IsFinished)
                {
                    continue;
                }

                if (researchPrerequisite.ResearchNode().Available)
                {
                    continue;
                }

                availableCache = false;
                return availableCache;
            }
        }

        if (Research.hiddenPrerequisites?.Any() == true)
        {
            foreach (var researchPrerequisite in Research.hiddenPrerequisites)
            {
                if (researchPrerequisite.IsFinished)
                {
                    continue;
                }

                if (researchPrerequisite.ResearchNode().Available)
                {
                    continue;
                }

                availableCache = false;
                return availableCache;
            }
        }

        availableCache = true;
        return availableCache;
    }

    private bool buildingPresent(ResearchProjectDef research)
    {
        if (DebugSettings.godMode && Prefs.DevMode) return true;

        if (_buildingPresentCache.TryGetValue(research, out var value))
        {
            if (!Assets.RefreshResearch || hasRefreshedBuildings) return value;
            if (currentCacheOrder < Assets.TotalAmountOfResearch)
            {
                currentCacheOrder++;
                return value;
            }
        }

        hasRefreshedBuildings = true;

        // 刷新 60tick 研究台缓存
        RefreshBenchCacheIfNeeded();

        // 是否存在可进行该研究的“任一”研究台
        bool anyBenchOk = (research.requiredResearchBuilding == null);
        if (!anyBenchOk)
        {
            for (int i = 0; i < _benchesCached.Count; i++)
            {
                if (research.CanBeResearchedAt(_benchesCached[i], true)) { anyBenchOk = true; break; }
            }
        }

        value = anyBenchOk;

        // 若本项目可做，则其祖先也必须可做
        if (value)
        {
            var ancestors = research.Ancestors();
            for (int i = 0; i < ancestors.Count; i++)
            {
                if (!buildingPresent(ancestors[i])) { value = false; break; }
            }
        }

        _buildingPresentCache[research] = value;
        return value;
    }


    public static implicit operator ResearchNode(ResearchProjectDef def)
    {
        return def.ResearchNode();
    }

    private List<ThingDef> missingFacilities(ResearchProjectDef research)
    {
        // 先读缓存
        if (_missingFacilitiesCache.TryGetValue(research, out var cached))
        {
            if (!Assets.RefreshResearch || hasRefreshedFacilities) return cached;
            if (currentCacheOrder < Assets.TotalAmountOfResearch)
            {
                currentCacheOrder++;
                return cached;
            }
        }

        hasRefreshedFacilities = true;

        // 刷新 60tick 研究台缓存
        RefreshBenchCacheIfNeeded();

        // 本项目 + 未完成祖先（且自身没可用研究台）
        var chain = new List<ResearchProjectDef>(8);
        var ancestors = research.Ancestors();
        for (int i = 0; i < ancestors.Count; i++)
        {
            var a = ancestors[i];
            if (!a.IsFinished && !a.PlayerHasAnyAppropriateResearchBench)
                chain.Add(a);
        }
        chain.Add(research);

        var missing = new List<ThingDef>(4);

        for (int i = 0; i < chain.Count; i++)
        {
            var item = chain[i];

            // 必需研究台
            var reqBench = item.requiredResearchBuilding;
            if (reqBench != null && !_benchDefsCached.Contains(reqBench))
                missing.Add(reqBench);

            // 必需设施
            var reqFacs = item.requiredResearchFacilities;
            if (reqFacs == null || reqFacs.Count == 0) continue;

            for (int f = 0; f < reqFacs.Count; f++)
            {
                var fac = reqFacs[f];
                bool anyHas = false;
                for (int b = 0; b < _benchesCached.Count; b++)
                {
                    if (_benchesCached[b].HasFacility(fac)) { anyHas = true; break; }
                }
                if (!anyHas) missing.Add(fac);
            }
        }

        // 去重
        if (missing.Count > 1)
        {
            var hs = new HashSet<ThingDef>(missing);
            missing = new List<ThingDef>(hs);
        }

        _missingFacilitiesCache[research] = missing;
        return missing;
    }


    public bool BuildingPresent()
    {
        return buildingPresent(Research);
    }

    public override void Draw(Rect visibleRect, bool forceDetailedMode = false)
    {
        if (!IsWithinViewport(visibleRect) || !IsVisible)
        {
            Highlighted = false;
            return;
        }

        if (MainTabWindow_ResearchTree.Instance.IsHighlighted(Research))
        {
            Highlighted = true;
        }

        var detailedMode = forceDetailedMode ||
                           MainTabWindow_ResearchTree.Instance.ZoomLevel < Constants.DetailedModeZoomLevelCutoff;
        var mouseOver = Mouse.IsOver(Rect) || Rect.Contains(Event.current.mousePosition);
        if (MainTabWindow_ResearchTree.Instance.IsQuickSearchWidgetActive())
        {
            mouseOver = false;
        }

        if (Event.current.type == EventType.Repaint)
        {
            // researches that are completed or could be started immediately, and that have the required building(s) available
            //GUI.color = Mouse.IsOver(Rect) ? GenUI.MouseoverColor : Color;
            var color = mouseOver ? GenUI.MouseoverColor : Color;

            if (mouseOver || Highlighted)
            {
                FastGUI.DrawTextureFast(Rect, Assets.ButtonActive, color);
            }
            else
            {
                FastGUI.DrawTextureFast(Rect, Assets.Button, color);
            }

            // grey out center to create a progress bar effect, completely greying out research not started.
            if (Available)
            {
                var position = Rect.ContractedBy(3f);
                //GUI.color = Assets.ColorAvailable[Research.techLevel];
                color = Assets.ColorAvailable[Research.techLevel];
                position.xMin += Research.ApparentPercent() * position.width;
                FastGUI.DrawTextureFast(position, BaseContent.WhiteTex, color);
            }

            Highlighted = false;
            if (!Completed && !Available)
            {
                GUI.color = Color.grey;
            }
            else
            {
                GUI.color = Color.white;
            }

            if (detailedMode)
            {
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = false;
                Text.Font = _largeLabel ? GameFont.Tiny : GameFont.Small;
                Widgets.Label(LabelRect, Research.LabelCap);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.WordWrap = false;
                Text.Font = GameFont.Medium;
                Widgets.Label(Rect, Research.LabelCap);
            }

            // draw research cost and icon
            if (detailedMode)
            {
                Text.Anchor = TextAnchor.UpperRight;
                var costString = $"{Research.CostApparent.ToStringByStyle(ToStringStyle.Integer)}";
                if (!Research.IsFinished)
                {
                    costString =
                        $"{Research.ProgressApparent.ToStringByStyle(ToStringStyle.Integer)}/{Research.CostApparent.ToStringByStyle(ToStringStyle.Integer)}";
                }

                Text.Font = costString.Length > 6 ? GameFont.Tiny : GameFont.Small;
                Widgets.Label(CostLabelRect, costString);
                var researchIcon = !Completed && !Available ? Assets.Lock : Assets.ResearchIcon;
                GUI.DrawTexture(CostIconRect, researchIcon, ScaleMode.ScaleToFit);
            }

            Text.WordWrap = true;
            buildTips();

            // draw unlock icons
            if (detailedMode)
            {
                var unlockDefsAndDescs = Research.GetUnlockDefsAndDescriptions();
                for (var i = 0; i < unlockDefsAndDescs.Count; i++)
                {
                    var rect = new Rect(IconsRect.xMax - ((i + 1) * (Constants.IconSize.x + 4f)),
                        IconsRect.yMin + ((IconsRect.height - Constants.IconSize.y) / 2f), Constants.IconSize.x,
                        Constants.IconSize.y);
                    if (rect.xMin - Constants.IconSize.x < IconsRect.xMin && i + 1 < unlockDefsAndDescs.Count)
                    {
                        rect.x = IconsRect.x + 4f;
                        GUI.DrawTexture(rect, Assets.MoreIcon, ScaleMode.ScaleToFit);
                        var text = string.Join("\n",
                            (from p in unlockDefsAndDescs.GetRange(i, unlockDefsAndDescs.Count - i)
                                select p.Second).ToArray());
                        TooltipHandler.TipRegion(rect, text);
                        break;
                    }

                    Widgets.DefIcon(rect, unlockDefsAndDescs[i].First);
                    if (Queue._draggedNode == null)
                    {
                        TooltipHandler_Modified.TipRegion(rect, unlockDefsAndDescs[i].Second);
                    }
                }
            }

            if (mouseOver)
            {
                if (Completed)
                {
                    foreach (var child in Children)
                    {
                        child.Highlighted = true;
                    }
                }
                else
                {
                    Highlighted = true;
                    foreach (var item in GetMissingRequiredRecursive())
                    {
                        item.Highlighted = true;
                    }
                }
            }
        }

        if (!Widgets.ButtonInvisible(Rect))
        {
            return;
        }

        if (Event.current.button == Constants.LeftClick || Event.current.button == Constants.RightClick)
        {
            UI.UnfocusCurrentControl();
        }

        if (Event.current.button == Constants.RightClick && !Event.current.shift)
        {
            var researchCard = new Dialog_ResearchInfoCard(Research);
            Find.WindowStack.Add(researchCard);
            return;
        }

        if (!Available)
        {
            return;
        }

        if (Event.current.button == Constants.LeftClick && Event.current.control && !Research.IsFinished)
        {
            Queue.EnqueueRangeFirst(GetMissingRequired());
        }

        if (Event.current.button == Constants.LeftClick && !Event.current.control && !Research.IsFinished)
        {
            if (!Queue.IsQueued(this))
            {
                Queue.EnqueueRange(GetMissingRequired(), Event.current.shift);
            }
            else
            {
                Queue.TryDequeue(this);
            }
        }

        if (Event.current.button != Constants.RightClick && !Event.current.shift)
        {
            return;
        }

        if (Event.current.button == Constants.LeftClick && Event.current.shift)
        {
            return;
        }

        if (!DebugSettings.godMode || !Prefs.DevMode || Research.IsFinished)
        {
            return;
        }

        // Shift + RClick + dev.godMod  finish this and start next
        Queue.Notify_InstantFinished(this);
    }

    public List<ResearchNode> GetMissingRequiredRecursive()
    {
        // 显式栈 + 去重，一次遍历；与原语义等价（不含 this）
        var result = new List<ResearchNode>(8);
        var seen = new HashSet<ResearchNode>();
        var stack = new List<ResearchNode>(8);

        void PushPrereqs(ResearchProjectDef rpd)
        {
            var pre = rpd.prerequisites;
            if (pre != null)
                for (int i = 0; i < pre.Count; i++)
                    if (!pre[i].IsFinished)
                    {
                        var n = pre[i].ResearchNode();
                        if (seen.Add(n)) stack.Add(n);
                    }

            var hid = rpd.hiddenPrerequisites;
            if (hid != null)
                for (int i = 0; i < hid.Count; i++)
                    if (!hid[i].IsFinished)
                    {
                        var n = hid[i].ResearchNode();
                        if (seen.Add(n)) stack.Add(n);
                    }
        }

        PushPrereqs(Research);

        while (stack.Count > 0)
        {
            var node = stack[stack.Count - 1];
            stack.RemoveAt(stack.Count - 1);
            result.Add(node);
            PushPrereqs(node.Research);
        }

        return result;
    }

    public List<ResearchNode> GetMissingRequired()
    {
        var list = GetMissingRequiredRecursive();
        if (!list.Contains(this)) list.Add(this);
        return list;
    }


    private List<ThingDef> MissingFacilities()
    {
        return missingFacilities(Research);
    }

    private void buildTips()
    {
        if (Queue._draggedNode != null)
        {
            return;
        }

        var researchTooltipString = getResearchTooltipString();
        var missingFacilities = MissingFacilities();
        if (missingFacilities?.Any() == true)
        {
            researchTooltipString.AppendLine();
            researchTooltipString.AppendLine("Fluffy.ResearchTree.MissingFacilities".Translate(string.Join(", ",
                missingFacilities.Select(td => td.LabelCap).ToArray())));
        }

        if (!Research.TechprintRequirementMet)
        {
            researchTooltipString.AppendLine();
            researchTooltipString.AppendLine("Fluffy.ResearchTree.MissingTechprints".Translate(
                Research.TechprintsApplied,
                Research.techprintCount));
        }

        if (!Research.InspectionRequirementsMet)
        {
            researchTooltipString.AppendLine();
            researchTooltipString.AppendLine("MissingGravEngineInspection".Translate());
        }

        if (!Research.AnalyzedThingsRequirementsMet)
        {
            researchTooltipString.AppendLine();
            researchTooltipString.AppendLine("Fluffy.ResearchTree.MissingStudiedThings".Translate(string.Join(", ",
                Research.requiredAnalyzed.Select(def => def.LabelCap))));
        }

        if (!Research.PlayerMechanitorRequirementMet)
        {
            researchTooltipString.AppendLine();
            researchTooltipString.AppendLine("Fluffy.ResearchTree.MissingMechanitorRequirement".Translate());
        }

        if (Assets.UsingVanillaVehiclesExpanded)
        {
            var valueArray = new object[] { Research, null };
            var boolResult = (bool)Assets.IsDisabledMethod.Invoke(null, valueArray);

            if (boolResult)
            {
                researchTooltipString.AppendLine();
                var wreck = (ThingDef)valueArray[1];
                if (wreck != null)
                {
                    researchTooltipString.AppendLine();
                    researchTooltipString.AppendLine("VVE_WreckNotRestored".Translate(wreck.LabelCap));
                }
            }
        }

        if (Assets.IsBlockedByGrimworld(Research))
        {
            researchTooltipString.AppendLine();
            researchTooltipString.AppendLine("Fluffy.ResearchTree.GrimworldDoesNotAllow".Translate());
        }

        if (Assets.IsBlockedByWorldTechLevel(Research))
        {
            researchTooltipString.AppendLine();
            researchTooltipString.AppendLine("Fluffy.ResearchTree.WorldTechLevelDoesNotAllow".Translate());
        }

        if (Assets.IsBlockedByMedievalOverhaul(Research))
        {
            researchTooltipString.AppendLine();
            if (Assets.TryGetBlockingSchematicFromMedievalOverhaul(Research, out var thingLabel))
            {
                researchTooltipString.AppendLine("DankPyon_RequiredSchematic".Translate() + $": {thingLabel}");
            }
            else
            {
                researchTooltipString.AppendLine("DankPyon_RequiredSchematic".Translate());
            }
        }

        if (Assets.SemiRandomResearchLoaded && Assets.SemiResearchEnabled)
        {
            researchTooltipString.AppendLine();
            researchTooltipString.AppendLine("Fluffy.ResearchTree.SemiRandomResearchLoaded".Translate());
        }

        if (Assets.UsingRimedieval && !Assets.RimedievalAllowedResearchDefs.Contains(Research))
        {
            researchTooltipString.AppendLine();
            researchTooltipString.AppendLine("Fluffy.ResearchTree.RimedievalDoesNotAllow".Translate());
        }

        TooltipHandler_Modified.TipRegion(Rect, researchTooltipString.ToString());
    }

    private StringBuilder getResearchTooltipString()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(Research.LabelCap.Colorize(ColoredText.TipSectionTitleColor) + "\n");
        stringBuilder.AppendLine(Research.description);

        stringBuilder.AppendLine();
        // TODO: Add settings so that shortcut key tips can be hidden
        if (Queue.IsQueued(this))
        {
            stringBuilder.AppendLine("Fluffy.ResearchTree.LClickRemoveFromQueue".Translate()
                .Colorize(ColoredText.SubtleGrayColor));
            stringBuilder.AppendLine("Fluffy.ResearchTree.CLClickMoveToFrontOfQueue".Translate()
                .Colorize(ColoredText.SubtleGrayColor));
        }
        else
        {
            stringBuilder.AppendLine("Fluffy.ResearchTree.LClickReplaceQueue".Translate()
                .Colorize(ColoredText.SubtleGrayColor));
            stringBuilder.AppendLine("Fluffy.ResearchTree.SLClickAddToQueue".Translate()
                .Colorize(ColoredText.SubtleGrayColor));
            stringBuilder.AppendLine("Fluffy.ResearchTree.CLClickAddToFrontOfQueue".Translate()
                .Colorize(ColoredText.SubtleGrayColor));
        }

        stringBuilder.AppendLine(
            "Fluffy.ResearchTree.SRClickShowInfo".Translate().Colorize(ColoredText.SubtleGrayColor));
        if (DebugSettings.godMode)
        {
            stringBuilder.AppendLine("Fluffy.ResearchTree.RClickInstaFinishNew".Translate()
                .Colorize(ColoredText.SubtleGrayColor));
        }

        return stringBuilder;
    }

    public void DrawAt(Vector2 pos, Rect visibleRect, bool forceDetailedMode = false)
    {
        SetRects(pos);
        Draw(visibleRect, forceDetailedMode);
        SetRects();
    }

    // TODO: The above code for handling key events should be extracted into a public method,
    //       so that the consistency of shortcut keys can be maintained.
    //       However, left-clicking on a node will probably conflict with the original view.
    //       We will try it out, but that’s it for now.
    public void HandleVanillaNodeClickEvent()
    {
        if (!Available)
        {
            return;
        }

        if (Input.GetKey(KeyCode.LeftShift) && !Completed)
        {
            if (!Queue.IsQueued(this))
            {
                Queue.EnqueueRange(GetMissingRequired(), Event.current.shift);
            }
            else
            {
                Queue.TryDequeue(this);
            }
        }
        else if (Input.GetKey(KeyCode.LeftControl) && !Completed)
        {
            Queue.EnqueueRangeFirst(GetMissingRequired());
        }
    }
}