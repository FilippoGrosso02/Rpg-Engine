using System.Collections.Generic;
using Game.Combat.UI.UGUI;
using Game.Data.Registry;
using Game.Data.Rules;
using Game.Data.State;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Overworld.Menu
{
    /// <summary>
    /// The small dungeon HUD in the top-left corner: an ESC key hint that opens the menu (click it or press Esc), and the party's
    /// portraits stacked below it, each with a thin HP bar. A hero without portrait art shows a framed initial in a colour of their own.
    /// Hidden during fights and while the menu is open. Built in code, like the menu.
    /// </summary>
    public class OverworldHud : MonoBehaviour
    {
        const float Margin = 4f;
        const float PortraitSize = 28f;
        const float SlotHeight = 36f;
        const float HintHeight = 11f;
        const float RefreshSeconds = 0.25f;

        static readonly Color[] FallbackColours =
        {
            new Color32(0x8C, 0x3A, 0x2E, 255),
            new Color32(0x3E, 0x7A, 0x3A, 255),
            new Color32(0x3A, 0x55, 0x8C, 255),
            new Color32(0x7A, 0x5A, 0x2E, 255),
            new Color32(0x6A, 0x3A, 0x7A, 255)
        };

        sealed class Slot
        {
            public GameObject root;
            public string actorId;
            public RectTransform hpFill;
            public int lastHp = -1;
        }

        OverworldDirector _director;
        OverworldMenu _menu;
        GameObject _canvas;
        RectTransform _hint;
        RectTransform _slotsRoot;
        readonly List<Slot> _slots = new List<Slot>();
        readonly List<string> _shownIds = new List<string>();
        float _nextRefresh;
        bool _dirty = true;

        void Awake()
        {
            _director = GetComponent<OverworldDirector>();
            Build();
        }

        void Start() => _menu = GetComponent<OverworldMenu>();

        void OnEnable() => PartyRoster.Changed += MarkDirty;

        void OnDisable() => PartyRoster.Changed -= MarkDirty;

        void MarkDirty() => _dirty = true;

        void Update()
        {
            bool visible = !OverworldMenu.IsOpen && (_director == null || !_director.InBattle);
            if (_canvas.activeSelf != visible) _canvas.SetActive(visible);
            if (!visible) return;

            if (_menu != null && Input.GetMouseButtonDown(0)
                && RectTransformUtility.RectangleContainsScreenPoint(_hint, Input.mousePosition, null))
            {
                _menu.Open();
                return;
            }

            if (_dirty || Time.unscaledTime >= _nextRefresh)
            {
                _nextRefresh = Time.unscaledTime + RefreshSeconds;
                Refresh();
            }
        }

        void Refresh()
        {
            if (_dirty || !SameParty())
            {
                _dirty = false;
                Rebuild();
            }

            foreach (var slot in _slots)
            {
                var sheet = HeroSheet.Build(DatabaseRegistry.GetActor(slot.actorId));
                if (sheet == null || sheet.currentHp == slot.lastHp) continue;

                slot.lastHp = sheet.currentHp;
                float fraction = Mathf.Clamp01(sheet.currentHp / (float)sheet.maxHp);
                slot.hpFill.sizeDelta = new Vector2(Mathf.Round((PortraitSize - 2f) * fraction), slot.hpFill.sizeDelta.y);
            }
        }

        bool SameParty()
        {
            if (_shownIds.Count != PartyRoster.Count) return false;
            for (int i = 0; i < _shownIds.Count; i++)
            {
                if (_shownIds[i] != PartyRoster.Members[i]) return false;
            }

            return true;
        }

        void Rebuild()
        {
            foreach (var slot in _slots) Destroy(slot.root);
            _slots.Clear();
            _shownIds.Clear();

            float y = 0f;
            foreach (var id in PartyRoster.Members)
            {
                var actor = DatabaseRegistry.GetActor(id);
                if (actor == null) continue;

                _shownIds.Add(id);
                _slots.Add(BuildSlot(actor, y, _slots.Count));
                y += SlotHeight;
            }
        }

        Slot BuildSlot(Game.Data.Definitions.ActorDefinition actor, float y, int index)
        {
            var root = UiFactory.Rect(_slotsRoot, "Slot_" + actor.id, 0, y, PortraitSize, SlotHeight);

            UiFactory.Box(root, "Frame", UiFactory.Gold, 0, 0, PortraitSize, PortraitSize);
            if (actor.portrait != null)
            {
                var portrait = UiFactory.Image(root, "Portrait", actor.portrait, 1, 1, PortraitSize - 2f, PortraitSize - 2f);
                portrait.preserveAspect = true;
            }
            else
            {
                UiFactory.Box(root, "Fill", FallbackColours[index % FallbackColours.Length], 1, 1, PortraitSize - 2f, PortraitSize - 2f);
                var initial = string.IsNullOrEmpty(actor.displayName) ? "?" : actor.displayName.Substring(0, 1);
                UiFactory.Text(root, "Initial", initial, 0, 0, PortraitSize, PortraitSize, PixelText.Align.Center, UiFactory.Cream);
            }

            UiFactory.Box(root, "HpBack", UiFactory.Dim, 0, PortraitSize + 1f, PortraitSize, 4f);
            var fill = UiFactory.Box(root, "HpFill", UiFactory.Heal, 1, PortraitSize + 2f, PortraitSize - 2f, 2f);

            return new Slot { root = root.gameObject, actorId = actor.id, hpFill = fill.rectTransform };
        }

        void Build()
        {
            _canvas = new GameObject("OverworldHudCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _canvas.SetActive(false);
            _canvas.transform.SetParent(transform, false);

            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 50;
            _canvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            _canvas.AddComponent<PixelPerfectScaler>(); // no UiRoot: it only sets the integer scale, and the corner stays the screen's corner

            // The ESC key hint, drawn like the battle HUD's key caps: a brown border around a cream cap.
            _hint = UiFactory.Rect(_canvas.transform, "EscHint", Margin, Margin, 40f, HintHeight);
            var cap = UiFactory.Box(_hint, "CapBorder", UiFactory.KeyBorder, 0, 0, 21f, HintHeight);
            UiFactory.Box(cap.transform, "CapFill", UiFactory.Cream, 1, 1, 19f, HintHeight - 2f);
            UiFactory.Text(cap.transform, "Key", "ESC", 0, 2, 21f, 8, PixelText.Align.Center, UiFactory.Ink, shadow: false);
            UiFactory.Text(_hint, "Label", "MENU", 24f, 2, 20f, 8, PixelText.Align.Left, UiFactory.Cream);

            _slotsRoot = UiFactory.Rect(_canvas.transform, "Portraits", Margin, Margin + HintHeight + 6f, PortraitSize, 5 * SlotHeight);
        }
    }
}
