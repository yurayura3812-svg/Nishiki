using System;
using System.IO;
using UnityEngine;

[Serializable]
public class AppSettingsSave
{
    public string language;
    public bool adminTools;
}

/// <summary>
/// 言語と管理者ツールのオンオフ。persistentDataPath に残す。
/// </summary>
public static class AppSettingsStore
{
    static AppSettingsSave cached;
    static string PathName => Path.Combine(Application.persistentDataPath, "KoiSaves", "settings.json");

    public static AppSettingsSave Load()
    {
        if (cached != null) return cached;
        if (!File.Exists(PathName))
        {
            cached = Fresh();
            return cached;
        }
        try
        {
            cached = JsonUtility.FromJson<AppSettingsSave>(File.ReadAllText(PathName));
            if (cached == null) cached = Fresh();
            if (cached.language != Loc.En) cached.language = Loc.Ja;
            return cached;
        }
        catch (Exception e)
        {
            Debug.LogWarning("AppSettingsStore: 読めませんでした: " + e.Message);
            cached = Fresh();
            return cached;
        }
    }

    public static void Save(AppSettingsSave save)
    {
        if (save == null) return;
        cached = save;
        Directory.CreateDirectory(Path.GetDirectoryName(PathName) ?? ".");
        File.WriteAllText(PathName, JsonUtility.ToJson(save, true));
    }

    public static bool AdminTools
    {
        get => Load().adminTools;
        set
        {
            var save = Load();
            save.adminTools = value;
            Save(save);
        }
    }

    static AppSettingsSave Fresh()
    {
        return new AppSettingsSave
        {
            language = Application.systemLanguage == SystemLanguage.English ? Loc.En : Loc.Ja,
            adminTools = false
        };
    }
}
