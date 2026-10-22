using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Applies the pixel UI pack (Assets/_Project/Art/UI) to the existing battle HUD prefabs in place.
    /// Layout, hierarchy and component wiring are left alone; only sprites, colors and a few
    /// decorative child images are touched. Safe to re-run (decorations are found by name).
    /// Run via: Tools/Combat/Reskin Battle HUD, or -executeMethod Game.EditorTools.BattleHudReskin.Run
    /// </summary>
    public static class BattleHudReskin
    {
        private const string ArtRoot = "Assets/_Project/Art/UI/";
        private const string UiFolder = "Assets/_Project/Prefabs/Combat/TrailsBattle3D/UI/";
        private const string FrameName = "SkinFrame";
        private const string MarkerName = "SkinMarker";

        private static readonly Color Cream = new Color32(239, 224, 184, 255);
        private static readonly Color Ink = new Color32(36, 22, 12, 255);

        [MenuItem("Tools/Combat/Reskin Battle HUD")]
        public static void Run()
        {
            ReskinHud();
            ReskinNameplate("AllyNameplate.prefab", "Bars/bar_mp_fill");
            ReskinNameplate("EnemyNameplate.prefab", "Bars/enemy_bar_fill");
            AssetDatabase.SaveAssets();
            Debug.Log("[BattleHudReskin] Done.");
        }

        private static void ReskinHud()
        {
            string path = UiFolder + "BattleHUD.prefab";
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var button in root.GetComponentsInChildren<Button>(true))
                {
                    SkinButton(button);
                }

                foreach (string cardName in new[] { "ActingUnitCard", "HoverStatsPanel" })
                {
                    var card = Find(root, cardName);
                    SetImage(card.GetComponent<Image>(), "Core/panel_dark");
                    TextColor(card, Cream);
                    foreach (var slider in card.GetComponentsInChildren<Slider>(true))
                    {
                        SkinSlider(slider, "Bars/bar_hp_fill", "Bars/bar_bg", "Bars/bar_frame", 20f);
                    }
                    foreach (var dot in Find(card, "FearDotMeter").GetComponentsInChildren<Image>(true))
                    {
                        if (dot.gameObject.name.StartsWith("Dot"))
                        {
                            dot.sprite = Load("Bars/cp_pip_full");
                            dot.type = Image.Type.Simple;
                            dot.preserveAspect = true;
                        }
                    }
                }

                var resultPanel = Find(root, "PanelRoot");
                SetImage(resultPanel.GetComponent<Image>(), "Core/panel_frame");
                TextColor(resultPanel, Cream, skipButtons: true);

                var rollBar = Find(root, "DamageRollBar");
                SetImage(rollBar.GetComponent<Image>(), "Core/panel_dark");
                TextColor(rollBar, Cream);

                var banner = Find(root, "TurnBanner");
                var bannerImage = banner.GetComponent<Image>() ?? banner.AddComponent<Image>();
                SetImage(bannerImage, "Battle/banner_plate_round");
                bannerImage.raycastTarget = false;
                TextColor(banner, Cream);

                var prompt = Find(root, "TargetingPrompt");
                TextColor(prompt, Cream, skipButtons: true);

                var chip = Find(root, "IconTemplate");
                SetImage(chip.GetComponent<Image>(), "Core/panel_light"); // tinted per side by TurnOrderIcon
                TextColor(chip, Ink);

                SkinTurnMarker(Find(root, "ActiveTurnIndicator"));

                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void ReskinNameplate(string prefabFile, string fillSprite)
        {
            string path = UiFolder + prefabFile;
            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var slider in root.GetComponentsInChildren<Slider>(true))
                {
                    SkinSlider(slider, fillSprite, "Bars/enemy_bar_bg", "Bars/enemy_bar_frame", 16f);
                }
                TextColor(root, Cream);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        private static void SkinButton(Button button)
        {
            var image = button.GetComponent<Image>();
            SetImage(image, "Core/btn_normal");
            button.targetGraphic = image;
            button.transition = Selectable.Transition.SpriteSwap;
            button.spriteState = new SpriteState
            {
                highlightedSprite = Load("Core/btn_highlighted"),
                pressedSprite = Load("Core/btn_pressed"),
                selectedSprite = Load("Core/btn_selected"),
                disabledSprite = Load("Core/btn_disabled"),
            };
            foreach (var text in button.GetComponentsInChildren<Text>(true))
            {
                text.color = Ink;
            }
        }

        private static void SkinSlider(Slider slider, string fill, string background, string frame, float height)
        {
            SetImage(slider.GetComponent<Image>(), background);
            if (slider.fillRect != null)
            {
                SetImage(slider.fillRect.GetComponent<Image>(), fill);
            }

            var rect = (RectTransform)slider.transform;
            if (Mathf.Approximately(rect.anchorMin.y, rect.anchorMax.y))
            {
                rect.sizeDelta = new Vector2(rect.sizeDelta.x, height);
            }

            var frameTransform = slider.transform.Find(FrameName);
            if (frameTransform == null)
            {
                var go = new GameObject(FrameName, typeof(RectTransform), typeof(Image));
                go.transform.SetParent(slider.transform, false);
                frameTransform = go.transform;
                var frameRect = (RectTransform)frameTransform;
                frameRect.anchorMin = Vector2.zero;
                frameRect.anchorMax = Vector2.one;
                frameRect.offsetMin = Vector2.zero;
                frameRect.offsetMax = Vector2.zero;
            }
            frameTransform.SetAsLastSibling();
            var frameImage = frameTransform.GetComponent<Image>();
            SetImage(frameImage, frame);
            frameImage.raycastTarget = false;
        }

        private static void SkinTurnMarker(GameObject indicator)
        {
            foreach (var text in indicator.GetComponentsInChildren<Text>(true))
            {
                text.enabled = false; // replaces the "▼" glyph; kept (not deleted) so the prefab stays reversible
            }

            var existing = indicator.transform.Find(MarkerName);
            GameObject marker = existing != null ? existing.gameObject : new GameObject(MarkerName, typeof(RectTransform), typeof(Image));
            marker.transform.SetParent(indicator.transform, false);
            var rect = (RectTransform)marker.transform;
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(9f * 6f, 6f * 6f);
            var image = marker.GetComponent<Image>();
            image.sprite = Load("Battle/turn_marker");
            image.type = Image.Type.Simple;
            image.preserveAspect = true;
            image.raycastTarget = false;
        }

        private static void TextColor(GameObject root, Color color, bool skipButtons = false)
        {
            foreach (var text in root.GetComponentsInChildren<Text>(true))
            {
                if (skipButtons && text.GetComponentInParent<Button>(true) != null)
                {
                    continue;
                }
                text.color = color;
            }
        }

        private static void SetImage(Image image, string sprite)
        {
            image.sprite = Load(sprite);
            image.type = Image.Type.Sliced;
            image.color = Color.white;
            image.pixelsPerUnitMultiplier = 1f;
        }

        private static Sprite Load(string relativePath)
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(ArtRoot + relativePath + ".png");
            if (sprite == null)
            {
                throw new System.InvalidOperationException("Missing UI sprite: " + ArtRoot + relativePath + ".png");
            }
            return sprite;
        }

        private static GameObject Find(GameObject root, string name)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == name)
                {
                    return t.gameObject;
                }
            }
            throw new System.InvalidOperationException("HUD object not found: " + name);
        }
    }
}
