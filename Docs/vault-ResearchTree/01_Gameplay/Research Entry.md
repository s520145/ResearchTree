---
title: Research Entry
aliases:
  - Research Tab Entry
tags:
  - researchtree/gameplay
  - researchtree/entry
status: current
updated: 2026-05-09
---

# Research Entry

研究入口由 XML Def、XML patch 和 Harmony patch 共同决定。

## 入口文件

- `1.6/Defs/MainTabDefs/MainTabWindow.xml` 定义隐藏的 `FluffyResearchTree` 主按钮，窗口类为 `FluffyResearchTree.MainTabWindow_ResearchTree`。
- `1.6/Patches/ResearchButton.xml` 将原版 `Research` 按钮的 `tabWindowClass` 保持为 `MainTabWindow_Research`，并按需添加 Better Research Tabs / Organized Research Tab 的隐藏按钮。
- `Source/ResearchTree/Patches/MainTabsRoot_ToggleTab.cs` 在运行时拦截 `MainTabsRoot.ToggleTab`，根据设置和按键决定最终打开哪个研究窗口。

## 决策规则

- `LoadTypeDoNotGenerateResearchTree`：不打开 ResearchTree，保留队列能力。
- `OverrideResearch == true` 且未按 Shift/Ctrl：打开 `FluffyResearchTree`。
- Shift 与 `ReverseShift` 相关设置共同决定“原版窗口”和“ResearchTree”的切换体验。
- Anomaly 研究页签处于激活状态时，保留原版 Anomaly 研究窗口路径。
- Better Research Tabs / Organized Research Tab 存在时，先切到对应隐藏 MainButtonDef，再按 ResearchTree 设置决定是否覆盖。

> [!warning]
> 入口逻辑同时受 RimWorld 主按钮、外部研究 UI 模组、Anomaly DLC 和玩家按键影响。修改这里必须回归 [[Verification Guide#Research Tab Entry]]。

## 相关笔记

- [[Harmony Patch Surface]]
- [[Compatibility]]
- [[Settings and Tab Filtering]]
