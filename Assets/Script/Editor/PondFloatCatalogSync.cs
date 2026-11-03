using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/UIimage/float のサブフォルダを PondFloatCatalog に載せる。
/// </summary>
public class PondFloatCatalogSync : AssetPostprocessor
{
    const string Folder = "Assets/UIimage/float";
    const string CatalogPath = "Assets/Resources/PondFloatCatalog.asset";

    static readonly Dictionary<string, string> Names = new Dictionary<string, string>
    {
        { "sakura", "さくら" },
        { "koyo", "もみじ" },
        { "ityo", "イチョウ" },
        { "ichou", "イチョウ" },
        { "icho", "イチョウ" },
        { "mone", "モネの池" },
        { "ahiru", "アヒル" },
        { "duck", "アヒル" }
    };

    [InitializeOnLoadMethod]
    static void EnsureCatalog()
    {
        EditorApplication.delayCall += () => Sync(false);
    }

    [MenuItem("Nishiki/浮かべるカタログを更新")]
    static void SyncMenu()
    {
        Sync(true);
    }

    static void OnPostprocessAllAssets(
        string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (Touches(imported) || Touches(deleted) || Touches(moved) || Touches(movedFrom))
            Sync(false);
    }

    static bool Touches(string[] paths)
    {
        if (paths == null) return false;
        foreach (var p in paths)
        {
            if (!string.IsNullOrEmpty(p) && p.Replace('\\', '/').StartsWith(Folder))
                return true;
        }
        return false;
    }

    static void Sync(bool log)
    {
        if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            AssetDatabase.CreateFolder("Assets", "Resources");
        if (!AssetDatabase.IsValidFolder(Folder))
        {
            if (log) Debug.LogWarning("PondFloatCatalogSync: " + Folder + " がありません。");
            return;
        }

        var catalog = AssetDatabase.LoadAssetAtPath<PondFloatCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<PondFloatCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        var byFolder = new Dictionary<string, List<Texture2D>>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid).Replace('\\', '/');
            var dir = Path.GetDirectoryName(path).Replace('\\', '/');
            if (dir == Folder) continue;
            var id = Path.GetFileName(dir);
            if (string.IsNullOrEmpty(id) || id == "huji" || id == "fuji") continue;
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex == null) continue;
            if (!byFolder.TryGetValue(id, out var list))
            {
                list = new List<Texture2D>();
                byFolder[id] = list;
            }
            list.Add(tex);
        }

        var ids = new List<string>(byFolder.Keys);
        ids.Sort((a, b) => Rank(a).CompareTo(Rank(b)));

        var entries = new List<PondFloatSet>();
        foreach (var id in ids)
        {
            var list = byFolder[id];
            list.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
            entries.Add(new PondFloatSet
            {
                id = id,
                displayName = Names.TryGetValue(id, out var n) ? n : id,
                textures = list.ToArray()
            });
        }

        catalog.entries = entries.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        if (log) Debug.Log("PondFloatCatalog: " + entries.Count + " セットに更新しました。");
    }

    static int Rank(string id)
    {
        if (id == "sakura") return 0;
        if (id == "koyo") return 1;
        if (id == "ityo" || id == "ichou" || id == "icho") return 3;
        if (id == "mone") return 4;
        if (id == "ahiru" || id == "duck") return 5;
        return 50;
    }
}
