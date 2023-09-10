using System.Reflection;
using System.Threading;
using HarmonyLib;
using Mlie;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

[StaticConstructorOnStartup]
internal class FluffyResearchTreeMod : Mod
{
    /// <summary>
    ///     The instance of the settings to be read by the mod
    /// </summary>
    public static FluffyResearchTreeMod instance;

    private static string currentVersion;

    public static Thread initializeWorker;

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="content"></param>
    public FluffyResearchTreeMod(ModContentPack content) : base(content)
    {
        instance = this;
        new Harmony("Fluffy.ResearchTree").PatchAll(Assembly.GetExecutingAssembly());
        Settings = GetSettings<FluffyResearchTreeSettings>();
        currentVersion = VersionFromManifest.GetVersionFromModMetaData(content.ModMetaData);

        switch (Settings.LoadType)
        {
            //case 0: // No point really
            //    LongEventHandler.QueueLongEvent(Tree.Initialize, "ResearchPal.BuildingResearchTree", false, null);
            //    return;
            case 1:
                LongEventHandler.QueueLongEvent(StartLoadingWorker, "ResearchPal.BuildingResearchTreeAsync", true,
                    null);
                break;
        }
    }

    /// <summary>
    ///     The instance-settings for the mod
    /// </summary>
    internal FluffyResearchTreeSettings Settings { get; }

    private static void StartLoadingWorker()
    {
        initializeWorker = new Thread(Tree.Initialize);
        Log.Message("[ResearchTree]: Initialization start in background");
        initializeWorker.Start();
    }

    /// <summary>
    ///     The title for the mod-settings
    /// </summary>
    /// <returns></returns>
    public override string SettingsCategory()
    {
        return "Research Tree";
    }

    /// <summary>
    ///     The settings-window
    ///     For more info: https://rimworldwiki.com/wiki/Modding_Tutorials/ModSettings
    /// </summary>
    /// <param name="rect"></param>
    public override void DoSettingsWindowContents(Rect rect)
    {
        var listing_Standard = new Listing_Standard();
        listing_Standard.Begin(rect);
        listing_Standard.Gap();
        listing_Standard.Label("Fluffy.ResearchTree.LoadType".Translate());
        //if (listing_Standard.RadioButton("Fluffy.ResearchTree.LoadTypeOne".Translate(), Settings.LoadType == 0))
        //{
        //    Settings.LoadType = 0;
        //}

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.LoadTypeTwo".Translate(), Settings.LoadType == 1))
        {
            Settings.LoadType = 1;
        }

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.LoadTypeThree".Translate(), Settings.LoadType == 2))
        {
            Settings.LoadType = 2;
        }

        listing_Standard.Gap();
        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.PauseOnOpen".Translate(), ref Settings.PauseOnOpen);
        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.VanillaGraphics".Translate(),
            ref Settings.VanillaGraphics);
        if (currentVersion != null)
        {
            listing_Standard.Gap();
            GUI.contentColor = Color.gray;
            listing_Standard.Label("Fluffy.ResearchTree.CurrentModVersion".Translate(currentVersion));
            GUI.contentColor = Color.white;
        }

        listing_Standard.End();
    }
}