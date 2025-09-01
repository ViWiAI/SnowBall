using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEngine.Audio;
using UnityEngine.Localization.Settings;

public class SettingsManager : MonoBehaviour
{
    // ����ģʽ��ȷ��ȫ��Ψһ
    public static SettingsManager Instance { get; private set; }

    [SerializeField] private AudioMixer audioMixer; // ��Ƶ����������ڿ�������
    [SerializeField] private string bgmVolumeParameter = "BGMVolume"; // AudioMixer �� BGM ������
    [SerializeField] private string sfxVolumeParameter = "SFXVolume"; // AudioMixer �� SFX ������

    // ֧�ֵķֱ����б�
    private readonly Resolution[] supportedResolutions = new Resolution[]
    {
        new Resolution { width = 1280, height = 720 },
        new Resolution { width = 1920, height = 1080 },
        new Resolution { width = 2560, height = 1440 },
        new Resolution { width = 3840, height = 2160 }
    };

    // Ĭ������
    private const string RESOLUTION_INDEX_KEY = "ResolutionIndex";
    private const string BGM_ENABLED_KEY = "BGMEnabled";
    private const string BGM_VOLUME_KEY = "BGMVolume";
    private const string SFX_VOLUME_KEY = "SFXVolume";
    private const string LANGUAGE_KEY = "Language";

    private void Awake()
    {
        // ������ʼ��
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // ���ֶ����ڳ����л�ʱ������
        }
        else
        {
            Destroy(gameObject);
        }

        // ��ʼ������
        ApplySavedSettings();
    }

    // ��ʼ����Ӧ�ñ��������
    private void ApplySavedSettings()
    {
        // �ֱ���
        int savedResolutionIndex = PlayerPrefs.GetInt(RESOLUTION_INDEX_KEY, GetDefaultResolutionIndex());
        SetResolution(savedResolutionIndex);

        // �������ֿ���
        bool bgmEnabled = PlayerPrefs.GetInt(BGM_ENABLED_KEY, 1) == 1;
        SetBGMEnabled(bgmEnabled);

        // ����
        float bgmVolume = PlayerPrefs.GetFloat(BGM_VOLUME_KEY, 1f);
        float sfxVolume = PlayerPrefs.GetFloat(SFX_VOLUME_KEY, 1f);
        SetBGMVolume(bgmVolume);
        SetSFXVolume(sfxVolume);

        // ����
        string language = PlayerPrefs.GetString(LANGUAGE_KEY, GetDefaultLanguage());
        SetLanguage(language);
    }

    // �ֱ��ʹ���
    public void SetResolution(int resolutionIndex)
    {
        if (resolutionIndex < 0 || resolutionIndex >= supportedResolutions.Length)
        {
            resolutionIndex = GetDefaultResolutionIndex();
        }

        Resolution res = supportedResolutions[resolutionIndex];
        // ʹ�� refreshRateRatio ��ȡ��ǰ��Ļ��ˢ���ʣ��Է�����ʽ��
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
        // Ĭ��ѡ����߷ֱ���
        return supportedResolutions.Length - 1;
    }

    // �������ֿ���
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

    // ������������
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

    // ��Ч����
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

    // �������ã�ʹ�� Unity Localization ����
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

    // ����������ת��Ϊ�ֱ������� AudioMixer��
    private float LinearToDecibel(float linear)
    {
        if (linear <= 0) return -80f; // ����
        return Mathf.Log10(linear) * 20f;
    }
}