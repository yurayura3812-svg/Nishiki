using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

public class KoiEditorManager : MonoBehaviour
{
    public enum KoiMode { Single, Dual, Triple }
    private KoiMode currentMode = KoiMode.Triple;

    [Header("Target")]
    public KoiController targetKoi; 
    
    [Header("UI Elements")]
    public RectTransform contentRoot;
    public GameObject sliderRowPrefab;
    public InputField fileNameInput;
    
    [Header("Data Template")]
    public KoiPatternData baseData; 

    [System.Serializable]
    public struct SliderSetting {
        public string label;
        public string propName;
        public float min;
        public float max;
    }
    public Toggle seedToggle;

    [Header("UI Colors")]
    public Color activeColor = Color.cyan;   // 選択中の色
    public Color inactiveColor = Color.white; // 非選択の色

    [Header("Mode Buttons")]
    public Image btnSingleImage; // ボタンのImageコンポーネントをアサイン
    public Image btnDualImage;
    public Image btnTripleImage;

    public List<SliderSetting> settings = new List<SliderSetting>();
    private Dictionary<string, float> currentValues = new Dictionary<string, float>();
    private List<EditorSliderRow> rows = new List<EditorSliderRow>();

    // グループ管理用（表示・非表示を切り替えるため）
    private List<EditorSliderRow> sub1Group = new List<EditorSliderRow>();
    private List<EditorSliderRow> sub2Group = new List<EditorSliderRow>();

    // --- 追加：シードのスライダーをまとめて管理するリスト ---
    private List<EditorSliderRow> seedRows = new List<EditorSliderRow>();
    void Start()
    {
        // 1. 設定項目の登録（名称を Main / Sub1 / Sub2 に統一）
        AddSetting("メイン 色(R)", "_MainColorR", 0, 1);
        AddSetting("メイン 色(G)", "_MainColorG", 0, 1);
        AddSetting("メイン 色(B)", "_MainColorB", 0, 1);

        AddSetting("サブ1 色(R)", "_SubColor1R", 0, 1);
        AddSetting("サブ1 色(G)", "_SubColor1G", 0, 1);
        AddSetting("サブ1 色(B)", "_SubColor1B", 0, 1);
        AddSetting("サブ1 量", "_Sub1Amount", 0, 1);
        AddSetting("サブ1 大きさ", "_Sub1Scale", 0.5f, 10.0f);
        AddSetting("サブ1 詳細", "_Sub1Detail", 0, 1);

        AddSetting("サブ2 色(R)", "_SubColor2R", 0, 1);
        AddSetting("サブ2 色(G)", "_SubColor2G", 0, 1);
        AddSetting("サブ2 色(B)", "_SubColor2B", 0, 1);
        AddSetting("サブ2 量", "_Sub2Amount", 0, 1);
        AddSetting("サブ2 大きさ", "_Sub2Scale", 0.5f, 10.0f);
        AddSetting("サブ2 詳細", "_Sub2Detail", 0, 1);

        AddSetting("模様シード X", "_SeedX", 0, 100);
        AddSetting("模様シード Y", "_SeedY", 0, 100);
        AddSetting("模様シード Z", "_SeedZ", 0, 100);

        InitializeDefaultValues();
        GenerateUI();

        // 最初は「単色」モードで起動
        SetModeSingle();
    }

    // --- ボタンから呼ぶモード切替用メソッド ---
    public void SetModeSingle() => ChangeMode(KoiMode.Single);
    public void SetModeDual() => ChangeMode(KoiMode.Dual);
    public void SetModeTriple() => ChangeMode(KoiMode.Triple);

    private void ChangeMode(KoiMode mode)
    {
        currentMode = mode;

        // ボタンの見た目を更新
        if (btnSingleImage) btnSingleImage.color = (mode == KoiMode.Single) ? activeColor : inactiveColor;
        if (btnDualImage)   btnDualImage.color   = (mode == KoiMode.Dual)   ? activeColor : inactiveColor;
        if (btnTripleImage) btnTripleImage.color = (mode == KoiMode.Triple) ? activeColor : inactiveColor;

        // UIのスライダー表示・非表示
        foreach (var r in sub1Group) r.gameObject.SetActive(mode != KoiMode.Single);
        foreach (var r in sub2Group) r.gameObject.SetActive(mode == KoiMode.Triple);

        // モード切替の命令が終わったあとで、「トグルがONならシード値を出す」と念押しする
        if (seedToggle != null)
        {
            foreach (var r in seedRows)
            {
                r.gameObject.SetActive(seedToggle.isOn);
            }
        }
        UpdateKoi();
    }

