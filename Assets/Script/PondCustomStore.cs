using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEngine;

[Serializable]
public class PondCustomSave
{
    public List<string> swimmingKoiIds = new List<string>();
    public bool koiSelectionSaved;
    public string floorId;
}

[Serializable]
public class PondFloorOption
{
    public string id;
    public string displayName;
    public Texture2D texture;
}

/// <summary>
/// 池カスタム（泳がせる個体・底の素材）を persistentDataPath に残す。
/// 底素材は Assets/UIimage/floor の画像（PondFloorCatalog）。
/// </summary>
public static class PondCustomStore
{
    static PondFloorOption[] cachedFloors;
    static readonly Regex TrailingCopy = new Regex(@"\s+\(\d+\)$");

    static readonly Dictionary<string, string> LegacyKeys = new Dictionary<string, string>
    {
        { "natural", "自然" },
        { "moss", "苔" },
        { "pink", "ピンク" },
        { "rainbow", "虹" }
    };

    static string PathName => System.IO.Path.Combine(Application.persistentDataPath, "KoiSaves", "pond.json");

    public static PondFloorOption[] FloorOptions
    {
        get
        {
            if (cachedFloors == null) cachedFloors = LoadFloors();
            return cachedFloors;
        }
    }

    public static string DefaultFloorId
    {
        get
        {
            var floors = FloorOptions;
            return floors.Length > 0 ? floors[0].id : "";
        }
    }

    public static PondCustomSave Load()
    {
        if (!File.Exists(PathName)) return new PondCustomSave();
        try
        {
            var save = JsonUtility.FromJson<PondCustomSave>(File.ReadAllText(PathName));
            if (save == null) return new PondCustomSave();
            if (save.swimmingKoiIds == null) save.swimmingKoiIds = new List<string>();
            if (string.IsNullOrEmpty(save.floorId)) save.floorId = DefaultFloorId;
            return save;
        }
        catch (Exception e)
        {
            Debug.LogWarning("PondCustomStore: 読めませんでした: " + e.Message);
            return new PondCustomSave();
        }
    }

    public static void Save(PondCustomSave save)
    {
        if (save == null) return;
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(PathName) ?? ".");
        File.WriteAllText(PathName, JsonUtility.ToJson(save, true));
    }

    public static string KoiKey(KoiPatternData data)
    {
        if (data == null) return "";
        if (!string.IsNullOrEmpty(data.koiId)) return data.koiId;
        return data.name;
    }

    public static List<KoiPatternData> FilterSwimming(List<KoiPatternData> all)
    {
        var save = Load();
        if (all == null) return new List<KoiPatternData>();
        if (!save.koiSelectionSaved) return all;

        var ids = new HashSet<string>(save.swimmingKoiIds ?? new List<string>());
        var result = new List<KoiPatternData>();
        foreach (var k in all)
        {
            if (k != null && ids.Contains(KoiKey(k))) result.Add(k);
        }
        return result;
    }

    public static PondFloorOption FindFloor(string id)
    {
        var floors = FloorOptions;
        if (floors.Length == 0) return null;
        if (string.IsNullOrEmpty(id)) return floors[0];

        foreach (var f in floors)
        {
            if (f.id == id) return f;
        }

        if (LegacyKeys.TryGetValue(id, out var key))
        {
            foreach (var f in floors)
            {
                if ((f.id != null && f.id.Contains(key))
                    || (f.displayName != null && f.displayName.Contains(key)))
                    return f;
            }
        }

        return floors[0];
    }

    public static void ApplyFloor(Renderer renderer)
    {
        if (renderer == null) return;
        var floor = FindFloor(Load().floorId);
        var mat = renderer.material;
        var tex = floor != null ? floor.texture : null;
        if (tex != null)
        {
            mat.SetTexture("_BaseMap", tex);
            mat.SetTexture("_DetailAlbedoMap", tex);
        }
        mat.SetColor("_BaseColor", Color.white);
    }

    static PondFloorOption[] LoadFloors()
    {
        var list = new List<PondFloorOption>();
        var seen = new HashSet<Texture2D>();

        var catalog = Resources.Load<PondFloorCatalog>("PondFloorCatalog");
        if (catalog != null && catalog.entries != null)
        {
            foreach (var e in catalog.entries)
            {
                if (e == null || e.texture == null) continue;
                if (!seen.Add(e.texture)) continue;
                if (string.IsNullOrEmpty(e.id)) e.id = e.texture.name;
                if (string.IsNullOrEmpty(e.displayName))
                    e.displayName = TrailingCopy.Replace(e.texture.name, "");
                list.Add(e);
            }
        }

        var extra = Resources.LoadAll<Texture2D>("floor");
        if (extra != null)
        {
            foreach (var tex in extra)
            {
                if (tex == null || !seen.Add(tex)) continue;
                list.Add(new PondFloorOption
                {
                    id = tex.name,
                    displayName = TrailingCopy.Replace(tex.name, ""),
                    texture = tex
                });
            }
        }

        return list.ToArray();
    }
}
