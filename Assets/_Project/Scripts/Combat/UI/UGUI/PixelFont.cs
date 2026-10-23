using System.Collections.Generic;
using UnityEngine;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// A 5x7 bitmap font drawn by code (no font asset): upper-case letters, digits and a little punctuation. The glyph atlas is
    /// generated once at runtime as a point-filtered texture, so text stays razor sharp at the HUD's integer scales.
    /// </summary>
    public static class PixelFont
    {
        public const int GlyphWidth = 5;
        public const int GlyphHeight = 7;

        /// <summary>Horizontal advance per character (glyph + 1 px gap).</summary>
        public const int Advance = 6;

        /// <summary>Vertical advance per line (glyph + 2 px gap).</summary>
        public const int LineHeight = 9;

        private const int Cell = 8;
        private const int Columns = 16;

        // Each entry: the character, then seven rows of five pixels ('#' = on).
        private static readonly string[] Glyphs =
        {
            "A|.###.|#...#|#...#|#####|#...#|#...#|#...#", "B|####.|#...#|#...#|####.|#...#|#...#|####.",
            "C|.###.|#...#|#....|#....|#....|#...#|.###.", "D|####.|#...#|#...#|#...#|#...#|#...#|####.",
            "E|#####|#....|#....|####.|#....|#....|#####", "F|#####|#....|#....|####.|#....|#....|#....",
            "G|.###.|#...#|#....|#.###|#...#|#...#|.###.", "H|#...#|#...#|#...#|#####|#...#|#...#|#...#",
            "I|#####|..#..|..#..|..#..|..#..|..#..|#####", "J|..###|...#.|...#.|...#.|...#.|#..#.|.##..",
            "K|#...#|#..#.|#.#..|##...|#.#..|#..#.|#...#", "L|#....|#....|#....|#....|#....|#....|#####",
            "M|#...#|##.##|#.#.#|#.#.#|#...#|#...#|#...#", "N|#...#|##..#|#.#.#|#..##|#...#|#...#|#...#",
            "O|.###.|#...#|#...#|#...#|#...#|#...#|.###.", "P|####.|#...#|#...#|####.|#....|#....|#....",
            "Q|.###.|#...#|#...#|#...#|#.#.#|#..#.|.##.#", "R|####.|#...#|#...#|####.|#.#..|#..#.|#...#",
            "S|.####|#....|#....|.###.|....#|....#|####.", "T|#####|..#..|..#..|..#..|..#..|..#..|..#..",
            "U|#...#|#...#|#...#|#...#|#...#|#...#|.###.", "V|#...#|#...#|#...#|#...#|#...#|.#.#.|..#..",
            "W|#...#|#...#|#...#|#.#.#|#.#.#|##.##|#...#", "X|#...#|#...#|.#.#.|..#..|.#.#.|#...#|#...#",
            "Y|#...#|#...#|.#.#.|..#..|..#..|..#..|..#..", "Z|#####|....#|...#.|..#..|.#...|#....|#####",
            "0|.###.|#...#|#..##|#.#.#|##..#|#...#|.###.", "1|..#..|.##..|..#..|..#..|..#..|..#..|.###.",
            "2|.###.|#...#|....#|...#.|..#..|.#...|#####", "3|.###.|#...#|....#|..##.|....#|#...#|.###.",
            "4|...#.|..##.|.#.#.|#..#.|#####|...#.|...#.", "5|#####|#....|####.|....#|....#|#...#|.###.",
            "6|..##.|.#...|#....|####.|#...#|#...#|.###.", "7|#####|....#|...#.|..#..|.#...|.#...|.#...",
            "8|.###.|#...#|#...#|.###.|#...#|#...#|.###.", "9|.###.|#...#|#...#|.####|....#|...#.|.##..",
            "!|..#..|..#..|..#..|..#..|..#..|.....|..#..", "?|.###.|#...#|....#|...#.|..#..|.....|..#..",
            ":|.....|..#..|.....|.....|.....|..#..|.....", ".|.....|.....|.....|.....|.....|.....|..#..",
            ",|.....|.....|.....|.....|..##.|...#.|..#..", "-|.....|.....|.....|#####|.....|.....|.....",
            "+|.....|..#..|..#..|#####|..#..|..#..|.....", "%|##..#|##..#|...#.|..#..|.#...|#..##|#..##",
            "/|....#|....#|...#.|..#..|.#...|#....|#....", "'|..#..|..#..|.....|.....|.....|.....|.....",
            "(|...#.|..#..|.#...|.#...|.#...|..#..|...#.", ")|.#...|..#..|...#.|...#.|...#.|..#..|.#...",
            "=|.....|.....|#####|.....|#####|.....|.....", "x|.....|.....|#...#|.#.#.|..#..|.#.#.|#...#",
            "*|.....|#.#.#|.###.|#####|.###.|#.#.#|.....", "<|...#.|..#..|.#...|#....|.#...|..#..|...#.",
            ">|.#...|..#..|...#.|....#|...#.|..#..|.#...",
        };

        private static Texture2D _texture;
        private static Dictionary<char, Rect> _uvs;

        public static Texture2D Texture
        {
            get
            {
                Ensure();
                return _texture;
            }
        }

        /// <summary>UV rectangle of the 5x7 glyph for <paramref name="c"/> (case-insensitive). False for characters the font lacks (a space is not a glyph).</summary>
        public static bool TryGetUv(char c, out Rect uv)
        {
            Ensure();
            return _uvs.TryGetValue(char.ToUpperInvariant(c), out uv);
        }

        /// <summary>Width in font pixels of one line of text at scale 1.</summary>
        public static int MeasureLine(string line)
        {
            return string.IsNullOrEmpty(line) ? 0 : line.Length * Advance - 1;
        }

        private static void Ensure()
        {
            if (_texture != null)
            {
                return;
            }

            int rows = (Glyphs.Length + Columns - 1) / Columns;
            int width = Columns * Cell;
            int height = rows * Cell;

            var pixels = new Color32[width * height];
            _uvs = new Dictionary<char, Rect>();

            for (int i = 0; i < Glyphs.Length; i++)
            {
                var parts = Glyphs[i].Split('|');
                char key = parts[0][0];
                int column = i % Columns;
                int row = i / Columns;
                int left = column * Cell;
                int top = height - row * Cell; // texture y runs upward

                for (int y = 0; y < GlyphHeight; y++)
                {
                    for (int x = 0; x < GlyphWidth; x++)
                    {
                        if (parts[1 + y][x] == '#')
                        {
                            pixels[(top - 1 - y) * width + left + x] = new Color32(255, 255, 255, 255);
                        }
                    }
                }

                // Inset the UVs by a hair-width of nothing: the glyph rect is exactly the 5x7 pixel block.
                _uvs[key] = new Rect(left / (float)width, (top - GlyphHeight) / (float)height, GlyphWidth / (float)width, GlyphHeight / (float)height);
            }

            _texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "PixelFontAtlas",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            _texture.SetPixels32(pixels);
            _texture.Apply(false, true);
        }
    }
}
