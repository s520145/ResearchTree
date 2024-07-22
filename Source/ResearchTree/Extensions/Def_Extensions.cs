// Def_Extensions.cs
// Copyright Karel Kroeze, 2018-2020

using System.Collections.Generic;
using System.Linq;
using RimWorld;
using UnityEngine;
using Verse;

namespace FluffyResearchTree;

public static class Def_Extensions
{
    private static readonly Dictionary<Def, Texture2D> _cachedDefIcons = new Dictionary<Def, Texture2D>();

    private static readonly Dictionary<Def, Color> _cachedIconColors = new Dictionary<Def, Color>();

    public static void DrawColouredIcon(this Def def, Rect canvas)
    {
        GUI.color = def.IconColor();
        GUI.DrawTexture(canvas, def.IconTexture(), ScaleMode.ScaleToFit);
        GUI.color = Color.white;
    }

    public static Color IconColor(this Def def)
    {
        if (def == null)
        {
            return Color.cyan;
        }

        if (_cachedIconColors.TryGetValue(def, out var color))
        {
            return color;
        }

        var buildableDef = def as BuildableDef;
        var thingDef = def as ThingDef;
        var pawnKindDef = def as PawnKindDef;
        if (def is RecipeDef recipeDef && !recipeDef.products.NullOrEmpty())
        {
            _cachedIconColors.Add(def, recipeDef.products.First().thingDef.IconColor());
            return _cachedIconColors[def];
        }

        if (pawnKindDef != null)
        {
            _cachedIconColors.Add(def, pawnKindDef.lifeStages.Last().bodyGraphicData.color);
            return _cachedIconColors[def];
        }

        if (buildableDef == null)
        {
            _cachedIconColors.Add(def, Color.white);
            return _cachedIconColors[def];
        }

        if (thingDef is { entityDefToBuild: not null })
        {
            _cachedIconColors.Add(def, thingDef.entityDefToBuild.IconColor());
            return _cachedIconColors[def];
        }

        if (buildableDef.graphic != null)
        {
            _cachedIconColors.Add(def, buildableDef.graphic.color);
            return _cachedIconColors[def];
        }

        if (thingDef is { MadeFromStuff: true })
        {
            var thingDef2 = GenStuff.DefaultStuffFor(thingDef);
            _cachedIconColors.Add(def, thingDef2.stuffProps.color);
            return _cachedIconColors[def];
        }

        _cachedIconColors.Add(def, Color.white);
        return _cachedIconColors[def];
    }

    public static Texture2D IconTexture(this Def def)
    {
        if (def == null)
        {
            return null;
        }

        if (_cachedDefIcons.TryGetValue(def, out var texture))
        {
            return texture;
        }

        var buildableDef = def as BuildableDef;
        var thingDef = def as ThingDef;
        var pawnKindDef = def as PawnKindDef;
        if (def is RecipeDef recipeDef && !recipeDef.products.NullOrEmpty())
        {
            _cachedDefIcons.Add(def, recipeDef.products.First().thingDef.IconTexture());
            return _cachedDefIcons[def];
        }

        if (pawnKindDef != null)
        {
            try
            {
                _cachedDefIcons.Add(def,
                    pawnKindDef.lifeStages.Last().bodyGraphicData.Graphic.MatSouth.mainTexture as Texture2D);
                return _cachedDefIcons[def];
            }
            catch
            {
                // ignored
            }
        }

        if (buildableDef != null)
        {
            if (thingDef?.entityDefToBuild != null)
            {
                _cachedDefIcons.Add(def, thingDef.entityDefToBuild.IconTexture());
                return _cachedDefIcons[def];
            }

            _cachedDefIcons.Add(def, buildableDef.uiIcon);
            return buildableDef.uiIcon;
        }

        _cachedDefIcons.Add(def, null);
        return null;
    }
}