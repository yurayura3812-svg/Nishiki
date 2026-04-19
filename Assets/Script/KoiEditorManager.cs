using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class KoiEditorManager : MonoBehaviour
{
    [Header("Target")]
    public KoiController targetKoi; // 編集対象の鯉（シーン内のオブジェクトをアサイン）
    
    [Header("UI Elements")]
    public RectTransform contentRoot;      // ScrollViewのContent (VerticalLayoutGroup付き)
    public GameObject sliderRowPrefab;     // EditorSliderRowのプレハブ
    public InputField fileNameInput;       // 保存名入力用InputField
    
    [Header("Data")]
    public KoiPatternData baseData;        // 編集の元にするデータ（あればアサイン）
    
    [System.Serializable]
    public struct SliderSetting {
        public string label;
        public string propName;
        public float min;
        public float max;
    }

    public List<SliderSetting> settings = new List<SliderSetting>();
    
    private Dictionary<string, float> currentValues = new Dictionary<string, float>();
    private List<EditorSliderRow> rows = new List<EditorSliderRow>();

    void Start()
    {
        // 1. スライダー項目の登録
        // 地肌の色のR,G,Bを追加
        AddSetting("地肌 色(R)", "_BaseColorR", 0, 1);
        AddSetting("地肌 色(G)", "_BaseColorG", 0, 1);
        AddSetting("地肌 色(B)", "_BaseColorB", 0, 1);

        AddSetting("赤 色(R)", "_RedColorR", 0, 1);
        AddSetting("赤 色(G)", "_RedColorG", 0, 1);
        AddSetting("赤 色(B)", "_RedColorB", 0, 1);
        AddSetting("赤 量", "_RedAmount", 0, 1);
        AddSetting("赤 大きさ", "_RedScale", 0.5f, 10.0f);
        AddSetting("赤 詳細", "_RedDetail", 0, 1);

        AddSetting("黒 色(R)", "_BlackColorR", 0, 1);
        AddSetting("黒 色(G)", "_BlackColorG", 0, 1);
        AddSetting("黒 色(B)", "_BlackColorB", 0, 1);
        AddSetting("黒 量", "_BlackAmount", 0, 1);
        AddSetting("黒 大きさ", "_BlackScale", 0.5f, 10.0f);
        AddSetting("黒 詳細", "_BlackDetail", 0, 1);

        AddSetting("シード X", "_SeedX", 0, 100);
        AddSetting("シード Y", "_SeedY", 0, 100);
        AddSetting("シード Z", "_SeedZ", 0, 100);

        AddSetting("お腹境界", "_BellyLimit", -0.5f, 0.0f);
        AddSetting("ボケ具合", "_PatternSoftness", 0.01f, 0.5f);

        // 2. 「真っ白」回避用の初期値を辞書にセット
        InitializeDefaultValues();

        // 3. UIの生成
        GenerateUI();
    }

    void InitializeDefaultValues()
    {
        // 錦鯉らしいデフォルト値をあらかじめセット
        // 初期値を白（1.0）にセット
        currentValues["_BaseColorR"] = 1.0f;
        currentValues["_BaseColorG"] = 1.0f;
        currentValues["_BaseColorB"] = 1.0f;

        currentValues["_RedColorR"] = 1.0f;
        currentValues["_RedColorG"] = 0.0f;
        currentValues["_RedColorB"] = 0.0f;
        currentValues["_RedAmount"] = 0.6f;
        currentValues["_RedScale"] = 2.5f;
        currentValues["_RedDetail"] = 0.5f;

        currentValues["_BlackColorR"] = 0.1f;
        currentValues["_BlackColorG"] = 0.1f;
        currentValues["_BlackColorB"] = 0.1f;
        currentValues["_BlackAmount"] = 0.3f;
        currentValues["_BlackScale"] = 3.5f;
        currentValues["_BlackDetail"] = 0.5f;

        currentValues["_SeedX"] = Random.Range(0f, 100f);
        currentValues["_SeedY"] = Random.Range(0f, 100f);
        currentValues["_SeedZ"] = Random.Range(0f, 100f);

        currentValues["_BellyLimit"] = -0.2f; 
        currentValues["_PatternSoftness"] = 0.05f;

        // もし baseData がアサインされていれば、そちらで上書きする
        if (baseData != null)
        {
            currentValues["_RedColorR"] = baseData.redColor.r;
            currentValues["_RedColorG"] = baseData.redColor.g;
            currentValues["_RedColorB"] = baseData.redColor.b;
            currentValues["_RedAmount"] = baseData.redAmount;
            currentValues["_RedScale"] = baseData.redScale;
            currentValues["_BlackAmount"] = baseData.blackAmount;
            currentValues["_BlackScale"] = baseData.blackScale;
            currentValues["_SeedX"] = baseData.patternSeed.x;
            currentValues["_SeedY"] = baseData.patternSeed.y;
            currentValues["_BellyLimit"] = baseData.bellyLimit;
        }
    }

    void AddSetting(string l, string p, float min, float max) {
        settings.Add(new SliderSetting { label = l, propName = p, min = min, max = max });
    }

    void GenerateUI()
    {
        foreach (var s in settings)
        {
            GameObject go = Instantiate(sliderRowPrefab, contentRoot);
            var row = go.GetComponent<EditorSliderRow>();
            
            // 辞書にセットされた初期値を取得
            float initVal = currentValues.ContainsKey(s.propName) ? currentValues[s.propName] : 0.5f; 
            
            row.Setup(s.label, s.propName, s.min, s.max, initVal, OnSliderChanged);
            rows.Add(row);
            currentValues[s.propName] = initVal;
        }
        UpdateKoi();
    }

    void OnSliderChanged(string prop, float val)
    {
        currentValues[prop] = val;
        UpdateKoi();
    }

    void UpdateKoi()
    {
        if (targetKoi == null) return;
        
        // 子要素すべて（体、ひれ等）のRendererを取得して反映
        Renderer[] renderers = targetKoi.GetComponentsInChildren<Renderer>();
        
        foreach (var r in renderers)
        {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);

            // 色の合成
            // 地肌の色（_BaseColor）をセット
            Color baseCol = new Color(GetValue("_BaseColorR"), GetValue("_BaseColorG"), GetValue("_BaseColorB"), 1);
            mpb.SetColor("_BaseColor", baseCol);

            Color red = new Color(GetValue("_RedColorR"), GetValue("_RedColorG"), GetValue("_RedColorB"), 1);
            mpb.SetColor("_RedColor", red);

            Color black = new Color(GetValue("_BlackColorR"), GetValue("_BlackColorG"), GetValue("_BlackColorB"), 1);
            mpb.SetColor("_BlackColor", black);

            // 数値パラメータ
            mpb.SetFloat("_RedAmount", GetValue("_RedAmount"));
            mpb.SetFloat("_RedScale", GetValue("_RedScale"));
            mpb.SetFloat("_RedDetail", GetValue("_RedDetail"));
            mpb.SetFloat("_BlackAmount", GetValue("_BlackAmount"));
            mpb.SetFloat("_BlackScale", GetValue("_BlackScale"));
            mpb.SetFloat("_BlackDetail", GetValue("_BlackDetail"));

            // Vector（Seed）
            Vector4 seed = new Vector4(GetValue("_SeedX"), GetValue("_SeedY"), GetValue("_SeedZ"), 0);
            mpb.SetVector("_Seed", seed);

            // その他
            mpb.SetFloat("_BellyLimit", GetValue("_BellyLimit"));
            mpb.SetFloat("_PatternSoftness", GetValue("_PatternSoftness"));

            r.SetPropertyBlock(mpb);
        }
    }

    float GetValue(string key) {
        return currentValues.ContainsKey(key) ? currentValues[key] : 0;
    }

        public void Randomize()
    {
        // 1. 各スライダーをランダムな位置に動かす
        foreach (var row in rows)
        {
            // 色以外のパラメータ（量や大きさ）をランダムに
            if (!row.propertyName.Contains("Color")) 
            {
                float rand = Random.Range(row.slider.minValue, row.slider.maxValue);
                row.SetValueQuietly(rand);
                currentValues[row.propertyName] = rand;
            }
        }

        // 2. 「色」をランダムに決める（錦鯉らしい色合いから選択）
        ApplyRandomKoiStyle();

        // 3. シード値を大きく変えて模様の形をガラッと変える
        currentValues["_SeedX"] = Random.Range(0f, 100f);
        currentValues["_SeedY"] = Random.Range(0f, 100f);
        currentValues["_SeedZ"] = Random.Range(0f, 100f);

        UpdateKoi(); 
    }

    // 錦鯉のパターンに合わせて色をセットするヘルパー
    void ApplyRandomKoiStyle()
    {
        float type = Random.value;

        // --- 1. まずベースカラー（地肌）を決める ---
        if (type < 0.7f) 
        {
            // 70%：標準的な白い地肌（紅白・三色系）
            SetColorValues("_BaseColor", Color.white);
        }
        else if (type < 0.9f)
        {
            // 20%：黄色・黄金系の地肌
            SetColorValues("_BaseColor", new Color(1f, 0.8f, 0.2f)); 
        }
        else
        {
            // 10%：珍しい青みがかった、あるいは暗めの地肌
            SetColorValues("_BaseColor", new Color(0.7f, 0.8f, 1f));
        }

        // --- 2. メイン模様（赤系）の色を決める ---
        Color[] redPalette = { Color.red, new Color(1f, 0.3f, 0f), new Color(1f, 0.5f, 0f) };
        SetColorValues("_RedColor", redPalette[Random.Range(0, redPalette.Length)]);
        currentValues["_RedAmount"] = Random.Range(0.3f, 0.7f);

        // --- 3. 黒模様（墨）を混ぜるかどうか ---
        if (Random.value < 0.6f) // 60%で黒が出る
        {
            SetColorValues("_BlackColor", new Color(0.05f, 0.05f, 0.05f));
            currentValues["_BlackAmount"] = Random.Range(0.2f, 0.5f);
        }
        else
        {
            currentValues["_BlackAmount"] = 0;
        }

        // スライダーに数値を同期
        SyncSlidersWithValues();
    }

    // 共通ヘルパー（"_BaseColor" なども受け取れるように）
    void SetColorValues(string propPrefix, Color c)
    {
        currentValues[propPrefix + "R"] = c.r;
        currentValues[propPrefix + "G"] = c.g;
        currentValues[propPrefix + "B"] = c.b;
    }

    void SyncSlidersWithValues()
    {
        foreach (var row in rows)
        {
            if (currentValues.ContainsKey(row.propertyName))
            {
                row.SetValueQuietly(currentValues[row.propertyName]);
            }
        }
    }

    public void SaveAsAsset()
    {
#if UNITY_EDITOR
        KoiPatternData newData = ScriptableObject.CreateInstance<KoiPatternData>();
        
        newData.redColor = new Color(GetValue("_RedColorR"), GetValue("_RedColorG"), GetValue("_RedColorB"));
        newData.redAmount = GetValue("_RedAmount");
        newData.redScale = GetValue("_RedScale");
        newData.blackAmount = GetValue("_BlackAmount");
        newData.blackScale = GetValue("_BlackScale");
        newData.patternSeed = new Vector3(GetValue("_SeedX"), GetValue("_SeedY"), GetValue("_SeedZ"));
        newData.bellyLimit = GetValue("_BellyLimit");

        string name = fileNameInput.text;
        if (string.IsNullOrEmpty(name)) name = "KoiData_" + System.DateTime.Now.ToString("HHmmss");

        string path = "Assets/KoiData/" + name + ".asset";
        
        if (!Directory.Exists("Assets/KoiData")) Directory.CreateDirectory("Assets/KoiData");

        AssetDatabase.CreateAsset(newData, path);
        AssetDatabase.SaveAssets();
        Debug.Log("Saved Asset to: " + path);
#endif
    }
}