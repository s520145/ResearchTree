using System;
using System.Collections.Generic;
using System.Linq;
using Verse;

namespace FluffyResearchTree;

internal class ResearchTabProfile : IExposable
{
    public string Id;
    public string Name;
    public HashSet<string> IncludedTabs = new(StringComparer.OrdinalIgnoreCase);

    public ResearchTabProfile()
    {
    }

    public ResearchTabProfile(string id, string name, IEnumerable<string> includedTabs)
    {
        Id = id;
        Name = name;
        IncludedTabs = new HashSet<string>(includedTabs ?? Enumerable.Empty<string>(), StringComparer.OrdinalIgnoreCase);
    }

    public void ExposeData()
    {
        Scribe_Values.Look(ref Id, "id");
        Scribe_Values.Look(ref Name, "name");
        Scribe_Collections.Look(ref IncludedTabs, "includedTabs", LookMode.Value);
        IncludedTabs ??= new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    }

    public ResearchTabProfileData ToData()
    {
        return new ResearchTabProfileData(Id, Name, IncludedTabs);
    }

    public static ResearchTabProfile FromData(ResearchTabProfileData data)
    {
        return new ResearchTabProfile(data.Id, data.Name, data.IncludedTabs);
    }
}
