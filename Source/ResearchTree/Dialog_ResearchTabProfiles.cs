using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FluffyResearchTree;

public class Dialog_ResearchTabProfiles : Window
{
    private const float RowHeight = 28f;
    private const float ButtonHeight = 36f;
    private const float Padding = 12f;
    private const float Gap = 8f;
    private const float ProfileColumnWidth = 220f;

    private readonly List<ResearchTabDef> _allTabs;
    private readonly List<ResearchTabProfileData> _profiles;
    private readonly Dictionary<string, string> _truncateCache = new();

    private string _selectedProfileId;
    private Vector2 _profileScroll;
    private Vector2 _tabScroll;

    public Dialog_ResearchTabProfiles()
    {
        absorbInputAroundWindow = true;
        closeOnClickedOutside = true;
        doCloseX = true;
        forcePause = FluffyResearchTreeMod.instance.Settings.PauseOnOpen;

        var settings = FluffyResearchTreeMod.instance.Settings;
        settings.EnsureTabCache();

        _allTabs = settings.AllTabsCache?.ToList() ?? [];
        _allTabs.Sort((left, right) => string.Compare(left.LabelCap, right.LabelCap, StringComparison.OrdinalIgnoreCase));

        _profiles = settings.Profiles.Select(profile => profile.ToData()).ToList();
        _selectedProfileId = settings.ActiveTabProfileId;
        if (SelectedProfile == null && _profiles.Count > 0)
        {
            _selectedProfileId = _profiles[0].Id;
        }
    }

    private ResearchTabProfileData SelectedProfile =>
        _profiles.FirstOrDefault(profile => string.Equals(profile.Id, _selectedProfileId, StringComparison.OrdinalIgnoreCase));

    public override Vector2 InitialSize => new(920f, 560f);

    public override void PreOpen()
    {
        TooltipHandler_Modified.GloballyDisabled = true;
        base.PreOpen();
    }

    public override void PostClose()
    {
        base.PostClose();
        TooltipHandler_Modified.GloballyDisabled = false;
    }

    public override void DoWindowContents(Rect inRect)
    {
        Text.Anchor = TextAnchor.MiddleLeft;
        Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "Fluffy.ResearchTree.ProfileManage".Translate());
        Text.Anchor = TextAnchor.UpperLeft;

        var footerTop = inRect.yMax - Padding - ButtonHeight;
        var bodyTop = inRect.y + 38f;
        var bodyRect = new Rect(inRect.x, bodyTop, inRect.width, Mathf.Max(0f, footerTop - bodyTop - Gap));

        var profileRect = new Rect(bodyRect.x, bodyRect.y, ProfileColumnWidth, bodyRect.height);
        var editorRect = new Rect(profileRect.xMax + Gap, bodyRect.y,
            Mathf.Max(0f, bodyRect.width - ProfileColumnWidth - Gap), bodyRect.height);

