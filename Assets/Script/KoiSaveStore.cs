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
    public float gloss;
    public float body;
    public bool hasStats;
}

[Serializable]
public class KoiSaveFile
{
    public List<KoiSaveRecord> koi = new List<KoiSaveRecord>();
    public List<string> deletedKeys = new List<string>();
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

    public static event Action<KoiPatternData> OnDeleted;

    public static List<KoiPatternData> LoadAll()
    {
        var result = new List<KoiPatternData>();
        var seen = new HashSet<string>();

        var deleted = DeletedSet();
        var saved = ReadFile().koi;

        foreach (var data in LoadBuiltIn())
        {
            if (data == null) continue;
            var key = KeyOf(data);
            if (deleted.Contains(key)) continue;
            if (!seen.Add(key)) continue;

            var live = UnityEngine.Object.Instantiate(data);
            live.name = data.name;
            live.koiId = data.koiId;
            OverlaySaved(live, FindSaved(saved, data));
            result.Add(live);
        }

        foreach (var data in LoadSaved())
        {
            if (data == null) continue;
            var key = KeyOf(data);
            if (deleted.Contains(key)) continue;
            if (seen.Add(key)) result.Add(data);
        }

        return result;
    }

    public static string UniqueDisplayName(string baseName)
    {
        if (string.IsNullOrEmpty(baseName)) baseName = Loc.Get("koi.unnamed");
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

        if (string.IsNullOrEmpty(data.koiId) && !IsNamelessBuiltIn(data.name))
            data.koiId = Guid.NewGuid().ToString("N");

        var file = ReadFile();
        var rec = FromAsset(data);
        int idx = IndexOfRecord(file.koi, data);
        if (string.IsNullOrEmpty(rec.photoFile) && idx >= 0 && !string.IsNullOrEmpty(file.koi[idx].photoFile))
            rec.photoFile = file.koi[idx].photoFile;
        if (idx >= 0) file.koi[idx] = rec;
        else file.koi.Add(rec);

        File.WriteAllText(JsonPath, JsonUtility.ToJson(file, true));
        Debug.Log($"<color=cyan>KoiSaveStore:</color> {data.name} を保存しました ({JsonPath})");
    }

    public static string KeyOf(KoiPatternData data)
    {
        if (data == null) return "";
        if (!string.IsNullOrEmpty(data.koiId)) return "id:" + data.koiId;
        return "name:" + data.name;
    }

    public static void Delete(KoiPatternData data)
    {
        if (data == null) return;
        var key = KeyOf(data);
        var file = ReadFile();
        if (file.deletedKeys == null) file.deletedKeys = new List<string>();
        if (!string.IsNullOrEmpty(key) && !file.deletedKeys.Contains(key))
            file.deletedKeys.Add(key);

        file.koi.RemoveAll(k =>
            k != null && (
                KeyOfSaved(k) == key
                || (!string.IsNullOrEmpty(data.koiId) && k.koiId == data.koiId)
                || k.displayName == data.name));

        Directory.CreateDirectory(Dir);
        File.WriteAllText(JsonPath, JsonUtility.ToJson(file, true));

        TryDeletePhoto(data.koiId);
        PondCustomStore.RemoveSwimming(data);
        HinpyokaiEntryStore.Remove(key);
        if (KoiDetailGate.EnteredKoiKey == key) KoiDetailGate.EnteredKoiKey = null;
        OnDeleted?.Invoke(data);
        Debug.Log($"<color=cyan>KoiSaveStore:</color> {data.name} を削除しました");
    }

    static HashSet<string> DeletedSet()
    {
        var file = ReadFile();
        var set = new HashSet<string>();
        if (file.deletedKeys == null) return set;
        foreach (var k in file.deletedKeys)
        {
            if (!string.IsNullOrEmpty(k)) set.Add(k);
        }
        return set;
    }

    public static string AliasDisplayName(string name)
    {
        switch (name)
        {
            case "紅白": return "あかね";
            case "大正三色": return "こはく";
            case "昭和三色": return "すみ";
            case "白写り": return "しらゆき";
            case "緋写り": return "べに";
            case "山吹黄金": return "きいろ";
            case "プラチナ": return "ぎん";
            case "空鯉": return "そら";
            case "黒": return "くろ";
            case "koi": return "みずき";
            case "test": return "ぽち";
            case "名無しの錦鯉": return "ひな";
            default: return name;
        }
    }

    static bool NamesMatch(string a, string b)
    {
        if (a == b) return true;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return false;
        return AliasDisplayName(a) == b || a == AliasDisplayName(b) || AliasDisplayName(a) == AliasDisplayName(b);
    }

    static bool IsNamelessBuiltIn(string displayName)
    {
        if (string.IsNullOrEmpty(displayName)) return false;
        foreach (var b in LoadBuiltIn())
        {
            if (b != null && b.name == displayName && string.IsNullOrEmpty(b.koiId))
                return true;
        }
        return false;
    }

