using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace FluffyResearchTree
{
    internal static class FastGUI
    {
        // 缓存类型与方法
        private static readonly Type ArgsType = AccessTools.TypeByName("UnityEngine.Internal_DrawTextureArguments");
        private static readonly MethodInfo DrawTexMI = AccessTools.Method(typeof(Graphics), "Internal_DrawTexture");

        // 缓存字段（只解析一次）
        private static readonly FieldInfo FI_screenRect = ArgsType?.GetField("screenRect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo FI_texture = ArgsType?.GetField("texture", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo FI_color = ArgsType?.GetField("color", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo FI_sourceRect = ArgsType?.GetField("sourceRect", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo FI_mat = ArgsType?.GetField("mat", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo FI_borderWidths = ArgsType?.GetField("borderWidths", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo FI_cornerRadiuses = ArgsType?.GetField("cornerRadiuses", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
        private static readonly FieldInfo FI_smoothCorners = ArgsType?.GetField("smoothCorners", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        private static readonly Rect FullRect01 = new Rect(0f, 0f, 1f, 1f);

        // 线程局部：复用装箱参数与 Invoke 缓冲
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

        // 仅首用创建装箱对象；每次只更新会变化的字段
        private static object PrepareArgs(Rect position, Texture image, Rect? src, Color tint)
        {
            if (_argsBoxed == null)
            {
                _argsBoxed = Activator.CreateInstance(ArgsType);
                // 与原行为一致：使用 RoundedRect 材质，不改其属性，避免任何“粘色”
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
            if (DrawTexMI == null) throw new InvalidOperationException("Unable to access Unity's internal methods.");

            // 语义保持：未传色则使用 GUI.color（不改 GUI.color，也不做额外乘色）
            Color tint = (color == new Color()) ? GUI.color : color;

            var boxed = PrepareArgs(position, image, null, tint);
            var buf = _invokeBuf ??= new object[1];
            buf[0] = boxed;
            DrawTexMI.Invoke(null, buf);
        }

        public static void DrawTextureFastWithCoords(Rect position, Texture image, Rect rect, Color color = new())
        {
            if (DrawTexMI == null) throw new InvalidOperationException("Unable to access Unity's internal methods.");

            Color tint = (color == new Color()) ? GUI.color : color;

            var boxed = PrepareArgs(position, image, rect, tint);
            var buf = _invokeBuf ??= new object[1];
            buf[0] = boxed;
            DrawTexMI.Invoke(null, buf);
        }
    }
}
