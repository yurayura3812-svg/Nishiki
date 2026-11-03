using UnityEngine;

/// <summary>
/// 底素材一覧。中身は Assets/UIimage/floor からエディタが更新する。
/// </summary>
[CreateAssetMenu(menuName = "Nishiki/Pond Floor Catalog")]
public class PondFloorCatalog : ScriptableObject
{
    public PondFloorOption[] entries;
}
