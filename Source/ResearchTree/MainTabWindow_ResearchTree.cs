// MainTabWindow_ResearchTree.cs
// Copyright Karel Kroeze, 2020-2020

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

    private Rect _baseViewRect;

    private Rect _baseViewRectInner;

    private Dictionary<ResearchProjectDef, List<Pair<ResearchPrerequisitesUtility.UnlockedHeader, List<Def>>>>
        _cachedUnlockedDefsGroupedByPrerequisites;

    private List<ResearchProjectDef> _cachedVisibleResearchProjects;

    private bool _dragging;

    private bool _logFirstDrawNextFrame;

    private Vector2 _mousePosition = Vector2.zero;

    private Rect _viewRect;

    private Rect _viewRectInner;

    private float _zoomLevel = 1f;

    private bool _panning;                 // 是否已进入平移
    private Vector2 _dragStart;            // 按下时坐标
    private const float PanThreshold = 4f; // 启动平移的像素阈值（避免轻点触发）
    private int _capturedMouseButton = -1; // 记录始于窗口内部的鼠标按钮
    private const float TopBarControlGap = 6f;
    private const float TopBarLabelPadding = 24f;
    private const float TopBarMinSearchWidth = 220f;
    private const float TopBarMinButtonWidth = 140f;
    private const long PreOpenWarnThresholdMs = 250;
    private const long FirstDrawWarnThresholdMs = 200;

    public bool ViewRectDirty = true;

    public bool ViewRectInnerDirty = true;

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
    }

    public override void PreOpen()
    {
        var sw = Stopwatch.StartNew();

        try
        {
            base.PreOpen();

            // 启用默认的吸收逻辑，确保地图不会在窗口上方被点击
            absorbInputAroundWindow = true;
            closeOnClickedOutside = false;   // 避免 RimWorld 的“点外面就关”的默认行为
            preventCameraMotion = true;      // 避免地图摄像机因这个点击而响应

            setRects();
            Tree.WaitForInitialization();
            Assets.RefreshResearch = true;
            _dragging = false;
            closeOnClickedOutside = false;
            _capturedMouseButton = -1;

            _cachedUnlockedDefsGroupedByPrerequisites = null;
            _cachedVisibleResearchProjects = null;
            _quickSearchWidget.Reset();
            updateSearchResults();

            _logFirstDrawNextFrame = true;

            ApplyTreeInitializedState();
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
    }

    private void setRects()
    {
        var uiScale = Prefs.UIScale;
        var marginScaled = StandardMargin / uiScale;
        var startPosition = new Vector2(marginScaled,
            Constants.TopBarHeight + Constants.Margin + marginScaled);

        var bottomPaddingView = marginScaled;
        var size = new Vector2((Screen.width - (StandardMargin * 2f)) / uiScale,
            Mathf.Max(0f, UI.screenHeight - MainButtonDef.ButtonHeight - bottomPaddingView - startPosition.y));

        _baseViewRect = new Rect(startPosition, size);
        _baseViewRectInner = _baseViewRect.ContractedBy(Constants.Margin / uiScale);
        windowRect.x = 0f;
        windowRect.y = 0f;
        windowRect.width = UI.screenWidth;
        var bottomPaddingWindow = Mathf.Max(0f, StandardMargin * uiScale);
        windowRect.height = Mathf.Max(0f,
            UI.screenHeight - MainButtonDef.ButtonHeight - bottomPaddingWindow);
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
        // 顶栏
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

        // 先处理输入（同帧生效）
        handleZoom();
        handleDolly();
        handleDragging();
        ClampScroll();

        // 再应用缩放并绘制
        applyZoomLevel();
        Stopwatch firstDrawTimer = null;
        if (_logFirstDrawNextFrame)
        {
            firstDrawTimer = Stopwatch.StartNew();
        }

        _scrollPosition = GUI.BeginScrollView(ViewRect, _scrollPosition, TreeRect, true, true);
        Tree.Draw(VisibleRect);
        Queue.DrawLabels(VisibleRect);
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
        var capturing = _capturedMouseButton != -1;

        if (!pointerOverWindow && !capturing)
        {
            return;
        }

        switch (e.type)
        {
            case EventType.MouseDown:
            case EventType.ScrollWheel:
            case EventType.ContextClick:
                e.Use();
                break;
            case EventType.MouseDrag:
                if (!capturing || (e.button != _capturedMouseButton && _capturedMouseButton != -1))
                {
                    return;
                }

                e.Use();
                break;
            case EventType.MouseUp:
                if (capturing && e.button == _capturedMouseButton)
                {
                    _capturedMouseButton = -1;
                }

                e.Use();
                break;
        }
    }

    private void DrawGenerationInProgressMessage(Rect canvas)
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

    private void DrawNoTabsSelectedMessage(Rect canvas)
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
        // 每帧一次，避免在 Layout/MouseMove 阶段重复加步长
        if (Event.current.type != EventType.Repaint) return;

        // 步长随帧时间与缩放变化：缩得越小（ZoomLevel大），单帧移动更多
        float step = 600f * Time.unscaledDeltaTime * Mathf.Max(1f, Instance.ZoomLevel);

        if (KeyBindingDefOf.MapDolly_Left.IsDown) _scrollPosition.x -= step;
        if (KeyBindingDefOf.MapDolly_Right.IsDown) _scrollPosition.x += step;
        if (KeyBindingDefOf.MapDolly_Up.IsDown) _scrollPosition.y -= step;
        if (KeyBindingDefOf.MapDolly_Down.IsDown) _scrollPosition.y += step;

        Instance.ClampScroll();
    }

    private void handleZoom()
    {
        if (!Tree.Initialized) return;
        if (!Event.current.isScrollWheel) return;

        if (Event.current.control == FluffyResearchTreeMod.instance.Settings.CtrlFunction)
        {
            _scrollPosition.y += Event.current.delta.y * 10f;
            ClampScroll();
            return;
        }

        var mousePosition = Event.current.mousePosition;
        var vector = (Event.current.mousePosition - _scrollPosition) / ZoomLevel;
        ZoomLevel += Event.current.delta.y * Constants.ZoomStep * ZoomLevel;
        _scrollPosition = mousePosition - (vector * ZoomLevel);
        ClampScroll();
        Event.current.Use();
    }

    private void handleDragging()
    {
        if (Queue._draggedNode != null) return;

        var e = Event.current;

        // 先确认鼠标在窗口内（不是只看 ViewRect）
        bool inWindow = Mouse.IsOver(this.windowRect);
        bool inView = inWindow && ViewRect.Contains(e.mousePosition);

        if (e.type == EventType.MouseDown && inWindow)
        {
            if (_capturedMouseButton == -1)
            {
                _capturedMouseButton = e.button;
            }

            _dragging = inView && e.button == 0; // 只有左键才触发平移
            _panning = false;
            _dragStart = _mousePosition = e.mousePosition;
            // 不 Use()，保留给节点/控件
            return;
        }

        if (e.type == EventType.MouseUp && e.button == _capturedMouseButton)
        {
            _dragging = _panning = false;
            _dragStart = _mousePosition = Vector2.zero;
            _capturedMouseButton = -1;
            return;
        }

        if (_dragging && e.type == EventType.MouseDrag && e.button == _capturedMouseButton)
        {
            if (!_panning)
            {
                if ((e.mousePosition - _dragStart).sqrMagnitude < PanThreshold * PanThreshold) return;
                _panning = true;
            }

            var cur = e.mousePosition;
            _scrollPosition += _mousePosition - cur;
            _mousePosition = cur;
            ClampScroll();
            e.Use(); // 只有真正平移时才吞事件
        }
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
        float rightColumnMinWidth = Mathf.Max(TopBarMinSearchWidth, buttonWidth);
        float innerRequiredWidth = buttonWidth + TopBarControlGap + rightColumnMinWidth;
        float leftWidth = Mathf.Min(canvas.width, innerRequiredWidth + (Constants.Margin * 2f));

        var left = new Rect(canvas.x, canvas.y, leftWidth, canvas.height);
        var right = canvas;
        right.xMin = Mathf.Min(canvas.xMax, left.xMax + Constants.Margin);

        DrawSearchBar(left.ContractedBy(Constants.Margin));
        Queue.DrawQueue(right.ContractedBy(Constants.Margin), !_dragging);
    }

    private void DrawSearchBar(Rect canvas)
    {
        bool skipCompleted = FluffyResearchTreeMod.instance.Settings.SkipCompleted;
        bool anomalyActive = ModsConfig.AnomalyActive;
        string anomalyLabel = "Fluffy.ResearchTree.Anomaly".Translate();
        string toggleLabel = skipCompleted ? "Fluffy.ResearchTree.invisible".Translate()
                                           : "Fluffy.ResearchTree.visible".Translate();
        string tabsLabel = "Fluffy.ResearchTree.filter".Translate();

        float buttonWidth = CalculateTopBarButtonWidth();
        const int rowCount = 2;
        float verticalGap = TopBarControlGap;
        float maxContentHeight = Mathf.Min(canvas.height, rowCount * Constants.QueueLabelSize + verticalGap);
        float rowHeight = Mathf.Max(0f, (maxContentHeight - verticalGap) / rowCount);
        float contentHeight = rowHeight * rowCount + verticalGap;
        float startY = canvas.yMin + Mathf.Max(0f, (canvas.height - contentHeight) / 2f);

        float leftColumnWidth = buttonWidth;
        float rightColumnX = canvas.xMin + leftColumnWidth + TopBarControlGap;
        float rightColumnWidth = Mathf.Max(0f, canvas.xMax - rightColumnX);

        var tabsRect = new Rect(canvas.xMin, startY, leftColumnWidth, rowHeight);
        var searchRect = new Rect(rightColumnX, startY, rightColumnWidth, rowHeight);

        float secondRowY = startY + rowHeight + verticalGap;

        Rect? anomalyRect = null;
        if (anomalyActive)
        {
            anomalyRect = new Rect(canvas.xMin, secondRowY, leftColumnWidth, rowHeight);
        }

        float toggleX = rightColumnX;
        float toggleWidth = Mathf.Max(0f, rightColumnWidth);
        var toggleRect = new Rect(toggleX, secondRowY, toggleWidth, rowHeight);

        if (Widgets.ButtonText(tabsRect, tabsLabel))
        {
            FluffyResearchTreeMod.instance.Settings.EnsureTabCache();
            Find.WindowStack.Add(new Dialog_SelectResearchTabs());
        }

        _quickSearchWidget.OnGUI(searchRect, () => updateSearchResults(canvas));

        if (anomalyRect.HasValue && Widgets.ButtonText(anomalyRect.Value, anomalyLabel))
        {
            ((MainTabWindow_Research)MainButtonDefOf.Research.TabWindow).CurTab = ResearchTabDefOf.Anomaly;
            Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Research);
            return;
        }

        var toggleColor = skipCompleted ? new Color(0.1f, 0.5f, 0.1f) : new Color(0.6f, 0.1f, 0.1f);
        Widgets.DrawBoxSolid(toggleRect, toggleColor);

        var oldAnchor = Text.Anchor;
        var oldColor = GUI.color;
        Text.Anchor = TextAnchor.MiddleCenter;
        GUI.color = Color.white;
        {
            var prevFont = Text.Font;
            Text.Font = GameFont.Small;
            var labelSize = Text.CalcSize(toggleLabel);
            if (labelSize.x > toggleRect.width - 8f)
            {
                Text.Font = GameFont.Tiny;
            }
            Widgets.Label(toggleRect, toggleLabel);
            Text.Font = prevFont;
        }
        Text.Anchor = oldAnchor;
        GUI.color = oldColor;

        if (Widgets.ButtonInvisible(toggleRect, doMouseoverSound: true))
        {
            FluffyResearchTreeMod.instance.Settings.SkipCompleted = !FluffyResearchTreeMod.instance.Settings.SkipCompleted;
            Tree.ResetNodeAvailabilityCache();
            Assets.RefreshResearch = true;
        }
    }

    private float CalculateTopBarButtonWidth()
    {
        float buttonWidth = TopBarMinButtonWidth;
        var oldFont = Text.Font;
        Text.Font = GameFont.Small;

        buttonWidth = Mathf.Max(buttonWidth, Text.CalcSize("Fluffy.ResearchTree.filter".Translate()).x + TopBarLabelPadding);

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