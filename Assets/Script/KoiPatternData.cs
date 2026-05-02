using UnityEngine;

[CreateAssetMenu(fileName = "NewKoiData", menuName = "Koi/PatternData")]
public class KoiPatternData : ScriptableObject
{
    [Header("Main Settings")]
    public Color mainColor = Color.white; 

    [Header("Sub Color 1 Settings")]
    public Color sub1Color = new Color(0.8f, 0.1f, 0.1f);
    public float sub1Scale = 1.5f;
    public float sub1Amount = 0.5f;
    public float sub1Detail = 0.5f;

    [Header("Sub Color 2 Settings")]
    public Color sub2Color = new Color(0.1f, 0.1f, 0.1f); // ★ここを sub2Color に統一
    public float sub2Scale = 2.0f;
    public float sub2Amount = 0.6f;
    public float sub2Detail = 0.5f;

    [Header("Individual Settings")]
    public Vector3 patternSeed; 
    [Range(-0.5f, 0.5f)] public float bellyLimit = 0.0f; 
    public Texture2D photoTexture;
}