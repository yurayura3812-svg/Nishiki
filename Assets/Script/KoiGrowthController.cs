using UnityEngine;

[ExecuteAlways]
public class KoiGrowthController : MonoBehaviour
{
    [Header("--- 成長パラメータ ---")]
    [Range(0f, 1f)] public float growth = 0.5f;
    [Range(0f, 1f)] public float condition = 0.5f;
    [Range(0f, 1f)] public float density = 1.0f;

    [Header("--- サイズ設定 ---")]
    public Vector3 minScale = new Vector3(0.2f, 0.2f, 0.2f);
    public Vector3 maxScale = new Vector3(1.2f, 1.2f, 1.2f);

    [Header("--- 模様の維持設定 ---")]
    public Renderer koiRenderer;
    public string sub1ScaleName = "_Sub1Scale";
    public string sub2ScaleName = "_Sub2Scale";

    // インスペクター画像(image_d46bb9.png)に合わせた初期値
    [SerializeField] private float baseSub1Scale = 1.5f;
    [SerializeField] private float baseSub2Scale = 2.0f;

    private MaterialPropertyBlock _mpb;
    private int _sub1Id, _sub2Id;
    private float _lastGrowth, _lastCondition, _lastDensity;

    void OnEnable()
    {
        _mpb = new MaterialPropertyBlock();
        _sub1Id = Shader.PropertyToID(sub1ScaleName);
        _sub2Id = Shader.PropertyToID(sub2ScaleName);
        ApplyGrowth();
    }

    void Update()
    {
        // 変化があった時だけ更新
        if (growth != _lastGrowth || condition != _lastCondition || density != _lastDensity)
        {
            ApplyGrowth();
        }
    }

    private void ApplyGrowth()
    {
        // 1. スケール計算
        float environmentEffect = Mathf.Lerp(0.5f, 1.0f, density);
        float targetLength = Mathf.Lerp(minScale.z, maxScale.z, growth) * environmentEffect;
        float fatness = Mathf.Lerp(0.8f, 1.4f, condition);
        float targetWidth = Mathf.Lerp(minScale.x, maxScale.x, growth) * fatness * environmentEffect;

        // 2. モデル自体の大きさを変える
        transform.localScale = new Vector3(targetWidth, targetWidth, targetLength);

        // 3. 模様のズレを「逆算」で止める
        if (koiRenderer != null && targetLength > 0.001f)
        {
            koiRenderer.GetPropertyBlock(_mpb);

            // モデルが2倍に伸びたら、模様のScale（密度）を1/2にして相殺する
            // これにより、見た目上の模様の位置が固定されます
            float compensation = 1f / targetLength; 
            
            _mpb.SetFloat(_sub1Id, baseSub1Scale * compensation);
            _mpb.SetFloat(_sub2Id, baseSub2Scale * compensation);
            
            koiRenderer.SetPropertyBlock(_mpb);
        }

        _lastGrowth = growth;
        _lastCondition = condition;
        _lastDensity = density;
    }

    // インスペクターで値をいじった時に即座に反映させる
    void OnValidate()
    {
        if (_mpb == null) _mpb = new MaterialPropertyBlock();
        ApplyGrowth();
    }
}