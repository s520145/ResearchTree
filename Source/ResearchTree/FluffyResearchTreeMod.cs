using ColourPicker;
using HarmonyLib;
using Mlie;
using RimWorld;
using System.Reflection;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

internal class FluffyResearchTreeMod : Mod
{
    /// <summary>
    ///     The instance of the settings to be read by the mod
    /// </summary>
    public static FluffyResearchTreeMod instance;

    private static string currentVersion;

    private Vector2 scrollPosTabs = Vector2.zero;

    //public static Thread initializeWorker;

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
    }

    /// <summary>
    ///     The instance-settings for the mod
    /// </summary>
    internal FluffyResearchTreeSettings Settings { get; }

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
        listing_Standard.ColumnWidth = rect.width / 2f - 12f;
        Settings.EnsureTabCache();
        listing_Standard.Gap();
        listing_Standard.Label("Fluffy.ResearchTree.LoadType".Translate());

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.LoadTypeTwo".Translate(),
                Settings.LoadType == Constants.LoadTypeLoadInBackground))
        {
            Settings.LoadType = Constants.LoadTypeLoadInBackground;
        }

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.LoadTypeThree".Translate(),
                Settings.LoadType == Constants.LoadTypeFirstTimeOpening))
        {
            Settings.LoadType = Constants.LoadTypeFirstTimeOpening;
        }

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.LoadTypeFour".Translate(),
                Settings.LoadType == Constants.LoadTypeDoNotGenerateResearchTree))
        {
            Settings.LoadType = Constants.LoadTypeDoNotGenerateResearchTree;
            Settings.OverrideResearch = false;
        }

        if (Settings.LoadType == Constants.LoadTypeLoadInBackground && Prefs.UIScale > 1f)
        {
            listing_Standard.Label("Fluffy.ResearchTree.UIScaleWarning".Translate());
        }

        listing_Standard.Gap();

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.CtrlFunctionScroll".Translate(), Settings.CtrlFunction))
        {
            Settings.CtrlFunction = true;
        }

        if (listing_Standard.RadioButton("Fluffy.ResearchTree.CtrlFunctionZoom".Translate(), !Settings.CtrlFunction))
        {
            Settings.CtrlFunction = false;
        }

        listing_Standard.Gap();
        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.OverrideResearch".Translate(),
            ref Settings.OverrideResearch);
        if (Settings.OverrideResearch && Settings.LoadType == Constants.LoadTypeDoNotGenerateResearchTree)
        {
            Settings.LoadType = Constants.LoadTypeLoadInBackground;
        }
        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.ReverseShift".Translate(), ref Settings.ReverseShift, "Fluffy.ResearchTree.ReverseShiftTT".Translate());
        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.PauseOnOpen".Translate(), ref Settings.PauseOnOpen);
        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.ShowCompletion".Translate(), ref Settings.ShowCompletion);
        if (ModsConfig.IdeologyActive)
        {
            listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.NoIdeologyPopup".Translate(),
                ref Settings.NoIdeologyPopup, "Fluffy.ResearchTree.NoIdeologyPopupTT".Translate());
        }
        else
        {
            Settings.NoIdeologyPopup = false;
        }

        if (Assets.UsingWorldTechLevel || Assets.UsingRimedieval)
        {
            listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.HideNodesBlockedByTechLevel".Translate(),
                ref Settings.HideNodesBlockedByTechLevel);
        }

        var colorRect = listing_Standard.GetRect(30f);
        Widgets.Label(colorRect.LeftHalf(), "Fluffy.ResearchTree.BackgroundColor".Translate());
        Widgets.DrawBoxSolidWithOutline(colorRect.RightHalf().RightHalf(), Settings.BackgroundColor,
            Assets.WindowBgBorderColor, 2);
        if (Widgets.ButtonInvisible(colorRect.RightHalf().RightHalf()))
        {
            Find.WindowStack.Add(new Dialog_ColourPicker(Settings.BackgroundColor,
                color => { Settings.BackgroundColor = color; }));
        }

        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.SkipCompleted".Translate(), ref Settings.SkipCompleted);
        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.VisualGroupByTab".Translate(), ref Settings.VisualGroupByTab);
        if (Widgets.ButtonText(listing_Standard.GetRect(30f), "Fluffy.ResearchTree.RequestRebuild".Translate()))
        {
            //Tree.RequestRebuild(reason: "SettingsChanged");
        }

        listing_Standard.GapLine();
        if (listing_Standard.ButtonTextLabeledPct("Fluffy.ResearchTree.ResetLabel".Translate(),
                "Fluffy.ResearchTree.Reset".Translate(), 0.75f))
        {
            Settings.Reset();
        }

        listing_Standard.CheckboxLabeled("Fluffy.ResearchTree.VerboseLogging".Translate(), ref Settings.VerboseLogging);


        if (currentVersion != null)
        {
            listing_Standard.Gap();
            GUI.contentColor = Color.gray;
            listing_Standard.Label("Fluffy.ResearchTree.CurrentModVersion".Translate(currentVersion));
            GUI.contentColor = Color.white;
        }

        listing_Standard.NewColumn();

        // 右列标题
        listing_Standard.Label("Fluffy.ResearchTree.AllTabsCache".Translate());

        // 计算可视区域和内容高度
        float outRectHeight = rect.height - 80f; // 给顶部按钮等留一点空间
        Rect outRect = listing_Standard.GetRect(outRectHeight);
        Rect viewRect = new Rect(0f, 0f, outRect.width - 20f, (Settings.AllTabsCache?.Count ?? 0) * 28f);

        // 开始滚动视图
        Widgets.BeginScrollView(outRect, ref scrollPosTabs, viewRect);
        var ls2 = new Listing_Standard { ColumnWidth = viewRect.width };
        ls2.Begin(viewRect);

        if (Settings.AllTabsCache != null)
        {
            foreach (var tab in Settings.AllTabsCache)
            {
                if (tab == null) continue;

                bool on = Settings.IncludedTabs.Contains(tab.defName);
                var label = $"{tab.LabelCap} ({tab.modContentPack?.Name ?? "Core"})";
                ls2.CheckboxLabeled(label, ref on);
                if (on) Settings.IncludedTabs.Add(tab.defName);
                else Settings.IncludedTabs.Remove(tab.defName);
            }
        }

        ls2.End();
        Widgets.EndScrollView();

        listing_Standard.End();
    }
}