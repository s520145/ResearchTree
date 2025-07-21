using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FluffyResearchTree;

internal static class FastGUI
{
    private static readonly Type UnityInternalDrawTextureArgumentsType =
        AccessTools.TypeByName("UnityEngine.Internal_DrawTextureArguments");

    private static readonly MethodInfo UnityInternalDrawTextureMethod =
        AccessTools.Method(typeof(Graphics), "Internal_DrawTexture");

    private static object createInternalDrawTextureArguments(Rect position, Texture image, Rect? sourceRect,
        Color color)
    {
        if (UnityInternalDrawTextureArgumentsType == null)
        {
            throw new InvalidOperationException("Unable to access Unity's internal types.");
        }

        var unityDrawArgs = Activator.CreateInstance(UnityInternalDrawTextureArgumentsType);

        UnityInternalDrawTextureArgumentsType.GetField("screenRect")?.SetValue(unityDrawArgs, position);
        UnityInternalDrawTextureArgumentsType.GetField("texture")?.SetValue(unityDrawArgs, image);
        UnityInternalDrawTextureArgumentsType.GetField("color")?.SetValue(unityDrawArgs, color);
        UnityInternalDrawTextureArgumentsType.GetField("sourceRect")
            ?.SetValue(unityDrawArgs, sourceRect ?? new Rect(0f, 0f, 1f, 1f));
        UnityInternalDrawTextureArgumentsType.GetField("mat")?.SetValue(unityDrawArgs, Assets.RoundedRectMaterial);
        UnityInternalDrawTextureArgumentsType.GetField("borderWidths")?.SetValue(unityDrawArgs, Vector4.zero);
        UnityInternalDrawTextureArgumentsType.GetField("cornerRadiuses")?.SetValue(unityDrawArgs, Vector4.zero);
        UnityInternalDrawTextureArgumentsType.GetField("smoothCorners")?.SetValue(unityDrawArgs, false);

        return unityDrawArgs;
    }

    public static void DrawTextureFast(Rect position, Texture image, Color color = new())
    {
        if (UnityInternalDrawTextureMethod == null)
        {
            throw new InvalidOperationException("Unable to access Unity's internal methods.");
        }

        if (color == new Color())
        {
            color = GUI.color;
        }

        var unityDrawArgs = createInternalDrawTextureArguments(position, image, null, color);
        UnityInternalDrawTextureMethod.Invoke(null, [unityDrawArgs]);
    }

    public static void DrawTextureFastWithCoords(Rect position, Texture image, Rect rect, Color color = new())
    {
        if (UnityInternalDrawTextureMethod == null)
        {
            throw new InvalidOperationException("Unable to access Unity's internal methods.");
        }

        if (color == new Color())
        {
            color = GUI.color;
        }

        var unityDrawArgs = createInternalDrawTextureArguments(position, image, rect, color);
        UnityInternalDrawTextureMethod.Invoke(null, [unityDrawArgs]);
    }
}