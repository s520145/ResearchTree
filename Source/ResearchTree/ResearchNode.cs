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

    public readonly ResearchProjectDef Research;

    private bool availableCache;

    private int cacheNumber;


    public ResearchNode(ResearchProjectDef research, int number)
    {
        Research = research;
        cacheNumber = number;
        availableCache = Research.CanStartNow;
        _pos = new Vector2(0f, research.researchViewY + 1f);
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

    private bool getCacheValue()
    {
        if (cacheNumber < Assets.AmountOfResearch)
        {
            cacheNumber++;
            return availableCache;
        }

        cacheNumber = 0;

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

        if (Research.prerequisites?.Any() != true)
        {
            availableCache = true;
            return availableCache;
        }

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


        availableCache = true;
        return availableCache;
    }

    public static bool BuildingPresent(ResearchProjectDef research)
    {
        if (DebugSettings.godMode && Prefs.DevMode)
        {
            return true;
        }

        if (_buildingPresentCache.TryGetValue(research, out var value))
        {
            return value;
        }

        value = research.requiredResearchBuilding == null || Find.Maps
            .SelectMany(map => map.listerBuildings.allBuildingsColonist).OfType<Building_ResearchBench>()
            .Any(b => research.CanBeResearchedAt(b, true));
        if (value)
        {
            value = research.Ancestors().All(BuildingPresent);
        }

        _buildingPresentCache.Add(research, value);
        return value;
    }

    public static void ClearCaches()
    {
        _buildingPresentCache.Clear();
        _missingFacilitiesCache.Clear();
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

    public static List<ThingDef> MissingFacilities(ResearchProjectDef research)
    {
        if (_missingFacilitiesCache.TryGetValue(research, out var value))
        {
            return value;
        }

        var list = (from rpd in research.Ancestors()
            where !rpd.IsFinished
            select rpd).ToList();
        list.Add(research);
        var source = Find.Maps.SelectMany(map => map.listerBuildings.allBuildingsColonist)
            .OfType<Building_ResearchBench>();
        var source2 = source.Select(b => b.def).Distinct();
        value = new List<ThingDef>();
        foreach (var item in list)
        {
            if (item.requiredResearchBuilding == null)
            {
                continue;
            }

            if (!source2.Contains(item.requiredResearchBuilding))
            {
                value.Add(item.requiredResearchBuilding);
            }

            if (item.requiredResearchFacilities.NullOrEmpty())
            {
                continue;
            }

            foreach (var facility in item.requiredResearchFacilities)
            {
                if (!source.Any(b => b.HasFacility(facility)))
                {
                    value.Add(facility);
                }
            }
        }

        value = value.Distinct().ToList();
        _missingFacilitiesCache.Add(research, value);
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
            GUI.color = Mouse.IsOver(Rect) ? GenUI.MouseoverColor : Color;
            if (Mouse.IsOver(Rect) || Highlighted)
            {
                GUI.DrawTexture(Rect, Assets.ButtonActive);
            }
            else
            {
                GUI.DrawTexture(Rect, Assets.Button);
            }

            if (Available)
            {
                var position = Rect.ContractedBy(3f);
                GUI.color = Assets.ColorAvailable[Research.techLevel];
                position.xMin += Research.ProgressPercent * position.width;
                GUI.DrawTexture(position, BaseContent.WhiteTex);
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
                Widgets.Label(LabelRect, Research.LabelCap);
            }
            else
            {
                Text.Anchor = TextAnchor.MiddleCenter;
                Text.WordWrap = false;
                Text.Font = GameFont.Medium;
                Widgets.Label(Rect, Research.LabelCap);
            }

            if (forceDetailedMode ||
                MainTabWindow_ResearchTree.Instance.ZoomLevel < Constants.DetailedModeZoomLevelCutoff)
            {
                Text.Anchor = TextAnchor.UpperRight;
                Text.Font = !(Research.CostApparent > 1000000f) ? GameFont.Small : GameFont.Tiny;
                Widgets.Label(CostLabelRect, Research.CostApparent.ToStringByStyle(ToStringStyle.Integer));
                GUI.DrawTexture(CostIconRect, !Completed && !Available ? Assets.Lock : Assets.ResearchIcon,
                    ScaleMode.ScaleToFit);
            }

            Text.WordWrap = true;
            TooltipHandler.TipRegion(Rect, GetResearchTooltipString, Research.GetHashCode());
            if (!BuildingPresent())
            {
                TooltipHandler.TipRegion(Rect, "Fluffy.ResearchTree.MissingFacilities".Translate(string.Join(", ",
                    (from td in MissingFacilities()
                        select td.LabelCap).ToArray())));
            }
            else if (!Research.TechprintRequirementMet)
            {
                TooltipHandler.TipRegion(Rect,
                    "Fluffy.ResearchTree.MissingTechprints".Translate(Research.TechprintsApplied,
                        Research.techprintCount));
            }
            else if (!Research.StudiedThingsRequirementsMet)
            {
                TooltipHandler.TipRegion(Rect,
                    "Fluffy.ResearchTree.MissingStudiedThings".Translate(string.Join(", ",
                        Research.requiredStudied.Select(def => def.LabelCap))));
            }
            else if (!Research.PlayerMechanitorRequirementMet)
            {
                TooltipHandler.TipRegion(Rect, "Fluffy.ResearchTree.MissingMechanitorRequirement".Translate());
            }

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
                    //unlockDefsAndDescs[i].First.DrawColouredIcon(rect);
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

        if (Event.current.button == 0 && !Research.IsFinished)
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
            (Research.prerequisites?.Where(rpd => !rpd.IsFinished) ?? Array.Empty<ResearchProjectDef>()).Select(rpd =>
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

    private string GetResearchTooltipString()
    {
        var stringBuilder = new StringBuilder();
        stringBuilder.AppendLine(Research.description);
        stringBuilder.AppendLine();
        if (Queue.IsQueued(this))
        {
            stringBuilder.AppendLine("Fluffy.ResearchTree.LClickRemoveFromQueue".Translate());
        }
        else
        {
            stringBuilder.AppendLine("Fluffy.ResearchTree.LClickReplaceQueue".Translate());
            stringBuilder.AppendLine("Fluffy.ResearchTree.SLClickAddToQueue".Translate());
        }

        stringBuilder.AppendLine("Fluffy.ResearchTree.SRClickShowInfo".Translate());
        if (DebugSettings.godMode)
        {
            stringBuilder.AppendLine("Fluffy.ResearchTree.RClickInstaFinishNew".Translate());
        }

        return stringBuilder.ToString();
    }

    public void DrawAt(Vector2 pos, Rect visibleRect, bool forceDetailedMode = false)
    {
        SetRects(pos);
        Draw(visibleRect, forceDetailedMode);
        SetRects();
    }
}