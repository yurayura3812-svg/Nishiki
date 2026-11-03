using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

[Serializable]
public class KoiSaveRecord
{
    public string koiId;
    public string displayName;
    public float[] mainColor;
    public float[] sub1Color;
    public float sub1Scale;
    public float sub1Amount;
    public float sub1Detail;
    public float[] sub2Color;
    public float sub2Scale;
    public float sub2Amount;
    public float sub2Detail;
    public float seedX;
    public float seedY;
    public float seedZ;
    public float bellyLimit;
    public string photoFile;
}

[Serializable]
public class KoiSaveFile
{
    public List<KoiSaveRecord> koi = new List<KoiSaveRecord>();
}

/// <summary>
/// ビルドでも残る個体保存。JSON + PNG を persistentDataPath に書く。
/// 初期個体は Editor の Assets/KoiData、または Resources/KoiData。
/// </summary>
public static class KoiSaveStore
{
    static string Dir => Path.Combine(Application.persistentDataPath, "KoiSaves");
    static string JsonPath => Path.Combine(Dir, "koi.json");
    static string PhotoDir => Path.Combine(Dir, "photos");

    public static List<KoiPatternData> LoadAll()
    {
        var result = new List<KoiPatternData>();
        var seen = new HashSet<string>();

        foreach (var data in LoadBuiltIn())
        {
            if (data == null) continue;
            if (seen.Add(KeyOf(data))) result.Add(data);
        }

        foreach (var data in LoadSaved())
        {
            if (data == null) continue;
            if (seen.Add(KeyOf(data))) result.Add(data);
        }

        return result;
    }

    public static string UniqueDisplayName(string baseName)
    {
        if (string.IsNullOrEmpty(baseName)) baseName = "名無しの錦鯉";
        var names = new HashSet<string>();
        foreach (var k in LoadAll())
        {
            if (k != null) names.Add(k.name);
        }

        if (!names.Contains(baseName)) return baseName;
        int i = 1;
        while (names.Contains($"{baseName} {i}")) i++;
        return $"{baseName} {i}";
    }

    public static void Save(KoiPatternData data)
    {
        if (data == null) return;

        Directory.CreateDirectory(Dir);
        Directory.CreateDirectory(PhotoDir);

        if (string.IsNullOrEmpty(data.koiId))
            data.koiId = Guid.NewGuid().ToString("N");

        var file = ReadFile();
        var rec = FromAsset(data);
        int idx = file.koi.FindIndex(k => k != null && k.koiId == rec.koiId);
        if (idx >= 0) file.koi[idx] = rec;
        else file.koi.Add(rec);

        File.WriteAllText(JsonPath, JsonUtility.ToJson(file, true));
        Debug.Log($"<color=cyan>KoiSaveStore:</color> {data.name} を保存しました ({JsonPath})");
    }

    static string KeyOf(KoiPatternData data)
    {
        if (!string.IsNullOrEmpty(data.koiId)) return "id:" + data.koiId;
        return "name:" + data.name;
    }

    static KoiSaveFile ReadFile()
    {
        if (!File.Exists(JsonPath)) return new KoiSaveFile();
        try
        {
            var file = JsonUtility.FromJson<KoiSaveFile>(File.ReadAllText(JsonPath));
            if (file == null || file.koi == null) return new KoiSaveFile();
            return file;
        }
        catch (Exception e)
        {
            Debug.LogWarning("KoiSaveStore: JSON を読めませんでした: " + e.Message);
            return new KoiSaveFile();
        }
    }

    static List<KoiPatternData> LoadBuiltIn()
    {
        var list = new List<KoiPatternData>();
        var fromResources = Resources.LoadAll<KoiPatternData>("KoiData");
        if (fromResources != null && fromResources.Length > 0)
            list.AddRange(fromResources);

#if UNITY_EDITOR
        const string folderPath = "Assets/KoiData";
        if (Directory.Exists(folderPath))
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:KoiPatternData", new[] { folderPath });
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var data = UnityEditor.AssetDatabase.LoadAssetAtPath<KoiPatternData>(path);
                if (data != null) list.Add(data);
            }
        }
#endif
        return list;
    }

    static List<KoiPatternData> LoadSaved()
    {
        var list = new List<KoiPatternData>();
        foreach (var rec in ReadFile().koi)
        {
            if (rec != null) list.Add(ToAsset(rec));
        }
        return list;
    }

    static float[] ToArr(Color c) => new[] { c.r, c.g, c.b, c.a };

    static Color ToColor(float[] a)
    {
        if (a == null || a.Length < 3) return Color.white;
        return new Color(a[0], a[1], a[2], a.Length > 3 ? a[3] : 1f);
    }

    static KoiSaveRecord FromAsset(KoiPatternData d)
    {
        var rec = new KoiSaveRecord
        {
            koiId = d.koiId,
            displayName = d.name,
            mainColor = ToArr(d.mainColor),
            sub1Color = ToArr(d.sub1Color),
            sub1Scale = d.sub1Scale,
            sub1Amount = d.sub1Amount,
            sub1Detail = d.sub1Detail,
            sub2Color = ToArr(d.sub2Color),
            sub2Scale = d.sub2Scale,
            sub2Amount = d.sub2Amount,
            sub2Detail = d.sub2Detail,
            seedX = d.patternSeed.x,
            seedY = d.patternSeed.y,
            seedZ = d.patternSeed.z,
            bellyLimit = d.bellyLimit
        };

        if (d.photoTexture != null)
        {
            rec.photoFile = d.koiId + ".png";
            try
            {
                byte[] png = d.photoTexture.EncodeToPNG();
                if (png != null && png.Length > 0)
                    File.WriteAllBytes(Path.Combine(PhotoDir, rec.photoFile), png);
            }
            catch (Exception e)
            {
                Debug.LogWarning("KoiSaveStore: 写真を書けませんでした: " + e.Message);
                rec.photoFile = null;
            }
        }

        return rec;
    }

    static KoiPatternData ToAsset(KoiSaveRecord rec)
    {
        var d = ScriptableObject.CreateInstance<KoiPatternData>();
        d.koiId = rec.koiId;
        d.name = string.IsNullOrEmpty(rec.displayName) ? rec.koiId : rec.displayName;
        d.mainColor = ToColor(rec.mainColor);
        d.sub1Color = ToColor(rec.sub1Color);
        d.sub1Scale = rec.sub1Scale;
        d.sub1Amount = rec.sub1Amount;
        d.sub1Detail = rec.sub1Detail;
        d.sub2Color = ToColor(rec.sub2Color);
        d.sub2Scale = rec.sub2Scale;
        d.sub2Amount = rec.sub2Amount;
        d.sub2Detail = rec.sub2Detail;
        d.patternSeed = new Vector3(rec.seedX, rec.seedY, rec.seedZ);
        d.bellyLimit = rec.bellyLimit;

        if (!string.IsNullOrEmpty(rec.photoFile))
        {
            string path = Path.Combine(PhotoDir, rec.photoFile);
            if (File.Exists(path))
            {
                var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                tex.LoadImage(File.ReadAllBytes(path));
                tex.name = "Photo_" + d.name;
                d.photoTexture = tex;
            }
        }

        return d;
    }
}
