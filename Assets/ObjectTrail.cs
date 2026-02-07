using UnityEngine;
using UnityEngine.InputSystem;

public class ObjectTrail : MonoBehaviour
{
    [Header("Settings")]
    public Material drawMaterial;
    public RenderTexture rtA; 
    public GameObject waterObject; 
    [Range(0.01f, 1.0f)] public float brushSize = 0.2f;

    void Update()
    {
        if (waterObject == null || rtA == null || drawMaterial == null) return;

        // 1. マウスからレイ（光線）を飛ばす
        Vector2 mousePos = Mouse.current.position.ReadValue();
        Ray ray = Camera.main.ScreenPointToRay(mousePos);
        RaycastHit hit;

        // 2. 水面モデルの「コライダー」に当たった場所を特定する
        // ※水面オブジェクトに Mesh Collider がついている必要があります
        if (Physics.Raycast(ray, out hit))
        {
            if (hit.collider.gameObject == waterObject)
            {
                // キューブを当たった場所に移動
                transform.position = hit.point;

                // 【重要】テクスチャ上の座標（UV）を、Unityが自動で計算した値(hit.textureCoord)から取得
                // これなら、スケールが1000倍でも、軸が逆でも、絶対にズレません
                Vector2 uv = hit.textureCoord;

                // 3. 描き込み処理
                drawMaterial.SetVector("_DrawPos", new Vector4(uv.x, uv.y, 0, 0));
                drawMaterial.SetFloat("_BrushSize", brushSize);

                RenderTexture temp = RenderTexture.GetTemporary(rtA.width, rtA.height);
                Graphics.Blit(rtA, temp, drawMaterial);
                Graphics.Blit(temp, rtA);
                RenderTexture.ReleaseTemporary(temp);
            }
        }
    }
}