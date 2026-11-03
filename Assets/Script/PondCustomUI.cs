using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SCR-04 池カスタム。Canvas の枠は触らない。既存の KoiPanel / FloorPanel にカードを入れるだけ。
/// </summary>
public class PondCustomUI : MonoBehaviour
{
    [Header("カードのおおもと（画像とボタンを乗せる枠）")]
    [SerializeField] Sprite koiCardBackground;
    [SerializeField] Sprite floorCardBackground;
    [SerializeField] Sprite detailButtonBackground;
    [SerializeField] Texture2D checkMark;

    PondCustomSave save;
    readonly HashSet<string> selectedKoi = new HashSet<string>();
    readonly Dictionary<string, GameObject> koiChecks = new Dictionary<string, GameObject>();
    readonly Dictionary<string, GameObject> floorChecks = new Dictionary<string, GameObject>();
    GameObject detailOverlay;
    Text detailTitle;
    RawImage detailPhoto;
    Font font;
    Sprite roundWhite;

    static readonly Color Ink = new Color(0.18f, 0.24f, 0.32f);
    static readonly Color Accent = new Color(0.73f, 0.78f, 0.93f);

    void Awake()
    {
        InitLook();
        save = PondCustomStore.Load();
        if (transform.Find("KoiPanel") == null)
        {
            Debug.LogError("PondCustomUI: Canvas 直下に KoiPanel がありません。ShopScene の Canvas を確認してください。");
            return;
        }
        WireAndFill();
    }

