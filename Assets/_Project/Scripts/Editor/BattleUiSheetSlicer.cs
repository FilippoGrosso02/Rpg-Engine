using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.U2D.Sprites;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// Cuts Assets/_Project/Art/UI/battle_ui_sheet_3x.png into named sprites. Re-runnable: a sprite that already exists keeps its
    /// id (so anything referencing it survives a re-slice). It only edits the sheet's import settings — it does not touch the PNG
    /// or any HUD prefab.
    ///
    /// The sheet is drawn at 3x (one art pixel = 3x3 texture pixels) on a solid dark-brown background with no alpha.
    /// Rects below are in the PNG's own pixel space with the origin at the TOP-left, inclusive on both ends. Borders are 9-slice,
    /// in texture pixels, in Unity's order (left, bottom, right, top); only parts that can sensibly stretch have any.
    /// Run via: Tools/UI/Slice Battle UI Sheet
    /// </summary>
    public static class BattleUiSheetSlicer
    {
        public const string SheetPath = "Assets/_Project/Art/UI/battle_ui_sheet_3x.png";

        /// <summary>One art pixel is 3 texture pixels. At 300 PPU (with the canvas reference of 100) one art pixel is exactly 1 canvas unit, which is the 480x270 HUD's native resolution: a 216x270 card is 72x90 units.</summary>
        private const float PixelsPerUnit = 300f;

        private const int SheetHeight = 1056;

        private struct Cut
        {
            public string name;
            public int x0, y0, x1, y1; // inclusive, top-left origin
            public Vector4 border;     // left, bottom, right, top (texture pixels)

            public Cut(string name, int x0, int y0, int x1, int y1, float l = 0, float b = 0, float r = 0, float t = 0)
            {
                this.name = name;
                this.x0 = x0; this.y0 = y0; this.x1 = x1; this.y1 = y1;
                border = new Vector4(l, b, r, t);
            }
        }

        private static List<Cut> BuildCuts()
        {
            var c = new List<Cut>
            {
                // --- Unit and spell cards (fixed size) ---
                new Cut("card_unit_ally", 24, 18, 239, 287),
                new Cut("card_unit_enemy", 270, 18, 485, 287),
                new Cut("card_spell_face", 516, 18, 665, 227),
                new Cut("card_spell_back", 690, 18, 839, 227),
                new Cut("card_spell_selected", 855, 3, 1028, 230),
                new Cut("card_spell_disabled", 1038, 18, 1187, 227),

                // --- Hit-chance tags (percentage is baked into the art) ---
                new Cut("tag_hit_chance_neutral", 1206, 24, 1373, 71),
                new Cut("tag_hit_chance_high", 1206, 84, 1373, 131),
                new Cut("tag_hit_chance_low", 1206, 144, 1373, 191),

                // --- Targeting ---
                new Cut("target_reticle", 1248, 246, 1334, 332),

                // --- Portrait frames, turn-order chips and markers ---
                new Cut("portrait_frame_ally", 24, 360, 89, 425, 9, 9, 9, 9),
                new Cut("portrait_frame_enemy", 108, 360, 173, 425, 9, 9, 9, 9),
                new Cut("portrait_frame_ally_b", 192, 360, 257, 425, 9, 9, 9, 9),
                new Cut("portrait_frame_enemy_b", 276, 360, 341, 425, 9, 9, 9, 9),
                new Cut("chip_arch_ally", 378, 318, 461, 437),
                new Cut("chip_arch_enemy", 486, 318, 569, 437),
                new Cut("marker_pointer_ally", 594, 360, 620, 374),
                new Cut("marker_pointer_enemy", 636, 360, 662, 374),

                // --- Gauge and pips ---
                new Cut("gauge_ornate", 672, 366, 983, 401, 36, 0, 36, 0),
                new Cut("pip_full", 1002, 360, 1040, 398),
                new Cut("pip_empty", 1044, 360, 1082, 398),

                // --- Ability slots (key hint baked in), resource orbs, stat icons, plain slots ---
                new Cut("slot_ability_q", 36, 492, 128, 584),
                new Cut("slot_ability_e", 150, 492, 242, 584),
                new Cut("slot_ability_f", 264, 492, 356, 584),
                new Cut("slot_ability_r", 378, 492, 470, 584),
                new Cut("slot_ability_empty", 492, 492, 578, 578),
                new Cut("orb_small_red", 612, 504, 674, 566),
                new Cut("orb_small_empty", 690, 504, 752, 566),
                new Cut("orb_large_red", 771, 495, 851, 575),
                new Cut("icon_heart", 894, 519, 926, 548),
                new Cut("icon_sword", 942, 519, 971, 548),
                new Cut("icon_shield", 984, 519, 1010, 548),
                new Cut("icon_boot", 1023, 519, 1046, 545),
                new Cut("slot_dark", 1074, 504, 1136, 566, 9, 9, 9, 9),
                new Cut("slot_cream", 1152, 504, 1214, 566, 9, 9, 9, 9),
                new Cut("slot_red", 1230, 504, 1292, 566, 9, 9, 9, 9),

                // --- Combat-feed rows (composite examples: portrait + icon + value baked in) ---
                new Cut("feed_row_buff", 24, 612, 395, 725),
                new Cut("feed_row_heal", 24, 744, 395, 857),
                new Cut("feed_row_miss", 24, 876, 395, 989),

                // --- Ribbons (left/right folds are fixed, the middle stretches) ---
                new Cut("ribbon_gold", 432, 612, 623, 659, 36, 0, 36, 0),
                new Cut("ribbon_red", 636, 612, 827, 659, 36, 0, 36, 0),
                new Cut("ribbon_brown", 840, 612, 1031, 659, 36, 0, 36, 0),

                // --- Slim plates (diamond ends fixed) ---
                new Cut("plate_slim", 432, 684, 599, 731, 30, 12, 30, 12),
                new Cut("plate_slim_active", 612, 684, 779, 731, 30, 12, 30, 12),
                new Cut("plate_slim_dim", 792, 687, 959, 734, 30, 12, 30, 12),
                new Cut("plate_slim_disabled", 972, 684, 1139, 731, 30, 12, 30, 12),

                // --- Panels and frames (9-slice; borders cover the frame bands and the corner studs) ---
                new Cut("panel_dark", 432, 762, 551, 881, 18, 18, 18, 18),
                new Cut("panel_parchment", 564, 762, 683, 881, 12, 12, 12, 12),
                new Cut("frame_dark_small", 696, 762, 785, 851, 12, 12, 12, 12),
                new Cut("plate_wide", 798, 762, 1079, 833, 39, 12, 39, 12),
                new Cut("frame_brown_small", 1092, 762, 1163, 833, 9, 9, 9, 9),
                new Cut("divider_ornate", 804, 852, 1130, 878),

                // --- Glyphs ---
                new Cut("text_miss", 696, 912, 791, 947),
                new Cut("glyph_plus_green", 696, 960, 719, 983),
                new Cut("glyph_x_red", 732, 954, 755, 989),
            };

            // Damage digits: three colours x ten digits on an exact 24 px pitch; each glyph is 18 px wide plus a 3 px drop shadow.
            var rows = new[] { ("white", 912), ("orange", 954), ("green", 996) };
            foreach (var (colour, top) in rows)
            {
                for (int digit = 0; digit < 10; digit++)
                {
                    int x = 432 + 24 * digit;
                    c.Add(new Cut($"digit_{colour}_{digit}", x, top, x + 20, top + 35));
                }
            }

            return c;
        }

        [MenuItem("Tools/UI/Slice Battle UI Sheet")]
        public static void Run()
        {
            var importer = AssetImporter.GetAtPath(SheetPath) as TextureImporter;
            if (importer == null)
            {
                Debug.LogError("[BattleUiSheetSlicer] Sheet not found at " + SheetPath);
                return;
            }

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Multiple;
            importer.spritePixelsPerUnit = PixelsPerUnit;
            importer.filterMode = FilterMode.Point;
            importer.mipmapEnabled = false;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.alphaIsTransparency = true;
            importer.SaveAndReimport();

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();

            // Keep the id of any sprite that already exists, so references to it survive a re-slice.
            var existing = provider.GetSpriteRects().ToDictionary(r => r.name, r => r.spriteID);

            var rects = new List<SpriteRect>();
            var seen = new HashSet<string>();
            foreach (var cut in BuildCuts())
            {
                if (!seen.Add(cut.name))
                {
                    Debug.LogError("[BattleUiSheetSlicer] Duplicate sprite name " + cut.name);
                    return;
                }

                int width = cut.x1 - cut.x0 + 1;
                int height = cut.y1 - cut.y0 + 1;
                rects.Add(new SpriteRect
                {
                    name = cut.name,
                    spriteID = existing.TryGetValue(cut.name, out var id) ? id : GUID.Generate(),
                    rect = new Rect(cut.x0, SheetHeight - (cut.y1 + 1), width, height),
                    alignment = SpriteAlignment.Center,
                    pivot = new Vector2(0.5f, 0.5f),
                    border = cut.border
                });
            }

            provider.SetSpriteRects(rects.ToArray());

            var nameFileIds = provider.GetDataProvider<ISpriteNameFileIdDataProvider>();
            nameFileIds.SetNameFileIdPairs(rects.Select(r => new SpriteNameFileIdPair(r.name, r.spriteID)).ToList());

            provider.Apply();
            importer.SaveAndReimport();

            Debug.Log($"[BattleUiSheetSlicer] Sliced {rects.Count} sprites from {SheetPath}.");
        }
    }
}
