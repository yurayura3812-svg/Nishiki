using UnityEngine;

/// <summary>
/// ビルドに載せる初期個体。中身は Assets/KoiData からエディタが更新する。
/// </summary>
[CreateAssetMenu(menuName = "Nishiki/Builtin Koi Catalog")]
public class BuiltinKoiCatalog : ScriptableObject
{
    public KoiPatternData[] koi;
}