    // --- 模様ガチャ（色は変えずシードだけランダム） ---
    public void RandomizeSeedOnly()
    {
        currentValues["_SeedX"] = Random.Range(0f, 100f);
        currentValues["_SeedY"] = Random.Range(0f, 100f);
        currentValues["_SeedZ"] = Random.Range(0f, 100f);

        foreach (var row in rows)
        {
            if (row.propertyName.Contains("Seed"))
            {
                row.SetValueQuietly(currentValues[row.propertyName]);
            }
        }
        UpdateKoi();
    }

    void InitializeDefaultValues()
    {
        currentValues["_MainColorR"] = 1.0f;
        currentValues["_MainColorG"] = 1.0f;
        currentValues["_MainColorB"] = 1.0f;

        currentValues["_SubColor1R"] = 0.8f;
        currentValues["_SubColor1G"] = 0.1f;
        currentValues["_SubColor1B"] = 0.1f;
        currentValues["_Sub1Amount"] = 0.5f;
        currentValues["_Sub1Scale"] = 2.0f;

        currentValues["_SubColor2R"] = 0.1f;
        currentValues["_SubColor2G"] = 0.1f;
        currentValues["_SubColor2B"] = 0.1f;
        currentValues["_Sub2Amount"] = 0.4f;
        currentValues["_Sub2Scale"] = 3.0f;

        currentValues["_SeedX"] = Random.Range(0f, 100f);
        currentValues["_SeedY"] = Random.Range(0f, 100f);
        currentValues["_SeedZ"] = Random.Range(0f, 100f);
    }

    void GenerateUI()
{
    foreach (var s in settings)
    {
        GameObject go = Instantiate(sliderRowPrefab, contentRoot);
        var row = go.GetComponent<EditorSliderRow>();
        float initVal = currentValues.ContainsKey(s.propName) ? currentValues[s.propName] : 0.5f; 
        row.Setup(s.label, s.propName, s.min, s.max, initVal, (p, v) => { currentValues[p] = v; UpdateKoi(); });
        rows.Add(row);

        // --- 1. シード値かどうかを真っ先に判定する ---
        if (s.propName.Contains("Seed"))
        {
            seedRows.Add(row);
            // ★重要：シード値だったら、ここでこの回の処理を「おしまい」にする。
            // これで、下の sub1Group とかには絶対に入らなくなります。
            continue; 
        }

        // --- 2. シード値じゃないものだけが、ここから下のグループ分けに進める ---
        if (s.propName.Contains("SubColor1") || s.propName.Contains("Sub1")) sub1Group.Add(row);
        if (s.propName.Contains("SubColor2") || s.propName.Contains("Sub2")) sub2Group.Add(row);
    }

    // 2. モードを適用
    ChangeMode(currentMode); // TripleでもcurrentModeでもOK

    // 3. 最後にトグルの状態を念押し！
    if (seedToggle != null) 
    {
        ToggleSeedVisible(seedToggle.isOn);
    }
}

    // --- 追加：チェックボックスから呼ばれる命令 ---
    public void ToggleSeedVisible(bool isOn)
    {
        foreach (var row in seedRows)
        {
            row.gameObject.SetActive(isOn); // チェックに応じて表示/非表示
        }
    }

    void UpdateKoi()
    {
        if (targetKoi == null) return;
        Renderer[] renderers = targetKoi.GetComponentsInChildren<Renderer>();

        // 現在のモードに合わせて送る値を調整
        float s1Amount = (currentMode == KoiMode.Single) ? 0 : GetValue("_Sub1Amount");
        float s2Amount = (currentMode == KoiMode.Triple) ? GetValue("_Sub2Amount") : 0;

        foreach (var r in renderers)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);

