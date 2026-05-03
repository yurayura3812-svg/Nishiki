#if UNITY_EDITOR
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace KoiPond.EditorTools
{
    /// <summary>
    /// 選択した複数のテクスチャを Texture2DArray アセットに変換するエディタツール。
    ///
    /// メニュー: Tools > KoiPond > Build Caustics Texture Array
    ///
    /// 使い方:
    ///   1. Project ウィンドウで 16 枚のテクスチャを選択 (caust_00.png 〜 caust_15.png)
    ///   2. メニューから実行
    ///   3. 同じフォルダに caustics_array.asset が生成される
    ///
    /// 注意:
    ///   - 全テクスチャが同じサイズ・フォーマットである必要があります
    ///   - 取り込み時に自動で Read/Write を有効化します
    ///   - 名前順 (caust_00 → caust_01 → ...) で配列インデックスが決まります
    /// </summary>
    public static class CausticsArrayBuilder
    {
        [MenuItem("Tools/KoiPond/Build Caustics Texture Array")]
        public static void BuildArray()
        {
            var selected = Selection.objects.OfType<Texture2D>().ToArray();
            if (selected.Length < 2)
            {
                EditorUtility.DisplayDialog("Caustics Array",
                    "2枚以上の Texture2D を選択してください。\n" +
                    $"現在選択数: {selected.Length}", "OK");
                return;
            }

            // 名前順にソート (caust_00, caust_01, ...)
            var textures = selected.OrderBy(t => t.name).ToArray();

            // すべて Read/Write 有効化
            foreach (var tex in textures)
            {
                string path = AssetDatabase.GetAssetPath(tex);
                var ti = AssetImporter.GetAtPath(path) as TextureImporter;
                if (ti != null && !ti.isReadable)
                {
                    ti.isReadable = true;
                    ti.SaveAndReimport();
                }
            }

            int width  = textures[0].width;
            int height = textures[0].height;

            // サイズ統一チェック
            for (int i = 1; i < textures.Length; i++)
            {
                if (textures[i].width != width || textures[i].height != height)
                {
                    EditorUtility.DisplayDialog("Caustics Array",
                        $"テクスチャサイズが揃っていません。\n" +
                        $"0番目: {width}x{height}\n" +
                        $"{i}番目 ({textures[i].name}): {textures[i].width}x{textures[i].height}",
                        "OK");
                    return;
                }
            }

            // Texture2DArray を生成
            var array = new Texture2DArray(
                width, height, textures.Length,
                TextureFormat.RGBA32, true /* mipmaps off は false に */);
            array.wrapMode = TextureWrapMode.Repeat;
            array.filterMode = FilterMode.Bilinear;

            // 各テクスチャを配列にコピー
            for (int i = 0; i < textures.Length; i++)
            {
                Color[] pixels = textures[i].GetPixels();
                array.SetPixels(pixels, i, 0);
            }
            array.Apply(updateMipmaps: true, makeNoLongerReadable: false);

            // 保存先パス
            string firstAssetPath = AssetDatabase.GetAssetPath(textures[0]);
            string folder = Path.GetDirectoryName(firstAssetPath).Replace('\\', '/');
            string outPath = $"{folder}/caustics_array.asset";

            AssetDatabase.CreateAsset(array, outPath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            Debug.Log($"[CausticsArrayBuilder] Texture2DArray を作成: {outPath}\n" +
                      $"  サイズ: {width}x{height}\n" +
                      $"  スライス数: {textures.Length}\n" +
                      $"  使用したフレーム順: {string.Join(", ", textures.Select(t => t.name))}");

            EditorUtility.DisplayDialog("Caustics Array",
                $"Texture2DArray を作成しました!\n\n" +
                $"パス: {outPath}\n" +
                $"サイズ: {width}x{height}\n" +
                $"スライス数: {textures.Length}\n\n" +
                $"このアセットを M_Koi と水底マテリアルの Caustics Array にセットしてください。",
                "OK");

            var generated = AssetDatabase.LoadAssetAtPath<Texture2DArray>(outPath);
            Selection.activeObject = generated;
            EditorGUIUtility.PingObject(generated);
        }
    }
}
#endif
