using System;
using System.Collections.Generic;
using UnityEngine;

namespace DuckovPad
{
    /// <summary>
    /// Shared look-up for controller button glyphs, plus a couple of texture helpers.
    /// The prompt bar renders these as real UI images; the optional debug overlay uses
    /// the flat colours directly.
    /// </summary>
    internal static class PadGui
    {
        public static readonly Color Text = new Color(0.92f, 0.94f, 0.97f, 1f);
        public static readonly Color TextDim = new Color(0.64f, 0.68f, 0.76f, 1f);
        public static readonly Color Accent = new Color(0.95f, 0.76f, 0.31f, 1f);
        public static readonly Color Neutral = new Color(0.24f, 0.27f, 0.33f, 1f);
        public static readonly Color Panel = new Color(0.07f, 0.08f, 0.11f, 0.92f);

        public struct GlyphInfo
        {
            public string Label;
            public Color Color;
            public float WidthFactor;
            /// <summary>0 = use the text label. 1-4 = draw an arrow (up, right, down, left).</summary>
            public int Arrow;
        }

        private struct Glyph
        {
            public string Xbox;
            public string PlayStation;
            public Color Color;
            public float WidthFactor;
            public int Arrow;
        }

        // Labels stay plain ASCII on purpose. The prompt bar borrows whatever font the game
        // is using, and there is no guarantee it covers ✕ ○ □ △ ≡ — a missing glyph would
        // render as an empty box. D-pad directions are drawn as geometry instead.
        private static readonly Dictionary<string, Glyph> Glyphs =
            new Dictionary<string, Glyph>(StringComparer.OrdinalIgnoreCase)
            {
                { "buttonSouth",     new Glyph { Xbox = "A",    PlayStation = "A",   Color = new Color(0.42f, 0.75f, 0.29f), WidthFactor = 1f } },
                { "buttonEast",      new Glyph { Xbox = "B",    PlayStation = "B",   Color = new Color(0.88f, 0.29f, 0.25f), WidthFactor = 1f } },
                { "buttonWest",      new Glyph { Xbox = "X",    PlayStation = "X",   Color = new Color(0.24f, 0.56f, 0.88f), WidthFactor = 1f } },
                { "buttonNorth",     new Glyph { Xbox = "Y",    PlayStation = "Y",   Color = new Color(0.91f, 0.71f, 0.17f), WidthFactor = 1f } },
                { "leftShoulder",    new Glyph { Xbox = "LB",   PlayStation = "L1",  Color = Neutral, WidthFactor = 1.6f } },
                { "rightShoulder",   new Glyph { Xbox = "RB",   PlayStation = "R1",  Color = Neutral, WidthFactor = 1.6f } },
                { "leftTrigger",     new Glyph { Xbox = "LT",   PlayStation = "L2",  Color = Neutral, WidthFactor = 1.6f } },
                { "rightTrigger",    new Glyph { Xbox = "RT",   PlayStation = "R2",  Color = Neutral, WidthFactor = 1.6f } },
                { "leftStickPress",  new Glyph { Xbox = "L3",   PlayStation = "L3",  Color = Neutral, WidthFactor = 1.6f } },
                { "leftStick",       new Glyph { Xbox = "LS",   PlayStation = "LS",  Color = Neutral, WidthFactor = 1.6f } },
                { "rightStickPress", new Glyph { Xbox = "R3",   PlayStation = "R3",  Color = Neutral, WidthFactor = 1.6f } },
                { "start",           new Glyph { Xbox = "MENU", PlayStation = "OPT", Color = Neutral, WidthFactor = 2.6f } },
                { "select",          new Glyph { Xbox = "VIEW", PlayStation = "SHR", Color = Neutral, WidthFactor = 2.6f } },
                { "dpad/up",         new Glyph { Color = Neutral, WidthFactor = 1f, Arrow = 1 } },
                { "dpad/right",      new Glyph { Color = Neutral, WidthFactor = 1f, Arrow = 2 } },
                { "dpad/down",       new Glyph { Color = Neutral, WidthFactor = 1f, Arrow = 3 } },
                { "dpad/left",       new Glyph { Color = Neutral, WidthFactor = 1f, Arrow = 4 } },
            };

        public static GlyphInfo GetGlyphInfo(string canonicalName, bool playStation)
        {
            if (Glyphs.TryGetValue(canonicalName ?? string.Empty, out var glyph))
            {
                return new GlyphInfo
                {
                    Label = playStation ? glyph.PlayStation : glyph.Xbox,
                    Color = glyph.Color,
                    WidthFactor = glyph.WidthFactor,
                    Arrow = glyph.Arrow
                };
            }

            return new GlyphInfo
            {
                Label = canonicalName ?? "?",
                Color = Neutral,
                WidthFactor = 2.2f,
                Arrow = 0
            };
        }

        /// <summary>Rotation, in degrees, for an arrow glyph index.</summary>
        public static float ArrowRotation(int arrow)
        {
            switch (arrow)
            {
                case 2: return -90f;  // right
                case 3: return 180f;  // down
                case 4: return 90f;   // left
                default: return 0f;   // up
            }
        }

        // ------------------------------------------------------------------

        private static Texture2D _pixel;
        private static Sprite _arrowSprite;

        public static Texture2D Pixel
        {
            get
            {
                if (_pixel == null)
                {
                    _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false) { hideFlags = HideFlags.HideAndDontSave };
                    _pixel.SetPixel(0, 0, Color.white);
                    _pixel.Apply();
                }
                return _pixel;
            }
        }

        /// <summary>Filled triangle pointing up; rotated by the caller for other directions.</summary>
        public static Sprite ArrowSprite
        {
            get
            {
                if (_arrowSprite != null) return _arrowSprite;

                const int size = 32;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    hideFlags = HideFlags.HideAndDontSave,
                    filterMode = FilterMode.Bilinear,
                    wrapMode = TextureWrapMode.Clamp
                };

                var pixels = new Color32[size * size];
                for (int y = 0; y < size; y++)
                {
                    // Row 0 is the bottom; the triangle narrows toward the top.
                    float t = y / (float)(size - 1);
                    float halfWidth = Mathf.Lerp(size * 0.45f, 0f, t);
                    float centre = size * 0.5f;

                    for (int x = 0; x < size; x++)
                    {
                        float distance = Mathf.Abs(x + 0.5f - centre);
                        float alpha = Mathf.Clamp01(halfWidth - distance);
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply();

                _arrowSprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
                return _arrowSprite;
            }
        }

        // ---- minimal IMGUI helpers, used only by the optional debug overlay ----

        public static void Fill(Rect rect, Color color)
        {
            var previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Pixel);
            GUI.color = previous;
        }
    }
}
