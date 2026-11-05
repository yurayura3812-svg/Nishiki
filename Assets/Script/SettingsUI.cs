using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// メニューの User を押すと設定を開く。言語と管理者ツールのオンオフ。
/// 枠の見た目は足さない。ダイアログは実行時に Canvas へ乗せる。
/// </summary>
public class SettingsUI : MonoBehaviour
{
    GameObject overlay;
    Font font;
    Sprite round;
    Text title;
    Text langLabel;
    Text adminLabel;
    Text closeLabel;
    Image jaBg;
    Image enBg;
    Image adminOnBg;
    Image adminOffBg;

    static readonly Color Ink = new Color(0.18f, 0.24f, 0.32f);
    static readonly Color Panel = new Color(0.93f, 0.97f, 1f, 1f);
    static readonly Color ChipOff = new Color(0.86f, 0.9f, 0.94f, 1f);
    static readonly Color ChipOn = new Color(0.706f, 0.855f, 0.957f, 1f);

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        round = MakeSliced(64, 24, Color.white);
        WireUser();
    }

    void Start()
    {
        WireUser();
        Loc.ApplyScene();
    }

    void OnEnable()
    {
        Loc.Changed += OnLoc;
    }

    void OnDisable()
    {
        Loc.Changed -= OnLoc;
    }

    void OnLoc()
    {
        Paint();
        Loc.ApplyScene();
    }

    void WireUser()
    {
        Transform user = null;
        foreach (var t in GetComponentsInChildren<Transform>(true))
        {
            if (t.name == "User") { user = t; break; }
        }
        if (user == null) return;

        var topImg = user.parent != null ? user.parent.GetComponent<Image>() : null;
        if (topImg != null && user.parent.name == "TopMenu")
            topImg.raycastTarget = false;

        BindClick(user);
        var userName = user.Find("UserName");
        if (userName != null) BindClick(userName);

        var hit = user.Find("SettingsHit") as RectTransform;
        if (hit == null)
        {
            var go = new GameObject("SettingsHit", typeof(RectTransform));
            hit = go.GetComponent<RectTransform>();
            hit.SetParent(user, false);
            hit.anchorMin = Vector2.zero;
            hit.anchorMax = Vector2.one;
            hit.offsetMin = new Vector2(-12f, -12f);
            hit.offsetMax = new Vector2(24f, 12f);
            var img = go.AddComponent<Image>();
            img.color = new Color(1f, 1f, 1f, 0f);
            img.raycastTarget = true;
            BindClick(hit);
            hit.SetAsLastSibling();
        }
    }

    void BindClick(Transform t)
    {
        if (t == null) return;
        var graphic = t.GetComponent<Graphic>();
        if (graphic != null) graphic.raycastTarget = true;
        var btn = t.GetComponent<Button>();
        if (btn == null) btn = t.gameObject.AddComponent<Button>();
        if (graphic != null) btn.targetGraphic = graphic;
        btn.transition = Selectable.Transition.None;
        btn.onClick.RemoveListener(Open);
        btn.onClick.AddListener(Open);
    }

    void Open()
    {
        if (overlay == null) overlay = Build();
        overlay.SetActive(true);
        overlay.transform.SetAsLastSibling();
        Paint();
    }

    void Close()
    {
        if (overlay != null) overlay.SetActive(false);
    }

    void Paint()
    {
        if (overlay == null || !overlay.activeSelf) return;
        if (title != null) title.text = Loc.Get("settings.title");
        if (langLabel != null) langLabel.text = Loc.Get("settings.language");
        if (adminLabel != null) adminLabel.text = Loc.Get("settings.admin");
        if (closeLabel != null) closeLabel.text = Loc.Get("settings.close");
        bool ja = Loc.Language != Loc.En;
        if (jaBg != null) jaBg.color = ja ? ChipOn : ChipOff;
        if (enBg != null) enBg.color = ja ? ChipOff : ChipOn;
        bool admin = AppSettingsStore.AdminTools;
        if (adminOnBg != null)
        {
            adminOnBg.color = admin ? ChipOn : ChipOff;
            SetChipText(adminOnBg, Loc.Get("settings.on"));
        }
        if (adminOffBg != null)
        {
            adminOffBg.color = admin ? ChipOff : ChipOn;
            SetChipText(adminOffBg, Loc.Get("settings.off"));
        }
    }

    static void SetChipText(Image img, string value)
    {
        if (img == null) return;
        var t = img.GetComponentInChildren<Text>();
        if (t != null) t.text = value;
    }

    GameObject Build()
    {
        var canvas = GetComponentInParent<Canvas>();
        var parent = canvas != null ? canvas.transform : transform;

        var go = new GameObject("SettingsOverlay", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt);
        var dim = go.AddComponent<Image>();
        dim.color = new Color(0.1f, 0.15f, 0.2f, 0.45f);
        var close = go.AddComponent<Button>();
        close.targetGraphic = dim;
        close.onClick.AddListener(Close);

        var panel = new GameObject("Panel", typeof(RectTransform));
        var prt = panel.GetComponent<RectTransform>();
        prt.SetParent(rt, false);
        prt.anchorMin = new Vector2(0.5f, 0.5f);
        prt.anchorMax = new Vector2(0.5f, 0.5f);
        prt.sizeDelta = new Vector2(560, 480);
        var pImg = panel.AddComponent<Image>();
        pImg.sprite = round;
        pImg.type = Image.Type.Sliced;
        pImg.color = Panel;
        var block = panel.AddComponent<Button>();
        block.transition = Selectable.Transition.None;
        block.targetGraphic = pImg;

        title = MakeText(prt, "Title", 36, new Vector2(0.08f, 0.82f), new Vector2(0.92f, 0.96f));
        langLabel = MakeText(prt, "LangLabel", 24, new Vector2(0.08f, 0.68f), new Vector2(0.92f, 0.8f));
        jaBg = Chip(prt, "Ja", "日本語", new Vector2(0.08f, 0.52f), new Vector2(0.48f, 0.66f), () => Loc.SetLanguage(Loc.Ja));
        enBg = Chip(prt, "En", "English", new Vector2(0.52f, 0.52f), new Vector2(0.92f, 0.66f), () => Loc.SetLanguage(Loc.En));
        adminLabel = MakeText(prt, "AdminLabel", 24, new Vector2(0.08f, 0.38f), new Vector2(0.92f, 0.5f));
        adminOnBg = Chip(prt, "AdminOn", Loc.Get("settings.on"), new Vector2(0.08f, 0.22f), new Vector2(0.48f, 0.36f), () => { AppSettingsStore.AdminTools = true; Paint(); });
        adminOffBg = Chip(prt, "AdminOff", Loc.Get("settings.off"), new Vector2(0.52f, 0.22f), new Vector2(0.92f, 0.36f), () => { AppSettingsStore.AdminTools = false; Paint(); });
        var closeBg = Chip(prt, "Close", Loc.Get("settings.close"), new Vector2(0.28f, 0.06f), new Vector2(0.72f, 0.18f), Close);
        closeLabel = closeBg.GetComponentInChildren<Text>();
        return go;
    }

    Image Chip(RectTransform parent, string name, string label, Vector2 min, Vector2 max, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.sprite = round;
        img.type = Image.Type.Sliced;
        img.color = ChipOff;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);
        var text = MakeText(rt, "Text", 24, Vector2.zero, Vector2.one);
        text.text = label;
        return img;
    }

    Text MakeText(RectTransform parent, string name, int size, Vector2 min, Vector2 max)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.fontSize = size;
        t.color = Ink;
        t.alignment = TextAnchor.MiddleCenter;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        return t;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    static Sprite MakeSliced(int size, int radius, Color color)
    {
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
        tex.wrapMode = TextureWrapMode.Clamp;
        float r = radius;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = Mathf.Min(x, size - 1 - x);
                float dy = Mathf.Min(y, size - 1 - y);
                float a = 1f;
                if (dx < r && dy < r)
                {
                    float d = Vector2.Distance(new Vector2(dx, dy), new Vector2(r, r));
                    a = Mathf.Clamp01(r - d + 0.5f);
                }
                tex.SetPixel(x, y, new Color(color.r, color.g, color.b, color.a * a));
            }
        }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(radius, radius, radius, radius));
    }
}
