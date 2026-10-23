using Game.Combat.Contracts;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Combat.UI.UGUI
{
    /// <summary>
    /// The end-of-battle panel: a ribbon title (VICTORY! / DEFEAT / ESCAPED), the XP and loot, and a Continue button. Once the battle
    /// is over the cursor is free again, so Continue can be clicked — or confirmed with Enter / Space.
    /// </summary>
    public class EncounterResultPanel : MonoBehaviour
    {
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private BattleUiSprites sprites;
        [SerializeField] private Image ribbon;
        [SerializeField] private PixelText titleLabel;
        [SerializeField] private PixelText detailsLabel;
        [SerializeField] private Button continueButton;

        private IPlayerCommandInput _input;

        public bool IsShowing => panelRoot != null && panelRoot.activeSelf;

        public void Build(BattleUiSprites spriteSource)
        {
            sprites = spriteSource;
            panelRoot = UiFactory.Rect(transform, "PanelRoot", 0, 0, UiFactory.ScreenWidth, UiFactory.ScreenHeight).gameObject;

            UiFactory.Box(panelRoot.transform, "Dim", new Color(UiFactory.Dim.r, UiFactory.Dim.g, UiFactory.Dim.b, 0.6f), 0, 0, UiFactory.ScreenWidth, UiFactory.ScreenHeight);
            UiFactory.Image(panelRoot.transform, "Panel", sprites.Get("panel_dark"), 140, 62, 200, 118, Image.Type.Sliced);
            ribbon = UiFactory.Image(panelRoot.transform, "Ribbon", sprites.Get("ribbon_gold"), 192, 54, 96, 16, Image.Type.Sliced);
            titleLabel = UiFactory.Text(panelRoot.transform, "Title", "", 192, 58, 96, 8, PixelText.Align.Center, UiFactory.Cream);
            detailsLabel = UiFactory.Text(panelRoot.transform, "Details", "", 150, 88, 180, 40, PixelText.Align.Center, UiFactory.Cream);

            var plate = UiFactory.Image(panelRoot.transform, "ContinueButton", sprites.Get("frame_dark_small"), 207, 152, 66, 16, Image.Type.Sliced);
            plate.raycastTarget = true; // the one clickable thing in the battle HUD, and only once the battle is over
            UiFactory.Text(plate.transform, "Label", "CONTINUE", 0, 4, 66, 8, PixelText.Align.Center, UiFactory.Cream);

            continueButton = plate.gameObject.AddComponent<Button>();
            continueButton.targetGraphic = plate;
            continueButton.transition = Selectable.Transition.SpriteSwap;
            continueButton.spriteState = new SpriteState
            {
                highlightedSprite = sprites.Get("frame_brown_small"),
                pressedSprite = sprites.Get("frame_brown_small"),
                selectedSprite = sprites.Get("frame_brown_small"),
                disabledSprite = sprites.Get("frame_dark_small")
            };

            panelRoot.SetActive(false);
        }

        public void Bind(IPlayerCommandInput input)
        {
            _input = input;
            if (continueButton != null)
            {
                continueButton.onClick.AddListener(HandleContinueClicked);
            }

            Hide();
        }

        public void Unbind()
        {
            if (continueButton != null)
            {
                continueButton.onClick.RemoveListener(HandleContinueClicked);
            }

            _input = null;
        }

        public void Show(EncounterResult result)
        {
            string title = result.fled ? "ESCAPED" : result.victory ? "VICTORY!" : "DEFEAT";
            ribbon.sprite = sprites.Get(result.fled ? "ribbon_brown" : result.victory ? "ribbon_gold" : "ribbon_red");
            titleLabel.text = title;

            if (result.fled)
            {
                detailsLabel.text = "YOU GOT AWAY SAFELY";
            }
            else
            {
                string loot = result.lootItemIds.Count > 0 ? string.Join(", ", result.lootItemIds) : "NONE";
                detailsLabel.text = $"XP: {result.xpAwarded}\nLOOT: {loot}";
            }

            panelRoot.SetActive(true);
        }

        public void Hide()
        {
            if (panelRoot != null)
            {
                panelRoot.SetActive(false);
            }
        }

        private void Update()
        {
            if (IsShowing && (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)))
            {
                HandleContinueClicked();
            }
        }

        private void HandleContinueClicked()
        {
            _input?.SubmitCommand(PlayerCommand.ContinueAfterResult);
            Hide();
        }
    }
}
