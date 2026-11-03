using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// SCR-03 品評会。Canvas の Menu は触らない。RankPanel / DetailPanel に中身を入れる。
/// </summary>
public class HinpyokaiUI : MonoBehaviour
{
    [SerializeField] Sprite rankRowBackground;

    Font font;
    Sprite roundWhite;
    readonly List<Entry> entries = new List<Entry>();
    int selected;

    static readonly Color Ink = new Color(0.22f, 0.32f, 0.45f);
    static readonly Color RowTint = new Color(0.82f, 0.91f, 0.96f, 0.95f);

    class Entry
    {
        public KoiPatternData koi;
        public string owner;
        public HinpyokaiScore score;
    }

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        roundWhite = MakeSliced(64, 24, Color.white);

        if (transform.Find("RankPanel") == null)
        {
            Debug.LogError("HinpyokaiUI: Canvas 直下に RankPanel がありません。HinpyokaiScene を確認してください。");
            return;
        }

        WireJoin();
        Refresh();
    }

    public void Refresh()
    {
        BuildEntries();
        FillRankList();
        int start = 0;
        if (!string.IsNullOrEmpty(KoiDetailGate.EnteredKoiKey))
        {
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].koi != null && KoiSaveStore.KeyOf(entries[i].koi) == KoiDetailGate.EnteredKoiKey)
                {
                    start = i;
                    break;
                }
            }
        }
        ShowDetail(start);
    }

    void WireJoin()
    {
        var join = transform.Find("JoinButton");
        if (join == null) return;
        var btn = join.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(OnJoin);
    }

    void OnJoin()
    {
        var all = KoiSaveStore.LoadAll();
        if (all == null || all.Count == 0)
        {
            Debug.Log("HinpyokaiUI: 出す鯉がありません。");
            return;
        }
        KoiDetailGate.OpenListForJudging();
    }

    void BuildEntries()
    {
        entries.Clear();
        foreach (var key in HinpyokaiEntryStore.LoadKeys())
        {
            var koi = KoiSaveStore.Find(key);
            if (koi == null) continue;
            entries.Add(new Entry
            {
                koi = koi,
                owner = string.IsNullOrEmpty(koi.name) ? "鯉" : koi.name,
                score = HinpyokaiScore.From(koi)
            });
        }
        entries.Sort((a, b) => b.score.total.CompareTo(a.score.total));
    }

    void FillRankList()
    {
        var content = EnsureRankScroll();
        if (content == null) return;
        ClearChildren(content);

        for (int i = 0; i < entries.Count; i++)
            CreateRow(content, i, entries[i]);
    }

    RectTransform EnsureRankScroll()
    {
        var body = FindPath("RankPanel", "Body");
        if (body == null) return null;
        var content = FindPath("RankPanel", "Body", "RankScroll", "Viewport", "Content");
        if (content != null) return content;

        var scrollGo = new GameObject("RankScroll", typeof(RectTransform));
        var scrollRt = scrollGo.GetComponent<RectTransform>();
        scrollRt.SetParent(body, false);
        Stretch(scrollRt);
        var scroll = scrollGo.AddComponent<ScrollRect>();
        scroll.horizontal = false;
        scroll.vertical = true;
        scroll.movementType = ScrollRect.MovementType.Elastic;
        scroll.scrollSensitivity = 40f;

        var viewportGo = new GameObject("Viewport", typeof(RectTransform));
        var viewport = viewportGo.GetComponent<RectTransform>();
        viewport.SetParent(scrollRt, false);
        Stretch(viewport);
        var hit = viewportGo.AddComponent<Image>();
        hit.color = new Color(1, 1, 1, 0);
        viewportGo.AddComponent<RectMask2D>();

        var contentGo = new GameObject("Content", typeof(RectTransform));
        content = contentGo.GetComponent<RectTransform>();
        content.SetParent(viewport, false);
        content.anchorMin = new Vector2(0, 1);
        content.anchorMax = new Vector2(1, 1);
        content.pivot = new Vector2(0.5f, 1);
        content.anchoredPosition = Vector2.zero;
        content.sizeDelta = Vector2.zero;

        var layout = contentGo.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(0, 0, 8, 8);
        layout.spacing = 8f;
        layout.childAlignment = TextAnchor.UpperCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = true;
        layout.childForceExpandHeight = false;
        var fitter = contentGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        scroll.viewport = viewport;
        scroll.content = content;
        return content;
    }

    static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    void CreateRow(RectTransform parent, int index, Entry entry)
    {
        var go = new GameObject("Rank_" + (index + 1), typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        var le = go.AddComponent<LayoutElement>();
        le.minHeight = 70f;
        le.preferredHeight = 70f;

        var img = go.AddComponent<Image>();
        if (rankRowBackground != null)
        {
            img.sprite = rankRowBackground;
            img.type = Image.Type.Simple;
            img.color = Color.white;
        }
        else
        {
            img.sprite = roundWhite;
            img.type = Image.Type.Sliced;
            img.color = RowTint;
        }

        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        int captured = index;
        btn.onClick.AddListener(() => ShowDetail(captured));

        Label(rt, (index + 1) + "位  " + entry.owner, 28, TextAnchor.MiddleLeft, new Vector2(28, 0), new Vector2(-120, 0));
        Label(rt, entry.score.total + "点", 28, TextAnchor.MiddleRight, new Vector2(0, 0), new Vector2(-28, 0));
    }

    void ShowDetail(int index)
    {
        if (entries.Count == 0)
        {
            SetText("DetailPanel", "RankLabel", "");
            SetText("DetailPanel", "NameLabel", "出す鯉がありません");
            SetText("DetailPanel", "BreedLabel", "");
            SetText("DetailPanel", "TotalLabel", "");
            SetText("DetailPanel", "GlossLabel", "艶");
            SetText("DetailPanel", "PatternLabel", "模様");
            SetText("DetailPanel", "BodyLabel", "体格");
            SetPhoto(null, Color.white);
            return;
        }

        selected = Mathf.Clamp(index, 0, entries.Count - 1);
        var e = entries[selected];
        var koiName = e.koi != null ? e.koi.name : "鯉";
        SetText("DetailPanel", "RankLabel", (selected + 1) + "位");
        SetText("DetailPanel", "NameLabel", koiName);
        SetText("DetailPanel", "BreedLabel", "品種");
        SetText("DetailPanel", "TotalLabel", e.score.total + "点");
        SetText("DetailPanel", "GlossLabel", "艶  " + e.score.gloss + "点");
        SetText("DetailPanel", "PatternLabel", "模様  " + e.score.pattern + "点");
        SetText("DetailPanel", "BodyLabel", "体格  " + e.score.body + "点");
        if (e.koi != null && e.koi.photoTexture != null)
            SetPhoto(e.koi.photoTexture, Color.white);
        else
            SetPhoto(Texture2D.whiteTexture, e.koi != null ? e.koi.mainColor : Color.white);
    }

    void SetText(string panel, string child, string value)
    {
        var t = FindPath(panel, child);
        if (t == null) return;
        var text = t.GetComponent<Text>();
        if (text != null) text.text = value;
    }

    void SetPhoto(Texture tex, Color color)
    {
        var t = FindPath("DetailPanel", "Photo");
        if (t == null) return;
        var raw = t.GetComponent<RawImage>();
        if (raw == null) return;
        raw.texture = tex;
        raw.color = color;
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
            Destroy(parent.GetChild(i).gameObject);
    }

    RectTransform Label(RectTransform parent, string text, int size, TextAnchor align, Vector2 minOff, Vector2 maxOff)
    {
        var go = new GameObject("Text", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
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
