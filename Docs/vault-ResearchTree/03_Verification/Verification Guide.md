---
title: Verification Guide
aliases:
  - Quality Gates
  - ResearchTree Verification
tags:
  - researchtree/verification
status: current
updated: 2026-05-09
---

# Verification Guide

## Automated gates

```powershell
dotnet run --project Source\ResearchTree.Tests\ResearchTree.Tests.csproj --no-restore
dotnet build Source\ResearchTree.slnx --no-restore -v:minimal /warnaserror
git diff --check
```

Expected:

- `ResearchTree.Tests` reports 5 tests passed.
- Solution build reports 0 warnings and 0 errors.
- Whitespace check is clean.

## What automated tests cover

- First-run tab selection selects every valid tab.
- Stale saved tabs from uninstalled mods are pruned.
- A deliberate empty tab selection stays empty.
- Tab matching is case-insensitive.
- Null inputs normalize safely.

## What must be tested in game

Automated .NET tests do not load RimWorld, Unity, Harmony transpilers, or external mod assemblies. Use `Docs/ManualTestPlan.md` for:

- Research tab entry.
- Tree generation and navigation.
- Search and right-click info card.
- Queue add/move/drag/finish/stop.
- Tab filtering and stale tabs.
- Settings persistence.
- Compatibility smoke checks.

## Research Tab Entry

Follow `Docs/ManualTestPlan.md#Research Tab Entry` after changing `MainTabsRoot_ToggleTab.cs`, `ResearchButton.xml`, `MainTabWindow.xml`, `FluffyResearchTreeMod.cs`, or any setting that changes Shift/Ctrl entry semantics.

## Compatibility Smoke Checks

Follow `Docs/ManualTestPlan.md#Compatibility Smoke Checks` after changing `Assets.cs`, compatibility-specific patches, or any reflection-based external mod boundary.

## Risk matrix

| Area | Automated? | Manual? | Notes |
| --- | --- | --- | --- |
| `ResearchTabSelection` | yes | light | Pure logic |
| Main window input | build only | yes | Requires Unity event loop |
| Tree layout | build only | yes | Requires real research Defs |
| Queue lifecycle | build only | yes | Requires ResearchManager |
| Harmony transpilers | build only | yes | Compile does not prove IL match |
| External compatibility | no | yes | Requires each external mod |

> [!important]
> A green build is not enough for this mod. Player-facing correctness depends on fresh-save runtime behavior and log cleanliness.

## Related notes

- [[Research Queue]]
- [[Tree Generation]]
- [[Harmony Patch Surface]]
- [[Compatibility]]
