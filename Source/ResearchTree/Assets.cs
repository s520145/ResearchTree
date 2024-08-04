// Assets.cs
// Copyright Karel Kroeze, 2018-2020

using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using HarmonyLib;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

[StaticConstructorOnStartup]
public static class Assets
{
    public static readonly Texture2D Button;

    public static readonly Texture2D ButtonActive;

    public static readonly Texture2D ResearchIcon;

    public static readonly Texture2D MoreIcon;

    public static readonly Texture2D Lock;

    internal static readonly Texture2D CircleFill;

    public static readonly Dictionary<TechLevel, Color> ColorCompleted;

    public static readonly Dictionary<TechLevel, Color> ColorAvailable;

    public static readonly Dictionary<TechLevel, Color> ColorUnavailable;

    public static Color TechLevelColor;

    public static readonly Texture2D SlightlyDarkBackground;

    public static bool RefreshResearch;

    public static int TotalAmountOfResearch;

    public static readonly bool UsingVanillaVehiclesExpanded;

    public static readonly bool UsingRimedieval;

    public static readonly bool UsingSOS2;

    public static readonly MethodInfo IsDisabledMethod;

    public static readonly MethodInfo GetAllowedProjectDefsMethod;

    public static readonly PropertyInfo Sos2WorldCompPropertyInfo;

    public static readonly FieldInfo Sos2UlocksFieldInfo;

    public static readonly List<ResearchProjectDef> AllowedResearchDefs;

    public static Thread initializeWorker;

    public static readonly bool BetterResearchTabLoaded;

    public static readonly bool SemiRandomResearchLoaded;

    public static readonly MainButtonDef BetterResearchTab;

    public static readonly bool OrganizedResearchTabLoaded;

    public static readonly MainButtonDef OrganizedResearchTab;

    public static readonly Texture2D SemiRandomTexture2D;

