# Manual Test Plan: Research Tab Pruning

This scenario verifies that uninstalling a modded research tab cannot break the tree population.

## Prerequisites
* Launch RimWorld with the Fluffy Research Tree mod enabled.
* Start a save that previously had at least one extra research tab provided by another mod. Ensure that the extra mod is now disabled.

## Steps
1. Start RimWorld and load the affected save.
2. Open the mod settings for **Research Tree** from the main menu.
3. Observe the list of tabs in the right-hand column.
   * Confirm that only tabs still provided by active mods are listed.
4. Close the settings window.
5. Open the research tree.

## Expected Results
* The research tree opens successfully and displays all remaining research tabs.
* No errors are logged about missing tab definitions.
* Reopening the settings shows only valid tabs selected; the previously uninstalled tab does not reappear.
