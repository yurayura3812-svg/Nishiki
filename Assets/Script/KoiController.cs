using UnityEngine;
using UnityEngine.Rendering;

public class KoiController : MonoBehaviour
{
    public KoiPatternData patternData;

    void Start() { ApplyDNA(); }
    void OnValidate() { ApplyDNA(); }

    public void ApplyDNA()
    {
        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (patternData == null) continue;

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);

            // リネーム後のプロパティ名に合わせる
            mpb.SetColor("_MainColor", patternData.mainColor);
            mpb.SetColor("_SubColor1", patternData.sub1Color);
            mpb.SetColor("_SubColor2", patternData.sub2Color);

            mpb.SetFloat("_Sub1Scale", patternData.sub1Scale);
            mpb.SetFloat("_Sub1Amount", patternData.sub1Amount);
            mpb.SetFloat("_Sub2Scale", patternData.sub2Scale);
            mpb.SetFloat("_Sub2Amount", patternData.sub2Amount);

            mpb.SetFloat("_Sub1Detail", patternData.sub1Detail);
            mpb.SetFloat("_Sub2Detail", patternData.sub2Detail);

            mpb.SetVector("_Seed", (Vector4)patternData.patternSeed);
            mpb.SetFloat("_BellyLimit", patternData.bellyLimit);

            r.SetPropertyBlock(mpb);
        }
    }
}