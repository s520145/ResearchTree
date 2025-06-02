// Constants.cs
// Copyright Karel Kroeze, 2018-2020

using UnityEngine;

namespace FluffyResearchTree;

public static class Constants
{
    public const double Epsilon = 0.0001;

    public const float HubSize = 16f;

    public const float DetailedModeZoomLevelCutoff = 1.5f;

    public const float Margin = 6f;

    public const float QueueLabelSize = 30f;

    public const float SmallQueueLabelSize = 20f;

    public const float AbsoluteMaxZoomLevel = 2.9f;

    public const float ZoomStep = 0.05f;

    public const int LeftClick = 0;

    public const int RightClick = 1;

    public const int LoadTypeLoadInBackground = 1;
    public const int LoadTypeFirstTimeOpening = 2;
    public const int LoadTypeDoNotGenerateResearchTree = 3;

    public static readonly Vector2 IconSize = new(18f, 18f);

    public static readonly Vector2 LargeIconSize = new(64f, 64f);

    public static readonly Vector2 NodeMargins = new(50f, 10f);

    public static readonly Vector2 NodeSize = new(200f, 50f);

    public static readonly float TopBarHeight = NodeSize.y + 12f + 20f;

    public static readonly Vector2 TechLevelLabelSize = new(200f, QueueLabelSize);
}