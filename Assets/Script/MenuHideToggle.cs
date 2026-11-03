using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 非表示ボタンで、それ以外のメニューを隠す／戻す。
/// </summary>
public class MenuHideToggle : MonoBehaviour
{
    readonly List<GameObject> extras = new List<GameObject>();
    bool hidden;

    void Awake()
    {
        var hide = FindDeep("HihyojiButton");
        if (hide == null) return;

        AddIfFound("SceneMenu");
        AddIfFound("EsaButton");
        AddIfFound("User");

        var btn = hide.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveListener(Toggle);
        btn.onClick.AddListener(Toggle);
    }

    void AddIfFound(string name)
    {
        var t = FindDeep(name);
        if (t != null) extras.Add(t.gameObject);
    }

    void Toggle()
    {
        hidden = !hidden;
        foreach (var go in extras)
        {
            if (go != null) go.SetActive(!hidden);
        }
    }

    Transform FindDeep(string name)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == name) return t;
        }
        return null;
    }
}
