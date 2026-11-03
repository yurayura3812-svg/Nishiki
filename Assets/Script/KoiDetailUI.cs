using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 個体詳細。Menu は既存。出品ボタンは品評会経由のときだけ出す。
/// </summary>
public class KoiDetailUI : MonoBehaviour
{
    KoiPatternData koi;
    GameObject confirm;
    Font font;

    void Awake()
    {
        font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (font == null) font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        koi = KoiSaveStore.Find(KoiDetailGate.KoiKey);
        Wire("CloseButton", KoiDetailGate.Close);
        Wire("EnterButton", KoiDetailGate.SubmitCurrent);
        Wire("DeleteButton", OpenConfirm);
        EnsureDeleteButton();
        Paint();
    }

    void Paint()
    {
        SetText("UserName", "User name");
        SetText("DetailPanel", "RankLabel", "");
        SetText("DetailPanel", "NameLabel", koi != null ? koi.name : "なまえ");
        SetText("DetailPanel", "BreedLabel", "品種");
        SetText("DetailPanel", "TotalLabel", "生まれた日");
        SetText("DetailPanel", "BodyLabel", "入賞回数");

        var score = HinpyokaiScore.From(koi);
        SetText("DetailPanel", "PatternLabel", "模様          " + score.pattern + " 点");
        SetText("DetailPanel", "GlossLabel", "艶            " + score.gloss + " 点");

        var enter = FindDeep("EnterButton");
        if (enter != null)
        {
            bool show = KoiDetailGate.EnterJudging && KoiDetailGate.ReturnScene == "HinpyokaiScene";
            enter.gameObject.SetActive(show);
        }

        var del = FindDeep("DeleteButton");
        if (del != null) del.gameObject.SetActive(koi != null);

        var photo = FindDeep("KoiPhoto");
        if (photo == null) photo = FindPath("DetailPanel", "Photo");
        var raw = photo != null ? photo.GetComponent<RawImage>() : null;
        if (raw != null)
        {
            if (koi != null && koi.photoTexture != null)
            {
                raw.texture = koi.photoTexture;
                raw.color = Color.white;
            }
            else
            {
                raw.texture = Texture2D.whiteTexture;
                raw.color = koi != null ? koi.mainColor : Color.white;
            }
        }
    }

    void EnsureDeleteButton()
    {
        if (FindDeep("DeleteButton") != null) return;
        var panel = FindDeep("DetailPanel") as RectTransform;
        if (panel == null) return;

        var go = new GameObject("DeleteButton", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(panel, false);
        rt.anchorMin = new Vector2(0.18f, 0.18f);
        rt.anchorMax = new Vector2(0.82f, 0.3f);
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = new Color(0.92f, 0.55f, 0.5f, 1f);
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(OpenConfirm);

        var textGo = new GameObject("Text", typeof(RectTransform));
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(rt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var label = textGo.AddComponent<Text>();
        label.font = font;
        label.text = "削除";
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.fontSize = 24;
        label.raycastTarget = false;
    }

    void OpenConfirm()
    {
        if (koi == null) return;
        if (confirm == null) confirm = BuildConfirm();
        confirm.SetActive(true);
        confirm.transform.SetAsLastSibling();
    }

    void CloseConfirm()
    {
        if (confirm != null) confirm.SetActive(false);
    }

    void ConfirmDelete()
    {
        KoiSaveStore.Delete(koi);
        KoiDetailGate.Close();
    }

    GameObject BuildConfirm()
    {
        var overlay = new GameObject("DeleteConfirm", typeof(RectTransform));
        var rt = overlay.GetComponent<RectTransform>();
        rt.SetParent(transform, false);
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var dim = overlay.AddComponent<Image>();
        dim.color = new Color(0.12f, 0.16f, 0.22f, 0.55f);

        var box = new GameObject("Box", typeof(RectTransform));
        var boxRt = box.GetComponent<RectTransform>();
        boxRt.SetParent(rt, false);
        boxRt.anchorMin = new Vector2(0.5f, 0.5f);
        boxRt.anchorMax = new Vector2(0.5f, 0.5f);
        boxRt.sizeDelta = new Vector2(520, 280);
        boxRt.anchoredPosition = Vector2.zero;
        var boxImg = box.AddComponent<Image>();
        boxImg.color = new Color(0.93f, 0.97f, 1f, 1f);

        var msgGo = new GameObject("Message", typeof(RectTransform));
        var msgRt = msgGo.GetComponent<RectTransform>();
        msgRt.SetParent(boxRt, false);
        msgRt.anchorMin = new Vector2(0.08f, 0.42f);
        msgRt.anchorMax = new Vector2(0.92f, 0.88f);
        msgRt.offsetMin = Vector2.zero;
        msgRt.offsetMax = Vector2.zero;
        var msg = msgGo.AddComponent<Text>();
        msg.font = font;
        msg.text = "この鯉を本当に消しますか？";
        msg.alignment = TextAnchor.MiddleCenter;
        msg.color = new Color(0.22f, 0.32f, 0.45f);
        msg.fontSize = 28;
        msg.horizontalOverflow = HorizontalWrapMode.Wrap;
        msg.verticalOverflow = VerticalWrapMode.Overflow;
        msg.raycastTarget = false;

        MakeDialogButton(boxRt, "Cancel", "やめる", new Vector2(0.08f, 0.1f), new Vector2(0.48f, 0.34f), new Color(0.82f, 0.88f, 0.93f), CloseConfirm);
        MakeDialogButton(boxRt, "Ok", "消す", new Vector2(0.52f, 0.1f), new Vector2(0.92f, 0.34f), new Color(0.9f, 0.5f, 0.46f), ConfirmDelete);

        overlay.SetActive(false);
        return overlay;
    }

    void MakeDialogButton(RectTransform parent, string name, string label, Vector2 min, Vector2 max, Color color, UnityEngine.Events.UnityAction action)
    {
        var go = new GameObject(name, typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.SetParent(parent, false);
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
        var img = go.AddComponent<Image>();
        img.color = color;
        var btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(action);

        var textGo = new GameObject("Text", typeof(RectTransform));
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.SetParent(rt, false);
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;
        var t = textGo.AddComponent<Text>();
        t.font = font;
        t.text = label;
        t.alignment = TextAnchor.MiddleCenter;
        t.color = new Color(0.18f, 0.22f, 0.28f);
        t.fontSize = 24;
        t.raycastTarget = false;
    }

    void Wire(string name, UnityEngine.Events.UnityAction action)
    {
        var t = FindDeep(name);
        if (t == null) return;
        var btn = t.GetComponent<Button>();
        if (btn == null) return;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(action);
    }

    void SetText(params string[] pathAndValue)
    {
        if (pathAndValue == null || pathAndValue.Length < 2) return;
        var names = new string[pathAndValue.Length - 1];
        for (int i = 0; i < names.Length; i++) names[i] = pathAndValue[i];
        var value = pathAndValue[pathAndValue.Length - 1];
        var t = names.Length == 1 ? FindDeep(names[0]) : FindPath(names);
        if (t == null) return;
        var text = t.GetComponent<Text>();
        if (text != null) text.text = value;
    }

    Transform FindDeep(string name)
    {
        var direct = transform.Find(name);
        if (direct != null) return direct;
        foreach (var t in GetComponentsInChildren<Transform>(true))
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
}
