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

    public static readonly Vector2 IconSize = new Vector2(18f, 18f);

    public static readonly Vector2 LargeIconSize = new Vector2(64f, 64f);

    public static readonly Vector2 NodeMargins = new Vector2(50f, 10f);

    public static readonly Vector2 NodeSize = new Vector2(200f, 50f);

    public static readonly float TopBarHeight = NodeSize.y + 12f + 20f;

    public static readonly Vector2 TechLevelLabelSize = new Vector2(200f, QueueLabelSize);
}