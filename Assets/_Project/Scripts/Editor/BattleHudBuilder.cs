using System.Linq;
using Game.Combat.UI.UGUI;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Builds the battle HUD from the UI spec: a 480x270 reference screen shown at integer scale, laid out at the spec's coordinates,
    /// with the sliced battle UI sheet as its art. It rewrites the existing prefabs IN PLACE (BattleHUD, AllyNameplate, EnemyNameplate)
    /// so their identities — and the CombatSystemConfig reference to the HUD — survive. Re-runnable.
    /// Run via: Tools/Combat/Build Battle HUD
    /// </summary>
    public static class BattleHudBuilder
    {
        private const string SheetPath = "Assets/_Project/Art/UI/battle_ui_sheet_3x.png";
        private const string FolderPath = "Assets/_Project/Prefabs/Combat/TrailsBattle3D/UI";
        public const string HudPath = FolderPath + "/BattleHUD.prefab";
        public const string AllyNameplatePath = FolderPath + "/AllyNameplate.prefab";
        public const string EnemyNameplatePath = FolderPath + "/EnemyNameplate.prefab";

        [MenuItem("Tools/Combat/Build Battle HUD")]
        public static void Run()
        {
            Build();
        }

        /// <summary>Builds (or rebuilds) the three prefabs and returns the HUD prefab asset.</summary>
        public static GameObject Build()
        {
            var sprites = AssetDatabase.LoadAllAssetsAtPath(SheetPath).OfType<Sprite>().ToArray();
            if (sprites.Length == 0)
            {
                Debug.LogError("[BattleHudBuilder] No sprites found in " + SheetPath + " — run Tools/UI/Slice Battle UI Sheet first.");
                return null;
            }

            EnsureFolder(FolderPath);

            // No floating name/HP nameplates over characters any more (the cards carry that information); the HUD skips them when
            // these prefab references are null.
            CombatantNameplate allyPlate = null;
            CombatantNameplate enemyPlate = null;

            var root = LoadOrCreate(HudPath, "BattleHUD");
            try
            {
                Clear(root);

                var hudRoot = GetOrAdd<BattleHudRoot>(root);
                var spriteHolder = GetOrAdd<BattleUiSprites>(root);
                spriteHolder.SetSprites(sprites);
                var input = GetOrAdd<BattleInputController>(root);

                BuildEventSystem(root.transform);
                var canvasObject = BuildCanvas(root.transform, out var scaler);

                var plateLayer = UiFactory.Rect(canvasObject.transform, "NameplateLayer", 0, 0, 0, 0);
                UiFactory.Stretch(plateLayer);

                // Anchored to the real screen edges (not the letterboxed 480x270 frame), so the cards sit in the very corners at any window
                // size. Created before UiRoot so the modals in UiRoot draw over them.
                var cornerLayer = UiFactory.Rect(canvasObject.transform, "ScreenEdges", 0, 0, 0, 0);
                UiFactory.Stretch(cornerLayer);
                var heroCorner = Anchored(cornerLayer, "HeroCorner", new Vector2(0f, 0f), 130, UnitCardView.Height, 0, 0);
                var enemyCorner = Anchored(cornerLayer, "EnemyCorner", new Vector2(1f, 0f), UnitCardView.Width, UnitCardView.Height, 0, 0);
                var topCentre = Anchored(cornerLayer, "TopCentre", new Vector2(0.5f, 1f), UiFactory.ScreenWidth, 40, 0, 0);
                var bottomCentre = Anchored(cornerLayer, "BottomCentre", new Vector2(0.5f, 0f), DamageRollBar.Width, DamageRollBar.Height, 0, 2);

                var heroCard = Add<UnitCardView>(heroCorner, "HeroCard", 0, 0, UnitCardView.Width, UnitCardView.Height);
                heroCard.Build(spriteHolder, enemy: false);
                var commandBar = Add<CommandBarView>(heroCorner, "CommandBar", 0, 0, 130, UnitCardView.Height);
                commandBar.Build(spriteHolder);
                var pips = Add<ApPipMeter>(heroCorner, "ApPips", 2, -25, 140, 13);
                pips.Build(spriteHolder);

                var enemyCard = Add<UnitCardView>(enemyCorner, "EnemyCard", 0, 0, UnitCardView.Width, UnitCardView.Height);
                enemyCard.Build(spriteHolder, enemy: true);
                var hitBadge = Add<HitBadgeView>(enemyCorner, "HitBadge", 2, -29, 56, 16);
                hitBadge.Build(spriteHolder);

                var turnSlider = Add<TurnSliderView>(topCentre, "TurnSlider", 0, 0, UiFactory.ScreenWidth, 40);
                turnSlider.Build(spriteHolder);

                // The damage calculation after each attack, bottom centre.
                var rollLog = Add<DamageRollBar>(bottomCentre, "DamageCalculation", 0, 0, DamageRollBar.Width, DamageRollBar.Height);
                rollLog.Build(spriteHolder);

                var uiRoot = UiFactory.Rect(canvasObject.transform, "UiRoot", 0, 0, UiFactory.ScreenWidth, UiFactory.ScreenHeight);
                scaler.SetUiRoot(uiRoot);

                var spellHand = Add<SpellHandView>(uiRoot, "SpellHand", 0, 0, UiFactory.ScreenWidth, UiFactory.ScreenHeight);
                spellHand.Build(spriteHolder);

                var tooltip = Add<DetailTooltipView>(uiRoot, "DetailTooltip", 170, 58, 140, 26);
                tooltip.Build(spriteHolder);
                var ribbon = Add<ModeRibbonView>(uiRoot, "ModeRibbon", 208, 216, 64, 16);
                ribbon.Build(spriteHolder);

                var floating = Add<FloatingNumbersView>(uiRoot, "FloatingNumbers", 0, 0, UiFactory.ScreenWidth, UiFactory.ScreenHeight);
                floating.Build(spriteHolder);

                var reticle = Add<ReticleView>(uiRoot, "Reticle", 0, 0, UiFactory.ScreenWidth, UiFactory.ScreenHeight);
                reticle.Build(spriteHolder);

                var flee = Add<FleePromptView>(uiRoot, "FleePrompt", 0, 0, UiFactory.ScreenWidth, UiFactory.ScreenHeight);
                flee.Build(spriteHolder);
                var result = Add<EncounterResultPanel>(uiRoot, "EncounterResultPanel", 0, 0, UiFactory.ScreenWidth, UiFactory.ScreenHeight);
                result.Build(spriteHolder);

                // No world-space "current turn" marker over the acting character any more.
                ActiveTurnIndicator indicator = null;

                hudRoot.Assemble(spriteHolder, input, uiRoot, heroCard, enemyCard, pips, spellHand, tooltip, hitBadge,
                    turnSlider, commandBar, ribbon, flee, reticle, floating, rollLog, result, allyPlate, enemyPlate, plateLayer, indicator);

                heroCard.Show(null);
                enemyCard.Show(null);

                PrefabUtility.SaveAsPrefabAsset(root, HudPath);
                Debug.Log("[BattleHudBuilder] Battle HUD built at " + HudPath);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            AssetDatabase.SaveAssets();
            return AssetDatabase.LoadAssetAtPath<GameObject>(HudPath);
        }

        // ------------------------------------------------------------------ pieces

        private static CombatantNameplate BuildNameplate(string path, Color fill)
        {
            var root = LoadOrCreate(path, System.IO.Path.GetFileNameWithoutExtension(path));
            try
            {
                Clear(root);
                var plate = GetOrAdd<CombatantNameplate>(root);
                if (root.GetComponent<RectTransform>() == null)
                {
                    root.AddComponent<RectTransform>();
                }

                plate.Build(fill);
                PrefabUtility.SaveAsPrefabAsset(root, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }

            return AssetDatabase.LoadAssetAtPath<GameObject>(path).GetComponent<CombatantNameplate>();
        }

        private static void BuildEventSystem(Transform parent)
        {
            // Only the result screen has a clickable button; everything else in the battle ignores the mouse.
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(parent, false);
        }

        private static GameObject BuildCanvas(Transform parent, out PixelPerfectScaler scaler)
        {
            var go = new GameObject("OverlayCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 0;

            var canvasScaler = go.GetComponent<CanvasScaler>();
            canvasScaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
            canvasScaler.scaleFactor = 1f;

            scaler = go.AddComponent<PixelPerfectScaler>();
            return go;
        }

        /// <summary>A small pointer that bobs above whoever is acting. It lives in the world (a world-space canvas), outside the 480x270 screen.</summary>
        private static ActiveTurnIndicator BuildActiveTurnIndicator(Transform parent, BattleUiSprites sprites)
        {
            var go = new GameObject("ActiveTurnIndicator", typeof(RectTransform), typeof(Canvas));
            go.transform.SetParent(parent, false);

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = 5;

            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(27f, 15f);
            rect.localScale = Vector3.one * 0.04f;

            var marker = UiFactory.Image(go.transform, "Marker", sprites.Get("marker_pointer_ally"), 0, 0, 27, 15);
            marker.preserveAspect = true;

            return go.AddComponent<ActiveTurnIndicator>();
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>A rectangle pinned to a screen edge or corner (anchor and pivot are the same point); children are laid out from its top-left.</summary>
        private static RectTransform Anchored(RectTransform parent, string name, Vector2 anchor, float width, float height, float x, float y)
        {
            var rect = UiFactory.Rect(parent, name, 0, 0, width, height);
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = anchor;
            rect.anchoredPosition = new Vector2(x, y);
            return rect;
        }

        private static T Add<T>(RectTransform parent, string name, float x, float y, float width, float height) where T : Component
        {
            var rect = UiFactory.Rect(parent, name, x, y, width, height);
            return rect.gameObject.AddComponent<T>();
        }

        private static T GetOrAdd<T>(GameObject go) where T : Component
        {
            var existing = go.GetComponent<T>();
            return existing != null ? existing : go.AddComponent<T>();
        }

        /// <summary>Loads the prefab's contents for editing, or makes a new prefab asset first if there is none yet.</summary>
        private static GameObject LoadOrCreate(string path, string name)
        {
            if (AssetDatabase.LoadAssetAtPath<GameObject>(path) == null)
            {
                var seed = new GameObject(name, typeof(RectTransform));
                PrefabUtility.SaveAsPrefabAsset(seed, path);
                Object.DestroyImmediate(seed);
            }

            return PrefabUtility.LoadPrefabContents(path);
        }

        private static void Clear(GameObject root)
        {
            for (int i = root.transform.childCount - 1; i >= 0; i--)
            {
                Object.DestroyImmediate(root.transform.GetChild(i).gameObject);
            }
        }

        private static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
            {
                return;
            }

            string parent = System.IO.Path.GetDirectoryName(path)?.Replace('\\', '/');
            if (!string.IsNullOrEmpty(parent))
            {
                EnsureFolder(parent);
                AssetDatabase.CreateFolder(parent, System.IO.Path.GetFileName(path));
            }
        }
    }
}
