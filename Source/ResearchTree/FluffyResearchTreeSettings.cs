using Verse;

namespace FluffyResearchTree;

/// <summary>
///     Definition of the settings for the mod
/// </summary>
internal class FluffyResearchTreeSettings : ModSettings
{
    public bool CtrlFunction = true;
    public int LoadType = 1;
    public bool PauseOnOpen = true;
    public bool ShowCompletion;
    public bool VanillaGraphics;
    public bool OverrideResearch = true;

    /// <summary>
    ///     Saving and loading the values
    /// </summary>
    public override void ExposeData()
    {
        base.ExposeData();
        Scribe_Values.Look(ref PauseOnOpen, "PauseOnOpen", true);
        Scribe_Values.Look(ref CtrlFunction, "CtrlFunction", true);
        Scribe_Values.Look(ref OverrideResearch, "OverrideResearch", true);
        Scribe_Values.Look(ref VanillaGraphics, "VanillaGraphics");
        Scribe_Values.Look(ref ShowCompletion, "ShowCompletion");
        Scribe_Values.Look(ref LoadType, "LoadType", 1);
    }
}