// MainTabWindow_ResearchTree.cs
// Copyright Karel Kroeze, 2020-2020

using System.Collections.Generic;
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

    private Vector2 _mousePosition = Vector2.zero;

    private Rect _viewRect;

    private Rect _viewRectInner;

    private float _zoomLevel = 1f;

    private bool _panning;                 // 是否已进入平移
    private Vector2 _dragStart;            // 按下时坐标
    private const float PanThreshold = 4f; // 启动平移的像素阈值（避免轻点触发）

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
        setRects();
    }

    public override void PreOpen()
    {
        base.PreOpen();

        // 关键：吸收窗口周围输入，防止事件下沉到地图/底层 UI
        absorbInputAroundWindow = true;
        closeOnClickedOutside = false;   // 避免 RimWorld 的“点外面就关”的默认行为
        preventCameraMotion = true;      // 避免地图摄像机因这个点击而响应

        setRects();
        Tree.WaitForInitialization();
        Assets.RefreshResearch = true;
        if (Tree.FirstLoadDone)
        {
            Tree.ResetNodeAvailabilityCache();
        }
        else
        {
            Tree.FirstLoadDone = true;
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

        Queue.RefreshQueue();
        _dragging = false;
        closeOnClickedOutside = false;

        _cachedUnlockedDefsGroupedByPrerequisites = null;
        _cachedVisibleResearchProjects = null;
        _quickSearchWidget.Reset();
        updateSearchResults();
    }

    public override void WindowOnGUI()
    {
        base.WindowOnGUI();
        Assets.DrawWindowBackground(windowRect, FluffyResearchTreeMod.instance.Settings.BackgroundColor);
    }

    private void setRects()
    {
        var startPosition = new Vector2(StandardMargin / Prefs.UIScale,
            Constants.TopBarHeight + Constants.Margin + (StandardMargin / Prefs.UIScale));
        var size = new Vector2((Screen.width - (StandardMargin * 2f)) / Prefs.UIScale,
            UI.screenHeight - MainButtonDef.ButtonHeight - startPosition.y);

        _baseViewRect = new Rect(startPosition, size);
        _baseViewRectInner = _baseViewRect.ContractedBy(Constants.Margin / Prefs.UIScale);
        windowRect.x = 0f;
        windowRect.y = 0f;
        windowRect.width = UI.screenWidth;
        windowRect.height = UI.screenHeight - MainButtonDef.ButtonHeight;
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
            Close();
            return;
        }

        // 先处理输入（同帧生效）
        handleZoom();
        handleDolly();
        handleDragging();
        ClampScroll();

        // 再应用缩放并绘制
        applyZoomLevel();
        _scrollPosition = GUI.BeginScrollView(ViewRect, _scrollPosition, TreeRect, true, true); 
        Tree.Draw(VisibleRect);
        Queue.DrawLabels(VisibleRect);
        GUI.EndScrollView(false);
        ResetZoomLevel();
        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
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

        if (e.type == EventType.MouseDown && e.button == 0 && inWindow)
        {
            _dragging = inView;           // 只在视口内作为“候选拖拽”
            _panning = false;
            _dragStart = _mousePosition = e.mousePosition;
            // 不 Use()，保留给节点/控件
            return;
        }

        if (e.type == EventType.MouseUp && e.button == 0)
        {
            _dragging = _panning = false;
            _dragStart = _mousePosition = Vector2.zero;
            return;
        }

        if (_dragging && e.type == EventType.MouseDrag && e.button == 0)
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
        GUI.BeginClip(new Rect(0f, UI.screenHeight - Constants.TopBarHeight,
            UI.screenWidth, UI.screenHeight - MainButtonDef.ButtonHeight - Constants.TopBarHeight));
    }
    private void drawTopBar(Rect canvas)
    {
        // 左侧固定 400 宽作为“搜索 + 按钮”区域
        var left = new Rect(canvas.x, canvas.y, 400f, canvas.height);
        // 右侧队列区域：从左栏右侧再加一个通用间距
        var right = canvas;
        right.xMin = left.xMax + Constants.Margin;

        DrawSearchBar(left.ContractedBy(Constants.Margin));
        Queue.DrawQueue(right.ContractedBy(Constants.Margin), !_dragging);
    }

    private void DrawSearchBar(Rect canvas)
    {
        // 统一高度/间距
        float h = Constants.QueueLabelSize;
        float gap = 6f;                     // 垂直/水平间距
        float pad = 24f;                    // 按钮文字左右内边距

        // 文本 & 尺寸测量
        bool isShow = FluffyResearchTreeMod.instance.Settings.SkipCompleted;
        string anomalyLabel = "Fluffy.ResearchTree.Anomaly".Translate();
        string toggleLabel = isShow ? "Fluffy.ResearchTree.invisible".Translate()
                                     : "Fluffy.ResearchTree.visible".Translate();

        var oldFont = Text.Font;
        Text.Font = GameFont.Small;
        Vector2 szAnomaly = Text.CalcSize(anomalyLabel);
        Vector2 szToggle = Text.CalcSize(toggleLabel);
        Text.Font = oldFont;

        float btnH = h;
        float btnW1 = Mathf.Max(120f, szAnomaly.x + pad);
        float btnW2 = Mathf.Max(200f, szToggle.x + pad);

        // —— 布局：搜索框固定在上，占整行；按钮在下并排 —— //
        // 第一行：搜索框（整行）
        var searchRect = new Rect(
            canvas.xMin,
            canvas.yMin,            // 顶部
            canvas.width,
            h
        );

        // 第二行：按钮区域
        float buttonsY = searchRect.yMax + gap;

        // 右对齐两按钮：[  ...  ][ Anomaly ][ Toggle ]
        var toggleBtnRect = new Rect(canvas.xMax - btnW2, buttonsY, btnW2, btnH);
        var anomalyBtnRect = new Rect(toggleBtnRect.x - gap - btnW1, buttonsY, btnW1, btnH);

        // 若画布过窄（极端情况下），自动堆叠按钮以避免重叠
        if (anomalyBtnRect.x < canvas.xMin)
        {
            // 竖直堆叠，右对齐
            anomalyBtnRect = new Rect(canvas.xMax - btnW1, buttonsY, btnW1, btnH);
            toggleBtnRect = new Rect(canvas.xMax - btnW2, anomalyBtnRect.yMax + gap, btnW2, btnH);
        }

        // ---- Anomaly 按钮 ----
        if (ModsConfig.AnomalyActive && Widgets.ButtonText(anomalyBtnRect, anomalyLabel))
        {
            ((MainTabWindow_Research)MainButtonDefOf.Research.TabWindow).CurTab = ResearchTabDefOf.Anomaly;
            Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Research);
            return;
        }

        // ---- 切换按钮（暗绿/暗红 + 白字）----
        var bg = isShow ? new Color(0.1f, 0.5f, 0.1f) : new Color(0.6f, 0.1f, 0.1f);
        Widgets.DrawBoxSolid(toggleBtnRect, bg);

        var oldAnchor = Text.Anchor; var oldColor = GUI.color;
        Text.Anchor = TextAnchor.MiddleCenter; GUI.color = Color.white;
        Widgets.Label(toggleBtnRect, toggleLabel);
        Text.Anchor = oldAnchor; GUI.color = oldColor;

        if (Widgets.ButtonInvisible(toggleBtnRect, doMouseoverSound: true))
        {
            FluffyResearchTreeMod.instance.Settings.SkipCompleted = !FluffyResearchTreeMod.instance.Settings.SkipCompleted;
            Tree.ResetNodeAvailabilityCache();
            Assets.RefreshResearch = true;
        }

        // ---- 搜索框 ----
        _quickSearchWidget.OnGUI(searchRect, () => updateSearchResults(canvas));
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

        if (!IsQuickSearchWidgetActive())
        {
            return;
        }

        foreach (var researchProject in VisibleResearchProjects.Where(researchProject => !researchProject.IsHidden &&
                     (_quickSearchWidget.filter.Matches(researchProject.LabelCap) ||
                      matchesUnlockedDefs(researchProject))))
        {
            _matchingProjects.Add(researchProject);
        }

        _quickSearchWidget.noResultsMatched = !_matchingProjects.Any();

        var somethingHighlighted = true;
        var list = new List<FloatMenuOption>();
        foreach (var node in Tree.Nodes.OfType<ResearchNode>()
                     .Where(n => _matchingProjects.Contains(n.Research))
                     .OrderBy(n => n.Research.ResearchViewX))
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