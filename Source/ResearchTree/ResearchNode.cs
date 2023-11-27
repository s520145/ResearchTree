// ResearchNode.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class ResearchNode : Node
{
    private static readonly Dictionary<ResearchProjectDef, bool> _buildingPresentCache =
        new Dictionary<ResearchProjectDef, bool>();

    private static readonly Dictionary<ResearchProjectDef, List<ThingDef>> _missingFacilitiesCache =
        new Dictionary<ResearchProjectDef, List<ThingDef>>();

    private readonly int cacheOrder;

    public readonly ResearchProjectDef Research;

    private bool availableCache;

    private int currentCacheOrder;

    private bool hasRefreshedAvailability;
    private bool hasRefreshedBuildings;
    private bool hasRefreshedFacilities;


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
                return Assets.ColorCompleted[Research.techLevel];
            }

            return Available ? Assets.ColorCompleted[Research.techLevel] : Assets.ColorUnavailable[Research.techLevel];
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
                return Assets.ColorCompleted[Research.techLevel];
            }

            return Available ? Assets.ColorAvailable[Research.techLevel] : Assets.ColorUnavailable[Research.techLevel];
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
            if (Research.IsFinished)
            {
                return false;
            }

            return DebugSettings.godMode || getCacheValue();
        }
    }

    public void ClearInstanceCaches()
    {
        hasRefreshedAvailability = false;
        hasRefreshedBuildings = false;
        hasRefreshedFacilities = false;
        currentCacheOrder = cacheOrder;
    }

    private bool getCacheValue()
    {
        if (!Assets.RefreshResearch)
        {
            return availableCache;
        }

        if (hasRefreshedAvailability)
        {
            return availableCache;
        }

        if (currentCacheOrder < Assets.TotalAmountOfResearch)
        {
            currentCacheOrder++;
            return availableCache;
        }

        hasRefreshedAvailability = true;

        if (Research.CanStartNow)
        {
            availableCache = true;
            return availableCache;
        }

        if (!Research.TechprintRequirementMet)
        {
            availableCache = false;
            return availableCache;
        }

        if (Research.requiredResearchBuilding != null && !Research.PlayerHasAnyAppropriateResearchBench)
        {
            availableCache = false;
            return availableCache;
        }

        if (!Research.PlayerMechanitorRequirementMet)
        {
            availableCache = false;
            return availableCache;
        }

        if (!Research.StudiedThingsRequirementsMet)
        {
            availableCache = false;
            return availableCache;
        }

        object[] parameters = { Research, null };
        if (Assets.UsingVanillaVehiclesExpanded)
        {
            var boolResult = (bool)Assets.IsDisabledMethod.Invoke(null, parameters);
            if (boolResult)
            {
                availableCache = false;
                return availableCache;
            }
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

    public bool BuildingPresent(ResearchProjectDef research)
    {
        if (DebugSettings.godMode && Prefs.DevMode)
        {
            return true;
        }

        var hasCache = _buildingPresentCache.TryGetValue(research, out var value);

        if (!Assets.RefreshResearch && hasCache)
        {
            return value;
        }

        if (hasRefreshedBuildings && hasCache)
        {
            return value;
        }

        if (currentCacheOrder < Assets.TotalAmountOfResearch && hasCache)
        {
            currentCacheOrder++;
            return value;
        }

        hasRefreshedBuildings = true;

        value = research.requiredResearchBuilding == null || Find.Maps
            .SelectMany(map => map.listerBuildings.allBuildingsColonist).OfType<Building_ResearchBench>()
            .Any(b => research.CanBeResearchedAt(b, true));
        if (value)
        {
            value = research.Ancestors().All(BuildingPresent);
        }

        _buildingPresentCache[research] = value;
        return value;
    }

    public static void ClearCaches()
    {
        //_buildingPresentCache.Clear();
        //_missingFacilitiesCache.Clear();
    }

    public static implicit operator ResearchNode(ResearchProjectDef def)
    {
        return def.ResearchNode();
    }

    public int Matches(string query)
    {
        var culture = CultureInfo.CurrentUICulture;
        query = query.ToLower(culture);
        if (Research.LabelCap.RawText.ToLower(culture).Contains(query))
        {
            return 1;
        }

        if (Research.GetUnlockDefsAndDescs()
            .Any(unlock => unlock.First.LabelCap.RawText.ToLower(culture).Contains(query)))
        {
            return 2;
        }

        return Research.description.ToLower(culture).Contains(query) ? 3 : 0;
    }

    public List<ThingDef> MissingFacilities(ResearchProjectDef research, bool refresh = false)
    {
        var hasCache = _missingFacilitiesCache.TryGetValue(research, out var value);

        if (!Assets.RefreshResearch && hasCache)
        {
            return value;
        }

        if (hasRefreshedFacilities && hasCache)
        {
            return value;
        }

        if (currentCacheOrder < Assets.TotalAmountOfResearch && hasCache)
        {
            currentCacheOrder++;
            return value;
        }

        hasRefreshedFacilities = true;

        var list = research.Ancestors().Where(rpd => !rpd.IsFinished && !rpd.PlayerHasAnyAppropriateResearchBench)
            .ToList();
        list.Add(research);
        var availableBenches = Find.Maps.SelectMany(map => map.listerBuildings.allBuildingsColonist)
            .OfType<Building_ResearchBench>();
        var distinctBenches = availableBenches.Select(b => b.def).Distinct();
        value = new List<ThingDef>();
        foreach (var item in list)
        {
            if (item.requiredResearchBuilding == null)
            {
                continue;
            }

            if (!distinctBenches.Contains(item.requiredResearchBuilding))
            {
                value.Add(item.requiredResearchBuilding);
            }

            if (item.requiredResearchFacilities.NullOrEmpty())
            {
                continue;
            }

            foreach (var facility in item.requiredResearchFacilities)
            {
                if (!availableBenches.Any(b => b.HasFacility(facility)))
                {
                    value.Add(facility);
                }
            }
        }

        value = value.Distinct().ToList();
        _missingFacilitiesCache[research] = value;
        return value;
    }

    public bool BuildingPresent()
    {
        return BuildingPresent(Research);
    }

    public override void Draw(Rect visibleRect, bool forceDetailedMode = false)
    {
        if (!IsVisible(visibleRect))
        {
            Highlighted = false;
            return;
        }

        if (Event.current.type == EventType.Repaint)
        {
            //GUI.color = Mouse.IsOver(Rect) ? GenUI.MouseoverColor : Color;
            var color = Mouse.IsOver(Rect) ? GenUI.MouseoverColor : Color;
            if (Mouse.IsOver(Rect) || Highlighted)
            {
                FastGUI.DrawTextureFast(Rect, Assets.ButtonActive, color);
            }
            else
            {
                FastGUI.DrawTextureFast(Rect, Assets.Button, color);
            }

            if (Available)
            {
                var position = Rect.ContractedBy(3f);
                //GUI.color = Assets.ColorAvailable[Research.techLevel];
                color = Assets.ColorAvailable[Research.techLevel];
                position.xMin += Research.ProgressPercent * position.width;
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

            if (forceDetailedMode ||
                MainTabWindow_ResearchTree.Instance.ZoomLevel < Constants.DetailedModeZoomLevelCutoff)
            {
                Text.Anchor = TextAnchor.UpperLeft;
                Text.WordWrap = false;
                Text.Font = !_largeLabel ? GameFont.Small : GameFont.Tiny;

                if (Text.CalcSize(Research.LabelCap).x > LabelRect.width)
                {
                    Text.WordWrap = true;
                    var newRect = LabelRect;
                    newRect.height *= 2f;
                    Widgets.Label(newRect, Research.LabelCap);
                    Text.WordWrap = false;
                }
                else
                {
                    Widgets.Label(LabelRect, Research.LabelCap);
                }
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.WordWrap = false;
                Text.Font = GameFont.Medium;
                if (Text.CalcSize(Label).x > LabelRect.width)
                {
                    Text.Font = GameFont.Small;
                }

                if (Text.CalcSize(Label).x > LabelRect.width)
                {
                    Text.Font = GameFont.Tiny;
                }

                Widgets.Label(Rect, Research.LabelCap);
            }

            if (forceDetailedMode ||
                MainTabWindow_ResearchTree.Instance.ZoomLevel < Constants.DetailedModeZoomLevelCutoff)
            {
                Text.Anchor = TextAnchor.UpperRight;
                var costString = $"{Research.CostApparent.ToStringByStyle(ToStringStyle.Integer)}";
                if (!Research.IsFinished)
                {
                    costString =
                        $"{Research.ProgressReal.ToStringByStyle(ToStringStyle.Integer)}/{Research.CostApparent.ToStringByStyle(ToStringStyle.Integer)}";
                }

                Text.Font = costString.Length > 7 ? GameFont.Small : GameFont.Tiny;

                Widgets.Label(CostLabelRect, costString);
                GUI.DrawTexture(CostIconRect, !Completed && !Available ? Assets.Lock : Assets.ResearchIcon,
                    ScaleMode.ScaleToFit);
            }

            Text.WordWrap = true;
            var tooltipstring = GetResearchTooltipString();
            if (!BuildingPresent())
            {
                var missingFacilities = MissingFacilities();

                if (missingFacilities?.Any() == true)
                {
                    tooltipstring.AppendLine();
                    tooltipstring.AppendLine("Fluffy.ResearchTree.MissingFacilities".Translate(string.Join(", ",
                        MissingFacilities().Select(td => td.LabelCap).ToArray())));
                }
            }

            if (!Research.TechprintRequirementMet)
            {
                tooltipstring.AppendLine();
                tooltipstring.AppendLine("Fluffy.ResearchTree.MissingTechprints".Translate(Research.TechprintsApplied,
                    Research.techprintCount));
            }

            if (!Research.StudiedThingsRequirementsMet)
            {
                tooltipstring.AppendLine();
                tooltipstring.AppendLine("Fluffy.ResearchTree.MissingStudiedThings".Translate(string.Join(", ",
                    Research.requiredStudied.Select(def => def.LabelCap))));
            }

            if (!Research.PlayerMechanitorRequirementMet)
            {
                tooltipstring.AppendLine();
                tooltipstring.AppendLine("Fluffy.ResearchTree.MissingMechanitorRequirement".Translate());
            }

            if (Assets.UsingVanillaVehiclesExpanded)
            {
                var valueArray = new object[] { Research, null };
                var boolResult = (bool)Assets.IsDisabledMethod.Invoke(null, valueArray);

                if (boolResult)
                {
                    tooltipstring.AppendLine();
                    var wreck = (ThingDef)valueArray[1];
                    if (wreck != null)
                    {
                        tooltipstring.AppendLine("VVE_WreckNotRestored".Translate(wreck.LabelCap));
                    }
                }
            }

            TooltipHandler.TipRegion(Rect, tooltipstring.ToString());

            if (forceDetailedMode ||
                MainTabWindow_ResearchTree.Instance.ZoomLevel < Constants.DetailedModeZoomLevelCutoff)
            {
                var unlockDefsAndDescs = Research.GetUnlockDefsAndDescs();
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
                    TooltipHandler.TipRegion(rect, unlockDefsAndDescs[i].Second);
                }
            }

            if (Mouse.IsOver(Rect))
            {
                if (Available)
                {
                    Highlighted = true;
                    foreach (var item in GetMissingRequiredRecursive())
                    {
                        item.Highlighted = true;
                    }
                }
                else if (Completed)
                {
                    foreach (var child in Children)
                    {
                        child.Highlighted = true;
                    }
                }
            }
        }

        if (!Widgets.ButtonInvisible(Rect))
        {
            return;
        }

        if (Event.current.button == 1 && !Event.current.shift)
        {
            var researchCard = new Dialog_ResearchInfoCard(Research);
            Find.WindowStack.Add(researchCard);
            return;
        }

        if (!Available)
        {
            return;
        }

        if (Event.current.button == 0 && Event.current.control && !Research.IsFinished)
        {
            Queue.EnqueueRangeFirst(GetMissingRequiredRecursive().Concat(new List<ResearchNode>(new[] { this }))
                .Distinct());
        }

        if (Event.current.button == 0 && !Event.current.control && !Research.IsFinished)
        {
            if (!Queue.IsQueued(this))
            {
                Queue.EnqueueRange(
                    GetMissingRequiredRecursive().Concat(new List<ResearchNode>(new[] { this }))
                        .Distinct(), Event.current.shift);
            }
            else
            {
                Queue.Dequeue(this);
            }
        }

        if (Event.current.button != 1 && !Event.current.shift)
        {
            return;
        }

        if (!DebugSettings.godMode || !Prefs.DevMode || Research.IsFinished)
        {
            return;
        }

        Find.ResearchManager.FinishProject(Research);
        Queue.Notify_InstantFinished();
    }

    public List<ResearchNode> GetMissingRequiredRecursive()
    {
        var enumerable =
            (Research.prerequisites?.Where(rpd => !rpd.IsFinished) ?? Array.Empty<ResearchProjectDef>())
            .Concat(Research.hiddenPrerequisites?.Where(rpd => !rpd.IsFinished) ?? Array.Empty<ResearchProjectDef>())
            .Select(rpd =>
                rpd.ResearchNode());

        var list = new List<ResearchNode>(enumerable);
        foreach (var item in enumerable)
        {
            list.AddRange(item.GetMissingRequiredRecursive());
        }

        return list.Distinct().ToList();
    }

    public List<ThingDef> MissingFacilities()
    {
        return MissingFacilities(Research);
    }

    private StringBuilder GetResearchTooltipString()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(Research.description);

        stringBuilder.AppendLine();
        if (Queue.IsQueued(this))
        {
            stringBuilder.AppendLine("Fluffy.ResearchTree.LClickRemoveFromQueue".Translate());
            stringBuilder.AppendLine("Fluffy.ResearchTree.CLClickMoveToFrontOfQueue".Translate());
        }
        else
        {
            stringBuilder.AppendLine("Fluffy.ResearchTree.LClickReplaceQueue".Translate());
            stringBuilder.AppendLine("Fluffy.ResearchTree.SLClickAddToQueue".Translate());
            stringBuilder.AppendLine("Fluffy.ResearchTree.CLClickAddToFrontOfQueue".Translate());
        }

        stringBuilder.AppendLine("Fluffy.ResearchTree.SRClickShowInfo".Translate());
        if (DebugSettings.godMode)
        {
            stringBuilder.AppendLine("Fluffy.ResearchTree.RClickInstaFinishNew".Translate());
        }

        return stringBuilder;
    }

    public void DrawAt(Vector2 pos, Rect visibleRect, bool forceDetailedMode = false)
    {
        SetRects(pos);
        Draw(visibleRect, forceDetailedMode);
        SetRects();
    }
}