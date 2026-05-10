---
title: Search and Node Interaction
aliases:
  - Node Interaction
  - Research Search
tags:
  - researchtree/gameplay
  - researchtree/ui
status: current
updated: 2026-05-09
---

# Search and Node Interaction

`MainTabWindow_ResearchTree` 管理窗口输入、搜索栏、平移缩放和队列栏；`ResearchNode` 管理单个研究节点的绘制、提示和点击行为。

## 主要职责

### MainTabWindow_ResearchTree

- `DoWindowContents()`：顶栏、生成中提示、无标签提示、树绘制、队列绘制。
- `DrawSearchBar()` / `updateSearchResults()`：搜索匹配并居中结果。
- `handleZoom()`：滚轮缩放、Ctrl 纵向滚动和 Alt 横向滚动。
- `handleDragging()`：树平移。
- `handleDolly()`：WASD 移动。
- `AbsorbUnclaimedInput()`：吸收窗口内未被控件使用的鼠标事件，避免穿透地图。

### ResearchNode

- `Draw()`：节点背景、标题、图标、解锁项和队列交互。
- `getResearchTooltipString()`：研究说明、队列快捷键、前置要求和缺失设施。
- `GetMissingRequiredRecursive()`：递归收集未完成 prerequisite。
- `HandleVanillaNodeClickEvent()`：原版研究窗口中的节点点击队列行为。

## 交互规则

- 右键节点打开 `Dialog_ResearchInfoCard`。
- 鼠标悬停未完成节点时高亮缺失前置链。
- 搜索结果会写入 `_matchingProjects`，匹配节点改变高亮和居中逻辑。
- 快速搜索非空时，队列栏悬停不会自动居中队列节点。
- `Alt+鼠标滚轮` 始终横向滚动研究树；Ctrl 的滚动/缩放语义仍由设置决定。

> [!tip]
> 输入穿透问题通常不是 React/GUI 状态问题，而是 Unity 事件是否被 `Use()`、窗口是否 `preventCameraMotion`、滚动条区域是否被误判的问题。

## 相关笔记

- [[Tree Generation]]
- [[Research Queue]]
- [[Verification Guide]]
