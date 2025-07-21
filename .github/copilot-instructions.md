# GitHub Copilot Instructions for RimWorld Modding Project

## Mod Overview and Purpose
This mod aims to enhance RimWorld's research system by introducing an improved research tree interface and additional features to streamline research project management. It focuses on making the research process more intuitive and offering modders the flexibility to customize and extend research capabilities within the game.

## Key Features and Systems
- **Research Tree Interface:** Provides a graphical representation of research projects with features like zooming and panning for easier navigation.
- **Research Project Management:** Includes functionality for starting, stopping, and finishing research projects programmatically.
- **Extended UI Elements:** Adds integrated UI components like buttons and tooltips to enhance player interaction with the research system.

## Coding Patterns and Conventions
- **Class Naming:** Class names follow PascalCase and are descriptive of their roles, such as `Building_ResearchBench_Extensions` and `MainTabWindow_ResearchTree`.
- **Method Naming:** Methods are named using camelCase and often start with a verb to indicate their action, such as `HandleZoom()` and `ResetZoomLevel()`.
- **Static Types:** Helper and utility classes, such as `Def_Extensions` and `Building_ResearchBench_Extensions`, are declared as static for the extension methods they contain.

## XML Integration
- Although specific XML summaries were not provided, XML integration typically involves defining and managing game data and configurations.
- Utilize XML to define mod settings and integrate new elements into existing vanilla systems effectively.

## Harmony Patching
- **Harmony** is used to patch the game's source code, allowing for non-invasive modifications. For instance, classes ending with `_Extensions` likely use Harmony to add functionality to existing game methods.
- Ensure that patches target the correct methods and use Postfix or Prefix annotations to modify behavior without causing conflicts.

## Suggestions for Copilot
1. **Code Completion:** Provide suggestions based on patterns observed in existing classes, such as extension methods or UI handlers.
2. **Refactor Assistance:** Offer refactoring suggestions when redundant patterns are detected to improve code efficiency.
3. **XML Handling:** Suggest templates and common structures for XML, especially for defining new research projects or settings.
4. **Harmony Patch Guidance:** Recognize common patching scenarios and suggest appropriate annotations or method templates.

By following these instructions and utilizing GitHub Copilot effectively, you can streamline the development process and ensure that the mod remains compatible with future game updates.
