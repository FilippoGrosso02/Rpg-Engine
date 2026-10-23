using System.Collections.Generic;
using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// The turn slider at the very top centre. A track runs through a row of 16x16 turn tiles (gold frame for heroes, red for enemies);
    /// whoever is acting is the taller 20x28 arch with a pointer under it. Tiles slide left by one slot when the order advances.
    /// </summary>
    public class TurnSliderView : MonoBehaviour
    {
        public const int UpcomingSlots = 5;
        private const float TrackX = 130f;
        private const float TrackY = 8f;
        private const float TrackWidth = 220f;
        private const float ArchX = 180f;
        private const float ArchY = 1f;
        private const float ArchWidth = 20f;
        private const float ArchHeight = 28f;
        private const float TileStartX = 204f;
        private const float TileStep = 18f;
        private const float TileSize = 16f;
        private const float TileY = 4f;
        private const float SlideSpeed = 180f;

        private class Tile
        {
            public RectTransform root;
            public Image frame;
            public PixelText letter;
            public Vector2 current;
            public Vector2 target;
        }

        [SerializeField] private BattleUiSprites sprites;
        [SerializeField] private RectTransform archRoot;
        [SerializeField] private Image archFrame;
        [SerializeField] private PixelText archLetter;
        [SerializeField] private Image pointer;
        [SerializeField] private RectTransform tileLayer;

        private readonly List<Tile> _tiles = new List<Tile>();
        private readonly List<CombatantRuntime> _shownOrder = new List<CombatantRuntime>();

        public void Build(BattleUiSprites spriteSource)
        {
            sprites = spriteSource;

            var track = UiFactory.Image(transform, "Track", sprites.Get("gauge_ornate"), TrackX, TrackY, TrackWidth, 8, Image.Type.Sliced);
            track.color = new Color(1f, 1f, 1f, 0.9f);

            tileLayer = UiFactory.Rect(transform, "Tiles", 0, 0, UiFactory.ScreenWidth, 40);

            archFrame = UiFactory.Image(transform, "Current", sprites.Get("chip_arch_ally"), ArchX, ArchY, ArchWidth, ArchHeight);
            archRoot = archFrame.rectTransform;
            archLetter = UiFactory.Text(archRoot, "Letter", "", 0, 9, ArchWidth, 8, PixelText.Align.Center, UiFactory.Cream);
            pointer = UiFactory.Image(transform, "Pointer", sprites.Get("marker_pointer_ally"), ArchX + 6, ArchY + ArchHeight + 1, 8, 4);
            Clear();
        }

        public void Clear()
        {
            archRoot.gameObject.SetActive(false);
            pointer.gameObject.SetActive(false);
            foreach (var tile in _tiles)
            {
                tile.root.gameObject.SetActive(false);
            }

            _shownOrder.Clear();
        }

        /// <summary>The unit whose turn it is now.</summary>
        public void SetCurrent(CombatantRuntime current)
        {
            bool has = current != null;
            archRoot.gameObject.SetActive(has);
            pointer.gameObject.SetActive(has);
            if (!has)
            {
                return;
            }

            archFrame.sprite = sprites.Get(current.isPlayerSide ? "chip_arch_ally" : "chip_arch_enemy");
            pointer.sprite = sprites.Get(current.isPlayerSide ? "marker_pointer_ally" : "marker_pointer_enemy");
            archLetter.text = Initial(current);
        }

        /// <summary>The units acting after the current one, soonest first. Existing tiles slide left to their new slot.</summary>
        public void SetUpcoming(IReadOnlyList<CombatantRuntime> upcoming)
        {
            int count = Mathf.Min(UpcomingSlots, upcoming != null ? upcoming.Count : 0);
            while (_tiles.Count < count)
            {
                _tiles.Add(CreateTile(_tiles.Count));
            }

            // A tile that was already showing the same combatant keeps its on-screen position and slides to its new slot.
            var previous = new List<CombatantRuntime>(_shownOrder);
            _shownOrder.Clear();

            for (int i = 0; i < _tiles.Count; i++)
            {
                var tile = _tiles[i];
                if (i >= count)
                {
                    tile.root.gameObject.SetActive(false);
                    continue;
                }

                var combatant = upcoming[i];
                _shownOrder.Add(combatant);
                tile.target = new Vector2(TileStartX + i * TileStep, TileY);

                int was = previous.IndexOf(combatant);
                tile.current = was >= 0 && was < _tiles.Count && _tiles[was].root.gameObject.activeSelf
                    ? new Vector2(TileStartX + was * TileStep, TileY)
                    : tile.target;

                tile.frame.sprite = sprites.Get(combatant.isPlayerSide ? "portrait_frame_ally" : "portrait_frame_enemy");
                tile.letter.text = Initial(combatant);
                tile.root.gameObject.SetActive(true);
                Place(tile);
            }
        }

        private Tile CreateTile(int index)
        {
            var frame = UiFactory.Image(tileLayer, "Tile" + index, null, TileStartX + index * TileStep, TileY, TileSize, TileSize, Image.Type.Sliced);
            var letter = UiFactory.Text(frame.transform, "Letter", "", 0, 4, TileSize, 8, PixelText.Align.Center, UiFactory.Cream);
            return new Tile { root = frame.rectTransform, frame = frame, letter = letter };
        }

        private void Update()
        {
            foreach (var tile in _tiles)
            {
                if (tile.root.gameObject.activeSelf && tile.current != tile.target)
                {
                    tile.current = Vector2.MoveTowards(tile.current, tile.target, SlideSpeed * Time.unscaledDeltaTime);
                    Place(tile);
                }
            }
        }

        private static void Place(Tile tile)
        {
            tile.root.anchoredPosition = new Vector2(Mathf.Round(tile.current.x), -Mathf.Round(tile.current.y));
        }

        private static string Initial(CombatantRuntime combatant)
        {
            return string.IsNullOrEmpty(combatant.displayName) ? "?" : combatant.displayName.Substring(0, 1);
        }
    }
}
