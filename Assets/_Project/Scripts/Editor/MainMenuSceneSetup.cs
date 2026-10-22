using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>Builds Assets/_Project/Scenes/MainMenu/MainMenu.unity (UGUI) and registers it as build scene 0.</summary>
public static class MainMenuSceneSetup
{
    const string ScenePath = "Assets/_Project/Scenes/MainMenu/MainMenu.unity";
    const string GameTitle = "Tomb of the Goblin King";

    static readonly Color Bg = new Color(0.05f, 0.07f, 0.06f);
    static readonly Color Gold = new Color(0.86f, 0.74f, 0.38f);
    static readonly Color ButtonBg = new Color(0.14f, 0.2f, 0.16f, 0.95f);
    static readonly Color PanelBg = new Color(0.08f, 0.11f, 0.09f, 0.95f);
    static readonly Color Track = new Color(0.2f, 0.25f, 0.2f);

    static Font font;

    [MenuItem("Tools/Menu/Build Main Menu Scene")]
    public static void Build()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        System.IO.Directory.CreateDirectory("Assets/_Project/Scenes/MainMenu");
        var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        var camGO = new GameObject("Main Camera") { tag = "MainCamera" };
        var cam = camGO.AddComponent<Camera>();
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = Bg;
        camGO.AddComponent<AudioListener>();

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();

        var canvasGO = new GameObject("MenuCanvas", typeof(RectTransform));
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;
        canvasGO.AddComponent<GraphicRaycaster>();

        var bg = Stretch(NewUI("Background", canvasGO.transform));
        bg.gameObject.AddComponent<Image>().color = Bg;

        // Main panel
        var main = Stretch(NewUI("MainPanel", canvasGO.transform));
        var title = NewText("Title", main, GameTitle, 96, Gold, TextAnchor.MiddleCenter);
        Anchor(title, new Vector2(0.5f, 0.5f), new Vector2(1600, 160), new Vector2(0, 250));
        title.gameObject.AddComponent<Shadow>().effectDistance = new Vector2(4, -4);

        var column = NewUI("Buttons", main);
        Anchor(column, new Vector2(0.5f, 0.5f), new Vector2(420, 400), new Vector2(0, -90));
        var vlg = column.gameObject.AddComponent<VerticalLayoutGroup>();
        vlg.spacing = 20; vlg.childAlignment = TextAnchor.UpperCenter;
        vlg.childControlWidth = vlg.childControlHeight = true;
        vlg.childForceExpandWidth = true; vlg.childForceExpandHeight = false;

        var start = NewButton("StartButton", column, "Start Game", 72);
        var load = NewButton("LoadButton", column, "Load Game", 72);
        var settings = NewButton("SettingsButton", column, "Settings", 72);
        var quit = NewButton("QuitButton", column, "Quit", 72);

        // Settings panel
        var sp = Stretch(NewUI("SettingsPanel", canvasGO.transform));
        var box = NewUI("Box", sp);
        Anchor(box, new Vector2(0.5f, 0.5f), new Vector2(800, 620), Vector2.zero);
        box.gameObject.AddComponent<Image>().color = PanelBg;
        var hdr = NewText("Header", box, "Settings", 60, Gold, TextAnchor.MiddleCenter);
        Anchor(hdr, new Vector2(0.5f, 1f), new Vector2(700, 90), new Vector2(0, -70));

        var rows = NewUI("Rows", box);
        Anchor(rows, new Vector2(0.5f, 0.5f), new Vector2(680, 330), new Vector2(0, 10));
        var rvlg = rows.gameObject.AddComponent<VerticalLayoutGroup>();
        rvlg.spacing = 30; rvlg.childAlignment = TextAnchor.UpperCenter;
        rvlg.childControlWidth = rvlg.childControlHeight = true;
        rvlg.childForceExpandWidth = true; rvlg.childForceExpandHeight = false;

        var slider = NewSlider(NewRow(rows, "Master Volume"));
        var toggle = NewToggle(NewRow(rows, "Fullscreen"));
        var dropdown = NewDropdown(NewRow(rows, "Quality"));

