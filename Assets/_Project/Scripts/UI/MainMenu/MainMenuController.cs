using Game.Audio;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Title screen: Start Game / Settings / Quit. Load Game is a hidden placeholder until saves exist.</summary>
public class MainMenuController : MonoBehaviour
{
    public const string MasterVolumeKey = "settings.masterVolume";
    public const string FullscreenKey = "settings.fullscreen";
    public const string QualityKey = "settings.quality";

    [SerializeField] string startScene = "Dungeon_01";

    [Header("Panels")]
    [SerializeField] GameObject mainPanel;
    [SerializeField] GameObject settingsPanel;

    [Header("Main buttons")]
    [SerializeField] Button startButton;
    [SerializeField] Button loadButton; // hidden until a save system exists
    [SerializeField] Button settingsButton;
    [SerializeField] Button quitButton;

    [Header("Settings controls")]
    [SerializeField] Slider volumeSlider;
    [SerializeField] Toggle fullscreenToggle;
    [SerializeField] Dropdown qualityDropdown;
    [SerializeField] Button backButton;

    void Awake()
    {
        if (loadButton != null) loadButton.gameObject.SetActive(false);

        startButton.onClick.AddListener(StartGame);
        settingsButton.onClick.AddListener(() => ShowSettings(true));
        quitButton.onClick.AddListener(Quit);
        backButton.onClick.AddListener(() => ShowSettings(false));

        volumeSlider.onValueChanged.AddListener(SetVolume);
        fullscreenToggle.onValueChanged.AddListener(SetFullscreen);
        qualityDropdown.onValueChanged.AddListener(SetQuality);

        LoadSettings();
        ShowSettings(false);
        GameAudio.SetMusicState(AudioMusicState.Menu);
    }

    void Update()
    {
        if (settingsPanel.activeSelf && Input.GetKeyDown(KeyCode.Escape)) ShowSettings(false);
    }

    void StartGame() => SceneManager.LoadScene(startScene);

    void Quit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    void ShowSettings(bool show)
    {
        settingsPanel.SetActive(show);
        mainPanel.SetActive(!show);
        var first = show ? (Selectable)volumeSlider : startButton;
        first.Select();
    }

    void LoadSettings()
    {
        var names = QualitySettings.names;
        qualityDropdown.ClearOptions();
        qualityDropdown.AddOptions(new System.Collections.Generic.List<string>(names));

        float vol = PlayerPrefs.GetFloat(MasterVolumeKey, 1f);
        bool full = PlayerPrefs.GetInt(FullscreenKey, Screen.fullScreen ? 1 : 0) == 1;
        int quality = Mathf.Clamp(PlayerPrefs.GetInt(QualityKey, QualitySettings.GetQualityLevel()), 0, names.Length - 1);

        volumeSlider.SetValueWithoutNotify(vol);
        fullscreenToggle.SetIsOnWithoutNotify(full);
        qualityDropdown.SetValueWithoutNotify(quality);

        AudioListener.volume = vol;
        GameAudio.SetBusVolume(AudioBus.Master, vol);
        QualitySettings.SetQualityLevel(quality);
        Screen.fullScreen = full;
    }

    void SetVolume(float v)
    {
        AudioListener.volume = v;
        GameAudio.SetBusVolume(AudioBus.Master, v);
        PlayerPrefs.SetFloat(MasterVolumeKey, v);
    }

    void SetFullscreen(bool on)
    {
        Screen.fullScreen = on;
        PlayerPrefs.SetInt(FullscreenKey, on ? 1 : 0);
    }

    void SetQuality(int level)
    {
        QualitySettings.SetQualityLevel(level);
        PlayerPrefs.SetInt(QualityKey, level);
    }

    void OnDisable() => PlayerPrefs.Save();
}
