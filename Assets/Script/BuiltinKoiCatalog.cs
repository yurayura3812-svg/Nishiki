using UnityEngine;

/// <summary>
/// ビルドに載せる初期個体。中身は Assets/KoiData からエディタが更新する。
/// </summary>
[CreateAssetMenu(menuName = "Nishiki/Builtin Koi Catalog")]
public class BuiltinKoiCatalog : ScriptableObject
{
    public KoiPatternData[] koi;
    public string[] hiddenKeys;

    public bool IsHidden(KoiPatternData data)
    {
        if (data == null || hiddenKeys == null || hiddenKeys.Length == 0) return false;
        var key = KoiSaveStore.KeyOf(data);
        var nameKey = "name:" + data.name;
        foreach (var h in hiddenKeys)
        {
            if (h == key || h == nameKey) return true;
        }
        return false;
    }
}
