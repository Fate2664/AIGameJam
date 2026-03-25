using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

public class SettingsManager : MonoBehaviour
{
    public static SettingsManager Instance;
    
    public SettingsCollection AudioCollection;
    public SettingsCollection VideoCollection;
    public SettingsCollection GameplayColleciton;
    public SettingsCollection AccessibilityCollection;
    public SettingsMenu Menu;

    private Dictionary<string, Setting> settingsLookup;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(Instance);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(Instance);
    }

    private void Start()
    {
        settingsLookup = new Dictionary<string, Setting>();
        
        AddSettingsFromCollection(AudioCollection);
        AddSettingsFromCollection(GameplayColleciton);
        AddSettingsFromCollection(AccessibilityCollection);
        AddSettingsFromCollection(VideoCollection);
    }
    
    private void AddSettingsFromCollection(SettingsCollection collection)
    {
        if (collection == null || collection.Settings == null) return;

        foreach (var setting in collection.Settings)
        {
            if (!string.IsNullOrEmpty(setting.Key))
            {
                if (!settingsLookup.ContainsKey(setting.Key))
                {
                    settingsLookup.Add(setting.Key, setting);
                }
                else
                {
                    Debug.LogWarning($"Duplicate key: {setting.Key}");
                }
            }
        }
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += OnSceneLoaded;
    }
    private void OnDisable()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
    }
    
    #region GetDataTypes

    private float GetFloat(string key, float defualtValue)
    {
        if (settingsLookup != null && settingsLookup.TryGetValue(key, out Setting setting))
        {
            if (setting is FloatSetting floatSetting)
                return floatSetting.Value;
        }
        return defualtValue;
    }

    private bool GetBool(string key, bool defualtValue)
    {
        if (settingsLookup != null && settingsLookup.TryGetValue(key, out Setting setting))
        {
            if (setting is BoolSetting boolSetting)
                return boolSetting.IsChecked;
        }
        return defualtValue;
    }

    private int GetInt(string key, int defualtValue)
    {
        if (settingsLookup != null && settingsLookup.TryGetValue(key, out Setting setting))
        {
            if (setting is MultiOptionSetting multiOptionSetting)
                return multiOptionSetting.SelectedIndex;
        }
        return defualtValue;
    }
    
    #endregion

    public bool ParticEnabled => GetBool("ParticlesEnabled", true);
    public int Difficulty => GetInt("Difficulty", 0);
    public float MasterVolume => GetFloat("MasterVolume", 1f);
    public float MusicVolume => GetFloat("Music", 1f);
    public float EffectsVolume => GetFloat("Effects", 1f);
    public float MenuVolume => GetFloat("Menu", 1f);
    
    private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        
    }

    #region HandleSettings

    public void ResetAllSettings()
    {
        if (Menu == null) return;

        foreach (var collection in Menu.SettingsCollection)
        {
            foreach (var setting in collection.Settings)
            {
                setting.ResetToDefault();
            }
        }
        PlayerPrefs.Save();
        Menu.SettingsList.Refresh();        
    }

    public void LoadAllSettings()
    {
        if (Menu == null) return;

        foreach (var collection in Menu.SettingsCollection)
        {
            foreach (var setting in collection.Settings)
            {
                switch (setting)
                {
                    case BoolSetting boolSetting: boolSetting.Load(); break;
                    case FloatSetting floatSetting: floatSetting.Load(); break;
                    case MultiOptionSetting multiOptionSetting: multiOptionSetting.Load(); break;
                }
            }
        }
    }

    public void SaveAllSettings()
    {
        if  (Menu == null) return;

        foreach (var collection in Menu.SettingsCollection)
        {
            foreach (var setting in collection.Settings)
            {
                switch (setting)
                {
                    case BoolSetting boolSetting: boolSetting.Save(); break;
                    case FloatSetting floatSetting: floatSetting.Save(); break;
                    case MultiOptionSetting multiOptionSetting: multiOptionSetting.Save(); break;
                }
            }
        }
        PlayerPrefs.Save();
        Menu.SettingsList.Refresh();
    }

    public void UpdateSetting(Setting setting)
    {
        if (setting is FloatSetting floatSetting)
        {
            switch (floatSetting.category)
            {
                case Setting.SettingCategory.Audio:
                    //Update all volumes with AudioManager
                    break;
            }
        }
        else if (setting is MultiOptionSetting multiOptionSetting)
        {
            
        }
        else if (setting is BoolSetting boolSetting)
        {
            
        }
    }

    #endregion
}
