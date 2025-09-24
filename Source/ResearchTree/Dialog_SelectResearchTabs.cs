// Dialog_SelectResearchTabs.cs
// 二级菜单：选择要参与生成的研究标签（ResearchTabDef）
using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace FluffyResearchTree
{
    public class Dialog_SelectResearchTabs : Window
    {
        // ====== 常量/UI ======
        private const float RowH = 28f;
        private const float BtnH = 36f;
        private const float Pad = 12f;
        private const float Gap = 8f;

        // ====== 状态 ======
        private Vector2 _scroll;
        private readonly List<ResearchTabDef> _allTabs;
        private readonly HashSet<string> _workingIncluded;
        private readonly Dictionary<string, string> _truncateCache = new();

        public override Vector2 InitialSize => new(700f, 520f);

        public Dialog_SelectResearchTabs()
        {
            absorbInputAroundWindow = true;
            closeOnClickedOutside = true;
            doCloseX = true;
            forcePause = FluffyResearchTreeMod.instance.Settings.PauseOnOpen;

            // 准备数据：确保缓存 + 复制已选集合
            var settings = FluffyResearchTreeMod.instance.Settings;
            settings.EnsureTabCache(); // 收集所有可用 tab（ResearchTabDef）→ AllTabsCache
            _allTabs = settings.AllTabsCache?.ToList() ?? new List<ResearchTabDef>();
            _allTabs.Sort((a, b) => string.Compare(a.LabelCap, b.LabelCap, StringComparison.OrdinalIgnoreCase));

            _workingIncluded = new HashSet<string>(settings.IncludedTabs ?? new HashSet<string>(),
                                                   StringComparer.OrdinalIgnoreCase);
        }

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
            // 标题
            Text.Anchor = TextAnchor.MiddleLeft;
            Widgets.Label(new Rect(inRect.x, inRect.y, inRect.width, 30f), "Fluffy.ResearchTree.filter".Translate());
            Text.Anchor = TextAnchor.UpperLeft;

            // 工具条：全选/全不选/反选 + 统计
            var toolbar = new Rect(inRect.x, inRect.y + 34f, inRect.width, 30f);
            DrawToolbar(toolbar);

            float listTop = toolbar.yMax + Gap;
            float footerTop = inRect.yMax - Pad - BtnH;
            float listHeight = Mathf.Max(0f, footerTop - listTop - Gap);

            // 列表（滚动）
            var listRect = new Rect(inRect.x + Pad, listTop, inRect.width - Pad * 2, listHeight);
            DrawTabList(listRect);

            // 底部：取消 / 重新生成
            var bottom = new Rect(inRect.x + Pad, footerTop, inRect.width - Pad * 2, BtnH);
            DrawFooter(bottom);
        }

        private void DrawToolbar(Rect rect)
        {
            float x = rect.xMin;
            float w = 110f;

            if (Widgets.ButtonText(new Rect(x, rect.y, w, rect.height), "Fluffy.ResearchTree.selectAll".Translate()))
            {
                foreach (var t in _allTabs) _workingIncluded.Add(t.defName);
                SoundDefOf.Checkbox_TurnedOn.PlayOneShotOnCamera();
            }
            x += w + Gap;

            if (Widgets.ButtonText(new Rect(x, rect.y, w, rect.height), "Fluffy.ResearchTree.selectNone".Translate()))
            {
                _workingIncluded.Clear();
                SoundDefOf.Checkbox_TurnedOff.PlayOneShotOnCamera();
            }
            x += w + Gap;

            if (Widgets.ButtonText(new Rect(x, rect.y, w, rect.height), "Fluffy.ResearchTree.selectInvert".Translate()))
            {
                foreach (var t in _allTabs)
                {
                    if (!_workingIncluded.Remove(t.defName))
                        _workingIncluded.Add(t.defName);
                }
                SoundDefOf.Tick_High.PlayOneShotOnCamera();
            }

            // 右侧统计
            var stat = $"{_workingIncluded.Count}/{_allTabs.Count}";
            var sz = Text.CalcSize(stat);
            Widgets.Label(new Rect(rect.xMax - sz.x, rect.y + (rect.height - sz.y) / 2f, sz.x, sz.y), stat);
        }

        private void DrawTabList(Rect rect)
        {
            // 两列均分
            int total = _allTabs.Count;
            int perCol = Mathf.CeilToInt(total / 2f);
            float colW = (rect.width - Gap) / 2f;
            float viewH = Mathf.Max(perCol * RowH, rect.height);

            var viewRect = new Rect(0f, 0f, rect.width - 16f, viewH);
            Widgets.BeginScrollView(rect, ref _scroll, viewRect);

            var col1 = new Rect(0f, 0f, colW, perCol * RowH);
            var col2 = new Rect(col1.xMax + Gap, 0f, colW, perCol * RowH);

            DrawColumn(col1, _allTabs.Take(perCol));
            DrawColumn(col2, _allTabs.Skip(perCol));

            Widgets.EndScrollView();
        }

        private void DrawColumn(Rect rect, IEnumerable<ResearchTabDef> items)
        {
            int i = 0;
            foreach (var tab in items)
            {
                var row = new Rect(rect.x, rect.y + i * RowH, rect.width, RowH);
                DrawRow(row, tab);
                i++;
            }
        }

        private void DrawRow(Rect row, ResearchTabDef tab)
        {
            if (Mouse.IsOver(row)) Widgets.DrawHighlight(row);

            // 勾选框
            var cb = new Rect(row.x, row.y + 4f, 24f, 24f);
            bool on = _workingIncluded.Contains(tab.defName);
            Widgets.Checkbox(cb.position, ref on, 24f, paintable: true);

            if (on) _workingIncluded.Add(tab.defName);
            else _workingIncluded.Remove(tab.defName);

            // 文本按钮（点击切换）
            var txt = new Rect(cb.xMax + 6f, row.y, row.width - 32f, row.height);
            string display = Truncate(tab.LabelCap, txt.width);
            if (Widgets.ButtonText(txt, display, drawBackground: false))
            {
                if (!_workingIncluded.Remove(tab.defName)) _workingIncluded.Add(tab.defName);
                (on ? SoundDefOf.Checkbox_TurnedOff : SoundDefOf.Checkbox_TurnedOn).PlayOneShotOnCamera();
            }

            TooltipHandler.TipRegion(txt, tab.LabelCap);
        }

        private string Truncate(string text, float maxW)
        {
            if (string.IsNullOrEmpty(text)) return string.Empty;
            if (_truncateCache.TryGetValue(text, out var cached) && Text.CalcSize(cached).x <= maxW) return cached;

            if (Text.CalcSize(text).x <= maxW) { _truncateCache[text] = text; return text; }

            const string ell = "…";
            for (int len = Mathf.Min(text.Length, 64); len >= 1; len--)
            {
                var cand = text.Substring(0, len) + ell;
                if (Text.CalcSize(cand).x <= maxW) { _truncateCache[text] = cand; return cand; }
            }
            _truncateCache[text] = ell; return ell;
        }

        private void DrawFooter(Rect rect)
        {
            float btnW = 150f;
            float gap = Gap;
            var rebuild = new Rect(rect.xMax - btnW, rect.y, btnW, rect.height);
            var cancel = new Rect(rebuild.xMin - gap - btnW, rect.y, btnW, rect.height);

            if (cancel.xMin < rect.xMin)
            {
                cancel.x = rect.xMin;
                rebuild.x = Mathf.Min(rect.xMax - btnW, cancel.xMax + gap);
            }

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

            // 写回选择，并标记为已初始化（避免后续 EnsureTabCache() 把空集合当成默认状态重置）
            settings.IncludedTabs = new HashSet<string>(_workingIncluded, StringComparer.OrdinalIgnoreCase);
            settings.TabsInitialized = true;

            // 直接写盘，保证无需重新进入设置页也能持久化
            FluffyResearchTreeMod.instance.WriteSettings();

            // 触发刷新（现有窗口与绘制管线会据此刷新/重建）
            Assets.RefreshResearch = true;

            Close(doCloseSound: true);

            Tree.RequestRebuild(resetZoom: true, reopenResearchTab: false);

            Messages.Message("Fluffy.ResearchTree.refresh".Translate(), MessageTypeDefOf.TaskCompletion, historical: false);
        }

        // ====== 小工具：GUI 颜色域 ======
        private readonly struct GuiColorScope : IDisposable
        {
            private readonly Color _old;
            public GuiColorScope(Color c) { _old = GUI.color; GUI.color = c; }
            public void Dispose() => GUI.color = _old;
        }
    }
}
