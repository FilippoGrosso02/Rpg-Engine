using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// Builds HUD widgets using the spec's coordinate system: a 480x270 reference screen with the origin at the TOP-left, y growing
    /// downward, positions and sizes in whole pixels. Every graphic created here ignores the mouse (the cursor is locked to the
    /// reticle, so no widget may hit-test it).
    /// </summary>
    public static class UiFactory
    {
        public const float ScreenWidth = 480f;
        public const float ScreenHeight = 270f;

        // Palette from the spec.
        public static readonly Color Cream = new Color32(0xF2, 0xE6, 0xC6, 255);
        public static readonly Color Ink = new Color32(0x28, 0x1C, 0x15, 255);
        public static readonly Color Damage = new Color32(0xE2, 0x84, 0x32, 255);
        public static readonly Color Heal = new Color32(0x58, 0xB0, 0x40, 255);
        public static readonly Color Warning = new Color32(0xC4, 0x42, 0x30, 255);
        public static readonly Color Gold = new Color32(0xC8, 0x94, 0x3C, 255);
        public static readonly Color KeyBorder = new Color32(0x92, 0x64, 0x26, 255);
        public static readonly Color Dim = new Color32(0x15, 0x0D, 0x0B, 255);

        /// <summary>An empty RectTransform positioned by its top-left corner.</summary>
        public static RectTransform Rect(Transform parent, string name, float x, float y, float width, float height)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = (RectTransform)go.transform;
            Place(rect, x, y, width, height);
            return rect;
        }

        public static void Place(RectTransform rect, float x, float y, float width, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(0f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        public static Image Image(Transform parent, string name, Sprite sprite, float x, float y, float width, float height, Image.Type type = UnityEngine.UI.Image.Type.Simple)
        {
            var rect = Rect(parent, name, x, y, width, height);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null ? type : UnityEngine.UI.Image.Type.Simple;
            image.raycastTarget = false;
            if (sprite == null)
            {
                image.color = Color.white;
            }

            return image;
        }

        /// <summary>A flat colour rectangle (no sprite).</summary>
        public static Image Box(Transform parent, string name, Color color, float x, float y, float width, float height)
        {
            var image = Image(parent, name, null, x, y, width, height);
            image.color = color;
            return image;
        }

        public static PixelText Text(Transform parent, string name, string text, float x, float y, float width, float height,
            PixelText.Align align, Color color, bool shadow = true)
        {
            var rect = Rect(parent, name, x, y, width, height);
            var label = rect.gameObject.AddComponent<PixelText>();
            label.text = text;
            label.alignment = align;
            label.color = color;
            label.shadow = shadow;
            label.raycastTarget = false;
            return label;
        }

        /// <summary>Stretches a RectTransform to fill its parent.</summary>
        public static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
