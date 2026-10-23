using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// Numbers that rise from a target when an attack resolves: orange digits for damage, green for healing, the MISS word for a miss.
    /// Each floats up about 24 px over 0.6 s while fading. They can be paused (the flee prompt freezes them).
    /// </summary>
    public class FloatingNumbersView : MonoBehaviour
    {
        public const float Lifetime = 0.6f;
        public const float RiseDistance = 24f;
        private const float DigitWidth = 7f;
        private const float DigitHeight = 12f;

        private class Floater
        {
            public RectTransform root;
            public CanvasGroup group;
            public Vector2 start;
            public float age;
        }

        [SerializeField] private BattleUiSprites sprites;

        private readonly List<Floater> _active = new List<Floater>();

        /// <summary>Freezes every floating number in place (set while a modal is open).</summary>
        public bool Paused { get; set; }

        public int ActiveCount => _active.Count;

        public void Build(BattleUiSprites spriteSource)
        {
            sprites = spriteSource;
        }

        /// <summary>A damage or heal number centred on <paramref name="uiPosition"/> (top-left screen coordinates).</summary>
        public void SpawnNumber(Vector2 uiPosition, int value, bool heal)
        {
            string colour = heal ? "green" : "orange";
            string digits = Mathf.Max(0, value).ToString();
            float width = digits.Length * DigitWidth + (heal ? DigitWidth : 0f);
            var root = NewFloater(uiPosition, width, DigitHeight);

            float x = 0f;
            if (heal)
            {
                UiFactory.Image(root, "Plus", sprites.Get("glyph_plus_green"), x, 0, 8, 8);
                x += DigitWidth;
            }

            foreach (char digit in digits)
            {
                UiFactory.Image(root, "Digit", sprites.Get($"digit_{colour}_{digit}"), x, 0, DigitWidth, DigitHeight);
                x += DigitWidth;
            }
        }

        /// <summary>The MISS word centred on <paramref name="uiPosition"/>.</summary>
        public void SpawnMiss(Vector2 uiPosition)
        {
            var root = NewFloater(uiPosition, 32f, DigitHeight);
            UiFactory.Image(root, "Miss", sprites.Get("text_miss"), 0, 0, 32, DigitHeight);
        }

        private RectTransform NewFloater(Vector2 centre, float width, float height)
        {
            var start = new Vector2(Mathf.Round(centre.x - width * 0.5f), Mathf.Round(centre.y - height * 0.5f));
            var rect = UiFactory.Rect(transform, "Floater", start.x, start.y, width, height);
            var group = rect.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            _active.Add(new Floater { root = rect, group = group, start = start });
            return rect;
        }

        private void Update()
        {
            if (Paused)
            {
                return;
            }

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                var floater = _active[i];
                floater.age += Time.unscaledDeltaTime;
                float t = Mathf.Clamp01(floater.age / Lifetime);

                floater.root.anchoredPosition = new Vector2(floater.start.x, -Mathf.Round(floater.start.y - RiseDistance * t));
                floater.group.alpha = t < 0.5f ? 1f : 1f - (t - 0.5f) * 2f;

                if (floater.age >= Lifetime)
                {
                    Destroy(floater.root.gameObject);
                    _active.RemoveAt(i);
                }
            }
        }
    }
}
