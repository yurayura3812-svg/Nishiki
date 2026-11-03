using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

/// <summary>
/// Assets/KoiData の個体を BuiltinKoiCatalog に載せる。ビルド前にも必ず同期する。
/// </summary>
public class KoiDataBuildSync : AssetPostprocessor, IPreprocessBuildWithReport
{
    const string Folder = "Assets/KoiData";
    const string CatalogPath = "Assets/Resources/BuiltinKoiCatalog.asset";

    public int callbackOrder => 0;

    public void OnPreprocessBuild(BuildReport report)
    {
        Sync(true);
    }

    [InitializeOnLoadMethod]
    static void EnsureCatalog()
    {
        EditorApplication.delayCall += () => Sync(false);
    }

    [MenuItem("Nishiki/初期個体をビルド用に同期")]
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
        if (!Directory.Exists(Folder))
        {
            if (log) Debug.LogWarning("KoiDataBuildSync: " + Folder + " がありません。");
            return;
        }

        var catalog = AssetDatabase.LoadAssetAtPath<BuiltinKoiCatalog>(CatalogPath);
        if (catalog == null)
        {
            catalog = ScriptableObject.CreateInstance<BuiltinKoiCatalog>();
            AssetDatabase.CreateAsset(catalog, CatalogPath);
        }

        var found = new List<KoiPatternData>();
        foreach (var guid in AssetDatabase.FindAssets("t:KoiPatternData", new[] { Folder }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var data = AssetDatabase.LoadAssetAtPath<KoiPatternData>(path);
            if (data != null) found.Add(data);
        }

        found.Sort((a, b) => string.CompareOrdinal(a.name, b.name));

        bool changed = catalog.koi == null || catalog.koi.Length != found.Count;
        if (!changed)
        {
            for (int i = 0; i < found.Count; i++)
            {
                if (catalog.koi[i] != found[i])
                {
                    changed = true;
                    break;
                }
            }
        }

        if (!changed)
        {
            if (log) Debug.Log("BuiltinKoiCatalog: 変更なし（" + found.Count + " 体）");
            return;
        }

        catalog.koi = found.ToArray();
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssets();
        if (log) Debug.Log("BuiltinKoiCatalog: " + found.Count + " 体をビルド用に載せました。");
    }
}
