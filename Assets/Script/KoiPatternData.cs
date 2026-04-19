using UnityEngine;

// これにより右クリックから「Koi > PatternData」でファイルが作れるようになります
[CreateAssetMenu(fileName = "NewKoiData", menuName = "Koi/PatternData")]
public class KoiPatternData : ScriptableObject
{
    [Header("Red Settings")]
    public Color redColor = new Color(0.8f, 0.1f, 0.1f);
    public float redScale = 1.5f;
    public float redAmount = 0.5f;

    [Header("Black Settings")]
    public Color blackColor = new Color(0.1f, 0.1f, 0.1f);
    public float blackScale = 2.0f;
    public float blackAmount = 0.6f;

    [Header("Individual Settings")]
    public Vector3 patternSeed; // 模様をズラすための値
    [Range(-0.5f, 0.5f)] public float bellyLimit = 0.0f; // 白地の境界
}