            // プロパティ名がシェーダーの Properties ブロックと完全に一致しているか確認
            mpb.SetColor("_MainColor", new Color(GetValue("_MainColorR"), GetValue("_MainColorG"), GetValue("_MainColorB"), 1));
            mpb.SetColor("_SubColor1", new Color(GetValue("_SubColor1R"), GetValue("_SubColor1G"), GetValue("_SubColor1B"), 1));
            mpb.SetColor("_SubColor2", new Color(GetValue("_SubColor2R"), GetValue("_SubColor2G"), GetValue("_SubColor2B"), 1));
            mpb.SetFloat("_Sub1Detail", GetValue("_Sub1Detail"));
            mpb.SetFloat("_Sub2Detail", GetValue("_Sub2Detail"));

            mpb.SetFloat("_Sub1Amount", s1Amount);
            mpb.SetFloat("_Sub1Scale", GetValue("_Sub1Scale"));
            mpb.SetFloat("_Sub2Amount", s2Amount);
            mpb.SetFloat("_Sub2Scale", GetValue("_Sub2Scale"));

            // Vector4の第4引数(w)は0でOK
            mpb.SetVector("_Seed", new Vector4(GetValue("_SeedX"), GetValue("_SeedY"), GetValue("_SeedZ"), 0));
            
            r.SetPropertyBlock(mpb);
        }
    }
    // 全てのパラメータ（色・量・シード）をランダムにする
    public void RandomizeAll()
    {
        foreach (var row in rows)
        {
            // 各スライダーの最小値〜最大値でランダムな値を決める
            float rand = Random.Range(row.slider.minValue, row.slider.maxValue);
            
            // 内部データに保存
            currentValues[row.propertyName] = rand;
            
            // スライダーの見た目（つまみ）に反映
            row.SetValueQuietly(rand);
        }

        // 最後にマテリアルへ一括反映
        UpdateKoi();
    }

    public void SaveAsAsset()
    {
#if UNITY_EDITOR
        KoiPatternData newData = ScriptableObject.CreateInstance<KoiPatternData>();
        
        // メインカラーは常に保存
        newData.mainColor = new Color(GetValue("_MainColorR"), GetValue("_MainColorG"), GetValue("_MainColorB"), 1);

        // --- モード判定に基づいた書き出し ---
        
        // 単色（Single）ならサブ1・サブ2の量を0にして保存
        // 2色（Dual）ならサブ1はそのまま、サブ2の量を0にして保存
        // 3色（Triple）なら全てそのまま保存
        
        float s1Amount = (currentMode == KoiMode.Single) ? 0 : GetValue("_Sub1Amount");
        float s2Amount = (currentMode == KoiMode.Triple) ? GetValue("_Sub2Amount") : 0;

        // サブ1の設定
        newData.sub1Color = new Color(GetValue("_SubColor1R"), GetValue("_SubColor1G"), GetValue("_SubColor1B"), 1);
        newData.sub1Amount = s1Amount; 
        newData.sub1Scale = GetValue("_Sub1Scale");

        // サブ2の設定
        newData.sub2Color = new Color(GetValue("_SubColor2R"), GetValue("_SubColor2G"), GetValue("_SubColor2B"), 1);
        newData.sub2Amount = s2Amount;
        newData.sub2Scale = GetValue("_Sub2Scale");

        newData.sub1Detail = GetValue("_Sub1Detail"); // Dataスクリプトに変数がない場合は追加
        newData.sub2Detail = GetValue("_Sub2Detail");

        // その他共通設定
        newData.patternSeed = new Vector3(GetValue("_SeedX"), GetValue("_SeedY"), GetValue("_SeedZ"));
        newData.bellyLimit = 0; // 必要に応じてここもスライダー値を取得

        // 保存処理
        string name = fileNameInput.text;
        if (string.IsNullOrEmpty(name)) name = "KoiData_" + System.DateTime.Now.ToString("HHmmss");
        string path = "Assets/KoiData/" + name + ".asset";
        if (!Directory.Exists("Assets/KoiData")) Directory.CreateDirectory("Assets/KoiData");
        
        UnityEditor.AssetDatabase.CreateAsset(newData, path);
        UnityEditor.AssetDatabase.SaveAssets();
        UnityEditor.AssetDatabase.Refresh();
        
        Debug.Log($"<color=green>保存完了:</color> {currentMode}モードで {name}.asset を作成しました。");
#endif
    }

    void AddSetting(string l, string p, float min, float max) => settings.Add(new SliderSetting { label = l, propName = p, min = min, max = max });
    float GetValue(string key) => currentValues.ContainsKey(key) ? currentValues[key] : 0;
}