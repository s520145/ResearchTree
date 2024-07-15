# [Research Tree (Continued)](https://steamcommunity.com/sharedfiles/filedetails/?id=3030499331)

![Image](https://i.imgur.com/buuPQel.png)

Update of Fluffys mod https://steamcommunity.com/sharedfiles/filedetails/?id=1266570759

- Added research-infocard, visible by Rightclick
- Research can now be added/moved to the front of the queue with Ctrl+Leftclick
- Added the current progress values on not finished projects
- Locked camera when opening the research-window
- Original research-window can be opened by holding shift
- Added mod-option for when the tree should be generated, the default is now during game start
- Added mod-option to pause the game when the tree is open
- Added some caching of the research-nodes to avoid stuttering with large research-trees
- Added scroll-wheel scrolling when holding Ctrl
- Adapted some of the graphical tricks in [ResearchPowl](https://steamcommunity.com/sharedfiles/filedetails/?id=2877856030)
- Added a scroll-bar for the research-queue to be able to see all queued projects
- Fixed compatibility with Biotech
- Fixed check for hiddenRequirements
- Fixed compatibility with [Research Reinvented](https://steamcommunity.com/sharedfiles/filedetails/?id=2868392160)
- Fixed compatibility with [Research Whatever](https://steamcommunity.com/sharedfiles/filedetails/?id=2552092060)
- Added support for the custom research requirements in [Vanilla Vehicles Expanded](https://steamcommunity.com/workshop/filedetails/?id=3014906877)
- Added compatibility with the tech-hiding from [Save Our Ship 2](https://steamcommunity.com/sharedfiles/filedetails/?id=1909914131)
- Added compatibility with the tech-limiting in [Rimedieval](https://steamcommunity.com/sharedfiles/filedetails/?id=2516523040). Needs a reload if the settings in Rimedieval are changed.

![Image](https://i.imgur.com/pufA0kM.png)
	
![Image](https://i.imgur.com/Z4GOv8H.png)

A better research tree.

## Features

 - automatically generated to maximize readability*. - shows research projects, buildings, plants and recipes unlocked by each research project. - projects can be queued, and colonists will automatically start the next project when the current research project completes. - search functionality to quickly find research projects. 

## FAQ

*Can I add/remove this from an existing save?* You can add it to existing saves without problems. Removing this mod will lead to some errors when loading, but these should not affect gameplay - and will go away after saving.

*Why is research X in position Y?* Honestly, I have no idea. The placement of projects (nodes) is automated to minimize the number of crossings between dependencies (edges), and reduce the total length of these edges. There are many possible scenarios in which this can lead to placements that may appear non-optimal. Sometimes they really are non-optimal, sometimes they just appear to be so. See also the *technical* section below for more information.

*Can I use this with mod X* Most likely, yes. Added researches and their requirements are automatically parsed and the tree layout will be updated accordingly. ResearchPal implements a lot of the same functionality as this mod, and the research queue will likely not work correctly if both mods are loaded.

*This looks very similar to ResearchPal* Yep. ResearchPal is based on a legacy version of this mod that was kept up-to-date by SkyArkAngel in the HCSK modpack. I haven’t worked on this mod in a long time, but I recently had some spare time and decided to give it another go. Feel free to use whichever you like better (ResearchPal has an entirely different layout algorithm). You can run both mods side by side to check out the different tree layouts, but be aware that the research queue will not work correctly if both mods are loaded.

## Known Issues

 - Layouts are not perfect, if you have experience with graph layouts - please do feel free to look at the source code, and/or implement a Sugiyama layout algorithm for me that runs in C

## Contributors

 - Templarr: Russian translation - Suh. Junmin: Korean translation - rw-chaos: German translation - 53N4: Spanish translation - Silverside: Fix UI scaling bug for vertical text - shiuanyue: Chinese (traditional) translation - notfood: Implement techprint requirements - HanYaodong: Add simplified Chinese translation 

## License

All original code in this mod is licensed under the [MIT license](https://opensource.org/licenses/MIT). Do what you want, but give me credit. All original content (e.g. text, imagery, sounds) in this mod is licensed under the [CC-BY-SA 4.0 license](http://creativecommons.org/licenses/by-sa/4.0/).

Parts of the code in this mod, and some content may be licensed by their original authors. If this is the case, the original author and license will either be given in the source code, or be in a LICENSE file next to the content. Please do not decompile my mods, but use the original source code available on [GitHub](https://github.com/fluffy-mods/ResearchTree/), so license information in the source code is preserved.

![Image](https://i.imgur.com/PwoNOj4.png)



-  See if the the error persists if you just have this mod and its requirements active.
-  If not, try adding your other mods until it happens again.
-  Post your error-log using [HugsLib](https://steamcommunity.com/workshop/filedetails/?id=818773962) or the standalone [Uploader](https://steamcommunity.com/sharedfiles/filedetails/?id=2873415404) and command Ctrl+F12
-  For best support, please use the Discord-channel for error-reporting.
-  Do not report errors by making a discussion-thread, I get no notification of that.
-  If you have the solution for a problem, please post it to the GitHub repository.
-  Use [RimSort](https://github.com/RimSort/RimSort/releases/latest) to sort your mods



[![Image](https://img.shields.io/github/v/release/emipa606/ResearchTree?label=latest%20version&style=plastic&color=9f1111&labelColor=black)](https://steamcommunity.com/sharedfiles/filedetails/changelog/3030499331)
