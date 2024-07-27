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
            Log.Message(Format(message));
        }
    }

    public static void Warning(string message, bool once = false)
    {
        if (once)
        {
            Log.WarningOnce(Format(message), message.GetHashCode());
            return;
        }

        Log.Warning(Format(message));
    }


    public static void Error(string message, bool once = false)
    {
        if (once)
        {
            Log.ErrorOnce(Format(message), message.GetHashCode());
            return;
        }

        Log.Error(Format(message));
    }

    private static string Format(string message)
    {
        return $"[ResearchTree]: {message}";
    }
}