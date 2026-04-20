using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic; // ★追加：行列（Queue）を使うために必要

public class KoiPhotoStudio : MonoBehaviour
{
    public static KoiPhotoStudio Instance;

    [Header("--- 撮影用設定 ---")]
    public Camera studioCamera;      
    public KoiController studioKoi;  
    public RenderTexture targetRT;   

    // ★撮影待ちの行列
    private Queue<(KoiPatternData data, RawImage ui)> captureQueue = new Queue<(KoiPatternData, RawImage)>();
    private bool isCapturing = false;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 撮影を依頼する（行列に並ぶ）
    /// </summary>
    public void RequestCapture(KoiPatternData data, RawImage targetUI)
    {
        captureQueue.Enqueue((data, targetUI));
        
        // まだ撮影中でなければ、行列の処理を開始する
        if (!isCapturing)
        {
            StartCoroutine(ProcessQueue());
        }
    }

    private IEnumerator ProcessQueue()
    {
        isCapturing = true;

        while (captureQueue.Count > 0)
        {
            var request = captureQueue.Dequeue();
            
            // 実際の撮影コルーチンが終わるのを待つ
            yield return StartCoroutine(CaptureRoutine(request.data, request.ui));
        }

        isCapturing = false;
    }

    private IEnumerator CaptureRoutine(KoiPatternData data, RawImage targetUI)
    {
        // すでに撮影済みならセットだけして終了
        if (data.photoTexture != null)
        {
            if (targetUI != null) targetUI.texture = data.photoTexture;
            yield break;
        }

        // --- 撮影処理 ---
        studioKoi.gameObject.SetActive(true);
        studioKoi.patternData = data;
        studioKoi.ApplyDNA();

        // ★DNAの反映（テクスチャ生成）をしっかり待つ！
        // 1フレームではなく、少し待つとより安定します
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame(); 

        RenderTexture.active = targetRT;Texture2D photo = new Texture2D(targetRT.width, targetRT.height, TextureFormat.RGBA32, false, true);
        photo.ReadPixels(new Rect(0, 0, targetRT.width, targetRT.height), 0, 0);
        photo.Apply();
        RenderTexture.active = null;

        photo.name = "Photo_" + data.name;
        data.photoTexture = photo;

#if UNITY_EDITOR
        // アセットのパスを取得
        string path = UnityEditor.AssetDatabase.GetAssetPath(data);
        if (!string.IsNullOrEmpty(path))
        {
            // 1. 古い写真がすでに埋め込まれていれば削除する（ミスマッチ防止）
            Object[] subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var asset in subAssets)
            {
                if (asset is Texture2D && asset != data) 
                {
                    // data.photoTextureに今入れたもの以外の子アセットを掃除
                    if (asset != data.photoTexture)
                    {
                        UnityEditor.AssetDatabase.RemoveObjectFromAsset(asset);
                        DestroyImmediate(asset, true);
                    }
                }
            }

            // 2. 新しい写真をアセットに埋め込む
            UnityEditor.AssetDatabase.AddObjectToAsset(data.photoTexture, data);
            
            // 3. 変更を確定して保存
            UnityEditor.EditorUtility.SetDirty(data);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.ImportAsset(path);
            
            Debug.Log($"<color=cyan>【PhotoStudio】</color> {data.name} の写真をアセットに埋め込みました");
        }
#endif

        studioKoi.gameObject.SetActive(false);

        if (targetUI != null)
        {
            targetUI.texture = data.photoTexture;
        }
    }
}