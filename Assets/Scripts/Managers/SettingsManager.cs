using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Audio;
using UnityEngine.Localization.Settings;

public class SettingsManager : MonoBehaviour
{
    // 单例模式，确保全局唯一
    public static SettingsManager Instance { get; private set; }

    [SerializeField] private AudioMixer audioMixer; // 音频混合器，用于控制音量
    [SerializeField] private string bgmVolumeParameter = "BGMVolume"; // AudioMixer 的 BGM 参数名
    [SerializeField] private string sfxVolumeParameter = "SFXVolume"; // AudioMixer 的 SFX 参数名

    // 支持的分辨率列表
    private readonly Resolution[] supportedResolutions = new Resolution[]
    {
        new Resolution { width = 1280, height = 720 },
        new Resolution { width = 1920, height = 1080 },
        new Resolution { width = 2560, height = 1440 },
        new Resolution { width = 3840, height = 2160 }
    };

    // 默认设置
    private const string RESOLUTION_INDEX_KEY = "ResolutionIndex";
    private const string BGM_ENABLED_KEY = "BGMEnabled";
    private const string BGM_VOLUME_KEY = "BGMVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string LANGUAGE_KEY = "Language";

    private void Awake()
    {
        // 单例初始化
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 保持对象在场景切换时不销毁
        }
        else
        {
            Destroy(gameObject);
        }

        // 初始化设置
        ApplySavedSettings();
    }

    // 初始化并应用保存的设置
    private void ApplySavedSettings()
    {
        // 分辨率
        int savedResolutionIndex = PlayerPrefs.GetInt(RESOLUTION_INDEX_KEY, GetDefaultResolutionIndex());
        SetResolution(savedResolutionIndex);

        // 背景音乐开关
        bool bgmEnabled = PlayerPrefs.GetInt(BGM_ENABLED_KEY, 1) == 1;
        SetBGMEnabled(bgmEnabled);

        // 音量
        float bgmVolume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 1f);
        float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
        SetBGMVolume(bgmVolume);
        SetSFXVolume(sfxVolume);

        // 语言
        string language = PlayerPrefs.GetString(LANGUAGE_KEY, GetDefaultLanguage());
        SetLanguage(language);
    }

    // 分辨率管理
    public void SetResolution(int resolutionIndex)
    {
        if (resolutionIndex < 0 || resolutionIndex >= supportedResolutions.Length)
        {
            resolutionIndex = GetDefaultResolutionIndex();
        }

        Resolution res = supportedResolutions[resolutionIndex];
        // 使用 refreshRateRatio 获取当前屏幕的刷新率（以分数形式）
        RefreshRate refreshRate = Screen.currentResolution.refreshRateRatio;
        Screen.SetResolution(res.width, res.height, FullScreenMode.FullScreenWindow, refreshRate);
        PlayerPrefs.SetInt(RESOLUTION_INDEX_KEY, resolutionIndex);
        PlayerPrefs.Save();
    }

    public Resolution[] GetSupportedResolutions()
    {
        return supportedResolutions;
    }

    public int GetCurrentResolutionIndex()
    {
        return PlayerPrefs.GetInt(RESOLUTION_INDEX_KEY, GetDefaultResolutionIndex());
    }

    private int GetDefaultResolutionIndex()
    {
        // 默认选择最高分辨率
        return supportedResolutions.Length - 1;
    }

    // 背景音乐开关
    public void SetBGMEnabled(bool enabled)
    {
        float volume = enabled ? PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 1f) : 0f;
        audioMixer.SetFloat(bgmVolumeParameter, LinearToDecibel(volume));
        PlayerPrefs.SetInt(BGM_ENABLED_KEY, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    public bool GetBGMEnabled()
    {
        return PlayerPrefs.GetInt(BGM_ENABLED_KEY, 1) == 1;
    }

    // 背景音乐音量
    public void SetBGMVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        if (GetBGMEnabled())
        {
            audioMixer.SetFloat(bgmVolumeParameter, LinearToDecibel(volume));
        }
        PlayerPrefs.SetFloat(BGM_VOLUME_KEY, volume);
        PlayerPrefs.Save();
    }

    public float GetBGMVolume()
    {
        return PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 1f);
    }

    // 音效音量
    public void SetSFXVolume(float volume)
    {
        volume = Mathf.Clamp01(volume);
        audioMixer.SetFloat(sfxVolumeParameter, LinearToDecibel(volume));
        PlayerPrefs.SetFloat(SFX_VOLUME_KEY, volume);
        PlayerPrefs.Save();
    }

    public float GetSFXVolume()
    {
        return PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
    }

    // 语言设置（使用 Unity Localization 包）
    public void SetLanguage(string languageCode)
    {
        var availableLocales = LocalizationSettings.AvailableLocales.Locales;
        var locale = availableLocales.Find(l => l.Identifier.Code == languageCode);
        if (locale != null)
        {
            LocalizationSettings.SelectedLocale = locale;
            PlayerPrefs.SetString(LANGUAGE_KEY, languageCode);
            PlayerPrefs.Save();
        }
        else
        {
            Debug.LogWarning($"Language code {languageCode} not found, using default.");
            SetLanguage(GetDefaultLanguage());
        }
    }

    public string GetCurrentLanguage()
    {
        return PlayerPrefs.GetString(LANGUAGE_KEY, GetDefaultLanguage());
    }

    public string[] GetAvailableLanguages()
    {
        return LocalizationSettings.AvailableLocales.Locales.Select(l => l.Identifier.Code).ToArray();
    }

    private string GetDefaultLanguage()
    {
        return LocalizationSettings.AvailableLocales.Locales[0].Identifier.Code;
    }

    // 将线性音量转换为分贝（用于 AudioMixer）
    private float LinearToDecibel(float linear)
    {
        if (linear <= 0) return -80f; // 静音
        return Mathf.Log10(linear) * 20f;
    }
}