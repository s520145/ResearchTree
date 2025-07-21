// Logging.cs
// Copyright Karel Kroeze, 2018-2020

using Verse;

namespace FluffyResearchTree;

public static class Logging
{
    public static void Message(string message)
    {
        if (FluffyResearchTreeMod.instance.Settings.VerboseLogging)
        {
            Log.Message(format(message));
        }
    }

    public static void Warning(string message, bool once = false)
    {
        if (once)
        {
            Log.WarningOnce(format(message), message.GetHashCode());
            return;
        }

        Log.Warning(format(message));
    }


    public static void Error(string message, bool once = false)
    {
        if (once)
        {
            Log.ErrorOnce(format(message), message.GetHashCode());
            return;
        }

        Log.Error(format(message));
    }

    private static string format(string message)
    {
        return $"[ResearchTree]: {message}";
    }
}