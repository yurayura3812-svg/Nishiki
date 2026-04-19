using UnityEngine;

public class KoiController : MonoBehaviour
{
    public KoiPatternData patternData;

    void Start() { ApplyDNA(); }
    void OnValidate() { ApplyDNA(); }

    public void ApplyDNA()
    {
        if (patternData == null) return;

        // 【修正ポイント】単数形ではなく複数形で「全てのパーツ」を取得する
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        
        if (allRenderers.Length == 0) return;

        // 全てのパーツに対して一括でデータを流し込む
        foreach (Renderer r in allRenderers)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);

            // 地肌・赤・黒の色を反映
            mpb.SetColor("_BaseColor", patternData.baseColor);
            mpb.SetColor("_RedColor", patternData.redColor);
            mpb.SetColor("_BlackColor", patternData.blackColor);

            // 各種パラメータを反映
            mpb.SetFloat("_RedScale", patternData.redScale);
            mpb.SetFloat("_RedAmount", patternData.redAmount);
            mpb.SetFloat("_BlackScale", patternData.blackScale);
            mpb.SetFloat("_BlackAmount", patternData.blackAmount);

            // 模様のシード値とお腹の境界
            mpb.SetVector("_Seed", (Vector4)patternData.patternSeed);
            mpb.SetFloat("_BellyLimit", patternData.bellyLimit);

            r.SetPropertyBlock(mpb);
        }
    }
}