        var back = NewButton("BackButton", box, "Back", 70);
        Anchor(back.GetComponent<RectTransform>(), new Vector2(0.5f, 0f), new Vector2(300, 70), new Vector2(0, 70));

        // Wire controller
        var ctrl = canvasGO.AddComponent<MainMenuController>();
        var so = new SerializedObject(ctrl);
        so.FindProperty("mainPanel").objectReferenceValue = main.gameObject;
        so.FindProperty("settingsPanel").objectReferenceValue = sp.gameObject;
        so.FindProperty("startButton").objectReferenceValue = start;
        so.FindProperty("loadButton").objectReferenceValue = load;
        so.FindProperty("settingsButton").objectReferenceValue = settings;
        so.FindProperty("quitButton").objectReferenceValue = quit;
        so.FindProperty("volumeSlider").objectReferenceValue = slider;
        so.FindProperty("fullscreenToggle").objectReferenceValue = toggle;
        so.FindProperty("qualityDropdown").objectReferenceValue = dropdown;
        so.FindProperty("backButton").objectReferenceValue = back;
        so.ApplyModifiedPropertiesWithoutUndo();

        EditorSceneManager.SaveScene(scene, ScenePath);
        RegisterAsFirstBuildScene();
        Debug.Log("Main menu scene built at " + ScenePath);
    }

    static void RegisterAsFirstBuildScene()
    {
        var list = new System.Collections.Generic.List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
        list.RemoveAll(s => s.path == ScenePath);
        list.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
        EditorBuildSettings.scenes = list.ToArray();
    }

    // ---- helpers ----
    static RectTransform NewUI(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        return (RectTransform)go.transform;
    }

    static RectTransform Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        return rt;
    }

    static void Anchor(RectTransform rt, Vector2 anchor, Vector2 size, Vector2 pos)
    {
        rt.anchorMin = rt.anchorMax = anchor; rt.pivot = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = size; rt.anchoredPosition = pos;
    }

    static RectTransform NewText(string name, Transform parent, string content, int size, Color color, TextAnchor align)
    {
        var rt = NewUI(name, parent);
        var t = rt.gameObject.AddComponent<Text>();
        t.font = font; t.text = content; t.fontSize = size; t.color = color; t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        return rt;
    }

    static Button NewButton(string name, Transform parent, string label, int height)
    {
        var rt = NewUI(name, parent);
        var img = rt.gameObject.AddComponent<Image>(); img.color = ButtonBg;
        var btn = rt.gameObject.AddComponent<Button>(); btn.targetGraphic = img;
        var colors = btn.colors;
        colors.highlightedColor = new Color(1.4f, 1.3f, 0.9f);
        colors.selectedColor = colors.highlightedColor;
        colors.pressedColor = new Color(0.7f, 0.65f, 0.45f);
        btn.colors = colors;
        var le = rt.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = height; le.minHeight = height;
        Stretch(NewText("Label", rt, label, 36, Gold, TextAnchor.MiddleCenter));
        return btn;
    }

    static RectTransform NewRow(Transform parent, string label)
    {
        var row = NewUI(label + "Row", parent);
        var le = row.gameObject.AddComponent<LayoutElement>(); le.preferredHeight = 60; le.minHeight = 60;
        var t = NewText("Label", row, label, 34, Gold, TextAnchor.MiddleLeft);
        t.anchorMin = Vector2.zero; t.anchorMax = new Vector2(0.4f, 1); t.offsetMin = t.offsetMax = Vector2.zero;
        return row;
    }

    // Control area: right 55% of the row, vertically centered.
    static RectTransform Right(RectTransform parent, string name, float height)
    {
        var rt = NewUI(name, parent);
        rt.anchorMin = new Vector2(0.45f, 0.5f); rt.anchorMax = new Vector2(1, 0.5f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.offsetMin = new Vector2(0, -height / 2); rt.offsetMax = new Vector2(0, height / 2);
        return rt;
    }

    static Slider NewSlider(RectTransform row)
    {
        var rt = Right(row, "VolumeSlider", 30);
        var bgR = Stretch(NewUI("Background", rt));
        bgR.gameObject.AddComponent<Image>().color = Track;
        var fillArea = Stretch(NewUI("Fill Area", rt));
        var fill = Stretch(NewUI("Fill", fillArea));
        fill.gameObject.AddComponent<Image>().color = Gold;
        var handleArea = Stretch(NewUI("Handle Slide Area", rt));
        handleArea.offsetMin = new Vector2(12, 0); handleArea.offsetMax = new Vector2(-12, 0);
        var handle = NewUI("Handle", handleArea); handle.sizeDelta = new Vector2(24, 44);
        var handleImg = handle.gameObject.AddComponent<Image>(); handleImg.color = Color.white;
        var s = rt.gameObject.AddComponent<Slider>();
        s.fillRect = fill; s.handleRect = handle; s.targetGraphic = handleImg;
        s.minValue = 0; s.maxValue = 1; s.value = 1;
        return s;
    }

    static Toggle NewToggle(RectTransform row)
    {
        var rt = NewUI("FullscreenToggle", row);
        rt.anchorMin = rt.anchorMax = new Vector2(0.45f, 0.5f);
        rt.pivot = new Vector2(0, 0.5f); rt.sizeDelta = new Vector2(44, 44); rt.anchoredPosition = Vector2.zero;
        var bgR = Stretch(NewUI("Background", rt));
        var bgImg = bgR.gameObject.AddComponent<Image>(); bgImg.color = Track;
        var check = Stretch(NewUI("Checkmark", bgR));
        check.offsetMin = new Vector2(8, 8); check.offsetMax = new Vector2(-8, -8);
        var checkImg = check.gameObject.AddComponent<Image>(); checkImg.color = Gold;
        var t = rt.gameObject.AddComponent<Toggle>();
        t.targetGraphic = bgImg; t.graphic = checkImg; t.isOn = true;
        return t;
    }

    static Dropdown NewDropdown(RectTransform row)
    {
        var rt = Right(row, "QualityDropdown", 50);
        var img = rt.gameObject.AddComponent<Image>(); img.color = ButtonBg;
        var label = Stretch(NewText("Label", rt, "", 30, Gold, TextAnchor.MiddleLeft));
        label.offsetMin = new Vector2(16, 0); label.offsetMax = new Vector2(-40, 0);

        var template = NewUI("Template", rt);
        template.anchorMin = Vector2.zero; template.anchorMax = new Vector2(1, 0);
        template.pivot = new Vector2(0.5f, 1); template.sizeDelta = new Vector2(0, 200);
        template.anchoredPosition = Vector2.zero;
        template.gameObject.AddComponent<Image>().color = PanelBg;
        var sr = template.gameObject.AddComponent<ScrollRect>();
        sr.horizontal = false; sr.movementType = ScrollRect.MovementType.Clamped;
        var viewport = Stretch(NewUI("Viewport", template));
        viewport.gameObject.AddComponent<RectMask2D>();
        var content = NewUI("Content", viewport);
        content.anchorMin = new Vector2(0, 1); content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1); content.sizeDelta = new Vector2(0, 50);
        var item = NewUI("Item", content);
        item.anchorMin = new Vector2(0, 0.5f); item.anchorMax = new Vector2(1, 0.5f); item.sizeDelta = new Vector2(0, 50);
        var itemBg = Stretch(NewUI("Item Background", item));
        var itemBgImg = itemBg.gameObject.AddComponent<Image>(); itemBgImg.color = ButtonBg;
        var itemLabel = Stretch(NewText("Item Label", item, "", 30, Gold, TextAnchor.MiddleLeft));
        itemLabel.offsetMin = new Vector2(16, 0);
        var itemToggle = item.gameObject.AddComponent<Toggle>();
        itemToggle.targetGraphic = itemBgImg;
        sr.viewport = viewport; sr.content = content;
        template.gameObject.SetActive(false);

        var dd = rt.gameObject.AddComponent<Dropdown>();
        dd.targetGraphic = img; dd.template = template;
        dd.captionText = label.GetComponent<Text>(); dd.itemText = itemLabel.GetComponent<Text>();
        return dd;
    }
}
