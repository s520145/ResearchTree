---
title: Harmony Patch Surface
aliases:
  - Harmony Patches
tags:
  - researchtree/architecture
  - researchtree/harmony
status: current
updated: 2026-05-09
---

# Harmony Patch Surface

Harmony patch 是模组接入 RimWorld 原版研究生命周期的主要方式。

## Patch 分类

| 文件 | 目标 | 目的 |
| --- | --- | --- |
| `MainTabsRoot_ToggleTab.cs` | `MainTabsRoot.ToggleTab` | 研究入口路由 |
| `MainButtonWorker_DoButton.cs` | `MainButtonWorker.DoButton` | 主按钮队列标签 |
| `MainTabWindow_Research_AttemptBeginResearch.cs` | `AttemptBeginResearch` transpiler | 替换确认弹窗流程 |
| `MainTabWindow_Research_DoBeginResearch.cs` | `DoBeginResearch` | 开始研究时同步队列 |
| `MainTabWindow_Research_ListProjects.cs` | `ListProjects` transpiler | 原版列表中绘制队列标签和点击行为 |
| `ResearchManager_FinishProject.cs` | `ResearchManager.FinishProject` | 完成研究后推进队列 |
| `ResearchManager_StopProject.cs` | `ResearchManager.StopProject` | 停止研究时出队 |
| `MainTabWindow_Research_ComputeUnlockedDefsThatHaveMissingMemes.cs` | ideology missing meme check | 按设置隐藏提示 |
| `DiaOption_Activate.cs` | `DiaOption.Activate` | Void Monolith 进入 Anomaly 研究页 |
| `WindowStack_AdjustWindowsIfResolutionChanged.cs` | window stack resize | 分辨率变化时保持窗口状态 |
| `UINotIncluded_Button_Worker_OnRepaint.cs` | UINotIncluded button repaint | 替代 UI 的队列数字 |

## 修改原则

- Prefix/Postfix 能解决的，不扩展 transpiler。
- Transpiler 改动必须有游戏内回归，因为编译不能证明 IL 位置仍正确。
- 每个 patch 要能回答：目标方法、改变的玩家行为、失败时的症状。

> [!danger]
> `MainTabWindow_Research_ListProjects` 和 `MainTabWindow_Research_AttemptBeginResearch` 是最脆弱的 patch 面。RimWorld 更新或外部模组改 UI 时优先检查它们。

## 相关笔记

- [[Research Entry]]
- [[Research Queue]]
- [[Verification Guide]]
