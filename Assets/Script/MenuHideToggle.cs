using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 非表示ボタンで、それ以外のメニューを隠す／戻す。
/// 今いる画面のナビは、押したときと同じ水色にする。
/// </summary>
public class MenuHideToggle : MonoBehaviour
{
    readonly List<GameObject> extras = new List<GameObject>();
    bool hidden;

    void Awake()
    {
        if (GetComponent<SettingsUI>() == null)
            gameObject.AddComponent<SettingsUI>();

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

    void Start()
    {
        HighlightCurrent();
    }

    static readonly Color ActiveFill = new Color(0.706f, 0.855f, 0.957f, 1f);

    void HighlightCurrent()
    {
        var scene = SceneManager.GetActiveScene().name;
        if (scene == KoiDetailGate.SceneName)
            scene = "KoiScene";

        foreach (var loader in GetComponentsInChildren<SceneLoader>(true))
        {
            var img = loader.GetComponent<Image>();
            var btn = loader.GetComponent<Button>();
            if (img == null || btn == null) continue;
            if (loader.SceneName != scene) continue;

            btn.transition = Selectable.Transition.None;
            img.color = ActiveFill;
        }
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