    void InitLook()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        roundWhite = MakeSliced(64, 24, Color.white);
    }

    void WireAndFill()
    {
        selectedKoi.Clear();
        koiChecks.Clear();
        floorChecks.Clear();
        BindDetail();
        FillKoiGrid();
        FillFloorGrid();
    }

    RectTransform EnsureKoiScroll()
    {
        var body = FindPath("KoiPanel", "Body");
        if (body == null) return null;

        var content = FindPath("KoiPanel", "Body", "KoiScroll", "Viewport", "Content");
        if (content != null) return content;

        var created = CreateScroll(body, "KoiScroll");
        var grid = created.content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(240, 210);
        grid.spacing = new Vector2(24, 20);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 3;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(8, 8, 8, 8);

        var fitter = created.content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return created.content;
    }

    RectTransform EnsureFloorGrid()
    {
        var body = FindPath("FloorPanel", "Body");
        if (body == null) return null;

        var content = FindPath("FloorPanel", "Body", "FloorScroll", "Viewport", "Content");
        if (content != null) return content;

        var created = CreateScroll(body, "FloorScroll");
        var grid = created.content.gameObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(340, 230);
        grid.spacing = new Vector2(28, 24);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = 2;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.padding = new RectOffset(12, 12, 8, 8);

        var fitter = created.content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return created.content;
    }

    void FillKoiGrid()
    {
        var content = EnsureKoiScroll();
        if (content == null) return;
        ClearChildren(content);

        var all = KoiSaveStore.LoadAll();
        bool useSaved = save != null && save.koiSelectionSaved;
        foreach (var koi in all)
        {
            if (koi == null) continue;
            string key = PondCustomStore.KoiKey(koi);
            bool on = !useSaved || save.swimmingKoiIds.Contains(key);
            if (on) selectedKoi.Add(key);
            CreateKoiCard(content, koi, key, on);
        }
    }

    void CreateKoiCard(RectTransform parent, KoiPatternData koi, string key, bool on)
    {
        var card = new GameObject("Koi_" + key, typeof(RectTransform));
        var rt = card.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var photoBtn = card.AddComponent<Button>();
        var photoBg = card.AddComponent<Image>();
        ApplyFrame(photoBg, koiCardBackground);
        photoBtn.targetGraphic = photoBg;
        photoBtn.onClick.AddListener(() => ToggleKoi(key));

        var photoGo = new GameObject("Photo", typeof(RectTransform));
        var photoRt = photoGo.GetComponent<RectTransform>();
        photoRt.SetParent(rt, false);
        photoRt.anchorMin = new Vector2(0.08f, 0.28f);
        photoRt.anchorMax = new Vector2(0.92f, 0.96f);
        photoRt.offsetMin = Vector2.zero;
        photoRt.offsetMax = Vector2.zero;
        var raw = photoGo.AddComponent<RawImage>();
        raw.color = Color.white;
        if (koi.photoTexture != null) raw.texture = koi.photoTexture;
        else
        {
            raw.texture = Texture2D.whiteTexture;
            raw.color = koi.mainColor;
        }

        var check = CheckBadge(rt);
        check.SetActive(on);
        koiChecks[key] = check;

        var detail = PillButton(rt, "詳細", () => ShowDetail(koi));
        var dRt = detail.GetComponent<RectTransform>();
        dRt.anchorMin = new Vector2(0.25f, 0.04f);
        dRt.anchorMax = new Vector2(0.75f, 0.24f);
        dRt.offsetMin = Vector2.zero;
        dRt.offsetMax = Vector2.zero;
    }

    void FillFloorGrid()
    {
        var body = EnsureFloorGrid();
        if (body == null) return;
        ClearChildren(body);

        string selected = save == null || string.IsNullOrEmpty(save.floorId)
            ? PondCustomStore.DefaultFloorId
            : save.floorId;
        foreach (var opt in PondCustomStore.FloorOptions)
            CreateFloorCard(body, opt, opt.id == selected);
    }

    void CreateFloorCard(RectTransform parent, PondFloorOption opt, bool on)
    {
        var card = new GameObject("Floor_" + opt.id, typeof(RectTransform));
        var rt = card.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var btn = card.AddComponent<Button>();
        var bg = card.AddComponent<Image>();
        ApplyFrame(bg, floorCardBackground);
        btn.targetGraphic = bg;
        string id = opt.id;
        btn.onClick.AddListener(() => SelectFloor(id));

        var photoGo = new GameObject("Tex", typeof(RectTransform));
        var photoRt = photoGo.GetComponent<RectTransform>();
        photoRt.SetParent(rt, false);
        photoRt.anchorMin = new Vector2(0.06f, 0.08f);
        photoRt.anchorMax = new Vector2(0.94f, 0.96f);
        photoRt.offsetMin = Vector2.zero;
        photoRt.offsetMax = Vector2.zero;
        var raw = photoGo.AddComponent<RawImage>();
        raw.texture = opt.texture != null ? opt.texture : Texture2D.whiteTexture;
        raw.color = Color.white;

        var check = CheckBadge(rt);
        check.SetActive(on);
        floorChecks[opt.id] = check;
    }

    void ToggleKoi(string key)
    {
        if (selectedKoi.Contains(key)) selectedKoi.Remove(key);
        else selectedKoi.Add(key);

        if (koiChecks.TryGetValue(key, out var check))
            check.SetActive(selectedKoi.Contains(key));

        save.koiSelectionSaved = true;
        save.swimmingKoiIds = new List<string>(selectedKoi);
        PondCustomStore.Save(save);
    }

    void SelectFloor(string id)
    {
        save.floorId = id;
        PondCustomStore.Save(save);
        foreach (var pair in floorChecks)
            pair.Value.SetActive(pair.Key == id);
    }

    void BuildDetailOverlay(Transform parent)
    {
        detailOverlay = new GameObject("DetailOverlay", typeof(RectTransform));
        var rt = detailOverlay.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt);
        var dim = detailOverlay.AddComponent<Image>();
        dim.color = new Color(0.1f, 0.15f, 0.2f, 0.45f);
        var close = detailOverlay.AddComponent<Button>();
        close.targetGraphic = dim;
        close.onClick.AddListener(() => detailOverlay.SetActive(false));

        var panel = new GameObject("Panel", typeof(RectTransform));
        var prt = panel.GetComponent<RectTransform>();
        prt.SetParent(rt, false);
        prt.anchorMin = new Vector2(0.32f, 0.22f);
        prt.anchorMax = new Vector2(0.68f, 0.78f);
        prt.offsetMin = Vector2.zero;
        prt.offsetMax = Vector2.zero;
        var pImg = panel.AddComponent<Image>();
        pImg.sprite = roundWhite;
        pImg.type = Image.Type.Sliced;

        var titleRt = Label(prt, "", 36, TextAnchor.MiddleCenter, new Vector2(20, -20), new Vector2(-20, -80));
        titleRt.name = "Title";
        detailTitle = titleRt.GetComponent<Text>();

        var photoGo = new GameObject("Photo", typeof(RectTransform));
        var photoRt = photoGo.GetComponent<RectTransform>();
        photoRt.SetParent(prt, false);
        photoRt.anchorMin = new Vector2(0.12f, 0.12f);
        photoRt.anchorMax = new Vector2(0.88f, 0.72f);
        photoRt.offsetMin = Vector2.zero;
        photoRt.offsetMax = Vector2.zero;
        detailPhoto = photoGo.AddComponent<RawImage>();

        detailOverlay.SetActive(false);
        detailOverlay.transform.SetAsLastSibling();
    }

    void BindDetail()
    {
        var overlay = transform.Find("DetailOverlay");
        if (overlay == null)
        {
            BuildDetailOverlay(transform);
            overlay = transform.Find("DetailOverlay");
        }
        detailOverlay = overlay != null ? overlay.gameObject : null;
        if (detailOverlay == null) return;

        var title = FindPath("DetailOverlay", "Panel", "Title");
        if (title != null) detailTitle = title.GetComponent<Text>();
        var photo = FindPath("DetailOverlay", "Panel", "Photo");
        if (photo != null) detailPhoto = photo.GetComponent<RawImage>();

        var close = detailOverlay.GetComponent<Button>();
        if (close != null)
        {
            close.onClick.RemoveAllListeners();
            close.onClick.AddListener(() => detailOverlay.SetActive(false));
        }
        detailOverlay.SetActive(false);
    }

    void ShowDetail(KoiPatternData koi)
    {
        KoiDetailGate.Open(koi, false, "ShopScene");
    }

    GameObject CheckBadge(RectTransform parent)
    {
        var go = new GameObject("Check", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = new Vector2(1, 1);
        rt.anchorMax = new Vector2(1, 1);
        rt.pivot = new Vector2(1, 1);
        rt.anchoredPosition = new Vector2(-8, -8);
        rt.sizeDelta = new Vector2(36, 36);
        if (checkMark != null)
        {
            var raw = go.AddComponent<RawImage>();
            raw.texture = checkMark;
            raw.color = Color.white;
            raw.raycastTarget = false;
        }
        else
        {
            var img = go.AddComponent<Image>();
            img.sprite = roundWhite;
            img.type = Image.Type.Sliced;
            img.color = Accent;
            img.raycastTarget = false;
        }
        return go;
    }

    GameObject PillButton(RectTransform parent, string text, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(text, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var img = go.AddComponent<Image>();
        if (detailButtonBackground != null) ApplyFrame(img, detailButtonBackground);
        else
        {
            img.sprite = roundWhite;
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);
        Label(rt, text, 22, TextAnchor.MiddleCenter, Vector2.zero, Vector2.zero);
        return go;
    }

    (ScrollRect scroll, RectTransform content) CreateScroll(RectTransform parent, string name)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt);
        var scroll = go.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;

        var viewport = FullRect("Viewport", rt);
        viewport.gameObject.AddComponent<RectMask2D>();

        var content = FullRect("Content", viewport);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = new Vector2(0, 0);

        scroll.viewport = viewport;
        scroll.content = content;
        return (scroll, content);
    }

    RectTransform FindPath(params string[] names)
    {
        Transform t = transform;
        foreach (var n in names)
        {
            if (t == null) return null;
            t = t.Find(n);
        }
        return t as RectTransform;
    }

    void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
        {
            var child = parent.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child);
            else DestroyImmediate(child);
        }
    }

    RectTransform FullRect(string name, Transform parent)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt);
        return rt;
    }

    static void ApplyFrame(Image img, Sprite sprite)
    {
        if (sprite != null)
        {
            img.sprite = sprite;
            img.type = Image.Type.Simple;
            img.preserveAspect = false;
            img.color = Color.white;
        }
        else
        {
            img.sprite = null;
            img.color = new Color(1, 1, 1, 0f);
        }
    }

    RectTransform Label(RectTransform parent, string text, int size, TextAnchor align, Vector2 minOff, Vector2 maxOff)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        Stretch(rt);
        rt.offsetMin = minOff;
        rt.offsetMax = maxOff;
        var t = go.AddComponent<Text>();
        t.font = font;
        t.text = text;
        t.fontSize = size;
        t.color = Ink;
        t.alignment = align;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.raycastTarget = false;
        return rt;
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
