using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SCR-02 の鯉一覧。Menu と既存の交配・編集パネルは触らない。
/// </summary>
public class KoiListUI : MonoBehaviour
{
    [SerializeField] Sprite cardBackground;
    [SerializeField] Color tabOn = new Color(1f, 0.86f, 0.72f, 1f);
    [SerializeField] Color tabOff = Color.white;

    Font font;
    SelectManager selectManager;

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        selectManager = FindAnyObjectByType<SelectManager>();

        if (FindDeep("KoiListPanel") == null)
        {
            Debug.LogError("KoiListUI: KoiListPanel がありません。");
            return;
        }

        WireTab("TabList", ShowList);
        WireTab("TabBreed", ShowBreed);
        WireTab("TabCreate", ShowCreate);
        WireTab("TabEdit", ShowEdit);
        FillList();
        ShowList();
        Loc.Apply(transform);
        Loc.Changed += OnLoc;
    }

    void OnDestroy()
    {
        Loc.Changed -= OnLoc;
    }

    void OnLoc()
    {
        FillList();
        Loc.Apply(transform);
    }

    public void ShowList()
    {
        SetListVisible(true);
        if (selectManager != null) selectManager.ShowList();
        PaintTabs("TabList");
        FillList();
    }

    public void ShowBreed()
    {
        SetListVisible(false);
        if (selectManager != null) selectManager.OnClickCrossButton();
        PaintTabs("TabBreed");
    }

    public void ShowCreate()
    {
        SetListVisible(false);
        if (selectManager != null) selectManager.OnClickNewCreateButton();
        PaintTabs("TabCreate");
    }

    public void ShowEdit()
    {
        SetListVisible(false);
        if (selectManager != null) selectManager.OnClickEditModeButton();
        PaintTabs("TabEdit");
    }

    void SetListVisible(bool on)
    {
        var list = FindDeep("KoiListPanel");
        if (list != null) list.gameObject.SetActive(on);
    }

    void WireTab(string name, UnityEngine.Events.UnityAction action)
    {
        var t = FindDeep(name);
        if (t == null) return;
        var btn = t.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }

    void PaintTabs(string active)
    {
        PaintTab("TabList", active == "TabList");
        PaintTab("TabBreed", active == "TabBreed");
        PaintTab("TabCreate", active == "TabCreate");
        PaintTab("TabEdit", active == "TabEdit");
    }

    void PaintTab(string name, bool on)
    {
        var t = FindDeep(name);
        if (t == null) return;
        var img = t.GetComponent<Image>();
        if (img != null) img.color = on ? tabOn : tabOff;
    }

    void FillList()
    {
        var content = EnsureGrid();
        if (content == null) return;
        FitGrid(content);
        ClearChildren(content);

        var all = KoiSaveStore.LoadAll();
        if (all == null) return;
        foreach (var koi in all)
        {
            if (koi == null) continue;
            CreateCard(content, koi);
        }
        StartCoroutine(FitAfterLayout(content));
    }

    System.Collections.IEnumerator FitAfterLayout(RectTransform content)
    {
        yield return null;
        if (content != null) FitGrid(content);
    }

    RectTransform EnsureGrid()
    {
        var body = FindPath("KoiListPanel", "Body");
        if (body == null) return null;
        var content = FindPath("KoiListPanel", "Body", "KoiScroll", "Viewport", "Content");
        if (content != null) return content;

        var scrollGo = new GameObject("KoiScroll", typeof(RectTransform));
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.SetParent(body, false);
        Stretch(scrollRt);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;

        var viewportGo = new GameObject("Viewport", typeof(RectTransform));
        var viewport = viewportGo.GetComponent<RectTransform>();
        viewport.SetParent(scrollRt, false);
        Stretch(viewport);
        viewportGo.AddComponent<RectMask2D>();

        var contentGo = new GameObject("Content", typeof(RectTransform));
        content = contentGo.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        scroll.viewport = viewport;
        scroll.content = content;

        contentGo.AddComponent<GridLayoutGroup>();
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        return content;
    }

    static void FitGrid(RectTransform content)
    {
        var grid = content.GetComponent<GridLayoutGroup>();
        if (grid == null) grid = content.gameObject.AddComponent<GridLayoutGroup>();

        const int columns = 3;
        const float gap = 16f;
        const float aspect = 200f / 280f;

        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;
        grid.childAlignment = TextAnchor.UpperCenter;
        grid.spacing = new Vector2(gap, gap);
        grid.padding = new RectOffset((int)gap, (int)gap, (int)gap, (int)gap);

        Canvas.ForceUpdateCanvases();
        var viewport = content.parent as RectTransform;
        float width = viewport != null ? viewport.rect.width : content.rect.width;
        if (width < 1f) return;

        float inner = width - grid.padding.horizontal - grid.spacing.x * (columns - 1);
        float cellW = Mathf.Max(80f, inner / columns);
        grid.cellSize = new Vector2(cellW, cellW * aspect);
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void CreateCard(RectTransform parent, KoiPatternData koi)
    {
        var go = new GameObject("Koi_" + koi.name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        if (cardBackground != null)
        {
            img.sprite = cardBackground;
            img.type = Image.Type.Simple;
            img.color = Color.white;
        }
        else img.color = new Color(1, 1, 1, 0.35f);

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        var picked = koi;
        btn.onClick.AddListener(() =>
        {
            bool enter = KoiDetailGate.EnterJudging && KoiDetailGate.ReturnScene == "HinpyokaiScene";
            string back = enter ? "HinpyokaiScene" : "KoiScene";
            KoiDetailGate.Open(picked, enter, back);
        });

        var photoGo = new GameObject("Photo", typeof(RectTransform));
        var photoRt = photoGo.GetComponent<RectTransform>();
        photoRt.SetParent(rt, false);
        photoRt.anchorMin = new Vector2(0.08f, 0.22f);
        photoRt.anchorMax = new Vector2(0.92f, 0.94f);
        photoRt.offsetMin = Vector2.zero;
        photoRt.offsetMax = Vector2.zero;
        var raw = photoGo.AddComponent<RawImage>();
        raw.raycastTarget = false;
        if (koi.photoTexture != null)
        {
            raw.texture = koi.photoTexture;
            raw.color = Color.white;
        }
        else if (KoiPhotoStudio.Instance != null)
        {
            raw.texture = Texture2D.whiteTexture;
            raw.color = koi.mainColor;
            KoiPhotoStudio.Instance.RequestCapture(koi, raw);
        }
        else
        {
            raw.texture = Texture2D.whiteTexture;
            raw.color = koi.mainColor;
        }

        var nameGo = new GameObject("Name", typeof(RectTransform));
        var nameRt = nameGo.GetComponent<RectTransform>();
        nameRt.SetParent(rt, false);
        nameRt.anchorMin = new Vector2(0.06f, 0.02f);
        nameRt.anchorMax = new Vector2(0.94f, 0.2f);
        nameRt.offsetMin = Vector2.zero;
        nameRt.offsetMax = Vector2.zero;
        var label = nameGo.AddComponent<Text>();
        label.font = font;
        label.text = Loc.KoiName(koi.name);
        label.alignment = TextAnchor.MiddleCenter;
        label.color = new Color(0.22f, 0.16f, 0.12f, 1f);
        label.fontSize = 20;
        label.resizeTextForBestFit = true;
        label.resizeTextMinSize = 10;
        label.resizeTextMaxSize = 22;
        label.horizontalOverflow = HorizontalWrapMode.Overflow;
        label.raycastTarget = false;
    }

    Transform FindDeep(string name)
    {
        var direct = transform.Find(name);
        if (direct != null) return direct;
        var all = GetComponentsInChildren<Transform>(true);
        foreach (var t in all)
        {
            if (t.name == name) return t;
        }
        return null;
    }

    RectTransform FindPath(params string[] names)
    {
        Transform t = FindDeep(names[0]);
        for (int i = 1; i < names.Length; i++)
        {
            if (t == null) return null;
            t = t.Find(names[i]);
        }
        return t as RectTransform;
    }

    void ClearChildren(Transform parent)
    {
        for (int i = parent.childCount - 1; i >= 0; i--)
            Destroy(parent.GetChild(i).gameObject);
    }
}
