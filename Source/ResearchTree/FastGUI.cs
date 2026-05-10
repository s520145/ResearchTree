using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FluffyResearchTree;

internal static class FastGUI
{
    private static readonly Type ArgsType = AccessTools.TypeByName("UnityEngine.Internal_DrawTextureArguments");
    private static readonly MethodInfo DrawTexMI = AccessTools.Method(typeof(Graphics), "Internal_DrawTexture");

    private static readonly FieldInfo FI_screenRect =
        ArgsType?.GetField("screenRect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo FI_texture =
        ArgsType?.GetField("texture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo FI_color =
        ArgsType?.GetField("color", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo FI_sourceRect =
        ArgsType?.GetField("sourceRect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo FI_mat =
        ArgsType?.GetField("mat", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo FI_borderWidths =
        ArgsType?.GetField("borderWidths", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo FI_cornerRadiuses =
        ArgsType?.GetField("cornerRadiuses", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly FieldInfo FI_smoothCorners =
        ArgsType?.GetField("smoothCorners", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

    private static readonly Rect FullRect01 = new(0f, 0f, 1f, 1f);

    [ThreadStatic] private static object _argsBoxed;
    [ThreadStatic] private static object[] _invokeBuf;

    static FastGUI()
    {
        if (ArgsType == null || DrawTexMI == null ||
            FI_screenRect == null || FI_texture == null || FI_color == null ||
            FI_sourceRect == null || FI_mat == null || FI_borderWidths == null ||
            FI_cornerRadiuses == null || FI_smoothCorners == null)
        {
            throw new InvalidOperationException("Unable to access Unity's Internal_DrawTexture members.");
        }
    }

    private static object PrepareArgs(Rect position, Texture image, Rect? src, Color tint)
    {
        if (_argsBoxed == null)
        {
            _argsBoxed = Activator.CreateInstance(ArgsType);
            FI_mat.SetValue(_argsBoxed, Assets.RoundedRectMaterial);
            FI_borderWidths.SetValue(_argsBoxed, Vector4.zero);
            FI_cornerRadiuses.SetValue(_argsBoxed, Vector4.zero);
            FI_smoothCorners.SetValue(_argsBoxed, false);
        }

        FI_screenRect.SetValue(_argsBoxed, position);
        FI_texture.SetValue(_argsBoxed, image);
        FI_color.SetValue(_argsBoxed, tint);
        FI_sourceRect.SetValue(_argsBoxed, src ?? FullRect01);

        return _argsBoxed;
    }

    public static void DrawTextureFast(Rect position, Texture image, Color color = new())
    {
        if (DrawTexMI == null)
        {
            throw new InvalidOperationException("Unable to access Unity's internal methods.");
        }

        var tint = color == new Color() ? GUI.color : color;
        var buffer = _invokeBuf ??= new object[1];
        buffer[0] = PrepareArgs(position, image, null, tint);
        DrawTexMI.Invoke(null, buffer);
    }

    public static void DrawTextureFastWithCoords(Rect position, Texture image, Rect rect, Color color = new())
    {
        if (DrawTexMI == null)
        {
            throw new InvalidOperationException("Unable to access Unity's internal methods.");
        }

        var tint = color == new Color() ? GUI.color : color;
        var buffer = _invokeBuf ??= new object[1];
        buffer[0] = PrepareArgs(position, image, rect, tint);
        DrawTexMI.Invoke(null, buffer);
    }
}
