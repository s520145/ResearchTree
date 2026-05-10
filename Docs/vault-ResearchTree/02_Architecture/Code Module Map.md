---
title: Code Module Map
aliases:
  - Source Map
  - Module Map
tags:
  - researchtree/code
  - researchtree/architecture
status: current
updated: 2026-05-09
---

# Code Module Map

## Core

| File | Role |
| --- | --- |
| `FluffyResearchTreeMod.cs` | Mod 启动、Harmony patch、设置窗口 |
| `FluffyResearchTreeSettings.cs` | 设置持久化、研究标签缓存 |
| `ResearchTabSelection.cs` | 可测试的标签选择归一化 |
| `Tree.cs` | 研究图生成、布局、可见缓存、绘制 |
| `Node.cs` | 节点基础状态、位置、通用绘制接口 |
| `ResearchNode.cs` | 研究节点可用性、交互、提示、解锁展示 |
| `DummyNode.cs` | 跨层边中间节点 |
| `Edge.cs` | 依赖边绘制和排序 |
| `Queue.cs` | 研究队列 WorldComponent |

## UI

| File | Role |
| --- | --- |
| `MainTabWindow_ResearchTree.cs` | 主研究树窗口、搜索、输入、缩放、队列栏 |
| `Dialog_ResearchInfoCard.cs` | 右键研究信息卡 |
| `Dialog_SelectResearchTabs.cs` | 研究标签过滤窗口 |
| `FloatMenu_Fixed.cs` | 固定位置浮动菜单 |
| `TooltipHandler_Modified.cs` | 可全局禁用的 tooltip 转发 |

## Support

| File | Role |
| --- | --- |
| `Assets.cs` | 贴图、颜色、兼容检测、反射缓存 |
| `Constants.cs` | 尺寸、点击、加载类型常量 |
| `FastGUI.cs` | Unity 内部绘制反射快速路径 |
| `Logging.cs` | Verbose logging 和性能日志 |
| `Profiler.cs` | 简单性能计时 |
| `Extensions/*` | RimWorld Def / ResearchProjectDef / bench 扩展 |

## Tests and quality

| File | Role |
| --- | --- |
| `Source/ResearchTree.Tests/Program.cs` | 标签选择规则的轻量单测 |
| `Directory.Build.props` | .NET analyzer 和 warning policy |
| `Docs/ManualTestPlan.md` | 游戏内玩法回归清单 |

## 相关笔记

- [[Runtime Architecture]]
- [[Gameplay MOC]]
- [[Verification Guide]]
