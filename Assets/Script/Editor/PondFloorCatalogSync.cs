using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Assets/UIimage/floor の PNG を PondFloorCatalog に載せる。足した分は末尾に追加する。
/// </summary>
public class PondFloorCatalogSync : AssetPostprocessor
{
    const string Folder = "Assets/UIimage/floor";
    const string CatalogPath = "Assets/Resources/PondFloorCatalog.asset";

    static readonly Regex TrailingCopy = new Regex(@"\s+\(\d+\)$");

    [InitializeOnLoadMethod]
    static void EnsureCatalog()
    {
        EditorApplication.delayCall += () => Sync(false);
    }

    [MenuItem("Nishiki/底素材カタログを更新")]
    static void SyncMenu()
    {
        Sync(true);
    }

    static void OnPostprocessAllAssets(
        string[] imported, string[] deleted, string[] moved, string[] movedFrom)
    {
        if (TouchesFloorFolder(imported) || TouchesFloorFolder(deleted)
            || TouchesFloorFolder(moved) || TouchesFloorFolder(movedFrom))
            Sync(false);
    }

    static bool TouchesFloorFolder(string[] paths)
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
            if (log) Debug.LogWarning("PondFloorCatalogSync: " + Folder + " がありません。");
            return;
        }

        var catalog = AssetDatabase.LoadAssetAtPath<PondFloorCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<PondFloorCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        var found = new List<Texture2D>();
        foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            if (tex != null) found.Add(tex);
        }

        found.Sort((a, b) => Rank(a.name).CompareTo(Rank(b.name)) != 0
            ? Rank(a.name).CompareTo(Rank(b.name))
            : string.CompareOrdinal(a.name, b.name));

        var existing = catalog.entries != null
            ? new List<PondFloorOption>(catalog.entries)
            : new List<PondFloorOption>();

        existing.RemoveAll(e => e == null || e.texture == null || !found.Contains(e.texture));

        var known = new HashSet<Texture2D>();
        foreach (var e in existing)
            known.Add(e.texture);

        if (existing.Count == 0)
        {
            foreach (var tex in found)
                existing.Add(FromTexture(tex));
        }
        else
        {
            foreach (var tex in found)
            {
                if (known.Contains(tex)) continue;
                existing.Add(FromTexture(tex));
            }
        }

        bool changed = catalog.entries == null || catalog.entries.Length != existing.Count;
        if (!changed)
        {
            for (int i = 0; i < existing.Count; i++)
            {
                var a = catalog.entries[i];
                var b = existing[i];
                if (a.id != b.id || a.displayName != b.displayName || a.texture != b.texture)
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
        {
            if (log) Debug.Log("PondFloorCatalog: 変更なし（" + existing.Count + " 枚）");
            return;
        }

        catalog.entries = existing.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        if (log) Debug.Log("PondFloorCatalog: " + existing.Count + " 枚に更新しました。");
    }

    static PondFloorOption FromTexture(Texture2D tex)
    {
        return new PondFloorOption
        {
            id = tex.name,
            displayName = TrailingCopy.Replace(tex.name, ""),
            texture = tex
        };
    }

    static int Rank(string name)
    {
        if (name.Contains("自然")) return 0;
        if (name.Contains("苔")) return 1;
        if (name.Contains("ピンク")) return 2;
        if (name.Contains("虹")) return 3;
        return 100;
    }
}
