using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class KoiPhotoStudio : MonoBehaviour
{
    public static KoiPhotoStudio Instance;

    [Header("--- 撮影用設定 ---")]
    public Camera studioCamera;      
    public KoiController studioKoi;  
    public RenderTexture targetRT;   

    // ★修正：データ、UI、そして「終わった後の処理(Action)」をセットで並べる
    private Queue<(KoiPatternData data, RawImage ui, System.Action callback)> captureQueue 
        = new Queue<(KoiPatternData, RawImage, System.Action)>();
    
    private bool isCapturing = false;

    void Awake()
    {
        Instance = this;
    }

    /// <summary>
    /// 撮影を依頼する。終わったら callback を実行する。
    /// </summary>
    public void RequestCapture(KoiPatternData data, System.Action callback)
    {
        // UIへの反映が不要な場合は null を渡して行列に追加
        captureQueue.Enqueue((data, null, callback));
        
        if (!isCapturing)
        {
            StartCoroutine(ProcessQueue());
        }
    }

    // 交配画面などで使う「UIに映すだけ」のオーバーロード（既存との互換性用）
    public void RequestCapture(KoiPatternData data, RawImage targetUI)
    {
        captureQueue.Enqueue((data, targetUI, null));
        if (!isCapturing) StartCoroutine(ProcessQueue());
    }

    private IEnumerator ProcessQueue()
    {
        isCapturing = true;

        while (captureQueue.Count > 0)
        {
            var request = captureQueue.Dequeue();
            
            // 撮影ルーチンを実行。終わるまで待機。
            yield return StartCoroutine(CaptureRoutine(request.data, request.ui));

            // ★撮影が完全に終わったので、依頼主に「終わったよ！」と報告
            request.callback?.Invoke();
        }

        isCapturing = false;
    }

    private IEnumerator CaptureRoutine(KoiPatternData data, RawImage targetUI)
    {
        // --- 撮影準備 ---
        studioKoi.gameObject.SetActive(true);
        studioKoi.patternData = data;
        studioKoi.ApplyDNA();

        // 反映を待つ（1フレームだと不安なら2〜3フレーム待機）
        yield return new WaitForEndOfFrame();
        yield return new WaitForEndOfFrame(); 

        // --- 実際の撮影（ピクセル読み込み） ---
        RenderTexture.active = targetRT;
        Texture2D photo = new Texture2D(targetRT.width, targetRT.height, TextureFormat.RGBA32, false, true);
        photo.ReadPixels(new Rect(0, 0, targetRT.width, targetRT.height), 0, 0);
        photo.Apply();
        RenderTexture.active = null;

        photo.name = "Photo_" + data.name;
        data.photoTexture = photo;

        // --- 撮影完了後、UIがあれば反映 ---
        if (targetUI != null) targetUI.texture = data.photoTexture;
        
        studioKoi.gameObject.SetActive(false);
        yield break;
    }
}