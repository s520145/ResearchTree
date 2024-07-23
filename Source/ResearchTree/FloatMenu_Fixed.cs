// FloatMenu_Fixed.cs
// Copyright Karel Kroeze, 2018-2020

using System.Collections.Generic;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public class FloatMenu_Fixed : FloatMenu
{
    private readonly Rect _screenLocation;

    /// <summary>
    ///     Constructor
    /// </summary>
    /// <param name="location">Menu location in screen coordinates</param>
    /// <param name="options">menu items</param>
    /// <remarks>
    ///     Menu will be shown at the edge of provided rect. Default edge is the right one, but it will be shown at the left if
    ///     there is not enough space at the right.
    ///     To obtain screen coordinates use GUIUtility.GUIToScreenRect method
    /// </remarks>
    public FloatMenu_Fixed(Rect location, List<FloatMenuOption> options) : base(options)
    {
        _screenLocation = location;
        vanishIfMouseDistant = false;
        onlyOneOfTypeAllowed = false;
        focusWhenOpened = false;
    }

    public override void SetInitialSizeAndPosition()
    {
        var menuSize = InitialSize;

        // trying to show menu at the right of provided rect
        var vector = new Vector2(_screenLocation.xMax, _screenLocation.yMin + Constants.SmallQueueLabelSize);
        if (vector.x + InitialSize.x > UI.screenWidth)
        {
            // but showing at the left if there is not enough space
            vector.x = _screenLocation.xMin - menuSize.x;
        }

        if (vector.y + InitialSize.y > UI.screenHeight)
        {
            vector.y = UI.screenHeight - menuSize.y;
        }

        windowRect = new Rect(vector.x, vector.y, menuSize.x, menuSize.y);
    }
}