    static Assets()
    {
        if (ModLister.GetActiveModWithIdentifier("andery233xj.mod.BetterResearchTabs", true) != null)
        {
            BetterResearchTab = DefDatabase<MainButtonDef>.GetNamed("BetterResearchTab");
            BetterResearchTabLoaded = true;
        }

        if (ModLister.GetActiveModWithIdentifier("Mlie.OrganizedResearchTab", true) != null)
        {
            OrganizedResearchTab = DefDatabase<MainButtonDef>.GetNamed("OrganizedResearchTab");
            OrganizedResearchTabLoaded = true;
        }

        if (ModLister.GetActiveModWithIdentifier("CaptainMuscles.SemiRandomResearch.unofficial", true) != null)
        {
            SemiRandomTexture2D =
                ContentFinder<Texture2D>.Get("UI/Buttons/MainButtons/CM_Semi_Random_Research_ResearchTree");
            new Harmony("Mlie.ResearchTree").Patch(
                AccessTools.Method("CM_Semi_Random_Research.MainTabWindow_NextResearch:DrawGoToTechTreeButton"),
                new HarmonyMethod(SemiRandomResearch_DrawGoToTechTreeButton.Prefix));
            SemiRandomResearchLoaded = true;
        }

        Button = ContentFinder<Texture2D>.Get("Buttons/button");
        ButtonActive = ContentFinder<Texture2D>.Get("Buttons/button-active");
        ResearchIcon = ContentFinder<Texture2D>.Get("Icons/Research");
        MoreIcon = ContentFinder<Texture2D>.Get("Icons/more");
        Lock = ContentFinder<Texture2D>.Get("Icons/padlock");
        CircleFill = ContentFinder<Texture2D>.Get("Icons/circle-fill");
        ColorCompleted = new Dictionary<TechLevel, Color>();
        ColorAvailable = new Dictionary<TechLevel, Color>();
        ColorUnavailable = new Dictionary<TechLevel, Color>();
        TechLevelColor = new Color(1f, 1f, 1f, 0.2f);
        SlightlyDarkBackground = SolidColorMaterials.NewSolidColorTexture(0f, 0f, 0f, 0.1f);
        AllowedResearchDefs = [];

        UsingRimedieval =
            ModLister.GetActiveModWithIdentifier("Ogam.Rimedieval") != null;
        if (UsingRimedieval)
        {
            var defCleanerType = AccessTools.TypeByName("Rimedieval.DefCleaner");
            if (defCleanerType == null)
            {
                Logging.Warning(
                    "Failed to find the DefCleaner-type in Rimedieval. Will not be able to show or block research based on Rimedieval settings.");
                UsingRimedieval = false;
            }
            else
            {
                GetAllowedProjectDefsMethod = AccessTools.Method(defCleanerType, "GetAllowedProjectDefs",
                    [typeof(List<ResearchProjectDef>)]);
                if (GetAllowedProjectDefsMethod == null)
                {
                    Logging.Warning(
                        "Failed to find method GetAllowedProjectDefs in Rimedieval. Will not be able to show or block research based on Rimedieval settings.");
                    UsingRimedieval = false;
                }
                else
                {
                    AllowedResearchDefs =
                        (List<ResearchProjectDef>)GetAllowedProjectDefsMethod.Invoke(null,
                        [
                            DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(def => !def.IsAnomalyResearch())
                        ]);
                }
            }
        }

        UsingVanillaVehiclesExpanded =
            ModLister.GetActiveModWithIdentifier("OskarPotocki.VanillaVehiclesExpanded") != null;
        if (UsingVanillaVehiclesExpanded)
        {
            var utilsType = AccessTools.TypeByName("VanillaVehiclesExpanded.Utils");
            if (utilsType == null)
            {
                Logging.Warning(
                    "Failed to find the Utils-type in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                UsingVanillaVehiclesExpanded = false;
            }
            else
            {
                var utilsMethods = AccessTools.GetDeclaredMethods(utilsType);
                if (utilsMethods == null || !utilsMethods.Any())
                {
                    Logging.Warning(
                        "Failed to find any methods in Utils in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                    UsingVanillaVehiclesExpanded = false;
                }
                else
                {
                    IsDisabledMethod =
                        utilsMethods.FirstOrDefault(methodInfo => methodInfo.GetParameters().Length == 2);
                    if (IsDisabledMethod == null)
                    {
                        Logging.Warning(
                            "Failed to find any methods in Utils in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                        UsingVanillaVehiclesExpanded = false;
                    }
                }
            }
        }


        UsingSOS2 =
            ModLister.GetActiveModWithIdentifier("kentington.saveourship2") != null;
        if (UsingSOS2)
        {
            var shipInteriorType = AccessTools.TypeByName("SaveOurShip2.ShipInteriorMod2");
            if (shipInteriorType == null)
            {
                Logging.Warning(
                    "Failed to find the ShipInteriorType-type in SOS2. Will not be able to show or block research based on ArchotechSpore.");
                UsingSOS2 = false;
            }
            else
            {
                Sos2WorldCompPropertyInfo = AccessTools.Property(shipInteriorType, "WorldComp");
                if (Sos2WorldCompPropertyInfo == null)
                {
                    Logging.Warning(
                        "Failed to find method ShipWorldComp in ShipInteriorMod2 in SOS2. Will not be able to show or block research based on ArchotechSpore.");
                    UsingSOS2 = false;
                }
                else
                {
                    var shipWorldCompType = AccessTools.TypeByName("SaveOurShip2.ShipWorldComp");
                    if (shipWorldCompType == null)
                    {
                        Logging.Warning(
                            "Failed to find type shipWorldCompType in ShipInteriorMod2 in SOS2. Will not be able to show or block research based on ArchotechSpore.");
                        UsingSOS2 = false;
                    }
                    else
                    {
                        Sos2UlocksFieldInfo = AccessTools.Field(shipWorldCompType, "Unlocks");
                        if (Sos2UlocksFieldInfo == null)
                        {
                            Logging.Warning(
                                "Failed to find field Sos2UlocksFieldInfo in ShipInteriorMod2 in SOS2. Will not be able to show or block research based on ArchotechSpore.");
                            UsingSOS2 = false;
                        }
                    }
                }
            }
        }

        var relevantTechLevels = Tree.RelevantTechLevels;
        var count = relevantTechLevels.Count;
        for (var i = 0; i < count; i++)
        {
            ColorCompleted[relevantTechLevels[i]] = Color.HSVToRGB(1f / count * i, 0.75f, 0.75f);
            ColorAvailable[relevantTechLevels[i]] = Color.HSVToRGB(1f / count * i, 0.33f, 0.33f);
            ColorUnavailable[relevantTechLevels[i]] = Color.HSVToRGB(1f / count * i, 0.125f, 0.33f);
        }

        if (FluffyResearchTreeMod.instance.Settings.LoadType == 1)
        {
            LongEventHandler.QueueLongEvent(StartLoadingWorker, "ResearchPal.BuildingResearchTreeAsync", true, null);
        }
    }

    public static bool IsBlockedBySOS2(ResearchProjectDef researchProject)
    {
        if (!UsingSOS2)
        {
            return false;
        }

        if (researchProject.tab is not { defName: "ResearchTabArchotech" })
        {
            return false;
        }

        var worldComp = Sos2WorldCompPropertyInfo.GetValue(null, null);
        if (worldComp == null)
        {
            return false;
        }

        var unlocks = (List<string>)Sos2UlocksFieldInfo.GetValue(worldComp);
        if (unlocks == null)
        {
            return false;
        }

        if (!unlocks.Any())
        {
            return true;
        }

        return !unlocks.Contains("ArchotechUplink");
    }

    public static void StartLoadingWorker()
    {
        initializeWorker = new Thread(Tree.Initialize);
        Logging.Message("Initialization start in background");
        initializeWorker.Start();
    }


    [StaticConstructorOnStartup]
    public static class Lines
    {
        public static readonly Texture2D Circle = ContentFinder<Texture2D>.Get("Lines/Outline/circle");

        public static readonly Texture2D End = ContentFinder<Texture2D>.Get("Lines/Outline/end");

        public static readonly Texture2D EW = ContentFinder<Texture2D>.Get("Lines/Outline/ew");

        public static readonly Texture2D NS = ContentFinder<Texture2D>.Get("Lines/Outline/ns");
    }


    [DefOf]
    public static class MainButtonDefOf
    {
        public static MainButtonDef FluffyResearchTree;
    }
}