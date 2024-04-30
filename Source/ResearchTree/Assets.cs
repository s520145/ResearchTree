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

    public static readonly Texture2D Search;

    public static bool RefreshResearch;

    public static int TotalAmountOfResearch;

    public static readonly bool UsingVanillaVehiclesExpanded;

    public static readonly bool UsingVanillaExpanded;

    public static readonly bool UsingRimedieval;

    public static readonly MethodInfo IsDisabledMethod;

    public static readonly MethodInfo TechLevelAllowedMethod;

    public static readonly MethodInfo GetAllowedProjectDefsMethod;

    public static readonly List<ResearchProjectDef> AllowedResearchDefs;

    public static Thread initializeWorker;

    static Assets()
    {
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
        Search = ContentFinder<Texture2D>.Get("Icons/magnifying-glass");
        UsingRimedieval =
            ModLister.GetActiveModWithIdentifier("Ogam.Rimedieval") != null;
        AllowedResearchDefs = [];

        if (UsingRimedieval)
        {
            var defCleanerType = AccessTools.TypeByName("Rimedieval.DefCleaner");
            if (defCleanerType == null)
            {
                Log.Warning(
                    "[FluffyResearchTree]: Failed to find the DefCleaner-type in Rimedieval. Will not be able to show or block research based on Rimedieval settings.");
                UsingRimedieval = false;
            }
            else
            {
                GetAllowedProjectDefsMethod = AccessTools.Method(defCleanerType, "GetAllowedProjectDefs",
                    [typeof(List<ResearchProjectDef>)]);
                if (GetAllowedProjectDefsMethod == null)
                {
                    Log.Warning(
                        "[FluffyResearchTree]: Failed to find method GetAllowedProjectDefs in Rimedieval. Will not be able to show or block research based on Rimedieval settings.");
                    UsingRimedieval = false;
                }
                else
                {
                    AllowedResearchDefs =
                        (List<ResearchProjectDef>)GetAllowedProjectDefsMethod.Invoke(null,
                        [
                            DefDatabase<ResearchProjectDef>.AllDefsListForReading.Where(def =>
                                def.knowledgeCategory == null)
                        ]);
                }
            }
        }

        UsingVanillaExpanded =
            ModLister.GetActiveModWithIdentifier("OskarPotocki.VanillaFactionsExpanded.Core") != null;
        if (UsingVanillaExpanded)
        {
            var storyTellerUtility = AccessTools.TypeByName("VanillaStorytellersExpanded.CustomStorytellerUtility");
            if (storyTellerUtility == null)
            {
                Log.Warning(
                    "[FluffyResearchTree]: Failed to find the CustomStorytellerUtility-type in VanillaExpanded. Will not be able to show or block research based on storyteller limitations.");
                UsingVanillaExpanded = false;
            }
            else
            {
                TechLevelAllowedMethod =
                    AccessTools.Method(storyTellerUtility, "TechLevelAllowed", [typeof(TechLevel)]);
                if (TechLevelAllowedMethod == null)
                {
                    Log.Warning(
                        "[FluffyResearchTree]: Failed to find method TechLevelAllowed in VanillaExpanded. Will not be able to show or block research based on storyteller limitations.");
                    UsingVanillaExpanded = false;
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
                Log.Warning(
                    "[FluffyResearchTree]: Failed to find the Utils-type in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                UsingVanillaVehiclesExpanded = false;
            }
            else
            {
                var utilsMethods = AccessTools.GetDeclaredMethods(utilsType);
                if (utilsMethods == null || !utilsMethods.Any())
                {
                    Log.Warning(
                        "[FluffyResearchTree]: Failed to find any methods in Utils in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                    UsingVanillaVehiclesExpanded = false;
                }
                else
                {
                    IsDisabledMethod =
                        utilsMethods.FirstOrDefault(methodInfo => methodInfo.GetParameters().Length == 2);
                    if (IsDisabledMethod == null)
                    {
                        Log.Warning(
                            "[FluffyResearchTree]: Failed to find any methods in Utils in VanillaVehiclesExpanded. Will not be able to show or block research based on non-restored vehicles.");
                        UsingVanillaVehiclesExpanded = false;
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

    public static void OpenResearchWindow()
    {
        if (!Event.current.shift && !Tree.Initialized && FluffyResearchTreeMod.instance.Settings.LoadType != 2)
        {
            Messages.Message("Fluffy.ResearchTree.StillLoading".Translate(), MessageTypeDefOf.RejectInput);
            return;
        }

        Find.MainTabsRoot.ToggleTab(Event.current.shift
            ? MainButtonDefOf.ResearchOriginal
            : RimWorld.MainButtonDefOf.Research);
    }

    public static void StartLoadingWorker()
    {
        initializeWorker = new Thread(Tree.Initialize);
        Log.Message("[ResearchTree]: Initialization start in background");
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
        public static MainButtonDef ResearchOriginal;
    }
}