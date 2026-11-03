using UnityEngine;

[System.Serializable]
public class PondFloatSet
{
    public string id;
    public string displayName;
    public Texture2D[] textures;
}

/// <summary>
/// 水面に浮かべるセット。中身は Assets/UIimage/float のフォルダからエディタが更新する。
/// </summary>
[CreateAssetMenu(menuName = "Nishiki/Pond Float Catalog")]
public class PondFloatCatalog : ScriptableObject
{
    public PondFloatSet[] entries;
}
