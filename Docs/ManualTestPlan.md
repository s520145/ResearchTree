# Manual Test Plan: Research Tree Gameplay

Run these checks in a fresh save when possible, then repeat the queue and tab-filter checks in one existing save that already contains queued research.

## Baseline

* Launch RimWorld with Research Tree enabled and no known conflicting research UI mods.
* Start or load a colony with at least three unfinished research projects across two research tabs.
* Keep the player log open or easy to inspect.

## Research Tab Entry

1. Open the Research main tab.
2. Confirm the Research Tree window opens when override is enabled.
3. Hold Shift while opening the Research main tab.
4. Confirm the vanilla research window opens when the Shift override path is active.
5. Toggle the mod setting for reverse Shift behavior and repeat steps 1-4.

Expected:

* The selected research UI matches the setting.
* The camera does not pan or select map objects when clicking inside the Research Tree window.
* No input-tracing debug spam is written to the log.

## Tree Generation And Navigation

1. Use the default load type and open the Research Tree.
2. Confirm the generation-in-progress message appears only while the tree is building.
3. Pan the tree with mouse drag and WASD.
4. Zoom and vertical-scroll with Ctrl according to the configured Ctrl behavior.
5. Hold Alt and use the mouse wheel to scroll horizontally.
6. Drag over vertical and horizontal scroll bars.

Expected:

* The tree finishes building and displays connected research nodes.
* Panning, scroll bars, zoom, and WASD movement do not leak clicks to the map.
* Large trees remain navigable without repeated layout stutter.

## Search And Node Details

1. Search for an unfinished research project by partial label.
2. Confirm matching nodes highlight and the view remains stable.
3. Clear the search.
4. Right-click a research node.

Expected:

* Search highlights only matching projects.
* Clearing search restores normal highlighting.
* The research info card opens and shows the project description and unlocked content.

## Queue Gameplay

1. Left-click an available project to replace or add to the queue according to the current Reverse Shift setting.
2. Shift-click an available project and confirm the opposite queue behavior.
3. Ctrl-left-click a project with prerequisites.
4. Drag queued projects to reorder them.
5. Finish the first queued research naturally or with dev mode.
6. Stop the current research from the vanilla research UI.

Expected:

* Required prerequisites are added in dependency order.
* Ctrl-left-click moves the selected dependency chain to the front.
* Queue labels update in the tree, main button, and vanilla research list.
* Finishing or stopping research dequeues the correct project and starts the next valid queued project.
* Saved games reload with the same valid queue order.

## Tab Filtering And Stale Tabs

1. Open the Research Tree tab filter.
2. Select all tabs, rebuild, and confirm all active research tabs display.
3. Select no tabs, rebuild, and confirm the no-tabs-selected message displays instead of silently restoring all tabs.
4. Select one tab, rebuild, and confirm queued projects and their prerequisites remain visible.
5. Load a save that previously had an extra research tab from another mod, with that mod now disabled.

Expected:

* The filter list contains only tabs from active mods.
* Stale saved tab names are pruned from settings.
* A deliberate empty selection stays empty.
* Queued projects remain visible with their prerequisite chain even when their tab would otherwise be filtered.

## Research Tag Profiles

1. Open the Research Tree and confirm the default profile tab is visible above search.
2. Use the profile manager to create a new profile, rename it, select a different research tab combination, and rebuild.
3. Switch between the default profile and the new profile.
4. Reopen the Research Tree and confirm the saved profiles and active profile persist.
5. Create a profile with no selected research tabs and switch to it.
6. Delete the active profile while at least one other profile exists.
7. Finish or stop a queued research project, then switch to a cached profile.

Expected:

* Cached profiles switch without re-opening the checkbox dialog.
* Uncached profiles show a loading state and switch once prepared.
* Each profile keeps its own research tab combination and in-memory scroll/zoom position.
* Empty profiles show the no-tabs-selected message.
* Deleting the active profile selects another profile and never leaves zero profiles.
* Finished/stopped research updates cached profile nodes and queue state.

## Settings

1. Toggle load type, override research, pause on open, show completion, show research lines, skip completed, visual group by tab, and background color.
2. Close and reopen settings.
3. Use Reset.

Expected:

* Settings persist across close/reopen.
* Reset returns values to defaults and selects all currently valid research tabs.
* Do-not-generate mode keeps queue mechanics available in the vanilla research window.

## Compatibility Smoke Checks

Run each check only when the named mod is present.

* Better Research Tabs / Organized Research Tab: opening their research button path reaches the expected research UI.
* Semi Random Research: the go-to-tree button opens Research Tree and queue labels are not drawn over incompatible UI.
* Rimedieval / World Tech Level: hidden or blocked research is disabled or hidden according to the Research Tree setting.
* Medieval Overhaul / GrimWorld Framework / Vanilla Vehicles Expanded: extra prerequisite text appears without null-reference errors.
* Save Our Ship 2: hidden Archotech research stays hidden until unlocked.

Expected:

* Compatibility reflection failures are absent from the log.
* Unsupported or missing compatibility targets fail closed instead of breaking vanilla research.