        DrawProfileList(profileRect);
        DrawProfileEditor(editorRect);
        DrawFooter(new Rect(inRect.x + Padding, footerTop, inRect.width - (Padding * 2), ButtonHeight));
    }

    private void DrawProfileList(Rect rect)
    {
        Widgets.DrawMenuSection(rect);

        var buttonRow = new Rect(rect.x + Padding, rect.yMax - Padding - ButtonHeight,
            rect.width - (Padding * 2), ButtonHeight);
        var listRect = new Rect(rect.x + Padding, rect.y + Padding,
            rect.width - (Padding * 2), Mathf.Max(0f, buttonRow.yMin - rect.y - (Padding * 2)));

        var viewRect = new Rect(0f, 0f, listRect.width - 16f, Mathf.Max(listRect.height, _profiles.Count * RowHeight));
        Widgets.BeginScrollView(listRect, ref _profileScroll, viewRect);

        for (var i = 0; i < _profiles.Count; i++)
        {
            var profile = _profiles[i];
            var row = new Rect(0f, i * RowHeight, viewRect.width, RowHeight);
            var selected = string.Equals(profile.Id, _selectedProfileId, StringComparison.OrdinalIgnoreCase);
            if (selected)
            {
                Widgets.DrawHighlightSelected(row);
            }
            else if (Mouse.IsOver(row))
            {
                Widgets.DrawHighlight(row);
            }

            if (Widgets.ButtonText(row, Truncate(profile.Name, row.width), drawBackground: false))
            {
                _selectedProfileId = profile.Id;
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }

            TooltipHandler.TipRegion(row, profile.Name);
        }

        Widgets.EndScrollView();

        var halfWidth = (buttonRow.width - Gap) / 2f;
        if (Widgets.ButtonText(new Rect(buttonRow.x, buttonRow.y, halfWidth, buttonRow.height),
                "Fluffy.ResearchTree.ProfileNew".Translate()))
        {
            AddProfile();
        }

        var deleteRect = new Rect(buttonRow.x + halfWidth + Gap, buttonRow.y, halfWidth, buttonRow.height);
        var oldColor = GUI.color;
        if (_profiles.Count <= 1)
        {
            GUI.color = Color.gray;
        }

        if (Widgets.ButtonText(deleteRect, "Fluffy.ResearchTree.ProfileDelete".Translate()) && _profiles.Count > 1)
        {
            DeleteSelectedProfile();
        }

        GUI.color = oldColor;
    }

    private void DrawProfileEditor(Rect rect)
    {
        Widgets.DrawMenuSection(rect);
        rect = rect.ContractedBy(Padding);

        var selected = SelectedProfile;
        if (selected == null)
        {
            return;
        }

        var nameRow = new Rect(rect.x, rect.y, rect.width, RowHeight);
        var labelWidth = Mathf.Min(130f, nameRow.width * 0.35f);
        Widgets.Label(new Rect(nameRow.x, nameRow.y, labelWidth, nameRow.height),
            "Fluffy.ResearchTree.ProfileName".Translate());
        selected.Name = Widgets.TextField(new Rect(nameRow.x + labelWidth + Gap, nameRow.y,
            Mathf.Max(0f, nameRow.width - labelWidth - Gap), nameRow.height), selected.Name ?? string.Empty);

        var toolbar = new Rect(rect.x, nameRow.yMax + Gap, rect.width, 30f);
        DrawToolbar(toolbar, selected);

        var listTop = toolbar.yMax + Gap;
        DrawTabList(new Rect(rect.x, listTop, rect.width, Mathf.Max(0f, rect.yMax - listTop)), selected);
    }

    private void DrawToolbar(Rect rect, ResearchTabProfileData selected)
    {
        var x = rect.xMin;
        const float buttonWidth = 110f;

        if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "Fluffy.ResearchTree.selectAll".Translate()))
        {
            foreach (var tab in _allTabs)
            {
                selected.IncludedTabs.Add(tab.defName);
            }

            SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
        }

        x += buttonWidth + Gap;

        if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "Fluffy.ResearchTree.selectNone".Translate()))
        {
            selected.IncludedTabs.Clear();
            SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
        }

        x += buttonWidth + Gap;

        if (Widgets.ButtonText(new Rect(x, rect.y, buttonWidth, rect.height), "Fluffy.ResearchTree.selectInvert".Translate()))
        {
            foreach (var tab in _allTabs)
            {
                ToggleTab(selected, tab.defName);
            }

            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        var selectedCount = $"{selected.IncludedTabs.Count}/{_allTabs.Count}";
        var selectedCountSize = Text.CalcSize(selectedCount);
        Widgets.Label(
            new Rect(rect.xMax - selectedCountSize.x, rect.y + ((rect.height - selectedCountSize.y) / 2f),
                selectedCountSize.x, selectedCountSize.y),
            selectedCount);
    }

    private void DrawTabList(Rect rect, ResearchTabProfileData selected)
    {
        var total = _allTabs.Count;
        var perColumn = Mathf.CeilToInt(total / 2f);
        var columnWidth = (rect.width - Gap) / 2f;
        var viewHeight = Mathf.Max(perColumn * RowHeight, rect.height);

        var viewRect = new Rect(0f, 0f, rect.width - 16f, viewHeight);
        Widgets.BeginScrollView(rect, ref _tabScroll, viewRect);

        var firstColumn = new Rect(0f, 0f, columnWidth, perColumn * RowHeight);
        var secondColumn = new Rect(firstColumn.xMax + Gap, 0f, columnWidth, perColumn * RowHeight);

        DrawColumn(firstColumn, _allTabs.Take(perColumn), selected);
        DrawColumn(secondColumn, _allTabs.Skip(perColumn), selected);

        Widgets.EndScrollView();
    }

    private void DrawColumn(Rect rect, IEnumerable<ResearchTabDef> items, ResearchTabProfileData selected)
    {
        var rowIndex = 0;
        foreach (var tab in items)
        {
            DrawRow(new Rect(rect.x, rect.y + (rowIndex * RowHeight), rect.width, RowHeight), tab, selected);
            rowIndex++;
        }
    }

    private void DrawRow(Rect row, ResearchTabDef tab, ResearchTabProfileData selected)
    {
        if (Mouse.IsOver(row))
        {
            Widgets.DrawHighlight(row);
        }

        var checkboxRect = new Rect(row.x, row.y + 4f, 24f, 24f);
        var checkedNow = selected.IncludedTabs.Contains(tab.defName);
        Widgets.Checkbox(checkboxRect.position, ref checkedNow, 24f, paintable: true);

        if (checkedNow)
        {
            selected.IncludedTabs.Add(tab.defName);
        }
        else
        {
            selected.IncludedTabs.Remove(tab.defName);
        }

        var labelRect = new Rect(checkboxRect.xMax + 6f, row.y, row.width - 32f, row.height);
        if (Widgets.ButtonText(labelRect, Truncate(tab.LabelCap, labelRect.width), drawBackground: false))
        {
            ToggleTab(selected, tab.defName);
            SoundDefOf.Tick_High.PlayOneShotOnCamera();
        }

        TooltipHandler.TipRegion(labelRect, tab.LabelCap);
    }

    private string Truncate(string text, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var cacheKey = $"{text}|{Mathf.RoundToInt(maxWidth)}";
        if (_truncateCache.TryGetValue(cacheKey, out var cached))
        {
            return cached;
        }

        if (Text.CalcSize(text).x <= maxWidth)
        {
            _truncateCache[cacheKey] = text;
            return text;
        }

        const string ellipsis = "...";
        for (var length = Mathf.Min(text.Length, 64); length >= 1; length--)
        {
            var candidate = text.Substring(0, length) + ellipsis;
            if (Text.CalcSize(candidate).x <= maxWidth)
            {
                _truncateCache[cacheKey] = candidate;
                return candidate;
            }
        }

        _truncateCache[cacheKey] = ellipsis;
        return ellipsis;
    }

    private void DrawFooter(Rect rect)
    {
        const float buttonWidth = 150f;
        var rebuild = new Rect(rect.xMax - buttonWidth, rect.y, buttonWidth, rect.height);
        var cancel = new Rect(rebuild.xMin - Gap - buttonWidth, rect.y, buttonWidth, rect.height);

        if (Widgets.ButtonText(cancel, "Fluffy.ResearchTree.cancel".Translate()))
        {
            Close(doCloseSound: true);
            return;
        }

        if (Widgets.ButtonText(rebuild, "Fluffy.ResearchTree.rebuild".Translate()))
        {
            ApplyAndRequestRebuild();
        }
    }

    private void ApplyAndRequestRebuild()
    {
        var settings = FluffyResearchTreeMod.instance.Settings;

        settings.ReplaceProfiles(_profiles, _selectedProfileId);
        settings.SetActiveProfile(_selectedProfileId);
        FluffyResearchTreeMod.instance.WriteSettings();

        Assets.RefreshResearch = true;
        Tree.InvalidateProfileCache();

        Close(doCloseSound: true);
        Tree.RequestRebuild(resetZoom: true, reopenResearchTab: false);

        Messages.Message("Fluffy.ResearchTree.ProfileApplied".Translate(), MessageTypeDefOf.TaskCompletion,
            historical: false);
    }

    private void AddProfile()
    {
        var selected = SelectedProfile;
        var id = ResearchTabProfileSelection.CreateUniqueId(_profiles);
        var name = ResearchTabProfileSelection.CreateUniqueName(
            _profiles,
            "Fluffy.ResearchTree.ProfileNewDefaultName".Translate().ToString());
        var included = selected?.IncludedTabs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var profile = new ResearchTabProfileData(id, name, included);
        _profiles.Add(profile);
        _selectedProfileId = profile.Id;
        SoundDefOf.Tick_High.PlayOneShotOnCamera();
    }

    private void DeleteSelectedProfile()
    {
        var selected = SelectedProfile;
        if (selected == null || _profiles.Count <= 1)
        {
            return;
        }

        var index = _profiles.IndexOf(selected);
        _profiles.RemoveAt(index);
        var nextIndex = Mathf.Clamp(index, 0, _profiles.Count - 1);
        _selectedProfileId = _profiles[nextIndex].Id;
        SoundDefOf.Tick_Low.PlayOneShotOnCamera();
    }

    private static void ToggleTab(ResearchTabProfileData profile, string defName)
    {
        if (!profile.IncludedTabs.Remove(defName))
        {
            profile.IncludedTabs.Add(defName);
        }
    }
}
