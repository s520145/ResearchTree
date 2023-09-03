using System.Linq;
using RimWorld;
using Verse;

namespace FluffyResearchTree;

public static class Building_ResearchBench_Extensions
{
    public static bool HasFacility(this Building_ResearchBench building, ThingDef facility)
    {
        var comp = building.GetComp<CompAffectedByFacilities>();
        return comp != null && comp.LinkedFacilitiesListForReading.Select(f => f.def).Contains(facility);
    }
}