# GitHub Copilot Instructions for Research Tree (Continued) Mod

## Mod Overview and Purpose
The Research Tree (Continued) mod is an update of Fluffy's original mod, designed to enhance the research process in RimWorld. It introduces a visually intuitive way to navigate and manage research projects through an automated research tree interface. The mod ensures better readability and facilitates effortless project management in the game. 

## Key Features and Systems
- **Enhanced Research Infocard:** Access detailed information by right-clicking on research projects.
- **Queue Management:** Add or prioritize research projects to the front of the queue with Ctrl+Left Click. Reorder the queue via drag-and-drop.
- **Progress Visibility:** Display current progress values on ongoing projects.
- **Camera Locking:** Prevent accidental movement by locking the camera when the research window is open.
- **Original Window Access:** Open the vanilla research window by holding Shift.
- **Mod Options:** Configure when to generate the tree and whether to pause the game when the tree is displayed.
- **Caching:** Implements caching of research nodes to reduce stutter with large research-trees.
- **Scroll Functionality:** Incorporates scroll-wheel scrolling with Ctrl and a scrollbar for the research queue.
- **Customizable Display:** Choose background colors and hide specific warnings or blocked research items.
- **Compatibility:** Enhanced compatibility with several popular mods, including Biotech, Research Reinvented, and GrimWorld 40,000 - Framework.

## Coding Patterns and Conventions
- **Static Classes:** Common utility extensions and handlers are implemented as static classes (e.g., `Building_ResearchBench_Extensions`).
- **Subclass Inheritance:** Derive specialized classes from base classes (e.g., `ResearchNode` extends `Node`) to promote code reuse.
- **World Component Usage:** Use `WorldComponent` for persistent game data management (`Queue` class).
- **Singleton Patterns:** Ensure a single point of access for settings and configurations, as shown by `FluffyResearchTreeSettings`.

## XML Integration
- The mod heavily relies on XML for defining research projects and their dependencies.
- XML files are parsed to extract research details, automatically updating the research tree layout.

## Harmony Patching
- Utilize Harmony for non-invasive code modifications and compatibility with existing game mechanisms.
- Example usage can be seen in files like `MainTabWindow_Research_AttemptBeginResearch.cs` to safely extend existing functionalities without altering the core game code.

## Suggestions for Copilot
- **Code Consistency:** Maintain consistency with method and class naming as seen across different files.
- **XML Handling:** Suggest XML parsing and manipulation for handling research dependencies.
- **Harmony Use:** Propose patches that interact with RimWorld's core mechanics without direct modification.
- **Utility Functions:** Recommend utility functions for frequently used operations, including logging via the `Logging` class.
- **Interface Implementation:** Suggest class definitions that extend base classes such as `Window` for UI modifications.
- **Performance Optimization:** Consider using cached values where applicable to reduce performance overhead.
- **Documented Examples:** Recommend well-documented examples when suggesting code to provide inline clarity.

By following these instructions, you can effectively contribute to extending and refining the Research Tree (Continued) mod, ensuring a cohesive integration with RimWorld's systems and a smooth user experience.
