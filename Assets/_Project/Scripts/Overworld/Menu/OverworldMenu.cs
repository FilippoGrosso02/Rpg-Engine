using System.Collections.Generic;
using Game.Audio;
using Game.Combat.UI.UGUI;
using Game.Data.State;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Overworld.Menu
{
    /// <summary>
    /// The overworld pause menu: Characters, Equipment, Items, Grimoire and System tabs on a 480x270 pixel canvas. Esc or Tab opens it
    /// (and freezes the overworld by stopping time), Q/E switch tabs, W/S/A/D or the arrows move, Enter/Space chooses and Esc steps
    /// back, closing the menu from the top level. It lives next to the <see cref="OverworldDirector"/> and stays shut during a fight.
    /// Everything is built in code, so no scene or prefab is involved.
    /// </summary>
    public class OverworldMenu : MonoBehaviour
    {
        const float PanelX = 20f;
        const float PanelY = 12f;
        const float PanelWidth = 440f;
        const float PanelHeight = 246f;
        const float TabWidth = 84f;

        /// <summary>The stock the party starts with until something else (shops, chests, saves) provides items.</summary>
        static readonly (string id, int count)[] StartingItems = { ("item_healing_potion", 2) };
        static readonly string[] StartingWeapons = { "weapon_sword", "weapon_dagger", "weapon_shortbow" };
        static readonly string[] StartingArmor = { "armor_leather", "armor_chain_mail", "armor_shield" };

        public static bool IsOpen { get; private set; }

        OverworldDirector _director;
        GameObject _canvas;
        readonly List<MenuTab> _tabs = new List<MenuTab>();
        readonly List<Image> _tabPlates = new List<Image>();
        readonly List<PixelText> _tabLabels = new List<PixelText>();
        int _tabIndex;
        float _previousTimeScale = 1f;

        void Awake()
        {
            _director = GetComponent<OverworldDirector>();
            Inventory.EnsureSeeded(StartingItems, StartingWeapons, StartingArmor);
            Build();
        }

        void OnDestroy()
        {
            if (IsOpen) Close();
        }

        void Update()
        {
            if (_director != null && _director.InBattle)
            {
                if (IsOpen) Close();
                return;
            }

            if (!IsOpen)
            {
                if (Input.GetKeyDown(KeyCode.Escape) || Input.GetKeyDown(KeyCode.Tab)) Open();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Tab))
            {
                Close();
                return;
            }

            if (Input.GetKeyDown(KeyCode.Q)) { GameAudio.Play(AudioCue.UiMove); SelectTab(_tabIndex - 1); }
            if (Input.GetKeyDown(KeyCode.E)) { GameAudio.Play(AudioCue.UiMove); SelectTab(_tabIndex + 1); }

            if (Input.GetKeyDown(KeyCode.Escape))
            {
                if (!_tabs[_tabIndex].HandleBack()) Close();
                else GameAudio.Play(AudioCue.UiBack);
                return;
            }

            var input = ReadInput();
            if (input.moveX != 0 || input.moveY != 0) GameAudio.Play(AudioCue.UiMove);
            if (input.confirm) GameAudio.Play(AudioCue.UiConfirm);
            _tabs[_tabIndex].Handle(input);
        }

        static MenuInput ReadInput()
        {
            int x = 0;
            int y = 0;
            if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.LeftArrow)) x--;
            if (Input.GetKeyDown(KeyCode.D) || Input.GetKeyDown(KeyCode.RightArrow)) x++;
            if (Input.GetKeyDown(KeyCode.W) || Input.GetKeyDown(KeyCode.UpArrow)) y--;
            if (Input.GetKeyDown(KeyCode.S) || Input.GetKeyDown(KeyCode.DownArrow)) y++;

            return new MenuInput
            {
                moveX = x,
                moveY = y,
                confirm = Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space)
            };
        }

        public void Open()
        {
            if (IsOpen) return;

            IsOpen = true;
            InteractionSystem.MenuOpen = true;
            GameAudio.Play(AudioCue.MenuOpen);
            _previousTimeScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            Inventory.Changed += RefreshCurrentTab;
            PartyRoster.Changed += RefreshCurrentTab;
            _canvas.SetActive(true);
            SelectTab(_tabIndex);
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            InteractionSystem.MenuOpen = false;
            GameAudio.Play(AudioCue.MenuClose);
            Time.timeScale = _previousTimeScale;
            Inventory.Changed -= RefreshCurrentTab;
            PartyRoster.Changed -= RefreshCurrentTab;
            foreach (var tab in _tabs) tab.Reset();
            if (_canvas != null) _canvas.SetActive(false);
        }

        void RefreshCurrentTab()
        {
            if (IsOpen) _tabs[_tabIndex].Refresh();
        }

        /// <summary>Opens the menu on a given tab (0 = Characters ... 4 = System).</summary>
        public void ShowTab(int index)
        {
            Open();
            SelectTab(index);
        }

        void SelectTab(int index)
        {
            _tabs[_tabIndex].Reset();
            _tabIndex = ((index % _tabs.Count) + _tabs.Count) % _tabs.Count;

            for (int i = 0; i < _tabs.Count; i++)
            {
                bool active = i == _tabIndex;
                _tabs[i].Root.gameObject.SetActive(active);
                _tabPlates[i].color = active ? UiFactory.Gold : UiFactory.KeyBorder;
                _tabLabels[i].color = active ? UiFactory.Ink : UiFactory.Cream;
                _tabLabels[i].shadow = !active;
            }

            _tabs[_tabIndex].Refresh();
        }

        void Build()
        {
            _tabs.Add(new CharactersTab());
            _tabs.Add(new EquipmentTab());
            _tabs.Add(new ItemsTab());
            _tabs.Add(new GrimoireTab());
            _tabs.Add(new SystemTab());

            // Built inactive so the pixel scaler lays the screen out the moment the menu is first opened.
            _canvas = new GameObject("OverworldMenuCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            _canvas.SetActive(false);
            _canvas.transform.SetParent(transform, false);

            var canvas = _canvas.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            _canvas.GetComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;

            var scaler = _canvas.AddComponent<PixelPerfectScaler>();
            var backdrop = UiFactory.Box(_canvas.transform, "Backdrop", new Color(UiFactory.Dim.r, UiFactory.Dim.g, UiFactory.Dim.b, 0.82f), 0, 0, 1, 1);
            UiFactory.Stretch(backdrop.rectTransform);

            var uiRoot = UiFactory.Rect(_canvas.transform, "UiRoot", 0, 0, UiFactory.ScreenWidth, UiFactory.ScreenHeight);
            scaler.SetUiRoot(uiRoot);

            UiFactory.Box(uiRoot, "Border", UiFactory.KeyBorder, PanelX, PanelY, PanelWidth, PanelHeight);
            UiFactory.Box(uiRoot, "Fill", UiFactory.Ink, PanelX + 2, PanelY + 2, PanelWidth - 4, PanelHeight - 4);

            for (int i = 0; i < _tabs.Count; i++)
            {
                float x = PanelX + 4 + i * (TabWidth + 2);
                var plate = UiFactory.Box(uiRoot, "Tab" + i, UiFactory.KeyBorder, x, PanelY + 5, TabWidth, 14);
                _tabPlates.Add(plate);
                _tabLabels.Add(UiFactory.Text(uiRoot, "TabLabel" + i, _tabs[i].Title, x, PanelY + 8, TabWidth, 8, PixelText.Align.Center, UiFactory.Cream));
            }

            UiFactory.Box(uiRoot, "TabRule", UiFactory.Gold, PanelX + 4, PanelY + 21, PanelWidth - 8, 1);

            var content = UiFactory.Rect(uiRoot, "Content", PanelX + 8, PanelY + 28, PanelWidth - 16, 192);
            foreach (var tab in _tabs) tab.Attach(content);

            UiFactory.Text(uiRoot, "Hints", "Q/E TABS   W/S/A/D MOVE   ENTER CHOOSE   ESC BACK", PanelX, PanelY + PanelHeight - 14, PanelWidth, 8, PixelText.Align.Center, UiFactory.KeyBorder);
        }
    }

    /// <summary>Adds the menu to the scene's <see cref="OverworldDirector"/> (scenes loaded later included) so no scene has to be edited.</summary>
    static class OverworldMenuBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            UnityEngine.SceneManagement.SceneManager.sceneLoaded += (_, __) => Attach();
            Attach();
        }

        static void Attach()
        {
            var director = Object.FindFirstObjectByType<OverworldDirector>();
            if (director == null) return;

            if (director.GetComponent<OverworldMenu>() == null) director.gameObject.AddComponent<OverworldMenu>();
            if (director.GetComponent<OverworldHud>() == null) director.gameObject.AddComponent<OverworldHud>();
            if (director.GetComponent<InteractionSystem>() == null) director.gameObject.AddComponent<InteractionSystem>();
            if (director.GetComponent<DemoEndPanel>() == null) director.gameObject.AddComponent<DemoEndPanel>();
        }
    }
}
