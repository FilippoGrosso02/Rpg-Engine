// Put this file anywhere under an Editor folder, e.g. Assets/_Project/Art/UI/Editor/UIPixelImporter.cs
// Auto-configures every PNG under Assets/_Project/Art/UI/ as a crisp pixel-art sprite and applies the 9-slice borders.
// To apply it to already-imported files: right-click the UI folder > Reimport.
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

public class UIPixelImporter : AssetPostprocessor
{
    const string Root = "Assets/_Project/Art/UI/";
    // How many canvas pixels one art pixel covers at the 1920x1080 reference (2 here: the battle HUD canvas is 1280x720, so 2 canvas px = 3 screen px at 1080p, which keeps pixels crisp).
    const float PixelScale = 2f;

    // Border order is Unity's: left, bottom, right, top.
    static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
    {
        {"panel_frame", new Vector4(10,10,10,10)},
        {"panel_light", new Vector4(6,6,6,6)},
        {"panel_dark", new Vector4(6,6,6,6)},
        {"tooltip_box", new Vector4(6,6,6,6)},
        {"btn_normal", new Vector4(6,6,6,6)},
        {"btn_highlighted", new Vector4(6,6,6,6)},
        {"btn_pressed", new Vector4(6,6,6,6)},
        {"btn_selected", new Vector4(6,6,6,6)},
        {"btn_disabled", new Vector4(6,6,6,6)},
        {"list_highlight", new Vector4(4,4,4,4)},
        {"divider_h", new Vector4(8,0,8,0)},
        {"divider_v", new Vector4(0,8,0,8)},
        {"banner_title", new Vector4(14,6,14,6)},
        {"banner_plate_victory", new Vector4(16,8,16,8)},
        {"banner_plate_defeat", new Vector4(16,8,16,8)},
        {"banner_plate_round", new Vector4(16,8,16,8)},
        {"scroll_track", new Vector4(2,2,2,2)},
        {"scroll_handle", new Vector4(2,2,2,2)},
        {"slider_track", new Vector4(3,3,3,3)},
        {"bar_frame", new Vector4(3,3,3,3)},
        {"bar_bg", new Vector4(1,1,1,1)},
        {"bar_hp_fill", new Vector4(1,1,1,1)},
        {"bar_mp_fill", new Vector4(1,1,1,1)},
        {"bar_break_fill", new Vector4(1,1,1,1)},
        {"bar_xp_fill", new Vector4(1,1,1,1)},
        {"bar_hp_low_fill", new Vector4(1,1,1,1)},
        {"enemy_bar_frame", new Vector4(2,2,2,2)},
        {"enemy_bar_bg", new Vector4(1,1,1,1)},
        {"portrait_frame", new Vector4(5,5,5,5)},
        {"turn_chip_frame", new Vector4(4,4,4,4)},
        {"turn_chip_frame_current", new Vector4(4,4,4,4)},
        {"turn_strip_bg", new Vector4(5,5,5,5)},
        {"enemy_nameplate", new Vector4(4,4,4,4)},
        {"status_slot_frame", new Vector4(3,3,3,3)},
        {"loot_slot_frame", new Vector4(4,4,4,4)}
    };

    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Root)) return;
        var ti = (TextureImporter)assetImporter;
        ti.textureType = TextureImporterType.Sprite;
        ti.spriteImportMode = SpriteImportMode.Single;
        ti.spritePixelsPerUnit = 100f / PixelScale; // Canvas reference PPU is 100, so this gives PixelScale x
        ti.filterMode = FilterMode.Point;
        ti.textureCompression = TextureImporterCompression.Uncompressed;
        ti.mipmapEnabled = false;
        ti.alphaIsTransparency = true;
        var s = new TextureImporterSettings();
        ti.ReadTextureSettings(s);
        s.spriteMeshType = SpriteMeshType.FullRect;
        ti.SetTextureSettings(s);
        if (Borders.TryGetValue(Path.GetFileNameWithoutExtension(assetPath), out var b)) ti.spriteBorder = b;
    }
}
