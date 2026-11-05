using UnityEngine;
using UnityEngine.Rendering;

public class KoiController : MonoBehaviour
{
    public KoiPatternData patternData;

    Vector3 baseScale = Vector3.one;
    float nextNibble;
    float saveAt = -1f;

    void Awake()
    {
        baseScale = transform.localScale;
        if (baseScale.sqrMagnitude < 0.0001f) baseScale = Vector3.one;
    }

    void Start() { ApplyDNA(); }
    void OnValidate() { ApplyDNA(); }

    void Update()
    {
        if (saveAt > 0f && Time.time >= saveAt)
        {
            saveAt = -1f;
            if (patternData != null) KoiSaveStore.Save(patternData);
        }
    }

    public bool Nibble(FeedPellet pellet)
    {
        if (pellet == null || patternData == null) return false;
        if (Time.time < nextNibble) return false;
        nextNibble = Time.time + 0.28f;
        pellet.Consume();

        patternData.hasStats = true;
        patternData.gloss = Mathf.Min(100f, patternData.gloss + 2.4f);
        patternData.body = Mathf.Min(100f, patternData.body + 1.1f);
        ApplyDNA();
        saveAt = Time.time + 0.9f;
        return true;
    }

    public void ApplyDNA()
    {
        if (patternData != null)
        {
            float t = Mathf.Clamp01(patternData.body / 100f);
            float s = Mathf.Lerp(0.88f, 1.22f, t);
            transform.localScale = baseScale * s;
        }

        foreach (var r in GetComponentsInChildren<Renderer>())
        {
            r.shadowCastingMode = ShadowCastingMode.Off;
            r.receiveShadows = false;
            if (patternData == null) continue;

            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);

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

            float g = Mathf.Clamp01(patternData.gloss / 100f);
            mpb.SetFloat("_GuanineStrength", Mathf.Lerp(0.7f, 2.5f, g));
            mpb.SetFloat("_GuanineRoughness", Mathf.Lerp(0.28f, 0.08f, g));
            mpb.SetFloat("_CoatF0", Mathf.Lerp(0.014f, 0.048f, g));
            mpb.SetFloat("_CoatRoughness", Mathf.Lerp(0.22f, 0.05f, g));

            r.SetPropertyBlock(mpb);
        }
    }
}