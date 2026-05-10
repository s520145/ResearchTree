// MainTabWindow_ResearchTree.cs
// Copyright Karel Kroeze, 2020-2020

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class MainTabWindow_ResearchTree : MainTabWindow
{
    private static Vector2 _scrollPosition = Vector2.zero;

    private static Rect _treeRect;
    private readonly HashSet<ResearchProjectDef> _matchingProjects = [];

    private readonly QuickSearchWidget _quickSearchWidget = new();

    private readonly Dictionary<string, ProfileViewState> _profileViewStates = new();

    private Rect _baseViewRect;

    private Rect _baseViewRectInner;

    private Dictionary<ResearchProjectDef, List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>>>
        _cachedUnlockedDefsGroupedByPrerequisites;

    private List<ResearchProjectDef> _cachedVisibleResearchProjects;

    private bool _logFirstDrawNextFrame;

    private Vector2 _mousePosition = Vector2.zero;
    private Vector2 _profileTabScroll = Vector2.zero;

    private Rect _viewRect;

    private Rect _viewRectInner;

    private float _zoomLevel = 1f;

    private bool _panning;
    private Vector2 _dragStart;
    private const float PanThreshold = 4f;
    private readonly HashSet<int> _capturedMouseButtons = new();
    private const float TopBarControlGap = 6f;
    private const float TopBarLabelPadding = 24f;
    private const float TopBarMinSearchWidth = 290f;
    private const float TopBarMinButtonWidth = 140f;
    private const float ProfileTabMinWidth = 86f;
    private const float ProfileTabMaxWidth = 280f;
    private const float ProfileTabTextPadding = 30f;
    private const float ProfileTabAddWidth = 34f;
    private const float ProfileTabManageWidth = 86f;
    private const float ProfileRowHeight = 32f;
    private const float ProfileRowInset = 3f;
    private const float WheelScrollStep = 10f;
    private const long PreOpenWarnThresholdMs = 250;
    private const long FirstDrawWarnThresholdMs = 200;

    public bool ViewRectDirty = true;

    public bool ViewRectInnerDirty = true;

    private readonly struct ProfileViewState
    {
        public ProfileViewState(Vector2 scrollPosition, float zoomLevel)
        {
            ScrollPosition = scrollPosition;
            ZoomLevel = zoomLevel;
        }

        public Vector2 ScrollPosition { get; }

        public float ZoomLevel { get; }
    }

    public MainTabWindow_ResearchTree()
    {
        doWindowBackground = Assets.UsingMinimap;
        closeOnClickedOutside = false;
        Instance = this;
        preventCameraMotion = true;
        forcePause = FluffyResearchTreeMod.instance.Settings.PauseOnOpen;
    }

    public static MainTabWindow_ResearchTree Instance { get; private set; }

    public float ScaledMargin => Constants.Margin * ZoomLevel / Prefs.UIScale;

    public float ZoomLevel
    {
        get => _zoomLevel;
        private set
        {
            _zoomLevel = Mathf.Clamp(value, 1f, MaxZoomLevel);
            ViewRectDirty = true;
            ViewRectInnerDirty = true;
        }
    }

    private Rect ViewRect
    {
        get
        {
            if (!ViewRectDirty)
            {
                return _viewRect;
            }

            _viewRect = new Rect(
                _baseViewRect.xMin * ZoomLevel,
                _baseViewRect.yMin * ZoomLevel,
                _baseViewRect.width * ZoomLevel,
                _baseViewRect.height * ZoomLevel);
            ViewRectDirty = false;

            return _viewRect;
        }
    }

    private Rect ViewRect_Inner
    {
        get
        {
            if (!ViewRectInnerDirty)
            {
                return _viewRectInner;
            }

            _viewRectInner = _viewRect.ContractedBy(Margin * ZoomLevel);
            ViewRectInnerDirty = false;

            return _viewRectInner;
        }
    }

    private Rect InteractionRect
    {
        get
        {
            var rect = ViewRect;
            var zoom = ZoomLevel;

            if (!Mathf.Approximately(zoom, 1f) && zoom > 0f)
            {
                rect.xMin /= zoom;
                rect.yMin /= zoom;
                rect.width /= zoom;
                rect.height /= zoom;
            }

            return rect;
        }
    }

    private static Rect TreeRect
    {
        get
        {
            if (_treeRect != default)
            {
                return _treeRect;
            }

            var width = Tree.Size.x * (Constants.NodeSize.x + Constants.NodeMargins.x);
            var height = Tree.Size.z * (Constants.NodeSize.y + Constants.NodeMargins.y) * 1.02f; // To avoid cutoff
            _treeRect = new Rect(0f, 0f, width, height);

            return _treeRect;
        }
    }

    internal static void InvalidateTreeRectCache()
    {
        _treeRect = default;
    }

    private Rect VisibleRect => new(_scrollPosition.x, _scrollPosition.y, ViewRect_Inner.width, ViewRect_Inner.height);

    private float MaxZoomLevel
    {
        get
        {
            // get the minimum zoom level at which the entire tree fits onto the screen, or a static maximum zoom level.
            var fitZoomLevel = Mathf.Max(TreeRect.width / _baseViewRectInner.width,
                TreeRect.height / _baseViewRectInner.height);
            return Mathf.Min(fitZoomLevel, Constants.AbsoluteMaxZoomLevel);
        }
    }

    private float DragSpeedMultiplier
    {
        get
        {
            if (!Tree.Initialized || _baseViewRectInner.width <= 0f || _baseViewRectInner.height <= 0f)
            {
                return 1f;
            }

            float widthRatio = TreeRect.width / _baseViewRectInner.width;
            float heightRatio = TreeRect.height / _baseViewRectInner.height;
            float sizeRatio = Mathf.Max(widthRatio, heightRatio, 1f);

            return Mathf.Clamp(Mathf.Sqrt(sizeRatio), 1f, 3f);
        }
    }

    private List<ResearchProjectDef> VisibleResearchProjects
    {
        get
        {
            return _cachedVisibleResearchProjects ??=
            [
                ..DefDatabase<ResearchProjectDef>.AllDefsListForReading
                    .Where(d => Find.Storyteller.difficulty.AllowedBy(d.hideWhen) ||
                                Find.ResearchManager.IsCurrentProject(d))
            ];
        }
    }

    public void Notify_TreeInitialized()
    {
        Assets.RefreshResearch = true;
        InvalidateTreeRectCache();
        setRects();
        ApplyTreeInitializedState();
        ClampScroll();
        Tree.QueueBackgroundProfileBuilds();
    }

    public void SaveActiveProfileViewState()
    {
        var profileId = FluffyResearchTreeMod.instance?.Settings?.ActiveTabProfileId;
        if (string.IsNullOrWhiteSpace(profileId))
        {
            return;
        }

        _profileViewStates[profileId] = new ProfileViewState(_scrollPosition, ZoomLevel);
    }

    public void Notify_ProfileTreeSwitched(string profileId)
    {
        Assets.RefreshResearch = true;
        InvalidateTreeRectCache();
        setRects();
        ApplyTreeInitializedState();

        if (!string.IsNullOrWhiteSpace(profileId) && _profileViewStates.TryGetValue(profileId, out var state))
        {
            ZoomLevel = state.ZoomLevel;
            _scrollPosition = state.ScrollPosition;
        }

        ClampScroll();
    }

    public override void PreOpen()
    {
        var sw = Stopwatch.StartNew();

        try
        {
            base.PreOpen();

            closeOnClickedOutside = false;
            preventCameraMotion = true;

            setRects();
            Tree.WaitForInitialization();
            Assets.RefreshResearch = true;
            closeOnClickedOutside = false;

            _capturedMouseButtons.Clear();
            _panning = false;
            _dragStart = Vector2.zero;
            _mousePosition = Vector2.zero;

            _cachedUnlockedDefsGroupedByPrerequisites = null;
            _cachedVisibleResearchProjects = null;
            _quickSearchWidget.Reset();
            updateSearchResults();

            _logFirstDrawNextFrame = true;

            ApplyTreeInitializedState();
            Tree.QueueBackgroundProfileBuilds();
        }
        finally
        {
            sw.Stop();
            Logging.Performance("MainTabWindow_ResearchTree.PreOpen", sw.ElapsedMilliseconds,
                PreOpenWarnThresholdMs);
        }
    }

    public override void WindowOnGUI()
    {
        base.WindowOnGUI();
        Assets.DrawWindowBackground(windowRect, FluffyResearchTreeMod.instance.Settings.BackgroundColor);
    }

    private void ApplyTreeInitializedState()
    {
        if (!Tree.Initialized)
        {
            return;
        }

        var firstLoad = !Tree.FirstLoadDone;
        if (!firstLoad)
        {
            Tree.ResetNodeAvailabilityCache();
        }

        if (Assets.SemiRandomResearchLoaded)
        {
            var preValue = Assets.SemiResearchEnabled;
            Assets.SemiResearchEnabled = (bool)AccessTools
                .Field("CM_Semi_Random_Research.SemiRandomResearchModSettings:featureEnabled")
                .GetValue(Assets.SettingsInstance);
            if (preValue != Assets.SemiResearchEnabled)
            {
                Tree.ResetNodeAvailabilityCache();
            }
        }

        if (firstLoad)
        {
            Queue.Notify_TreeReinitialized();
        }

        Queue.RefreshQueue();
        _cachedUnlockedDefsGroupedByPrerequisites = null;
        _cachedVisibleResearchProjects = null;
        updateSearchResults();

        ViewRectDirty = true;
        ViewRectInnerDirty = true;

        if (firstLoad)
        {
            Tree.FirstLoadDone = true;
        }

        Tree.CacheActiveProfileState();
    }

    private void setRects()
    {
        var uiScale = Prefs.UIScale;
        var marginScaled = StandardMargin / uiScale;
        var startPosition = new Vector2(marginScaled,
            Constants.TopBarHeight + Constants.Margin + marginScaled);

        var size = new Vector2((Screen.width - (StandardMargin * 2f)) / uiScale,
            Mathf.Max(0f, UI.screenHeight - MainButtonDef.ButtonHeight - startPosition.y));

        _baseViewRect = new Rect(startPosition, size);
        _baseViewRectInner = _baseViewRect.ContractedBy(Constants.Margin / uiScale);
        windowRect.x = 0f;
        windowRect.y = 0f;
        windowRect.width = UI.screenWidth;
        windowRect.height = Mathf.Max(0f, UI.screenHeight - MainButtonDef.ButtonHeight);
    }
    private void ClampScroll()
    {
        if (!Tree.Initialized) return;
        var maxX = Mathf.Max(0f, TreeRect.width - ViewRect.width);
        var maxY = Mathf.Max(0f, TreeRect.height - ViewRect.height);
        _scrollPosition.x = Mathf.Clamp(_scrollPosition.x, 0f, maxX);
        _scrollPosition.y = Mathf.Clamp(_scrollPosition.y, 0f, maxY);
    }

    public override void DoWindowContents(Rect canvas)
    {
        drawTopBar(new Rect(canvas.xMin, canvas.yMin, canvas.width, Constants.TopBarHeight));

        if (!Tree.Initialized)
        {
            DrawGenerationInProgressMessage(canvas);
            return;
        }

        if (!Tree.FirstLoadDone)
        {
            ApplyTreeInitializedState();
        }

        if (Tree.NoTabsSelected)
        {
            DrawNoTabsSelectedMessage(canvas);
            return;
        }

        bool shouldAbsorbMouseDown = false;
        var evt = Event.current;
        if (evt.type == EventType.MouseDown && (evt.button == 0 || evt.button == 1) && Mouse.IsOver(windowRect))
        {
            if (!IsPointInScrollbarArea(evt.mousePosition))
            {
                shouldAbsorbMouseDown = true;
            }
        }

        handleZoom();
        handleDolly();
        handleDragging();
        ClampScroll();

        applyZoomLevel();
        Stopwatch firstDrawTimer = null;
        if (_logFirstDrawNextFrame)
        {
            firstDrawTimer = Stopwatch.StartNew();
        }

        var skipTreeDraw = ShouldSkipTreeDrawThisEvent();

        _scrollPosition = GUI.BeginScrollView(ViewRect, _scrollPosition, TreeRect, true, true);
        if (!skipTreeDraw)
        {
            Tree.Draw(VisibleRect);
            Queue.DrawLabels(VisibleRect);

            if (shouldAbsorbMouseDown && Event.current.type == EventType.MouseDown)
            {
                Event.current.Use();
            }
        }
        GUI.EndScrollView(false);
        ResetZoomLevel();
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;

        AbsorbUnclaimedInput();

        if (firstDrawTimer != null)
        {
            firstDrawTimer.Stop();
            Logging.Performance("MainTabWindow_ResearchTree.DoWindowContents.FirstDraw",
                firstDrawTimer.ElapsedMilliseconds, FirstDrawWarnThresholdMs);
            _logFirstDrawNextFrame = false;
        }
    }

    private bool ShouldSkipTreeDrawThisEvent()
    {
        var evt = Event.current;
        if (evt == null)
        {
            return false;
        }

        switch (evt.type)
        {
            case EventType.Layout:
                return true;
            case EventType.MouseDrag:
                return _panning;
            case EventType.ScrollWheel:
                return true;
            default:
                return false;
        }
    }

    private void AbsorbUnclaimedInput()
    {
        var e = Event.current;
        if (!e.isMouse)
        {
            return;
        }

        if (e.type == EventType.Used)
        {
            return;
        }

        var pointerOverWindow = Mouse.IsOver(windowRect);
        var capturing = _capturedMouseButtons.Count > 0;

        if (!pointerOverWindow && !capturing)
        {
            return;
        }

        switch (e.type)
        {
            case EventType.MouseDown:
                if (IsPointInScrollbarArea(e.mousePosition))
                {
                    return;
                }
                _capturedMouseButtons.Add(e.button);
                e.Use();
                break;
            case EventType.ScrollWheel:
            case EventType.ContextClick:
                e.Use();
                break;
            case EventType.MouseDrag:
                if (!capturing || !_capturedMouseButtons.Contains(e.button))
                {
                    return;
                }

                e.Use();
                break;
            case EventType.MouseUp:
                _capturedMouseButtons.Remove(e.button);
                e.Use();
                break;
        }
    }


    private static void DrawGenerationInProgressMessage(Rect canvas)
    {
        var messageRect = new Rect(
            canvas.xMin,
            canvas.yMin + Constants.TopBarHeight,
            canvas.width,
            Mathf.Max(0f, canvas.height - Constants.TopBarHeight));

        if (messageRect.height <= 0f)
        {
            return;
        }

        messageRect = messageRect.ContractedBy(Constants.Margin);
        if (messageRect.height <= 0f)
        {
            return;
        }

        var previousColor = GUI.color;
        GUI.color = Color.yellow;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(messageRect, "Fluffy.ResearchTree.GenerationInProgress".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = previousColor;
    }

    private static void DrawNoTabsSelectedMessage(Rect canvas)
    {
        var messageRect = new Rect(
            canvas.xMin,
            canvas.yMin + Constants.TopBarHeight,
            canvas.width,
            Mathf.Max(0f, canvas.height - Constants.TopBarHeight));

        if (messageRect.height <= 0f)
        {
            return;
        }

        messageRect = messageRect.ContractedBy(Constants.Margin);
        if (messageRect.height <= 0f)
        {
            return;
        }

        var previousColor = GUI.color;
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.MiddleCenter;
        Widgets.Label(messageRect, "Fluffy.ResearchTree.NoTabsSelected".Translate());
        Text.Anchor = TextAnchor.UpperLeft;
        GUI.color = previousColor;
    }


    public override void Notify_ClickOutsideWindow()
    {
        base.Notify_ClickOutsideWindow();
        _quickSearchWidget.Unfocus();
    }

    // default W A S D move
    private static void handleDolly()
    {
        if (Event.current.type != EventType.Repaint) return;

        float step = 600f * Time.unscaledDeltaTime * Mathf.Max(1f, Instance.ZoomLevel) * Instance.DragSpeedMultiplier;

        if (KeyBindingDefOf.MapDolly_Left.IsDown) _scrollPosition.x -= step;
        if (KeyBindingDefOf.MapDolly_Right.IsDown) _scrollPosition.x += step;
        if (KeyBindingDefOf.MapDolly_Up.IsDown) _scrollPosition.y -= step;
        if (KeyBindingDefOf.MapDolly_Down.IsDown) _scrollPosition.y += step;

        Instance.ClampScroll();
    }

    private void handleZoom()
    {
        if (!Tree.Initialized) return;
        var evt = Event.current;
        if (!evt.isScrollWheel) return;

        if (evt.alt)
        {
            _scrollPosition.x += evt.delta.y * WheelScrollStep;
            ClampScroll();
            return;
        }

        if (evt.control == FluffyResearchTreeMod.instance.Settings.CtrlFunction)
        {
            _scrollPosition.y += evt.delta.y * WheelScrollStep;
            ClampScroll();
            return;
        }

        var mousePosition = evt.mousePosition;
        var vector = (evt.mousePosition - _scrollPosition) / ZoomLevel;
        ZoomLevel += evt.delta.y * Constants.ZoomStep * ZoomLevel;
        _scrollPosition = mousePosition - (vector * ZoomLevel);
        ClampScroll();
        evt.Use();
    }

    private void handleDragging()
    {
        if (Queue._draggedNode != null) return;

        var e = Event.current;

        if (e.type == EventType.Used) return;

        bool inWindow = Mouse.IsOver(this.windowRect);

        if (e.type == EventType.MouseDown && inWindow && e.button == 0)
        {
            if (IsPointInScrollbarArea(e.mousePosition))
            {
                _dragStart = Vector2.zero;
                return;
            }

            _dragStart = _mousePosition = e.mousePosition;
            return;
        }

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            if (_panning)
            {
                _panning = false;
                _capturedMouseButtons.Remove(e.button);
            }
            _dragStart = _mousePosition = Vector2.zero;
            return;
        }

        if (e.type == EventType.MouseDrag && e.button == 0 && _dragStart != Vector2.zero)
        {
            bool inView = inWindow && InteractionRect.Contains(_dragStart);
            if (!inView) return;

            if (!_panning)
            {
                if ((e.mousePosition - _dragStart).sqrMagnitude < PanThreshold * PanThreshold)
                    return;

                _panning = true;
                _capturedMouseButtons.Add(e.button);
            }

            var delta = e.mousePosition - _mousePosition;
            _scrollPosition -= delta * DragSpeedMultiplier / ZoomLevel;
            ClampScroll();
            _mousePosition = e.mousePosition;

            e.Use();
            return;
        }
    }

    private bool IsPointInScrollbarArea(Vector2 point)
    {
        float scrollbarSize = 20f;

        var winRect = windowRect;

        if (TreeRect.height > _baseViewRect.height)
        {
            float scrollbarLeft = winRect.xMax - 50f;
            float scrollbarRight = winRect.xMax + 20f;

            if (point.x >= scrollbarLeft &&
                point.x <= scrollbarRight &&
                point.y >= winRect.yMin &&
                point.y <= winRect.yMax)
            {
                return true;
            }
        }

        if (TreeRect.width > _baseViewRect.width)
        {
            float scrollbarTop = winRect.yMax - scrollbarSize;
            float scrollbarBottom = winRect.yMax + 20f;

            if (point.y >= scrollbarTop &&
                point.y <= scrollbarBottom &&
                point.x >= winRect.xMin &&
                point.x <= winRect.xMax)
            {
                return true;
            }
        }

        return false;
    }


    private void applyZoomLevel()
    {
        GUI.EndClip();
        GUI.EndClip();
        GUI.matrix = Matrix4x4.TRS(new Vector3(0f, 0f, 0f), Quaternion.identity,
            new Vector3(Prefs.UIScale / ZoomLevel, Prefs.UIScale / ZoomLevel, 1f));
    }

    public void ResetZoomLevel()
    {
        UI.ApplyUIScale();
        GUI.BeginClip(windowRect);
        var contentHeight = Mathf.Max(0f, windowRect.height - Constants.TopBarHeight);
        GUI.BeginClip(new Rect(0f, Constants.TopBarHeight, windowRect.width, contentHeight));
    }
    private void drawTopBar(Rect canvas)
    {
        float buttonWidth = CalculateTopBarButtonWidth();
        float innerRequiredWidth = TopBarMinSearchWidth;
        if (ModsConfig.AnomalyActive)
        {
            innerRequiredWidth += buttonWidth + TopBarControlGap;
        }

        float leftWidth = Mathf.Min(canvas.width, innerRequiredWidth + (Constants.Margin * 2f));

        var primaryRow = new Rect(canvas.x, canvas.y, canvas.width, Constants.PrimaryTopBarHeight);
        var left = new Rect(primaryRow.x, primaryRow.y, leftWidth, primaryRow.height);
        var right = primaryRow;
        right.xMin = Mathf.Min(canvas.xMax, left.xMax + Constants.Margin);

        DrawSearchBar(left.ContractedBy(Constants.Margin));
        Queue.DrawQueue(right.ContractedBy(Constants.Margin), !_panning);

        var profileRow = new Rect(
            canvas.x + Constants.Margin,
            primaryRow.yMax + TopBarControlGap,
            Mathf.Max(0f, canvas.width - (Constants.Margin * 2f)),
            ProfileRowHeight);
        DrawProfileTabs(profileRow);
    }

    private void DrawSearchBar(Rect canvas)
    {
        bool skipCompleted = FluffyResearchTreeMod.instance.Settings.SkipCompleted;
        bool anomalyActive = ModsConfig.AnomalyActive;
        string anomalyLabel = "Fluffy.ResearchTree.Anomaly".Translate();
        string toggleLabel = skipCompleted ? "Fluffy.ResearchTree.invisible".Translate()
                                           : "Fluffy.ResearchTree.visible".Translate();

        float buttonWidth = CalculateTopBarButtonWidth();
        const int rowCount = 2;
        float verticalGap = TopBarControlGap;
        float maxContentHeight = Mathf.Min(canvas.height, rowCount * Constants.QueueLabelSize + verticalGap);
        float rowHeight = Mathf.Max(0f, (maxContentHeight - verticalGap) / rowCount);
        float contentHeight = rowHeight * rowCount + verticalGap;
        float startY = canvas.yMin + Mathf.Max(0f, (canvas.height - contentHeight) / 2f);

        var searchRect = new Rect(canvas.xMin, startY, canvas.width, rowHeight);

        float secondRowY = startY + rowHeight + verticalGap;

        Rect? anomalyRect = null;
        float toggleX = canvas.xMin;
        if (anomalyActive)
        {
            anomalyRect = new Rect(canvas.xMin, secondRowY, buttonWidth, rowHeight);
            toggleX = anomalyRect.Value.xMax + TopBarControlGap;
        }

        float toggleWidth = Mathf.Max(0f, canvas.xMax - toggleX);
        var toggleRect = new Rect(toggleX, secondRowY, toggleWidth, rowHeight);

        _quickSearchWidget.OnGUI(searchRect, () => updateSearchResults(canvas));

        if (anomalyRect.HasValue && DrawTopBarButton(anomalyRect.Value, anomalyLabel))
        {
            ((MainTabWindow_Research)MainButtonDefOf.Research.TabWindow).CurTab = ResearchTabDefOf.Anomaly;
            Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Research);
            return;
        }

        var toggleColor = skipCompleted
            ? new Color(0.12f, 0.42f, 0.16f, 0.82f)
            : new Color(0.50f, 0.18f, 0.14f, 0.82f);
        if (DrawTopBarButton(toggleRect, toggleLabel, null, toggleColor))
        {
            FluffyResearchTreeMod.instance.Settings.SkipCompleted = !FluffyResearchTreeMod.instance.Settings.SkipCompleted;
            Tree.ResetNodeAvailabilityCache();
            Tree.InvalidateProfileCache();
            Assets.RefreshResearch = true;
        }
    }

    private void DrawProfileTabs(Rect canvas)
    {
        var settings = FluffyResearchTreeMod.instance.Settings;
        settings.EnsureTabCache();

        var profiles = settings.Profiles.ToList();
        if (profiles.Count == 0 || canvas.width <= 0f || canvas.height <= 0f)
        {
            return;
        }

        Widgets.DrawBoxSolidWithOutline(canvas, new Color(0.035f, 0.04f, 0.045f, 0.82f),
            new Color(0.22f, 0.24f, 0.26f, 0.9f), 1);
        var inner = canvas.ContractedBy(ProfileRowInset);

        var manageRect = new Rect(inner.x, inner.y, ProfileTabManageWidth, inner.height);
        var addRect = new Rect(inner.xMax - ProfileTabAddWidth, inner.y,
            ProfileTabAddWidth, inner.height);
        var scrollRect = new Rect(manageRect.xMax + TopBarControlGap, inner.y,
            Mathf.Max(0f, addRect.xMin - TopBarControlGap - manageRect.xMax - TopBarControlGap), inner.height);
        if (scrollRect.width <= 0f)
        {
            return;
        }

        if (DrawTopBarButton(manageRect, "Fluffy.ResearchTree.ProfileManageShort".Translate(),
                "Fluffy.ResearchTree.ProfileManage".Translate(), new Color(0.18f, 0.27f, 0.36f, 0.88f)))
        {
            Find.WindowStack.Add(new Dialog_ResearchTabProfiles());
        }

        var oldFont = Text.Font;
        Text.Font = GameFont.Small;
        var tabWidths = profiles.Select(GetProfileTabWidth).ToList();
        Text.Font = oldFont;

        var totalWidth = tabWidths.Sum() + Mathf.Max(0, tabWidths.Count - 1) * TopBarControlGap;
        var maxScroll = Mathf.Max(0f, totalWidth - scrollRect.width);
        HandleProfileTabWheel(scrollRect, maxScroll);
        _profileTabScroll.x = Mathf.Clamp(_profileTabScroll.x, 0f, maxScroll);
        _profileTabScroll.y = 0f;

        GUI.BeginGroup(scrollRect);
        var x = -_profileTabScroll.x;
        for (var i = 0; i < profiles.Count; i++)
        {
            var profile = profiles[i];
            var tabRect = new Rect(x, 0f, tabWidths[i], inner.height);
            DrawProfileTab(tabRect, profile, settings.ActiveTabProfileId);
            x += tabRect.width + TopBarControlGap;
        }
        GUI.EndGroup();

        if (DrawTopBarButton(addRect, "+", "Fluffy.ResearchTree.ProfileNewQuick".Translate(),
                new Color(0.18f, 0.34f, 0.22f, 0.84f)))
        {
            CreateProfileFromActiveSelection();
        }

    }

    private void HandleProfileTabWheel(Rect scrollRect, float maxScroll)
    {
        if (maxScroll <= 0f)
        {
            _profileTabScroll = Vector2.zero;
            return;
        }

        var evt = Event.current;
        if (evt.type != EventType.ScrollWheel || !scrollRect.Contains(evt.mousePosition))
        {
            return;
        }

        _profileTabScroll.x = Mathf.Clamp(_profileTabScroll.x + evt.delta.y * WheelScrollStep * 3f, 0f, maxScroll);
        _profileTabScroll.y = 0f;
        evt.Use();
    }

    private static void CreateProfileFromActiveSelection()
    {
        var settings = FluffyResearchTreeMod.instance.Settings;
        settings.EnsureTabCache();

        var profiles = settings.Profiles.Select(profile => profile.ToData()).ToList();
        var active = settings.ActiveProfile;
        var id = ResearchTabProfileSelection.CreateUniqueId(profiles);
        var name = ResearchTabProfileSelection.CreateUniqueName(
            profiles,
            "Fluffy.ResearchTree.ProfileNewDefaultName".Translate().ToString());
        var includedTabs = active?.IncludedTabs ?? new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        profiles.Add(new ResearchTabProfileData(id, name, includedTabs));
        settings.ReplaceProfiles(profiles, id);
        settings.SetActiveProfile(id);
        FluffyResearchTreeMod.instance.WriteSettings();

        Tree.CacheActiveProfileState();
        MainTabWindow_ResearchTree.Instance?.Notify_ProfileTreeSwitched(id);
        Messages.Message("Fluffy.ResearchTree.ProfileCreated".Translate(name),
            MessageTypeDefOf.TaskCompletion, historical: false);
    }

    private static void DrawProfileTab(Rect rect, ResearchTabProfile profile, string activeProfileId)
    {
        var active = string.Equals(profile.Id, activeProfileId, System.StringComparison.OrdinalIgnoreCase);
        var building = Tree.IsProfileBuilding(profile.Id);

        var label = building ? $"{profile.Name}..." : profile.Name;
        var fillColor = active
            ? new Color(0.18f, 0.30f, 0.42f, 0.94f)
            : building
                ? new Color(0.40f, 0.33f, 0.15f, 0.82f)
                : new Color(0.09f, 0.10f, 0.11f, 0.78f);
        var outlineColor = active ? new Color(0.64f, 0.78f, 0.95f, 0.95f) : new Color(0.32f, 0.32f, 0.32f, 0.78f);

        if (Mouse.IsOver(rect))
        {
            fillColor = active
                ? new Color(0.29f, 0.45f, 0.62f, 0.95f)
                : new Color(fillColor.r + 0.06f, fillColor.g + 0.06f, fillColor.b + 0.06f, fillColor.a);
        }

        Widgets.DrawBoxSolidWithOutline(rect, fillColor, outlineColor, 1);
        if (active)
        {
            Widgets.DrawBoxSolid(new Rect(rect.x + 3f, rect.yMax - 3f, Mathf.Max(0f, rect.width - 6f), 2f),
                new Color(0.63f, 0.86f, 1f, 0.95f));
        }

        DrawCenteredLabel(HorizontallyPadded(rect, 6f), label);

        if (Widgets.ButtonInvisible(rect, doMouseoverSound: true))
        {
            Tree.SwitchToProfile(profile.Id);
        }

        var tooltip = building
            ? "Fluffy.ResearchTree.ProfileLoading".Translate()
            : "Fluffy.ResearchTree.ProfileSwitch".Translate(profile.Name);
        TooltipHandler.TipRegion(rect, tooltip);
    }

    private static bool DrawTopBarButton(Rect rect, string label, string tooltip = null, Color? fillColor = null)
    {
        if (rect.width <= 0f || rect.height <= 0f)
        {
            return false;
        }

        var color = fillColor ?? new Color(0.14f, 0.14f, 0.14f, 0.76f);
        if (Mouse.IsOver(rect))
        {
            color = new Color(
                Mathf.Min(color.r + 0.08f, 1f),
                Mathf.Min(color.g + 0.08f, 1f),
                Mathf.Min(color.b + 0.08f, 1f),
                color.a);
        }

        Widgets.DrawBoxSolidWithOutline(rect, color, new Color(0.38f, 0.38f, 0.38f, 0.86f), 1);
        DrawCenteredLabel(HorizontallyPadded(rect, 6f), label);

        if (!string.IsNullOrEmpty(tooltip))
        {
            TooltipHandler.TipRegion(rect, tooltip);
        }

        return Widgets.ButtonInvisible(rect, doMouseoverSound: true);
    }

    private static void DrawCenteredLabel(Rect rect, string label)
    {
        var oldAnchor = Text.Anchor;
        var oldColor = GUI.color;
        var oldFont = Text.Font;

        Text.Font = GameFont.Small;
        var fittedLabel = FitLabelToWidth(label, rect.width);
        if (Text.CalcSize(fittedLabel).x > rect.width)
        {
            Text.Font = GameFont.Tiny;
            fittedLabel = FitLabelToWidth(label, rect.width);
        }

        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = Color.white;
        Widgets.Label(rect, fittedLabel);

        Text.Anchor = oldAnchor;
        GUI.color = oldColor;
        Text.Font = oldFont;
    }

    private static float GetProfileTabWidth(ResearchTabProfile profile)
    {
        var label = Tree.IsProfileBuilding(profile.Id) ? $"{profile.Name}..." : profile.Name ?? string.Empty;
        return Mathf.Clamp(Text.CalcSize(label).x + ProfileTabTextPadding, ProfileTabMinWidth, ProfileTabMaxWidth);
    }

    private static Rect HorizontallyPadded(Rect rect, float padding)
    {
        var appliedPadding = Mathf.Min(padding, rect.width / 2f);
        rect.xMin += appliedPadding;
        rect.xMax -= appliedPadding;
        return rect;
    }

    private static string FitLabelToWidth(string label, float width)
    {
        if (string.IsNullOrEmpty(label) || width <= 0f)
        {
            return string.Empty;
        }

        if (Text.CalcSize(label).x <= width)
        {
            return label;
        }

        const string suffix = "...";
        if (Text.CalcSize(suffix).x > width)
        {
            return string.Empty;
        }

        for (var length = Mathf.Min(label.Length, 64); length > 0; length--)
        {
            var candidate = label.Substring(0, length) + suffix;
            if (Text.CalcSize(candidate).x <= width)
            {
                return candidate;
            }
        }

        return suffix;
    }

    private static float CalculateTopBarButtonWidth()
    {
        float buttonWidth = TopBarMinButtonWidth;
        var oldFont = Text.Font;
        Text.Font = GameFont.Small;

        string toggleLabel = FluffyResearchTreeMod.instance.Settings.SkipCompleted
            ? "Fluffy.ResearchTree.invisible".Translate()
            : "Fluffy.ResearchTree.visible".Translate();
        buttonWidth = Mathf.Max(buttonWidth, Text.CalcSize(toggleLabel).x + TopBarLabelPadding);

        if (ModsConfig.AnomalyActive)
        {
            buttonWidth = Mathf.Max(buttonWidth,
                Text.CalcSize("Fluffy.ResearchTree.Anomaly".Translate()).x + TopBarLabelPadding);
        }

        Text.Font = oldFont;
        return buttonWidth;
    }

    public void CenterOn(Node node)
    {
        var scrollPosition = new Vector2((Constants.NodeSize.x + Constants.NodeMargins.x) * (node.X - 0.5f),
            (Constants.NodeSize.y + Constants.NodeMargins.y) * (node.Y - 0.5f));
        node.Highlighted = true;
        scrollPosition -= new Vector2(UI.screenWidth, UI.screenHeight) / 2f;
        scrollPosition.x = Mathf.Clamp(scrollPosition.x, 0f, TreeRect.width - ViewRect.width);
        scrollPosition.y = Mathf.Clamp(scrollPosition.y, 0f, TreeRect.height - ViewRect.height);
        _scrollPosition = scrollPosition;
    }

    public bool IsHighlighted(ResearchProjectDef research)
    {
        return IsQuickSearchWidgetActive() && _matchingProjects.Contains(research);
    }

    public bool IsQuickSearchWidgetActive()
    {
        return _quickSearchWidget.filter.Active;
    }

    public bool IsQuickSearchWidgetEmpty()
    {
        return string.IsNullOrEmpty(_quickSearchWidget.filter.Text);
    }

    private void updateSearchResults(Rect searchRect = default)
    {
        _quickSearchWidget.noResultsMatched = false;
        _matchingProjects.Clear();
        Find.WindowStack.FloatMenu?.Close(false);

        if (!IsQuickSearchWidgetActive() || !Tree.Initialized)
        {
            return;
        }

        var matchedNodes = new List<ResearchNode>();
        foreach (var node in Tree.Nodes.OfType<ResearchNode>())
        {
            var researchProject = node.Research;
            if (researchProject.IsHidden)
            {
                continue;
            }

            if (!_quickSearchWidget.filter.Matches(researchProject.LabelCap) && !matchesUnlockedDefs(researchProject))
            {
                continue;
            }

            _matchingProjects.Add(researchProject);
            matchedNodes.Add(node);
        }

        _quickSearchWidget.noResultsMatched = matchedNodes.Count == 0;

        var somethingHighlighted = true;
        var list = new List<FloatMenuOption>();
        foreach (var node in matchedNodes.OrderBy(n => n.Research.ResearchViewX))
        {
            list.Add(new FloatMenuOption(
                node.Label,
                delegate
                {
                    _quickSearchWidget.filter.Text = node.Label;
                    CenterOn(node);
                },
                MenuOptionPriority.Default,
                delegate
                {
                    somethingHighlighted = false;
                    _matchingProjects.Clear();
                    _matchingProjects.Add(node.Research);
                },
                playSelectionSound: false)
            );
            node.Highlighted = true;
            if (!somethingHighlighted)
            {
                continue;
            }

            //CenterOn(node);
            somethingHighlighted = false;
        }

        if (!_quickSearchWidget.CurrentlyFocused() || !list.Any())
        {
            return;
        }

        searchRect.x += QuickSearchWidget.IconSize;
        Find.WindowStack.Add(new FloatMenu_Fixed(searchRect, list));

        return;

        bool matchesUnlockedDefs(ResearchProjectDef proj)
        {
            return unlockedDefsGroupedByPrerequisites(proj)
                .SelectMany(groupedByPrerequisite => groupedByPrerequisite.Second)
                .Any(MatchesUnlockedDef);
        }
    }

    public bool MatchesUnlockedDef(Def unlocked)
    {
        return _quickSearchWidget.filter.Matches(unlocked.label);
    }

    private List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>> unlockedDefsGroupedByPrerequisites(
        ResearchProjectDef project)
    {
        _cachedUnlockedDefsGroupedByPrerequisites ??=
            new Dictionary<ResearchProjectDef, List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>>>();
        if (_cachedUnlockedDefsGroupedByPrerequisites.TryGetValue(project, out var pairList))
        {
            return pairList;
        }

        pairList = ResearchPrerequisitesUtility.UnlockedDefsGroupedByPrerequisites(project);
        _cachedUnlockedDefsGroupedByPrerequisites.Add(project, pairList);
        return pairList;
    }
}
