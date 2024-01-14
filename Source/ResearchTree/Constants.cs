// Constants.cs
// Copyright Karel Kroeze, 2018-2020

using System;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public static class Constants
{
    public const double Epsilon = 0.0001;

    public static readonly float DetailedModeZoomLevelCutoff = 1.5f;

    public static readonly float AbsoluteMaxZoomLevel = 2.9f;

    public static readonly float ZoomStep = 0.05f;

    public static readonly float HubSize = 16f / Prefs.UIScale;

    public static readonly float Margin = 6f / Prefs.UIScale;

    public static readonly float QueueLabelSize = 30f / Prefs.UIScale;

    public static readonly float SmallQueueLabelSize = 20f / Prefs.UIScale;

    public static readonly Vector2 IconSize = new Vector2(18f / Prefs.UIScale, 18f / Prefs.UIScale);

    public static readonly Vector2 LargeIconSize = new Vector2(64f / Prefs.UIScale, 64f / Prefs.UIScale);

    public static readonly Vector2 NodeMargins = new Vector2(50f / Prefs.UIScale, 10f / Prefs.UIScale);

    public static readonly Vector2 NodeSize =
        new Vector2(Math.Max(200f / Prefs.UIScale, 150f), Math.Max(50f / Prefs.UIScale, 40f));


    public static readonly float TopBarHeight = NodeSize.y + (12f / Prefs.UIScale) + (20f / Prefs.UIScale);

    public static readonly Vector2 TechLevelLabelSize = new Vector2(200f / Prefs.UIScale, QueueLabelSize);
}