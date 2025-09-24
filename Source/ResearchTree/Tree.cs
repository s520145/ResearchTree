// Tree.cs
// Copyright Karel Kroeze, 2020-2020

using HarmonyLib;
using RimWorld;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public static class Tree
{
    public static bool Initialized;

    public static bool NoTabsSelected { get; private set; }

    private static bool _reopenResearchTabAfterInit = true;

    public static IntVec2 Size = IntVec2.Zero;

    private static List<Node> _nodes;

    private static List<Edge<Node, Node>> _edges;

    private static List<TechLevel> _relevantTechLevels;

    private static Dictionary<TechLevel, IntRange> _techLevelBounds;

    private static volatile bool _initializing;

    public static bool OrderDirty;

    public static bool FirstLoadDone;

    private static readonly Dictionary<ResearchProjectDef, Node> ResearchToNodesCache = [];

    private static bool _loggedInitialDraw;

    // --- caches for hot paths ---
    private static List<Node>[] LayerBuckets;                 // [0..L]
    private static List<Edge<Node, Node>>[] InEdgesPerLayer;  // [0..L]
    private static List<Edge<Node, Node>>[] OutEdgesPerLayer; // [0..L]

    // 每层的 Y 槽位数组：LayerSlots[x][y-1] => Node
    private static Node[][] LayerSlots;

    private static readonly List<Node> VisibleNodesBuffer = new(256);

    private static readonly List<Edge<Node, Node>>[] EdgeDrawBuckets =
    {
        new List<Edge<Node, Node>>(64),
        new List<Edge<Node, Node>>(64),
        new List<Edge<Node, Node>>(64),
        new List<Edge<Node, Node>>(64)
    };

    private const float CullPadding = 120f;
    private const string InitializePerformancePrefix = "Tree.Initialize::";

    private static Dictionary<TechLevel, IntRange> TechLevelBounds
    {
        get
        {
            if (_techLevelBounds != null)
            {
                return _techLevelBounds;
            }

            Logging.Error("TechLevelBounds called before they are set.");
            return null;
        }
    }

    public static List<TechLevel> RelevantTechLevels
    {
        get
        {
            _relevantTechLevels ??= (from TechLevel tl in Enum.GetValues(typeof(TechLevel))
                where DefDatabase<ResearchProjectDef>.AllDefsListForReading.Any(rp => rp.techLevel == tl)
                select tl).OrderBy(tl => tl).ToList();

            return _relevantTechLevels;
        }
    }

    // 尝试从 ResearchProjectDef 上取出 ResearchTabDef
    private static ResearchTabDef GetProjectTab(ResearchProjectDef def)
    {
        if (def == null) return null;
        var t = def.GetType();

        // 优先找属性
        var prop = t.GetProperty("tab") ?? t.GetProperty("researchTab") ?? t.GetProperty("researchTabDef");
        if (prop != null && typeof(ResearchTabDef).IsAssignableFrom(prop.PropertyType))
            return prop.GetValue(def) as ResearchTabDef;

        // 再找字段
        var fld = t.GetField("tab") ?? t.GetField("researchTab") ?? t.GetField("researchTabDef");
        if (fld != null && typeof(ResearchTabDef).IsAssignableFrom(fld.FieldType))
            return fld.GetValue(def) as ResearchTabDef;

        // 兜底（大多数情况下是 Main）
        try { return ResearchTabDefOf.Main; } catch { return null; }
    }

    // 运行期：是否隐藏已完成（读设置，实时生效）
    private static bool SkipCompletedSetting =>
        FluffyResearchTreeMod.instance?.Settings?.SkipCompleted ?? false;

    // 根据“隐藏已完成”判断某个节点是否应被跳过（仅在绘制阶段使用，不影响布局）
    private static bool NodeHiddenBySkipCompleted(Node node)
    {
        if (!SkipCompletedSetting) return false;
        if (node is ResearchNode rn && rn.Research != null)
        {
            // vanilla: ResearchProjectDef 有 IsFinished 属性；若个别分支无此属性，可退化用 ResearchManager 判定
            return rn.Research.IsFinished;
        }
        return false;
    }

    // 找到从某个端点出发，沿着 dummy 节点一路走到最近的真实研究节点。
    // backward=true 走 In 方向；false 走 Out 方向。
    private static ResearchNode FindRealResearchEndpoint(Node n, bool backward)
    {
        var cur = n;
        int guard = 256; // 防御循环
        while (cur != null && !(cur is ResearchNode) && guard-- > 0)
        {
            if (backward)
                cur = (cur.InEdges != null && cur.InEdges.Count > 0) ? cur.InEdges[0].In : null;
            else
                cur = (cur.OutEdges != null && cur.OutEdges.Count > 0) ? cur.OutEdges[0].Out : null;
        }
        return cur as ResearchNode;
    }

    // 只要“隐藏已完成”开启，且这条小边所在链的两端任一真实研究节点已完成，就隐藏整条链
    private static bool EdgeHiddenBySkipCompleted<TIn, TOut>(Edge<TIn, TOut> e)
        where TIn : Node where TOut : Node
    {
        if (!(FluffyResearchTreeMod.instance?.Settings?.SkipCompleted ?? false)) return false;

        var left = FindRealResearchEndpoint(e.In, backward: true);
        var right = FindRealResearchEndpoint(e.Out, backward: false);

        bool leftDone = left != null && left.Research != null && left.Research.IsFinished;
        bool rightDone = right != null && right.Research != null && right.Research.IsFinished;

        return leftDone || rightDone;
    }

    public static List<Node> Nodes
    {
        get
        {
            if (_nodes == null)
            {
                populateNodes();
            }

            return _nodes;
        }
    }

    private static List<Edge<Node, Node>> Edges
    {
        get
        {
            if (_edges != null)
            {
                return _edges;
            }

            Logging.Error("Trying to access edges before they are initialized.");
            return null;
        }
    }

    public static Node ResearchToNode(ResearchProjectDef research)
    {
        var node = ResearchToNodesCache.TryGetValue(research);
        if (node != null)
        {
            return node;
        }

        _ = Nodes.OfType<ResearchNode>();
        return ResearchToNodesCache.TryGetValue(research);
    }

    public static void Reset(bool alsoZoom)
    {
        Messages.Message("Fluffy.ResearchTree.ResolutionChange".Translate(), MessageTypeDefOf.NeutralEvent);

        Queue.Notify_TreeWillReset();

        NoTabsSelected = false;

        Size = IntVec2.Zero;
        _nodes = null;
        ResearchToNodesCache.Clear();
        _edges = null;
        _relevantTechLevels = null;
        _techLevelBounds = null;
        _initializing = false;
        Initialized = false;
        OrderDirty = false;
        FirstLoadDone = false;
        MainTabWindow_ResearchTree.InvalidateTreeRectCache();
        if (MainTabWindow_ResearchTree.Instance != null)
        {
            if (alsoZoom)
            {
                MainTabWindow_ResearchTree.Instance.ResetZoomLevel();
            }

            MainTabWindow_ResearchTree.Instance.ViewRectInnerDirty = true;
            MainTabWindow_ResearchTree.Instance.ViewRectDirty = true;
        }

        if (FluffyResearchTreeMod.instance.Settings.LoadType == Constants.LoadTypeLoadInBackground)
        {
            LongEventHandler.QueueLongEvent(Initialize, "ResearchPal.BuildingResearchTreeAsync", false,
                null);
        }
    }
    private static void ProfiledStep(string label, Action step)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            step();
        }
        finally
        {
            sw.Stop();
            Logging.Performance(label, sw.ElapsedMilliseconds, 250);
        }
    }

    private static void QueueProfiledLongEvent(Action step, string label, string textKey, bool doAsynchronously = false,
        Action<Exception> extraAction = null)
    {
        LongEventHandler.QueueLongEvent(() => ProfiledStep(label, step), textKey, doAsynchronously, extraAction);
    }

    public static void Initialize()
    {
        if (FluffyResearchTreeMod.instance?.Settings?.LoadType == Constants.LoadTypeDoNotGenerateResearchTree
            || Initialized || _initializing)
        {
            return;
        }

        _initializing = true;

        if (FluffyResearchTreeMod.instance?.Settings?.LoadType == Constants.LoadTypeLoadInBackground)
        {
            try
            {
                ProfiledStep($"{InitializePerformancePrefix}CheckPrerequisites", CheckPrerequisites);
                ProfiledStep($"{InitializePerformancePrefix}CreateEdges", createEdges);
                ProfiledStep($"{InitializePerformancePrefix}HorizontalPositions", horizontalPositions);
                ProfiledStep($"{InitializePerformancePrefix}NormalizeEdges", normalizeEdges);
                ProfiledStep($"{InitializePerformancePrefix}BuildBuckets", BuildBuckets);
                ProfiledStep($"{InitializePerformancePrefix}Collapse", collapse);
                ProfiledStep($"{InitializePerformancePrefix}MinimizeCrossings", minimizeCrossings);
                ProfiledStep($"{InitializePerformancePrefix}MinimizeEdgeLength", minimizeEdgeLength);
                ProfiledStep($"{InitializePerformancePrefix}RemoveEmptyRows", removeEmptyRows);

                Logging.Message("Done");
                Initialized = true;
            }
            catch (Exception ex)
            {
                Logging.Error("Error initializing research tree, will retry." + ex, true);
            }

            _initializing = false;
            _reopenResearchTabAfterInit = true;
            return;
        }

        QueueProfiledLongEvent(CheckPrerequisites, $"{InitializePerformancePrefix}CheckPrerequisites",
            "Fluffy.ResearchTree.PreparingTree.Setup");
        QueueProfiledLongEvent(createEdges, $"{InitializePerformancePrefix}CreateEdges",
            "Fluffy.ResearchTree.PreparingTree.Setup");
        QueueProfiledLongEvent(horizontalPositions, $"{InitializePerformancePrefix}HorizontalPositions",
            "Fluffy.ResearchTree.PreparingTree.Setup");
        QueueProfiledLongEvent(normalizeEdges, $"{InitializePerformancePrefix}NormalizeEdges",
            "Fluffy.ResearchTree.PreparingTree.Setup");
        // 新增：构建桶与边缓存
        QueueProfiledLongEvent(BuildBuckets, $"{InitializePerformancePrefix}BuildBuckets",
            "Fluffy.ResearchTree.PreparingTree.Setup");
        QueueProfiledLongEvent(collapse, $"{InitializePerformancePrefix}Collapse",
            "Fluffy.ResearchTree.PreparingTree.CrossingReduction");
        QueueProfiledLongEvent(minimizeCrossings, $"{InitializePerformancePrefix}MinimizeCrossings",
            "Fluffy.ResearchTree.PreparingTree.CrossingReduction");
        QueueProfiledLongEvent(minimizeEdgeLength, $"{InitializePerformancePrefix}MinimizeEdgeLength",
            "Fluffy.ResearchTree.PreparingTree.LayoutNew");
        QueueProfiledLongEvent(removeEmptyRows, $"{InitializePerformancePrefix}RemoveEmptyRows",
            "Fluffy.ResearchTree.PreparingTree.LayoutNew");
        QueueProfiledLongEvent(() =>
            {
                Initialized = true;
                _initializing = false;
                Logging.Message("Done");
            }, $"{InitializePerformancePrefix}Finalize", "Fluffy.ResearchTree.PreparingTree.LayoutNew");
        QueueProfiledLongEvent(Queue.Notify_TreeReinitialized,
            $"{InitializePerformancePrefix}NotifyQueueReinitialized", "Fluffy.ResearchTree.RestoreQueue");
        QueueProfiledLongEvent(MainTabWindow_ResearchTree.Instance.Notify_TreeInitialized,
            $"{InitializePerformancePrefix}NotifyTreeInitialized", "Fluffy.ResearchTree.RestoreQueue");
        if (_reopenResearchTabAfterInit)
        {
            // open tab
            QueueProfiledLongEvent(() => { Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Research); },
                $"{InitializePerformancePrefix}ToggleResearchTab", "Fluffy.ResearchTree.RestoreQueue");
        }
        _reopenResearchTabAfterInit = true;
    }

    // 构建分层桶与分层边缓存。只依赖 Node.X / Node.InEdges / Node.OutEdges（Y 会在后续不断变化不要紧）
    private static void BuildBuckets()
    {
        // 以 Nodes 当前的最大层号为准（更稳妥，不依赖 Size.x 何时被设置）
        int L = 0;
        if (!Nodes.NullOrEmpty()) L = Nodes.Max(n => n.X);

        LayerBuckets = new List<Node>[L + 1];
        InEdgesPerLayer = new List<Edge<Node, Node>>[L + 1];
        OutEdgesPerLayer = new List<Edge<Node, Node>>[L + 1];

        for (int x = 0; x <= L; x++)
        {
            LayerBuckets[x] = new List<Node>(64);
            InEdgesPerLayer[x] = new List<Edge<Node, Node>>(64);
            OutEdgesPerLayer[x] = new List<Edge<Node, Node>>(64);
        }

        // 填充层桶
        foreach (var n in Nodes)
        {
            if (n.X >= 0 && n.X <= L) LayerBuckets[n.X].Add(n);
        }

        // 预收集每层的 in/out 边（集合成员固定，Y 后续可变）
        for (int x = 0; x <= L; x++)
        {
            var bucket = LayerBuckets[x];
            if (bucket.Count == 0) continue;

            foreach (var node in bucket)
            {
                if (!node.InEdges.NullOrEmpty()) InEdgesPerLayer[x].AddRange(node.InEdges);
                if (!node.OutEdges.NullOrEmpty()) OutEdgesPerLayer[x].AddRange(node.OutEdges);
            }
        }

        // 规范每层的 Y 顺序并建立槽位表
        LayerSlots = new Node[LayerBuckets.Length][];
        for (int x = 0; x < LayerBuckets.Length; x++)
        {
            var bucket = LayerBuckets[x];
            if (bucket == null || bucket.Count == 0)
            {
                LayerSlots[x] = Array.Empty<Node>();
                continue;
            }

            bucket.Sort((a, b) => a.Y.CompareTo(b.Y));
            for (int i = 0; i < bucket.Count; i++) bucket[i].Y = i + 1;

            LayerSlots[x] = bucket.ToArray(); // 与 LayerBuckets 保持同序
        }
    }

    private static void removeEmptyRows()
    {
        // 1) 统计最大行号（若 Size.z 不可靠，用 Nodes.Max 兜底）
        int maxY = Size.z > 0
            ? Size.z
            : (Nodes.Count == 0 ? 0 : Nodes.Max(n => n.Y));

        if (maxY <= 0 || Nodes.Count == 0)
        {
            Size = new IntVec2(Size.x, 0);
            return;
        }

        // 2) 标记占用行
        var used = new bool[maxY + 1];
        foreach (var n in Nodes)
        {
            int y = n.Y;
            if ((uint)y <= (uint)maxY) used[y] = true;
        }

        // 3) 构建 oldY -> newY 压缩映射
        var map = new int[maxY + 1];
        int next = 0;
        for (int y = 1; y <= maxY; y++)
        {
            if (used[y]) { next++; map[y] = next; }
        }

        // 4) 一次性重写所有节点 Y
        foreach (var n in Nodes)
        {
            int y = n.Y;
            if ((uint)y <= (uint)maxY && used[y]) n.Y = map[y];
        }

        // 5) 更新 Size.z
        Size = new IntVec2(Size.x, next);

        // 6) 若已建立分层缓存，则按新 Y 重新排序并同步
        if (LayerBuckets != null)
        {
            for (int x = 0; x < LayerBuckets.Length; x++)
            {
                var bucket = LayerBuckets[x];
                if (bucket == null || bucket.Count == 0) continue;
                bucket.Sort((a, b) => a.Y.CompareTo(b.Y));
                for (int i = 0; i < bucket.Count; i++) bucket[i].Y = i + 1;
            }
        }
        if (LayerSlots != null && LayerBuckets != null)
        {
            LayerSlots = new Node[LayerBuckets.Length][];
            for (int x = 0; x < LayerBuckets.Length; x++)
                LayerSlots[x] = (LayerBuckets[x] == null) ? Array.Empty<Node>() : LayerBuckets[x].ToArray();
        }
    }

    // 将某一层的顺序一次性替换为 newOrder，保持所有缓存一致
    private static void ApplyLayerOrder(int l, Node[] newOrder)
    {
        // 1) 更新 LayerSlots
        if (LayerSlots == null || (uint)l >= (uint)LayerSlots.Length)
            return;

        LayerSlots[l] = newOrder;

        // 2) 更新 LayerBuckets
        if (LayerBuckets != null && (uint)l < (uint)LayerBuckets.Length)
        {
            var bucket = LayerBuckets[l];
            bucket.Clear();
            bucket.AddRange(newOrder);
        }

        // 3) 规范化 Y = 1..Count
        for (int i = 0; i < newOrder.Length; i++)
            newOrder[i].Y = i + 1;
    }

    // 计算层 l 上每个节点的重心key（@in=true: 用 InEdges.In.Y；否则用 OutEdges.Out.Y）
    // 返回和 LayerSlots[l] 等长的 key 数组
    private static float[] ComputeBarycenterKeys(int l, bool @in)
    {
        var slots = LayerSlots;
        var arr = (slots != null && (uint)l < (uint)slots.Length) ? slots[l] : null;
        if (arr == null || arr.Length == 0) return Array.Empty<float>();

        var keys = new float[arr.Length];

        if (@in)
        {
            for (int i = 0; i < arr.Length; i++)
            {
                var n = arr[i];
                var eList = n.InEdges;
                if (eList == null || eList.Count == 0)
                {
                    keys[i] = n.Y; // 无邻居，保持原地
                    continue;
                }

                int sum = 0;
                for (int k = 0; k < eList.Count; k++) sum += eList[k].In.Y;
                keys[i] = (float)sum / eList.Count;
            }
        }
        else
        {
            for (int i = 0; i < arr.Length; i++)
            {
                var n = arr[i];
                var eList = n.OutEdges;
                if (eList == null || eList.Count == 0)
                {
                    keys[i] = n.Y;
                    continue;
                }

                int sum = 0;
                for (int k = 0; k < eList.Count; k++) sum += eList[k].Out.Y;
                keys[i] = (float)sum / eList.Count;
            }
        }

        return keys;
    }

    private static long TotalEdgeLength_Int(bool @in)
    {
        if (LayerSlots == null || LayerSlots.Length == 0) return 0;
        long sum = 0;
        for (int l = 0; l < LayerSlots.Length; l++)
        {
            // EdgeLength(l,@in) 返回 float（由 |int-int| 累加而来），这里用四舍五入转 long
            sum += (long)Math.Round(EdgeLength(l, @in));
        }
        return sum;
    }


    private static void minimizeEdgeLength()
    {
        // ====== 可调参数======
        const int MAX_PAIR_ITERS = 12;   // 最多做多少“对”（= 2*此值 次 Local）
        const int MIN_PAIR_ITERS = 2;    // 至少做多少“对”，避免过早停
        const double PAIR_REL_EPS = 0.04; // 每对相对收益阈值（4%）；更快停可设 0.06~0.08
        const int PAIR_ABS_EPS = 600;  // 每对绝对收益阈值（单位：Y 差总和）
        const double TARGET_CUM_REL = 0.65; // 累计相对收益达到 65% 即停；更快停可设 0.5~0.6

        // “收益稳定”（平台期）检测：最近 N 对的相对收益波动很小则停
        const int PLATEAU_SPAN = 3;    // 检测窗口（对数）
        const double PLATEAU_DELTA = 0.05; // 波动阈值（5% 相对波动）

        // ====== 运行时统计 ======
        var sw = new Stopwatch();
        long localMs = 0;
        double cumRel = 0.0;               // 累计相对收益（相对于每对开始前的 denom 累加）
        var lastPairRels = new Queue<double>(PLATEAU_SPAN);

        for (int pair = 0; pair < MAX_PAIR_ITERS; pair++)
        {
            // 以当前状态计算“对”的分母：in + out 的总边长（避免某一侧权重过低）
            long denomIn = TotalEdgeLength_Int(@in: true);
            long denomOut = TotalEdgeLength_Int(@in: false);
            long denom = Math.Max(1, denomIn + denomOut); // 防御

            // 执行一“对”：先 in，再 out（与你现有 iter 约定保持一致：偶数 in，奇数 out）
            int gainIn, gainOut;

            sw.Restart();
            bool impIn = EdgeLengthSweep_Local(2 * pair, out gainIn);   // in
            bool impOut = EdgeLengthSweep_Local(2 * pair + 1, out gainOut);  // out
            sw.Stop();

            localMs += sw.ElapsedMilliseconds;

            int pairGain = gainIn + gainOut;           // 这“一对”的真实收益
            double pairRel = (double)pairGain / denom;   // 这“一对”的相对收益

            Logging.Message($"[Profile] EdgeLengthSweep_Local pair={pair} took {sw.ElapsedMilliseconds} ms, " +
                            $"gainIn={gainIn}, gainOut={gainOut}, pairGain={pairGain}, pairRel={pairRel:P2}");

            // 累计相对收益
            cumRel += pairRel;

            // ---- 是否满足早停条件 ----
            bool stopByPairRel = pair >= MIN_PAIR_ITERS && pairRel < PAIR_REL_EPS;
            bool stopByPairAbs = pair >= MIN_PAIR_ITERS && pairGain < PAIR_ABS_EPS;
            bool stopByTarget = cumRel >= TARGET_CUM_REL;

            // 平台期检测：最近 N 对的相对收益波动是否很小
            bool stopByPlateau = false;
            if (PLATEAU_SPAN > 1)
            {
                if (lastPairRels.Count == PLATEAU_SPAN) lastPairRels.Dequeue();
                lastPairRels.Enqueue(pairRel);

                if (lastPairRels.Count == PLATEAU_SPAN)
                {
                    double min = double.MaxValue, max = double.MinValue;
                    foreach (var r in lastPairRels) { if (r < min) min = r; if (r > max) max = r; }
                    // 以窗口内均值为基准的相对波动
                    double mean = 0.0; foreach (var r in lastPairRels) mean += r; mean /= PLATEAU_SPAN;
                    if (mean > 0 && (max - min) / mean < PLATEAU_DELTA && pair >= MIN_PAIR_ITERS)
                        stopByPlateau = true;
                }
            }

            if (stopByPairRel || stopByPairAbs || stopByTarget || stopByPlateau)
            {
                Logging.Message(
                    $"[Profile] Local early-stop at pair={pair} " +
                    $"(pairRel={pairRel:P2}, pairGain={pairGain}, cumRel={cumRel:P2}, " +
                    $"by={(stopByPairRel ? "PAIR_REL" : stopByPairAbs ? "PAIR_ABS" : stopByTarget ? "TARGET_CUM" : "PLATEAU")})");
                break;
            }
        }

        Logging.Message($"[Profile] EdgeLengthSweep_Local(pair-mode) total {localMs} ms");

        var swg = Stopwatch.StartNew();
        EdgeLengthSweep_Global();
        swg.Stop();
        Logging.Message($"[Profile] EdgeLengthSweep_Global total {swg.ElapsedMilliseconds} ms");
    }


    private static void EdgeLengthSweep_Global()
    {
        if (LayerSlots == null || LayerSlots.Length == 0) return;

        //左右各 2 轮（可调小/大）
        const int ROUNDS = 2;

        for (int round = 0; round < ROUNDS; round++)
        {
            // 左->右：用 InEdges（上一层对当前层）
            for (int l = 0; l < LayerSlots.Length; l++)
                EdgeLengthSweep_Global_Layer(l, @in: true);

            // 右->左：用 OutEdges（当前层对下一层）
            for (int l = LayerSlots.Length - 1; l >= 0; l--)
                EdgeLengthSweep_Global_Layer(l, @in: false);
        }
    }


    private static bool EdgeLengthSweep_Local(int iter, out int totalGain)
    {
        bool useIn = (iter & 1) == 0;

        totalGain = 0;
        if (LayerSlots == null || LayerSlots.Length == 0)
            return false;

        bool improvedAny = false;
        for (int l = 0; l < LayerSlots.Length; l++)
        {
            var arr = LayerSlots[l];
            if (arr == null || arr.Length < 2) continue;

            int layerGain;
            if (EdgeLengthSweep_Local_Layer(l, useIn, out layerGain))
            {
                improvedAny = true;
                totalGain += layerGain;   // 真实收益累加
            }
        }
        return improvedAny;
    }


    // 用重心排序为层 l 重排；@in=true 表示使用 (l-1)->l 的邻居计算key，反之用 l->(l+1)
    private static void EdgeLengthSweep_Global_Layer(int l, bool @in)
    {
        var slots = LayerSlots;
        if (slots == null || (uint)l >= (uint)slots.Length) return;
        var arr = slots[l];
        if (arr == null || arr.Length < 2) return;

        // 1) 计算 key
        var keys = ComputeBarycenterKeys(l, @in);

        // 2) 拷贝当前顺序作为基线与候选
        var baseOrder = (Node[])arr.Clone();
        var candOrder = (Node[])arr.Clone();

        // 3) 稳定排序（key 小的在前；key 相等按原顺序保证稳定）
        //    为了稳定，我们比较 key，再比较原索引
        Array.Sort(candOrder, (a, b) =>
        {
            // 取原索引（在 baseOrder 中的位置）
            int ia = Array.IndexOf(baseOrder, a);
            int ib = Array.IndexOf(baseOrder, b);

            float ka = keys[ia];
            float kb = keys[ib];

            int c = ka.CompareTo(kb);
            if (c != 0) return c;
            return ia.CompareTo(ib);
        });

        // 4)不让交叉数变差（可以关掉这段以更激进地收短边）
        int beforeCross = Crossings(l);
        ApplyLayerOrder(l, candOrder);
        int afterCross = Crossings(l);
        if (afterCross > beforeCross)
        {
            // 回滚
            ApplyLayerOrder(l, baseOrder);
        }
    }

    // 仅针对相邻交换(A,Y=a)与(B,Y=b)计算边长变化：
    // @in=true  使用 A.InEdges/B.InEdges 与其来源节点 Y；
    // @in=false 使用 A.OutEdges/B.OutEdges 与其目标节点 Y。
    // 返回 after - before（负数代表更好）
    private static int DeltaEdgeLengthForAdjacentSwap(Node A, Node B, bool @in)
    {
        int aY = A.Y;
        int bY = B.Y;

        int before = 0, after = 0;

        if (@in)
        {
            var eA = A.InEdges;
            if (eA != null)
            {
                for (int k = 0; k < eA.Count; k++)
                {
                    int y = eA[k].In.Y;
                    before += Math.Abs(y - aY);
                    after += Math.Abs(y - bY);   // A 换到 bY
                }
            }

            var eB = B.InEdges;
            if (eB != null)
            {
                for (int k = 0; k < eB.Count; k++)
                {
                    int y = eB[k].In.Y;
                    before += Math.Abs(y - bY);
                    after += Math.Abs(y - aY);   // B 换到 aY
                }
            }
        }
        else
        {
            var eA = A.OutEdges;
            if (eA != null)
            {
                for (int k = 0; k < eA.Count; k++)
                {
                    int y = eA[k].Out.Y;
                    before += Math.Abs(y - aY);
                    after += Math.Abs(y - bY);
                }
            }

            var eB = B.OutEdges;
            if (eB != null)
            {
                for (int k = 0; k < eB.Count; k++)
                {
                    int y = eB[k].Out.Y;
                    before += Math.Abs(y - bY);
                    after += Math.Abs(y - aY);
                }
            }
        }

        return after - before;
    }

    private static bool EdgeLengthSweep_Local_Layer(int l, bool @in, out int layerGain)
    {
        layerGain = 0;

        var slots = LayerSlots;
        if (slots == null || (uint)l >= (uint)slots.Length) return false;
        var arr = slots[l];
        if (arr == null || arr.Length < 2) return false;

        bool improvedLayer = false;
        const int PASSES = 1; // 如需更强可设为 2

        for (int pass = 0; pass < PASSES; pass++)
        {
            for (int i = 0; i < arr.Length - 1; i++)
            {
                var A = arr[i];
                var B = arr[i + 1];

                int delta = DeltaEdgeLengthForAdjacentSwap(A, B, @in); // after - before
                if (delta < 0) // 交换后更短
                {
                    trySwap(A, B);              // 同步 LayerSlots/LayerBuckets 和 A.Y/B.Y
                    improvedLayer = true;
                    layerGain += -delta;     // 真实收益累加（before - after）

                    if (i > 0) i--;             // 冒泡推进
                }
            }
        }
        return improvedLayer;
    }


    private static void horizontalPositions()
    {
        var relevantTechLevels = RelevantTechLevels;
        var num = 1;
        const int num2 = 50;
        bool setDepth;
        do
        {
            var depth = 1;
            setDepth = false;
            foreach (var techlevel2 in relevantTechLevels)
            {
                var enumerable = from n in Nodes.OfType<ResearchNode>()
                    where n.Research.techLevel == techlevel2
                    select n;
                if (!enumerable.Any())
                {
                    continue;
                }

                foreach (var item in enumerable)
                {
                    setDepth = item.SetDepth(depth) || setDepth;
                }

                depth = enumerable.Max(n => n.X) + 1;
            }
        } while (setDepth && num++ < num2);

        _techLevelBounds = new Dictionary<TechLevel, IntRange>();
        foreach (var techlevel in relevantTechLevels)
        {
            var source = from n in Nodes.OfType<ResearchNode>()
                where n.Research.techLevel == techlevel
                select n;
            if (!source.Any())
            {
                continue;
            }

            _techLevelBounds[techlevel] = new IntRange(source.Min(n => n.X) - 1, source.Max(n => n.X));
        }
    }

    private static void normalizeEdges()
    {
        foreach (var item3 in new List<Edge<Node, Node>>(Edges.Where(e => e.Span > 1)))
        {
            Edges.Remove(item3);
            item3.In.OutEdges.Remove(item3);
            item3.Out.InEdges.Remove(item3);
            var node = item3.In;
            var num = (item3.Out.Yf - item3.In.Yf) / item3.Span;
            for (var i = item3.In.X + 1; i < item3.Out.X; i++)
            {
                var dummyNode = new DummyNode
                {
                    X = i,
                    Yf = item3.In.Yf + (num * (i - item3.In.X))
                };
                var item = new Edge<Node, Node>(node, dummyNode);
                node.OutEdges.Add(item);
                dummyNode.InEdges.Add(item);
                _nodes.Add(dummyNode);
                Edges.Add(item);
                node = dummyNode;
            }

            var item2 = new Edge<Node, Node>(node, item3.Out);
            node.OutEdges.Add(item2);
            item3.Out.InEdges.Add(item2);
            Edges.Add(item2);
        }
    }

    private static void createEdges()
    {
        if (_edges.NullOrEmpty())
        {
            _edges = [];
        }

        foreach (var item2 in Nodes.OfType<ResearchNode>())
        {
            if (item2.Research.prerequisites.NullOrEmpty())
            {
                continue;
            }

            foreach (var prerequisite in item2.Research.prerequisites)
            {
                ResearchNode researchNode = prerequisite;
                if (researchNode == null)
                {
                    continue;
                }

                var item = new Edge<Node, Node>(researchNode, item2);
                Edges.Add(item);
                item2.InEdges.Add(item);
                researchNode.OutEdges.Add(item);
            }
        }
    }

    private static void CheckPrerequisites()
    {
        var keepIterating = true;
        var iterator = 10;
        while (keepIterating)
        {
            if (checkPrerequisites())
            {
                keepIterating = false;
            }

            iterator--;
            if (iterator > 0)
            {
                continue;
            }

            Logging.Warning("Tried fixing research prerequisite issues for 10 iterations, aborting.");
            keepIterating = false;
        }
    }

    private static bool checkPrerequisites()
    {
        var queue = new Queue<ResearchNode>(Nodes.OfType<ResearchNode>());
        var returnValue = true;
        while (queue.Count > 0)
        {
            var researchNode = queue.Dequeue();
            if (researchNode.Research.prerequisites.NullOrEmpty())
            {
                continue;
            }

            var enumerable =
                researchNode.Research.prerequisites.SelectMany(r => r.Ancestors()).ToList().Intersect(researchNode
                    .Research.prerequisites);
            if (!enumerable.Any())
            {
                continue;
            }

            Logging.Warning(
                $"\tredundant prerequisites for {researchNode.Research.LabelCap}: {string.Join(", \n", enumerable.Select(r => r.LabelCap).ToArray())}. Removing.");
            foreach (var item in enumerable)
            {
                researchNode.Research.prerequisites.Remove(item);
            }

            returnValue = false;
        }

        queue = new Queue<ResearchNode>(Nodes.OfType<ResearchNode>());
        while (queue.Count > 0)
        {
            var node = queue.Dequeue();
            if (node.Research.prerequisites.NullOrEmpty() ||
                !node.Research.prerequisites.Any(r => (int)r.techLevel > (int)node.Research.techLevel))
            {
                continue;
            }

            Logging.Warning(
                $"\t{node.Research.defName} has a lower techlevel than (one of) it's prerequisites, increasing.");
            node.Research.techLevel = node.Research.prerequisites.Max(r => r.techLevel);
            returnValue = false;
            foreach (var researchNode in node.Children)
            {
                if (queue.Contains(researchNode))
                {
                    continue;
                }

                Logging.Warning(
                    $"Re-evaluating {researchNode.Research.defName} since one of its parents has changed tech-level.");
                queue.Enqueue(researchNode);
            }

            foreach (var researchNode in node.Parents)
            {
                if (queue.Contains(researchNode))
                {
                    continue;
                }

                Logging.Warning(
                    $"Re-evaluating {researchNode.Research.defName} since one of its children has changed tech-level.");
                queue.Enqueue(researchNode);
            }
        }

        return returnValue;
    }


    private static ResearchTabDef TryGetProjectTab(ResearchProjectDef def)
    {
        // 兼容不同字段名：tab / researchTab
        var f = AccessTools.Field(typeof(ResearchProjectDef), "tab")
                ?? AccessTools.Field(typeof(ResearchProjectDef), "researchTab");
        return f?.GetValue(def) as ResearchTabDef;
    }

    private static void populateNodes()
    {
        NoTabsSelected = false;

        // Filter Anomaly DLC research（你原有前两段保持）
        var allDefsListForReading =
            DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(def => def.knowledgeCategory == null).ToArray();
        var hidden = allDefsListForReading.Where(p => p.prerequisites?.Contains(p) ?? false);
        var second = allDefsListForReading.Where(p => p.Ancestors().Intersect(hidden).Any());
        var baseResearchList = allDefsListForReading.Except(hidden).Except(second).ToList();
        var researchList = baseResearchList;

        // ===== 按来源（ResearchTabDef）过滤 =====
        var st = FluffyResearchTreeMod.instance?.Settings;
        if (st != null)
        {
            st.EnsureTabCache(); // 用于处理中途增/减 mod 的 tab 集合  :contentReference[oaicite:2]{index=2}

            var hasSelection = st.IncludedTabs != null && st.IncludedTabs.Count > 0;
            var hasKnownTabs = st.AllTabsCache != null && st.AllTabsCache.Count > 0;

            if (hasKnownTabs && !hasSelection)
            {
                NoTabsSelected = true;
                researchList = [];
            }
            else if (hasSelection)
            {
                NoTabsSelected = false;

                var validDefs = new HashSet<ResearchProjectDef>(baseResearchList);
                var includedDefs = new HashSet<ResearchProjectDef>();

                foreach (var def in baseResearchList)
                {
                    var tab = TryGetProjectTab(def);
                    // 若取不到 Tab，保守地不做项目级过滤（避免误杀）
                    if (tab == null || st.TabIncluded(tab))
                    {
                        includedDefs.Add(def);
                    }
                }

                if (includedDefs.Count > 0)
                {
                    var queue = new Queue<ResearchProjectDef>(includedDefs);

                    void enqueuePrerequisites(IEnumerable<ResearchProjectDef> prereqs)
                    {
                        if (prereqs == null)
                        {
                            return;
                        }

                        foreach (var pre in prereqs)
                        {
                            if (!validDefs.Contains(pre))
                            {
                                continue;
                            }

                            if (includedDefs.Add(pre))
                            {
                                queue.Enqueue(pre);
                            }
                        }
                    }

                    while (queue.Count > 0)
                    {
                        var def = queue.Dequeue();
                        enqueuePrerequisites(def.prerequisites);
                        enqueuePrerequisites(def.hiddenPrerequisites);
                    }
                }

                researchList = baseResearchList.Where(includedDefs.Contains).ToList();
            }
            else
            {
                NoTabsSelected = false;
            }
        }

        // —— 并行创建节点 —— //
        _nodes = [];
        Assets.TotalAmountOfResearch = researchList.Count;

        Parallel.ForEach(researchList, (def, _, index) =>
        {
            var researchNode = new ResearchNode(def, (int)index);
            lock (_nodes)
            {
                _nodes.Add(researchNode);
                ResearchToNodesCache[def] = researchNode;
            }
        });
    }

    // Tree.cs 里（Tree 类内）新增：
    public static void RequestRebuild(bool resetZoom = true, bool reopenResearchTab = false)
    {
        // 1) 关闭刷新标志（避免重复重建），并重置所有缓存&尺寸
        Assets.RefreshResearch = false;

        _reopenResearchTabAfterInit = reopenResearchTab;

        // Reset() 已负责：Size/_nodes/_edges/缓存清空、窗口脏标记，
        // 且在“后台模式”会自动 QueueLongEvent(Initialize, ...)，非后台不会。
        Reset(alsoZoom: resetZoom);

        // 2) 非后台模式：立即初始化；后台模式：Reset 已经排队了 Initialize
        var st = FluffyResearchTreeMod.instance?.Settings;
        if (st == null || st.LoadType != Constants.LoadTypeLoadInBackground)
        {
            Initialize(); // 立刻按现有管线重建
                          // 非后台分支里，Initialize 自己会 Queue Notify_TreeInitialized（你现有实现已包含）
        }
        else
        {
            LongEventHandler.QueueLongEvent(
                Queue.Notify_TreeReinitialized,
                "Fluffy.ResearchTree.RestoreQueue",
                false, null
            );
            // 后台模式：Reset() 只排了 Initialize，这里再排一个“初始化完成后的通知”，保证窗口收到
            LongEventHandler.QueueLongEvent(
                MainTabWindow_ResearchTree.Instance.Notify_TreeInitialized,
                "Fluffy.ResearchTree.RestoreQueue",
                false, null
            );
        }

        // 3) 可选：重开研究面板（比如你是在对话框里点“重新生成”后希望直接看到新树）
        if (reopenResearchTab)
        {
            // 如果当前没打开，打开研究页签；已经打开不会打断
            Find.MainTabsRoot.ToggleTab(MainButtonDefOf.Research);
        }
    }

    private static void collapse()
    {
        _ = Size;
        for (var i = 1; i <= Size.x; i++)
        {
            var list = layer(i, true);
            var num = 1;
            foreach (var item in list)
            {
                item.Y = num++;
            }
        }
    }

    public static void ResetNodeAvailabilityCache()
    {
        foreach (var node in Nodes)
        {
            if (node is ResearchNode researchNode)
            {
                researchNode.ClearInstanceCaches();
            }
        }
    }

    public static void Draw(Rect visibleRect)
    {
        Stopwatch drawTimer = null;
        if (!_loggedInitialDraw)
        {
            drawTimer = Stopwatch.StartNew();
        }

        // Draw tech levels（不受“隐藏已完成”影响）
        foreach (var relevantTechLevel in RelevantTechLevels)
        {
            drawTechLevel(relevantTechLevel, visibleRect);
        }

        if (Edges == null || Nodes == null || Nodes.Count == 0)
        {
            return;
        }

        getVisibleLayerRange(visibleRect, out var minLayer, out var maxLayer);
        var maxRow = getMaxRow();
        getVisibleRowRange(visibleRect, maxRow, out var minRow, out var maxRowVisible);

        var canUseCulling = maxLayer >= minLayer && maxRow > 0 && maxRowVisible >= minRow;

        if (canUseCulling)
        {
            collectVisibleEdges(minLayer, maxLayer);
            foreach (var bucket in EdgeDrawBuckets)
            {
                for (var i = 0; i < bucket.Count; i++)
                {
                    bucket[i].Draw(visibleRect);
                }
            }

            collectVisibleNodes(visibleRect, minLayer, maxLayer, minRow, maxRowVisible);
            for (var i = 0; i < VisibleNodesBuffer.Count; i++)
            {
                VisibleNodesBuffer[i].Draw(visibleRect);
            }

            if (drawTimer != null)
            {
                drawTimer.Stop();
                _loggedInitialDraw = true;
                Logging.Performance(
                    $"Tree.Draw initial cull path (nodes={Nodes.Count}, edges={Edges.Count})",
                    drawTimer.ElapsedMilliseconds, 250);
            }

            return;
        }

        foreach (var edge in Edges)
        {
            if (EdgeHiddenBySkipCompleted(edge))
            {
                continue;
            }

            edge.Draw(visibleRect);
        }

        foreach (var node in Nodes)
        {
            if (NodeHiddenBySkipCompleted(node))
            {
                continue;
            }

            if (!node.IsWithinViewport(visibleRect))
            {
                continue;
            }

            node.Draw(visibleRect);
        }

        if (drawTimer != null)
        {
            drawTimer.Stop();
            _loggedInitialDraw = true;
            Logging.Performance(
                $"Tree.Draw initial full path (nodes={Nodes.Count}, edges={Edges.Count})",
                drawTimer.ElapsedMilliseconds, 250);
        }
    }

    private static void getVisibleLayerRange(Rect visibleRect, out int minLayer, out int maxLayer)
    {
        var maxLayerCount = Size.x;
        if (maxLayerCount <= 0)
        {
            if (LayerSlots != null && LayerSlots.Length > 0)
            {
                maxLayerCount = LayerSlots.Length - 1;
            }
            else if (!Nodes.NullOrEmpty())
            {
                maxLayerCount = Nodes.Max(n => n.X);
            }
        }

        if (maxLayerCount <= 0)
        {
            minLayer = 1;
            maxLayer = 0;
            return;
        }

        var span = Constants.NodeSize.x + Constants.NodeMargins.x;
        var padding = Mathf.Max(span, CullPadding);
        var minX = Mathf.Max(0f, visibleRect.xMin - padding);
        var maxX = visibleRect.xMax + padding;

        minLayer = Mathf.Clamp(Mathf.FloorToInt(minX / span) + 1, 1, maxLayerCount);
        maxLayer = Mathf.Clamp(Mathf.FloorToInt(maxX / span) + 1, minLayer, maxLayerCount);
    }

    private static int getMaxRow()
    {
        var maxRow = Size.z;
        if (maxRow > 0)
        {
            return maxRow;
        }

        if (LayerSlots != null && LayerSlots.Length > 0)
        {
            for (var i = LayerSlots.Length - 1; i >= 0; i--)
            {
                var column = LayerSlots[i];
                if (column == null || column.Length == 0)
                {
                    continue;
                }

                var candidate = column[column.Length - 1].Y;
                if (candidate > 0)
                {
                    return candidate;
                }
            }
        }

        if (Nodes.NullOrEmpty())
        {
            return 0;
        }

        return Nodes.Max(n => n.Y);
    }

    private static void getVisibleRowRange(Rect visibleRect, int maxRow, out int minRow, out int maxRowVisible)
    {
        if (maxRow <= 0)
        {
            minRow = 1;
            maxRowVisible = 0;
            return;
        }

        var span = Constants.NodeSize.y + Constants.NodeMargins.y;
        var padding = Mathf.Max(span, CullPadding);
        var minY = Mathf.Max(0f, visibleRect.yMin - padding);
        var maxY = visibleRect.yMax + padding;

        minRow = Mathf.Clamp(Mathf.FloorToInt(minY / span) + 1, 1, maxRow);
        maxRowVisible = Mathf.Clamp(Mathf.FloorToInt(maxY / span) + 1, minRow, maxRow);
    }

    private static void collectVisibleEdges(int minLayer, int maxLayer)
    {
        for (var i = 0; i < EdgeDrawBuckets.Length; i++)
        {
            EdgeDrawBuckets[i].Clear();
        }

        var edges = Edges;
        if (edges == null)
        {
            return;
        }

        foreach (var edge in edges)
        {
            if (EdgeHiddenBySkipCompleted(edge))
            {
                continue;
            }

            var edgeMinLayer = Mathf.Min(edge.In.X, edge.Out.X);
            var edgeMaxLayer = Mathf.Max(edge.In.X, edge.Out.X);
            if (edgeMaxLayer < minLayer || edgeMinLayer > maxLayer)
            {
                continue;
            }

            EdgeDrawBuckets[edge.DrawOrder].Add(edge);
        }
    }

    private static void collectVisibleNodes(Rect visibleRect, int minLayer, int maxLayer, int minRow, int maxRow)
    {
        VisibleNodesBuffer.Clear();

        if (LayerSlots == null || LayerSlots.Length == 0)
        {
            var nodes = Nodes;
            foreach (var node in nodes)
            {
                if (!node.IsVisible || NodeHiddenBySkipCompleted(node))
                {
                    continue;
                }

                if (!node.IsWithinViewport(visibleRect))
                {
                    continue;
                }

                VisibleNodesBuffer.Add(node);
            }

            return;
        }

        var minIndex = Mathf.Clamp(minLayer, 0, LayerSlots.Length - 1);
        var maxIndex = Mathf.Clamp(maxLayer, minIndex, LayerSlots.Length - 1);

        for (var layer = minIndex; layer <= maxIndex; layer++)
        {
            var column = LayerSlots[layer];
            if (column == null || column.Length == 0)
            {
                continue;
            }

            var start = findFirstNodeIndex(column, minRow);
            if (start < 0)
            {
                continue;
            }

            for (var i = start; i < column.Length; i++)
            {
                var node = column[i];
                if (node.Y > maxRow)
                {
                    break;
                }

                if (!node.IsVisible || NodeHiddenBySkipCompleted(node))
                {
                    continue;
                }

                if (!node.IsWithinViewport(visibleRect))
                {
                    continue;
                }

                VisibleNodesBuffer.Add(node);
            }
        }
    }

    private static int findFirstNodeIndex(Node[] nodes, int minRow)
    {
        var low = 0;
        var high = nodes.Length - 1;
        var result = -1;

        while (low <= high)
        {
            var mid = (low + high) / 2;
            if (nodes[mid].Y >= minRow)
            {
                result = mid;
                high = mid - 1;
            }
            else
            {
                low = mid + 1;
            }
        }

        return result;
    }

    public static bool IsEdgeVisible<T1, T2>(Edge<T1, T2> edge, Rect visibleRect)
        where T1 : Node where T2 : Node
    {
        // 适当外扩，减少边在视口边缘“忽隐忽现”
        const float MARGIN = 100f;
        var rect = new Rect(
            visibleRect.xMin - MARGIN, visibleRect.yMin - MARGIN,
            visibleRect.width + 2 * MARGIN, visibleRect.height + 2 * MARGIN);

        // 端点矩形（节点 Rect）有重叠——直接可见
        if (edge.In.Rect.Overlaps(rect) || edge.Out.Rect.Overlaps(rect)) return true;

        // 线段是否与矩形相交
        var a = edge.In.Rect.center;
        var b = edge.Out.Rect.center;
        return LineIntersectsRect(a, b, rect);

        static bool LineIntersectsRect(Vector2 p1, Vector2 p2, Rect r)
        {
            // 快速排除：整个线段在矩形外同一侧
            if (p1.x < r.xMin && p2.x < r.xMin) return false;
            if (p1.x > r.xMax && p2.x > r.xMax) return false;
            if (p1.y < r.yMin && p2.y < r.yMin) return false;
            if (p1.y > r.yMax && p2.y > r.yMax) return false;

            // 若任一端点在矩形内，也算相交
            if (r.Contains(p1) || r.Contains(p2)) return true;

            // 与矩形四条边是否相交
            return SegmentsIntersect(p1, p2, new Vector2(r.xMin, r.yMin), new Vector2(r.xMax, r.yMin)) ||
                   SegmentsIntersect(p1, p2, new Vector2(r.xMax, r.yMin), new Vector2(r.xMax, r.yMax)) ||
                   SegmentsIntersect(p1, p2, new Vector2(r.xMax, r.yMax), new Vector2(r.xMin, r.yMax)) ||
                   SegmentsIntersect(p1, p2, new Vector2(r.xMin, r.yMax), new Vector2(r.xMin, r.yMin));
        }

        static bool SegmentsIntersect(Vector2 a, Vector2 b, Vector2 c, Vector2 d)
        {
            float Cross(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

            var ab = b - a;
            var ac = c - a;
            var ad = d - a;
            var cd = d - c;
            var ca = a - c;
            var cb = b - c;

            var d1 = Cross(ab, ac);
            var d2 = Cross(ab, ad);
            var d3 = Cross(cd, ca);
            var d4 = Cross(cd, cb);

            if (((d1 > 0 && d2 < 0) || (d1 < 0 && d2 > 0)) &&
                ((d3 > 0 && d4 < 0) || (d3 < 0 && d4 > 0)))
                return true;

            // 共线重叠（极少数情况），做包围盒判定
            bool OnColinear(Vector2 p, Vector2 q, Vector2 r) =>
                Mathf.Min(p.x, q.x) <= r.x && r.x <= Mathf.Max(p.x, q.x) &&
                Mathf.Min(p.y, q.y) <= r.y && r.y <= Mathf.Max(p.y, q.y);

            if (Mathf.Approximately(d1, 0) && OnColinear(a, b, c)) return true;
            if (Mathf.Approximately(d2, 0) && OnColinear(a, b, d)) return true;
            if (Mathf.Approximately(d3, 0) && OnColinear(c, d, a)) return true;
            if (Mathf.Approximately(d4, 0) && OnColinear(c, d, b)) return true;

            return false;
        }
    }


    private static void drawTechLevel(TechLevel techlevel, Rect visibleRect)
    {
        if (!TechLevelBounds.ContainsKey(techlevel))
        {
            return;
        }

        if (Assets.IsHiddenByTechLevelRestrictions(techlevel))
        {
            return;
        }

        if (Event.current.type != EventType.Repaint)
        {
            return;
        }

        var num = ((Constants.NodeSize.x + Constants.NodeMargins.x) * TechLevelBounds[techlevel].min) -
                  (Constants.NodeMargins.x / 2f);
        var num2 = ((Constants.NodeSize.x + Constants.NodeMargins.x) * TechLevelBounds[techlevel].max) -
                   (Constants.NodeMargins.x / 2f);
        GUI.color = Assets.TechLevelColor;
        Text.Anchor = TextAnchor.MiddleCenter;
        if (TechLevelBounds[techlevel].min > 0 && num > visibleRect.xMin && num < visibleRect.xMax)
        {
            Widgets.DrawLine(new Vector2(num, visibleRect.yMin), new Vector2(num, visibleRect.yMax),
                Assets.TechLevelColor, 1f);
            verticalLabel(
                new Rect(num + (Constants.TechLevelLabelSize.y / 2f) - (Constants.TechLevelLabelSize.x / 2f),
                    visibleRect.center.y - (Constants.TechLevelLabelSize.y / 2f), Constants.TechLevelLabelSize.x,
                    Constants.TechLevelLabelSize.y), techlevel.ToStringHuman());
        }

        if (TechLevelBounds[techlevel].max < Size.x && num2 > visibleRect.xMin && num2 < visibleRect.xMax)
        {
            if (!Assets.IsHiddenByTechLevelRestrictions(techlevel + 1))
            {
                verticalLabel(
                    new Rect(num2 - (Constants.TechLevelLabelSize.y / 2f) - (Constants.TechLevelLabelSize.x / 2f),
                        visibleRect.center.y - (Constants.TechLevelLabelSize.y / 2f), Constants.TechLevelLabelSize.x,
                        Constants.TechLevelLabelSize.y), techlevel.ToStringHuman());
            }
        }

        GUI.color = Color.white;
        Text.Anchor = TextAnchor.UpperLeft;
    }

    private static void verticalLabel(Rect rect, string text)
    {
        var matrix = GUI.matrix;
        GUI.matrix = Matrix4x4.identity;
        GUIUtility.RotateAroundPivot(-90f, rect.center);
        GUI.matrix = matrix * GUI.matrix;
        Widgets.Label(rect, text);
        GUI.matrix = matrix;
    }

    private static Node nodeAt(int X, int Y)
    {
        var slots = LayerSlots;
        if (slots != null && (uint)X < (uint)slots.Length)
        {
            var arr = slots[X];
            if (arr != null && (uint)Y - 1 < (uint)arr.Length)
                return arr[Y - 1];
        }

        return Enumerable.FirstOrDefault(Nodes, n => n.X == X && n.Y == Y);
    }

    private static void minimizeCrossings()
    {
        // ====== 可调参数======
        const int MAX_PASSES_BARY = 50;  // 与原版一致：最多尝试 50 次
        const int MAX_PASSES_GREEDY = 50;
        const int MIN_PASSES_BARY = 0;   // 如需更保守可设 2~3；0 表示与原版一致
        const int MIN_PASSES_GREEDY = 0;
        const int FAILS_QUOTA_BARY = 2;   // “出现过一次成功后，允许的失败次数”
        const int FAILS_QUOTA_GREEDY = 2;   // “允许的失败次数”

        // ====== 预布局：保持原逻辑 ======
        Parallel.For(1, Size.x + 1, i =>
        {
            var list = (from n in layer(i)
                        orderby n.Descendants.Count
                        select n).ToList();
            for (var j = 0; j < list.Count; j++) list[j].Y = j + 1;
        });

        var totalSw = new System.Diagnostics.Stopwatch();
        totalSw.Start();

        // ====== Barymetric phase（语义等价于原：先要出现过 true，然后累计 2 次 false 即停）======
        var barySw = new System.Diagnostics.Stopwatch();
        int pass = 0;
        int baryFailsLeft = FAILS_QUOTA_BARY;
        bool seenBarySuccess = false;
        long baryMs = 0;

        while (pass < MAX_PASSES_BARY)
        {
            barySw.Restart();
            bool improved = barymetricSweep(pass++);
            barySw.Stop();
            baryMs += barySw.ElapsedMilliseconds;

            if (improved)
            {
                if (!seenBarySuccess) seenBarySuccess = true; // 第一次成功后才开始计算失败额度
            }
            else
            {
                if (seenBarySuccess && pass > MIN_PASSES_BARY) baryFailsLeft--;
            }

            Logging.Message($"[Profile] CrossingSweep bary pass={pass - 1} took {barySw.ElapsedMilliseconds} ms, improved={improved}, failsLeft={(seenBarySuccess ? baryFailsLeft : FAILS_QUOTA_BARY)}");

            if (seenBarySuccess && pass >= MIN_PASSES_BARY && baryFailsLeft <= 0)
            {
                Logging.Message($"[Profile] CrossingSweep early-stop bary at pass={pass - 1} (after first success, FAILS_QUOTA reached)");
                break;
            }
        }

        // ====== Greedy phase（语义等价于原：累计 2 次 false 即停）======
        var greedySw = new System.Diagnostics.Stopwatch();
        pass = 0;
        int greedyFailsLeft = FAILS_QUOTA_GREEDY;
        long greedyMs = 0;

        while (pass < MAX_PASSES_GREEDY && greedyFailsLeft > 0)
        {
            greedySw.Restart();
            bool improved = greedySweep(pass++);
            greedySw.Stop();
            greedyMs += greedySw.ElapsedMilliseconds;

            if (!improved && pass > MIN_PASSES_GREEDY) greedyFailsLeft--;

            Logging.Message($"[Profile] CrossingSweep greedy pass={pass - 1} took {greedySw.ElapsedMilliseconds} ms, improved={improved}, failsLeft={greedyFailsLeft}");
        }

        totalSw.Stop();
        Logging.Message($"[Profile] CrossingSweep took {totalSw.ElapsedMilliseconds} ms (bary={baryMs} ms, greedy={greedyMs} ms)");
    }



    private static bool greedySweep(int iteration)
    {
        var num = crossings();
        if (iteration % 2 == 0)
        {
            for (var i = 1; i <= Size.x; i++)
            {
                GreedySweep_Layer(i);
            }
        }
        else
        {
            for (var num2 = Size.x; num2 >= 1; num2--)
            {
                GreedySweep_Layer(num2);
            }
        }

        return crossings() < num;
    }

    private static void GreedySweep_Layer(int l)
    {
        // 快速退出
        int best = Crossings(l);
        if (best == 0) return;

        var slots = LayerSlots;
        if (slots == null || (uint)l >= (uint)slots.Length) return;
        var arr = slots[l];
        if (arr == null || arr.Length < 2) return;

        bool improved = true;
        while (improved)
        {
            improved = false;
            // 仅对相邻对 (i, i+1) 进行尝试
            for (int i = 0; i < arr.Length - 1; i++)
            {
                var A = arr[i];
                var B = arr[i + 1];
                if (!trySwap(A, B)) continue;

                int cur = Crossings(l);
                if (cur < best)
                {
                    best = cur;
                    improved = true;
                    // 注意：arr 已与 LayerSlots[l] 同引用，swap 后 arr[i]/arr[i+1] 也已更新
                }
                else
                {
                    // 恢复
                    trySwap(B, A);
                }
            }
        }
    }


    private static bool trySwap(Node A, Node B)
    {
        if (A.X != B.X)
        {
            Logging.Warning($"Can't swap {A} and {B}, nodes on different layers");
            return false;
        }

        int l = A.X;
        int ia = A.Y - 1;
        int ib = B.Y - 1;

        // 交换槽位数组
        if (LayerSlots != null && (uint)l < (uint)LayerSlots.Length)
        {
            var arr = LayerSlots[l];
            if (arr != null && (uint)ia < (uint)arr.Length && (uint)ib < (uint)arr.Length)
                (arr[ia], arr[ib]) = (arr[ib], arr[ia]);
        }

        // 交换 LayerBuckets 中对应位置，保持 layer(l, true) 返回列表的顺序一致
        if (LayerBuckets != null && (uint)l < (uint)LayerBuckets.Length)
        {
            var bucket = LayerBuckets[l];
            if (bucket != null && (uint)ia < (uint)bucket.Count && (uint)ib < (uint)bucket.Count)
                (bucket[ia], bucket[ib]) = (bucket[ib], bucket[ia]);
        }

        // 交换两个节点的 Y 值
        (A.Y, B.Y) = (B.Y, A.Y);
        return true;
    }


    private static bool barymetricSweep(int iteration)
    {
        var num = crossings();
        if (iteration % 2 == 0)
        {
            for (var i = 2; i <= Size.x; i++)
            {
                BarymetricSweep_Layer(i, true);
            }
        }
        else
        {
            for (var num2 = Size.x - 1; num2 > 0; num2--)
            {
                BarymetricSweep_Layer(num2, false);
            }
        }

        return crossings() < num;
    }

    private static void BarymetricSweep_Layer(int layer, bool left)
    {
        var orderedEnumerable =
            from n in Tree.layer(layer).ToDictionary(n => n, n => getBarycentre(n, left ? n.InNodes : n.OutNodes))
            orderby n.Value
            select n;
        var num = float.MinValue;
        var dictionary = new Dictionary<float, List<Node>>();
        foreach (var item in orderedEnumerable)
        {
            if (Math.Abs(item.Value - num) > Constants.Epsilon)
            {
                num = item.Value;
                dictionary[num] = [];
            }

            dictionary[num].Add(item.Key);
        }

        var num2 = 1;
        foreach (var item2 in dictionary)
        {
            var key = item2.Key;
            var count = item2.Value.Count;
            num2 = (int)Mathf.Max(num2, key - ((count - 1) / (float)2));
            foreach (var item3 in item2.Value)
            {
                item3.Y = num2++;
            }
        }
    }

    private static float getBarycentre(Node node, List<Node> neighbours)
    {
        if (neighbours.NullOrEmpty())
        {
            return node.Yf;
        }

        return neighbours.Sum(n => n.Yf) / neighbours.Count;
    }

    private static int crossings()
    {
        var num = 0;
        for (var i = 1; i < Size.x; i++)
        {
            num += Crossings(i, true);
        }

        return num;
    }

    private static float edgeLength()
    {
        var num = 0f;
        for (var i = 1; i < Size.x; i++)
        {
            num += EdgeLength(i, true);
        }

        return num;
    }

    private static int Crossings(int layer)
    {
        if (layer == 0)
        {
            return Crossings(layer, false);
        }

        if (layer == Size.x)
        {
            return Crossings(layer, true);
        }

        return Crossings(layer, true) + Crossings(layer, false);
    }

    // ---- Tree 类中新加：Fenwick 和通用逆序数统计 ----
    private sealed class Fenwick
    {
        private readonly int[] bit;
        public Fenwick(int n) { bit = new int[n + 2]; } // 1-based
        public void Add(int idx, int delta = 1)
        {
            for (int i = idx + 1; i < bit.Length; i += i & -i) bit[i] += delta;
        }
        public int SumPrefix(int idx)
        {
            int s = 0;
            for (int i = idx + 1; i > 0; i -= i & -i) s += bit[i];
            return s;
        }
    }

    private static int CountInversions(int[] arr)
    {
        if (arr.Length < 2) return 0;
        // 坐标压缩
        var uniq = arr.Distinct().ToList();
        uniq.Sort();
        var index = new Dictionary<int, int>(uniq.Count);
        for (int i = 0; i < uniq.Count; i++) index[uniq[i]] = i;

        var ft = new Fenwick(uniq.Count + 2);
        int inv = 0, seen = 0;
        for (int i = 0; i < arr.Length; i++)
        {
            int r = index[arr[i]];
            inv += seen - ft.SumPrefix(r);
            ft.Add(r, 1);
            seen++;
        }
        return inv;
    }

    private static int Crossings(int layer, bool @in)
    {
        var slots = LayerSlots;
        if (slots == null || (uint)layer >= (uint)slots.Length) return 0;
        var arr = slots[layer];
        if (arr == null || arr.Length < 2) return 0;

        // 收集“目标层”的 Y 序列（按源层 Y 升序）
        // 预估容量：每节点 2 条边作为初值，避免频繁扩容
        var targetY = new List<int>(arr.Length * 2);
        if (@in)
        {
            // 统计 (layer-1) -> layer
            for (int yi = 0; yi < arr.Length; yi++)
            {
                var n = arr[yi];
                var eList = n.InEdges;
                if (eList == null || eList.Count == 0) continue;
                for (int k = 0; k < eList.Count; k++) targetY.Add(eList[k].In.Y);
            }
        }
        else
        {
            // 统计 layer -> (layer+1)
            for (int yi = 0; yi < arr.Length; yi++)
            {
                var n = arr[yi];
                var eList = n.OutEdges;
                if (eList == null || eList.Count == 0) continue;
                for (int k = 0; k < eList.Count; k++) targetY.Add(eList[k].Out.Y);
            }
        }

        if (targetY.Count < 2) return 0;
        return CountInversions(targetY.ToArray()); // 使用现有的 Fenwick 逆序数
    }

    private static float EdgeLength(int layer, bool @in)
    {
        var slots = LayerSlots;
        if (slots == null || (uint)layer >= (uint)slots.Length) return 0f;
        var arr = slots[layer];
        if (arr == null || arr.Length == 0) return 0f;

        float sum = 0f;
        if (@in)
        {
            for (int yi = 0; yi < arr.Length; yi++)
            {
                var n = arr[yi];
                var eList = n.InEdges;
                if (eList == null || eList.Count == 0) continue;
                for (int k = 0; k < eList.Count; k++)
                    sum += Math.Abs(eList[k].In.Y - n.Y);
            }
        }
        else
        {
            for (int yi = 0; yi < arr.Length; yi++)
            {
                var n = arr[yi];
                var eList = n.OutEdges;
                if (eList == null || eList.Count == 0) continue;
                for (int k = 0; k < eList.Count; k++)
                    sum += Math.Abs(eList[k].Out.Y - n.Y);
            }
        }
        return sum;
    }

    private static List<Node> layer(int depth, bool ordered = false)
    {
        if (LayerBuckets != null && depth >= 0 && depth < LayerBuckets.Length)
            return LayerBuckets[depth];

        // 兜底：原实现
        if (!ordered || !OrderDirty)
            return Nodes.Where(n => n.X == depth).ToList();

        _nodes = (from n in Nodes orderby n.X, n.Y select n).ToList();
        OrderDirty = false;
        return Nodes.Where(n => n.X == depth).ToList();
    }

    private static List<Node> row(int Y)
    {
        return Nodes.Where(n => n.Y == Y).ToList();
    }

    public static void WaitForInitialization()
    {
        if (Initialized)
        {
            return;
        }

        if (_initializing)
        {
            return;
        }

        var sw = Stopwatch.StartNew();
        Initialize();
        sw.Stop();
        Logging.Performance("Tree.WaitForInitialization", sw.ElapsedMilliseconds, 250);
    }
}