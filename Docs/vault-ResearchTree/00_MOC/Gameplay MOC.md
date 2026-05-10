---
title: Gameplay MOC
aliases:
  - ResearchTree Gameplay
tags:
  - researchtree/gameplay
  - moc
status: current
updated: 2026-05-09
---

# Gameplay MOC

按玩家路径看，这个模组由五条主要玩法链组成。

## 玩法路径

1. [[Research Entry]]：玩家点击研究按钮，决定打开原版研究窗口、兼容研究窗口，还是 ResearchTree。
2. [[Tree Generation]]：模组生成研究节点图、依赖边、技术等级分区和可见缓存。
3. [[Search and Node Interaction]]：玩家搜索、查看详情、点击节点、查看解锁内容。
4. [[Research Queue]]：玩家把研究加入队列、移动到队首、拖拽排序，并在研究完成后自动推进。
5. [[Settings and Tab Filtering]]：玩家配置加载方式、快捷键语义、完成项隐藏、标签过滤。

## 辅助路径

- [[Compatibility]]：Better Research Tabs、Organized Research Tab、Semi Random Research、World Tech Level 等兼容。
- [[Verification Guide]]：每条玩法路径对应的构建、单测、手测证明。

> [!tip]
> 读代码时优先沿玩法链，而不是沿文件名。`MainTabWindow_ResearchTree` 是交互中心，但真正的状态和兼容分散在 `Tree`、`Queue`、`ResearchNode`、`Assets` 和 Harmony patches。