    static int IndexOfRecord(List<KoiSaveRecord> list, KoiPatternData data)
    {
        if (list == null || data == null) return -1;
        if (!string.IsNullOrEmpty(data.koiId))
        {
            int byId = list.FindIndex(k => k != null && k.koiId == data.koiId);
            if (byId >= 0) return byId;
        }
        return list.FindIndex(k =>
            k != null && NamesMatch(k.displayName, data.name) &&
            (string.IsNullOrEmpty(k.koiId) || string.IsNullOrEmpty(data.koiId)));
    }

    static KoiSaveRecord FindSaved(List<KoiSaveRecord> list, KoiPatternData data)
    {
        int i = IndexOfRecord(list, data);
        return i >= 0 ? list[i] : null;
    }

    static void OverlaySaved(KoiPatternData live, KoiSaveRecord rec)
    {
        if (live == null || rec == null) return;
        if (rec.hasStats)
        {
            live.gloss = rec.gloss;
            live.body = rec.body;
            live.hasStats = true;
        }
        var photo = LoadPhoto(rec);
        if (photo != null) live.photoTexture = photo;
    }

    public static string PhotoFileName(KoiPatternData d)
    {
        if (d == null) return "koi.png";
        if (!string.IsNullOrEmpty(d.koiId)) return d.koiId + ".png";
        var n = string.IsNullOrEmpty(d.name) ? "koi" : d.name;
        foreach (var c in Path.GetInvalidFileNameChars())
            n = n.Replace(c, '_');
        return "name_" + n + ".png";
    }

    static Texture2D LoadPhoto(KoiSaveRecord rec)
    {
        if (rec == null || string.IsNullOrEmpty(rec.photoFile)) return null;
        string path = Path.Combine(PhotoDir, rec.photoFile);
        if (!File.Exists(path)) return null;
        try
        {
            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(File.ReadAllBytes(path))) return null;
            tex.name = "Photo_" + rec.displayName;
            return tex;
        }
        catch (Exception e)
        {
            Debug.LogWarning("KoiSaveStore: 写真を読めませんでした: " + e.Message);
            return null;
        }
    }

    static string KeyOfSaved(KoiSaveRecord rec)
    {
        if (rec == null) return "";
        if (!string.IsNullOrEmpty(rec.koiId)) return "id:" + rec.koiId;
        return "name:" + rec.displayName;
    }

    static void TryDeletePhoto(string koiId)
    {
        if (string.IsNullOrEmpty(koiId)) return;
        try
        {
            var path = Path.Combine(PhotoDir, koiId + ".png");
            if (File.Exists(path)) File.Delete(path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("KoiSaveStore: 写真を消せませんでした: " + e.Message);
        }
    }

    public static KoiPatternData Find(string key)
    {
        if (string.IsNullOrEmpty(key)) return null;
        foreach (var koi in LoadAll())
        {
            if (koi != null && KeyOf(koi) == key) return koi;
        }
        return null;
    }

    static KoiSaveFile ReadFile()
    {
        if (!File.Exists(JsonPath)) return new KoiSaveFile();
        try
        {
            var file = JsonUtility.FromJson<KoiSaveFile>(File.ReadAllText(JsonPath));
            if (file == null) return new KoiSaveFile();
            if (file.koi == null) file.koi = new List<KoiSaveRecord>();
            if (file.deletedKeys == null) file.deletedKeys = new List<string>();
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
        var catalog = Resources.Load<BuiltinKoiCatalog>("BuiltinKoiCatalog");
        if (catalog != null && catalog.koi != null)
        {
            foreach (var data in catalog.koi)
            {
                if (data == null || catalog.IsHidden(data)) continue;
                list.Add(data);
            }
        }

        var fromResources = Resources.LoadAll<KoiPatternData>("KoiData");
        if (fromResources != null)
        {
            foreach (var data in fromResources)
            {
                if (data != null) list.Add(data);
            }
        }

#if UNITY_EDITOR
        const string folderPath = "Assets/KoiData";
        if (Directory.Exists(folderPath))
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("t:KoiPatternData", new[] { folderPath });
            foreach (string guid in guids)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guid);
                var data = UnityEditor.AssetDatabase.LoadAssetAtPath<KoiPatternData>(path);
                if (data == null) continue;
                if (catalog != null && catalog.IsHidden(data)) continue;
                list.Add(data);
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
            bellyLimit = d.bellyLimit,
            gloss = d.gloss,
            body = d.body,
            hasStats = d.hasStats
        };

        if (d.photoTexture != null && d.photoTexture.isReadable)
        {
            rec.photoFile = PhotoFileName(d);
            try
            {
                byte[] png = d.photoTexture.EncodeToPNG();
                if (png != null && png.Length > 0)
                    File.WriteAllBytes(Path.Combine(PhotoDir, rec.photoFile), png);
                else
                    rec.photoFile = null;
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
        d.gloss = rec.hasStats ? rec.gloss : 40f;
        d.body = rec.hasStats ? rec.body : 40f;
        d.hasStats = rec.hasStats;

        d.photoTexture = LoadPhoto(rec);

        return d;
    }
}
