---
title: Compatibility
aliases:
  - External Mod Compatibility
tags:
  - researchtree/compatibility
  - rimworld-mod
status: current
updated: 2026-05-09
---

# Compatibility

兼容集中在 `Assets.cs`、XML patch 和少量 Harmony patch 中。原则是：检测外部模组是否存在，缓存反射入口，再在研究可见性、按钮入口、节点提示或队列绘制时使用。

## 兼容矩阵

| Mod / Feature | 主要入口 | 行为 |
| --- | --- | --- |
| Better Research Tabs | `ResearchButton.xml`, `Assets.BetterResearchTabLoaded` | 添加隐藏按钮并在入口切换时优先路由 |
| Organized Research Tab | `ResearchButton.xml`, `Assets.OrganizedResearchTabLoaded` | 添加隐藏按钮并在入口切换时优先路由 |
| Semi Random Research | `Assets.SemiRandomResearchLoaded`, `SemiRandomResearch_*` patches | 增加 go-to-tree 按钮，避免不兼容 UI 中绘制错误队列标签 |
| Rimedieval | `Assets.RimedievalAllowedResearchDefs` | 根据允许研究集合隐藏或禁用节点 |
| World Tech Level | `WorldTechLevelProjectVisibleMethod` | 判断项目/技术等级是否可见 |
| Medieval Overhaul | `IsBlockedByMedievalOverhaul()` | 用外部 postfix 计算 schematic 阻塞 |
| GrimWorld Framework | `IsBlockedByGrimworld()` | 用外部 postfix 计算额外 prerequisite |
| Save Our Ship 2 | `IsBlockedBySOS2()` | 对 Archotech 研究做 unlock 判断 |
| Vanilla Vehicles Expanded | `UsingVanillaVehiclesExpanded` | 支持自定义研究需求 |
| UINotIncluded | `UINotIncluded_Button_Worker_OnRepaint` | 在替代按钮绘制中补队列数字 |

## 风险边界

> [!warning]
> 反射兼容最脆弱。外部模组改类型名、方法签名或字段名时，编译不会失败，只有游戏内 smoke test 和日志能暴露问题。

## 维护建议

- 新增兼容时优先放在 `Assets` 中做检测和缓存。
- Harmony patch 应该保持窄入口，避免复制外部模组业务逻辑。
- 每个兼容项至少在 [[Verification Guide#Compatibility Smoke Checks]] 中有一条手测。

## 相关笔记

- [[Research Entry]]
- [[Harmony Patch Surface]]
- [[Tree Generation]]
