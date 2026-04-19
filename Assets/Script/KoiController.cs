using UnityEngine;

public class KoiController : MonoBehaviour
{
    public KoiPatternData patternData; // ここにデータをセットする

    // 起動時とインスペクター変更時に反映
    void Start() { ApplyDNA(); }
    void OnValidate() { ApplyDNA(); }

    public void ApplyDNA()
    {
        if (patternData == null) return;

        // 子要素にあるMeshRenderer（鯉の本体）を探す
        Renderer renderer = GetComponentInChildren<Renderer>();
        if (renderer == null) return;

        // 他の個体に影響を与えず、かつメモリに優しい反映方法
        MaterialPropertyBlock mpb = new MaterialPropertyBlock();
        renderer.GetPropertyBlock(mpb);

        mpb.SetColor("_RedColor", patternData.redColor);
        mpb.SetFloat("_RedScale", patternData.redScale);
        mpb.SetFloat("_RedAmount", patternData.redAmount);

        mpb.SetColor("_BlackColor", patternData.blackColor);
        mpb.SetFloat("_BlackScale", patternData.blackScale);
        mpb.SetFloat("_BlackAmount", patternData.blackAmount);

        mpb.SetVector("_Seed", patternData.patternSeed);
        mpb.SetFloat("_BellyLimit", patternData.bellyLimit);

        renderer.SetPropertyBlock(mpb);
    }
}