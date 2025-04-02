using System.Threading;
using Verse;

namespace FluffyResearchTree;

public class TreeInitializeGameComponent : GameComponent
{
    public static Thread initializeWorker;

    public TreeInitializeGameComponent()
    {
    }

    public TreeInitializeGameComponent(Game game)
    {
    }

    public override void FinalizeInit()
    {
        if (FluffyResearchTreeMod.instance.Settings.LoadType == Constants.LoadTypeLoadInBackground)
        {
            LongEventHandler.QueueLongEvent(StartLoadingWorker, "ResearchPal.BuildingResearchTreeAsync", true, null);
        }
    }

    public static void StartLoadingWorker()
    {
        initializeWorker = new Thread(Tree.Initialize);
        Logging.Message("Initialization start in background");
        initializeWorker.Start();
    }
}