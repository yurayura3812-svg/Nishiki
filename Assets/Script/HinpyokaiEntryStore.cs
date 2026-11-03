using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
class HinpyokaiEntryFile
{
    public List<string> keys = new List<string>();
}

/// <summary>
/// 品評会に出した個体。出品ボタン経由だけ残す。
/// </summary>
public static class HinpyokaiEntryStore
{
    static string PathName => Path.Combine(Application.persistentDataPath, "KoiSaves", "hinpyokai.json");

    public static List<string> LoadKeys()
    {
        var file = Read();
        return file.keys ?? new List<string>();
    }

    public static void Submit(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var file = Read();
        if (file.keys == null) file.keys = new List<string>();
        if (!file.keys.Contains(key)) file.keys.Add(key);
        Write(file);
    }

    public static void Remove(string key)
    {
        if (string.IsNullOrEmpty(key)) return;
        var file = Read();
        if (file.keys == null) return;
        file.keys.Remove(key);
        Write(file);
    }

    static HinpyokaiEntryFile Read()
    {
        if (!File.Exists(PathName)) return new HinpyokaiEntryFile();
        try
        {
            var file = JsonUtility.FromJson<HinpyokaiEntryFile>(File.ReadAllText(PathName));
            if (file == null) return new HinpyokaiEntryFile();
            if (file.keys == null) file.keys = new List<string>();
            return file;
        }
        catch (Exception e)
        {
            Debug.LogWarning("HinpyokaiEntryStore: 読めませんでした: " + e.Message);
            return new HinpyokaiEntryFile();
        }
    }

    static void Write(HinpyokaiEntryFile file)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(PathName) ?? ".");
        File.WriteAllText(PathName, JsonUtility.ToJson(file, true));
    }
}
