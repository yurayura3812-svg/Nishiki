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
    public RectTransform contentRoot;
    public GameObject sliderRowPrefab;
    public InputField fileNameInput;
    public KoiPatternData baseData; 

    [System.Serializable]
    public struct SliderSetting {
        public string label; public string propName; public float min; public float max;
    }
    public Toggle seedToggle;

    [Header("UI Colors")]
    public Color activeColor = Color.cyan;   
    public Color inactiveColor = Color.white; 

    [Header("Mode Buttons")]
    public Image btnSingleImage; public Image btnDualImage; public Image btnTripleImage;

    [Header("Color Picker Integration")]
    public KoiColorPicker colorPicker; 
    public GameObject colorButtonPrefab; 
    public Transform colorButtonRoot;    
    private string activeColorPrefix = "_MainColor"; 

    [Header("Color Picker UI Group")]
    public GameObject pickerUIGroup;
    public Slider brightnessSlider; 

    private Dictionary<string, Color> savedHues = new Dictionary<string, Color>();

    public List<SliderSetting> settings = new List<SliderSetting>();
    private Dictionary<string, float> currentValues = new Dictionary<string, float>();
    private List<EditorSliderRow> rows = new List<EditorSliderRow>();
    private List<GameObject> pickerButtons = new List<GameObject>(); 

    private bool isUpdatingUI = false; // 無限ループ防止フラグ

    void Start()
    {
        AddSetting("メイン 色(R)", "_MainColorR", 0, 1);
        AddSetting("メイン 色(G)", "_MainColorG", 0, 1);
        AddSetting("メイン 色(B)", "_MainColorB", 0, 1);
        AddSetting("サブ1 色(R)", "_SubColor1R", 0, 1);
        AddSetting("サブ1 色(G)", "_SubColor1G", 0, 1);
        AddSetting("サブ1 色(B)", "_SubColor1B", 0, 1);
        AddSetting("サブ1 量", "_Sub1Amount", 0, 1);
        AddSetting("サブ1 大きさ", "_Sub1Scale", 0.5f, 10.0f);
        AddSetting("サブ1 詳細", "_Sub1Detail", 0, 10);
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
        
        SaveHueToMemory("_MainColor");
        SaveHueToMemory("_SubColor1");
        SaveHueToMemory("_SubColor2");

        GenerateUI();
        SetModeSingle();

        if (colorPicker != null) colorPicker.OnColorSelected = OnColorPicked;
        if (pickerUIGroup != null) pickerUIGroup.SetActive(false);
    }

    private void SaveHueToMemory(string prefix)
    {
        float r = GetValue(prefix + "R");
        float g = GetValue(prefix + "G");
        float b = GetValue(prefix + "B");
        float h, s, v;
        Color.RGBToHSV(new Color(r, g, b), out h, out s, out v);
        if (v > 0.01f || !savedHues.ContainsKey(prefix))
        {
            savedHues[prefix] = Color.HSVToRGB(h, s, 1.0f);
        }
    }

    private float GetCurrentBrightness()
    {
        float r = GetValue(activeColorPrefix + "R");
        float g = GetValue(activeColorPrefix + "G");
        float b = GetValue(activeColorPrefix + "B");
        float h, s, v;
        Color.RGBToHSV(new Color(r, g, b), out h, out s, out v);
        return v;
    }

    public void SetPickerTarget(string prefix)
    {
        activeColorPrefix = prefix;
        if (pickerUIGroup != null) pickerUIGroup.SetActive(true);

        isUpdatingUI = true;
        if (brightnessSlider != null) brightnessSlider.SetValueWithoutNotify(GetCurrentBrightness());
        SaveHueToMemory(prefix);
        isUpdatingUI = false;
        
        UpdateUIVisibility();
    }

    public void OnColorPicked(Color col)
    {
        if (isUpdatingUI) return;
        float h, s, v;
        Color.RGBToHSV(col, out h, out s, out _);
        savedHues[activeColorPrefix] = Color.HSVToRGB(h, s, 1.0f);
        ApplyColorToTarget();
    }

    public void OnBrightnessChanged(float val)
    {
        if (isUpdatingUI) return;
        ApplyColorToTarget();
    }

    private void ApplyColorToTarget()
    {
        if (!savedHues.ContainsKey(activeColorPrefix)) return;
        float b = (brightnessSlider != null) ? brightnessSlider.value : 1.0f; 
        Color baseHue = savedHues[activeColorPrefix]; 

        float h, s, v;
        Color.RGBToHSV(baseHue, out h, out s, out _);
        Color finalColor = Color.HSVToRGB(h, s, b);

        UpdateColorValue(activeColorPrefix + "R", finalColor.r);
        UpdateColorValue(activeColorPrefix + "G", finalColor.g);
        UpdateColorValue(activeColorPrefix + "B", finalColor.b);
        UpdateKoi();
        
        UpdateUIVisibility();
    }

    public void CloseColorPicker()
    {
        if (pickerUIGroup != null) pickerUIGroup.SetActive(false);
    }

    private void UpdateColorValue(string propName, float value)
    {
        if (currentValues.ContainsKey(propName))
        {
            currentValues[propName] = value;
            var row = rows.Find(r => r.propertyName == propName);
            if (row != null) row.SetValueQuietly(value);
        }
    }

    public void SetModeSingle() => ChangeMode(KoiMode.Single);
    public void SetModeDual() => ChangeMode(KoiMode.Dual);
    public void SetModeTriple() => ChangeMode(KoiMode.Triple);

    private void ChangeMode(KoiMode mode)
    {
        currentMode = mode;
        if (btnSingleImage) btnSingleImage.color = (mode == KoiMode.Single) ? activeColor : inactiveColor;
        if (btnDualImage)   btnDualImage.color   = (mode == KoiMode.Dual)   ? activeColor : inactiveColor;
        if (btnTripleImage) btnTripleImage.color = (mode == KoiMode.Triple) ? activeColor : inactiveColor;

        if (mode == KoiMode.Single && activeColorPrefix != "_MainColor") SetPickerTarget("_MainColor");
        
        UpdateUIVisibility(); 
        UpdateKoi();
    }

    void InitializeDefaultValues()
    {
        currentValues["_MainColorR"] = 1.0f; currentValues["_MainColorG"] = 1.0f; currentValues["_MainColorB"] = 1.0f;
        currentValues["_SubColor1R"] = 0.8f; currentValues["_SubColor1G"] = 0.1f; currentValues["_SubColor1B"] = 0.1f;
        currentValues["_Sub1Amount"] = 0.5f; currentValues["_Sub1Scale"] = 2.0f;
        currentValues["_SubColor2R"] = 0.1f; currentValues["_SubColor2G"] = 0.1f; currentValues["_SubColor2B"] = 0.1f;
        currentValues["_Sub2Amount"] = 0.4f; currentValues["_Sub2Scale"] = 3.0f;
        currentValues["_SeedX"] = Random.Range(0f, 100f); currentValues["_SeedY"] = Random.Range(0f, 100f); currentValues["_SeedZ"] = Random.Range(0f, 100f);
    }

    void GenerateUI()
    {
        foreach (var s in settings)
        {
            GameObject go = Instantiate(sliderRowPrefab, contentRoot);
            var row = go.GetComponent<EditorSliderRow>();
            float initVal = currentValues.ContainsKey(s.propName) ? currentValues[s.propName] : 0.5f; 
            row.Setup(s.label, s.propName, s.min, s.max, initVal, (p, v) => { currentValues[p] = v; UpdateKoi(); UpdateUIVisibility(); });
            rows.Add(row);
        }

        CreatePickerButton("メイン", "_MainColor");
        CreatePickerButton("サブ1", "_SubColor1");
        CreatePickerButton("サブ2", "_SubColor2");

        UpdateUIVisibility(); 
    }

    void CreatePickerButton(string label, string prefix)
    {
        if (colorButtonPrefab == null || colorButtonRoot == null) return;
        GameObject go = Instantiate(colorButtonPrefab, colorButtonRoot);
        Button btn = go.GetComponentInChildren<Button>();
        Text txt = go.GetComponentInChildren<Text>();
        if (txt != null) txt.text = label;
        btn.onClick.AddListener(() => SetPickerTarget(prefix));
        
        go.name = prefix; 
        pickerButtons.Add(go);
    }

    public void ToggleSeedVisible(bool isOn) 
    { 
        UpdateUIVisibility(); 
    }
    void UpdateUIVisibility()
    {
        bool isAdvanced = (seedToggle != null && seedToggle.isOn);

        // 1. スライダーの表示管理
        foreach (var row in rows)
        {
            bool shouldShow = true;
            string p = row.propertyName;

            // ① 詳細設定の判定：ColorかSeedが含まれるものは、詳細設定OFFなら隠す
            if (p.Contains("Color") || p.Contains("Seed")) 
            { 
                if (!isAdvanced) shouldShow = false; 
            }

            // ② サブ1の判定（修正！）：「Sub1」または「SubColor1」が含まれるものは、単色モードなら絶対に隠す！
            if (p.Contains("Sub1") || p.Contains("SubColor1")) 
            { 
                if (currentMode == KoiMode.Single) shouldShow = false; 
            }

            // ③ サブ2の判定（修正！）：「Sub2」または「SubColor2」が含まれるものは、3色モード以外なら絶対に隠す！
            if (p.Contains("Sub2") || p.Contains("SubColor2")) 
            { 
                if (currentMode != KoiMode.Triple) shouldShow = false; 
            }

            row.gameObject.SetActive(shouldShow);
        }

        // 2. ピッカーボタンの表示管理と、ボタン「自体」の色変更
        foreach (var go in pickerButtons)
        {
            bool shouldShow = true;
            string prefix = go.name;

            if (prefix.Contains("SubColor1") && currentMode == KoiMode.Single) shouldShow = false;
            if (prefix.Contains("SubColor2") && currentMode != KoiMode.Triple) shouldShow = false;
            
            go.SetActive(shouldShow);

            if (shouldShow)
            {
                // ★プレハブの中にある「Button」を探して、その色を現在の色にする！
                Button btn = go.GetComponentInChildren<Button>();
                if (btn != null)
                {
                    Image img = btn.GetComponent<Image>();
                    if (img != null)
                    {
                        float r = GetValue(prefix + "R");
                        float g = GetValue(prefix + "G");
                        float b = GetValue(prefix + "B");
                        img.color = new Color(r, g, b, 1.0f);
                    }
                }
            }
        }
    }

    void UpdateKoi()
    {
        if (targetKoi == null) return;
        Renderer[] renderers = targetKoi.GetComponentsInChildren<Renderer>();
        float s1Amount = (currentMode == KoiMode.Single) ? 0 : GetValue("_Sub1Amount");
        float s2Amount = (currentMode == KoiMode.Triple) ? GetValue("_Sub2Amount") : 0;
        foreach (var r in renderers) {
            MaterialPropertyBlock mpb = new MaterialPropertyBlock();
            r.GetPropertyBlock(mpb);
            mpb.SetColor("_MainColor", new Color(GetValue("_MainColorR"), GetValue("_MainColorG"), GetValue("_MainColorB"), 1));
            mpb.SetColor("_SubColor1", new Color(GetValue("_SubColor1R"), GetValue("_SubColor1G"), GetValue("_SubColor1B"), 1));
            mpb.SetColor("_SubColor2", new Color(GetValue("_SubColor2R"), GetValue("_SubColor2G"), GetValue("_SubColor2B"), 1));
            mpb.SetFloat("_Sub1Detail", GetValue("_Sub1Detail"));
            mpb.SetFloat("_Sub2Detail", GetValue("_Sub2Detail"));
            mpb.SetFloat("_Sub1Amount", s1Amount);
            mpb.SetFloat("_Sub1Scale", GetValue("_Sub1Scale"));
            mpb.SetFloat("_Sub2Amount", s2Amount);
            mpb.SetFloat("_Sub2Scale", GetValue("_Sub2Scale"));
            mpb.SetVector("_Seed", new Vector4(GetValue("_SeedX"), GetValue("_SeedY"), GetValue("_SeedZ"), 0));
            r.SetPropertyBlock(mpb);
        }
    }

    public void RandomizeSeedOnly()
    {
        currentValues["_SeedX"] = Random.Range(0f, 100f);
        currentValues["_SeedY"] = Random.Range(0f, 100f);
        currentValues["_SeedZ"] = Random.Range(0f, 100f);
        foreach (var row in rows) { if (row.propertyName.Contains("Seed")) row.SetValueQuietly(currentValues[row.propertyName]); }
        UpdateKoi();
        UpdateUIVisibility();
    }

    public void RandomizeAll()
    {
        foreach (var row in rows) {
            float rand = Random.Range(row.slider.minValue, row.slider.maxValue);
            currentValues[row.propertyName] = rand;
            row.SetValueQuietly(rand);
        }
        UpdateKoi();
        UpdateUIVisibility();
    }

    // KoiEditorManager.cs に追加
    public void LoadAssetForEdit(KoiPatternData data)
    {
        if (data == null) return;

        currentEditingAsset = data;
        baseData = data; // 今いじっているアセットを記憶
        fileNameInput.text = data.name; // 名前をInputFieldに入れる

        // --- 辞書(currentValues)の中身をアセットの数値で上書きする ---
        currentValues["_MainColorR"] = data.mainColor.r;
        currentValues["_MainColorG"] = data.mainColor.g;
        currentValues["_MainColorB"] = data.mainColor.b;
        
        currentValues["_SubColor1R"] = data.sub1Color.r;
        currentValues["_SubColor1G"] = data.sub1Color.g;
        currentValues["_SubColor1B"] = data.sub1Color.b;
        currentValues["_Sub1Amount"] = data.sub1Amount;
        currentValues["_Sub1Scale"]  = data.sub1Scale;
        currentValues["_Sub1Detail"] = data.sub1Detail;

        currentValues["_SubColor2R"] = data.sub2Color.r;
        currentValues["_SubColor2G"] = data.sub2Color.g;
        currentValues["_SubColor2B"] = data.sub2Color.b;
        currentValues["_Sub2Amount"] = data.sub2Amount;
        currentValues["_Sub2Scale"]  = data.sub2Scale;
        currentValues["_Sub2Detail"] = data.sub2Detail;

        currentValues["_SeedX"] = data.patternSeed.x;
        currentValues["_SeedY"] = data.patternSeed.y;
        currentValues["_SeedZ"] = data.patternSeed.z;

        // --- UI（スライダー）のつまみを、書き換えた数値の位置に動かす ---
        foreach (var row in rows)
        {
            if (currentValues.ContainsKey(row.propertyName))
            {
                // UI側に反映（スライダーのイベントを飛ばさずに値をセット）
                row.SetValueQuietly(currentValues[row.propertyName]);
            }
        }

        // モードの自動判定（サブ2があれば3色、サブ1だけなら2色、なければ単色）
        if (data.sub2Amount > 0.01f) SetModeTriple();
        else if (data.sub1Amount > 0.01f) SetModeDual();
        else SetModeSingle();

        UpdateKoi(); // 3Dモデルに反映
        UpdateUIVisibility(); // UIの表示/非表示を更新
    }

    // --- クラスの冒頭（変数宣言のところ）に追加 ---
    private KoiPatternData currentEditingAsset = null; 

    // --- クラスの中（関数が並んでいるところ）に追加 ---
    public void ResetToNew()
    {
        // 1. 編集中のアセット情報をクリア（これで新規作成モードになる）
        currentEditingAsset = null;

        // 2. 入力欄を空にする
        if (fileNameInput != null) fileNameInput.text = "";

        // 3. パラメータを初期値に戻す（既存の関数を流用）
        InitializeDefaultValues();

        // 4. 見た目に反映
        UpdateKoi();
        UpdateUIVisibility();
        
        Debug.Log("<color=orange>【Editor】</color> 新規作成モードでリセットしました");
    }

    public void SaveAsAsset()
    {
#if UNITY_EDITOR
        bool isOverwrite = (currentEditingAsset != null);
        KoiPatternData dataToSave = isOverwrite ? currentEditingAsset : ScriptableObject.CreateInstance<KoiPatternData>();

        // 1. パラメータを代入（中身の更新）
        dataToSave.mainColor = new Color(GetValue("_MainColorR"), GetValue("_MainColorG"), GetValue("_MainColorB"), 1);
        float s1Amount = (currentMode == KoiMode.Single) ? 0 : GetValue("_Sub1Amount");
        float s2Amount = (currentMode == KoiMode.Triple) ? GetValue("_Sub2Amount") : 0;
        
        dataToSave.sub1Color = new Color(GetValue("_SubColor1R"), GetValue("_SubColor1G"), GetValue("_SubColor1B"), 1);
        dataToSave.sub1Amount = s1Amount; 
        dataToSave.sub1Scale = GetValue("_Sub1Scale");
        dataToSave.sub1Detail = GetValue("_Sub1Detail");

        dataToSave.sub2Color = new Color(GetValue("_SubColor2R"), GetValue("_SubColor2G"), GetValue("_SubColor2B"), 1);
        dataToSave.sub2Amount = s2Amount; 
        dataToSave.sub2Scale = GetValue("_Sub2Scale");
        dataToSave.sub2Detail = GetValue("_Sub2Detail");

        dataToSave.patternSeed = new Vector3(GetValue("_SeedX"), GetValue("_SeedY"), GetValue("_SeedZ"));

        // 2. 撮影を依頼し、終わってから「FinalizeSave」を実行する
        if (KoiPhotoStudio.Instance != null)
        {
            // 撮影が終わった瞬間に実行される処理（ラムダ式）を渡す
            KoiPhotoStudio.Instance.RequestCapture(dataToSave, () => {
                FinalizeSave(dataToSave, isOverwrite);
            });
        }
#endif
    }

    private void FinalizeSave(KoiPatternData dataToSave, bool isOverwrite)
    {
#if UNITY_EDITOR
        string baseName = fileNameInput.text;
        // 名前が空ならデフォルト名。上書きなら現在のアセット名。
        if (string.IsNullOrEmpty(baseName)) baseName = isOverwrite ? currentEditingAsset.name : "NewKoi";
        
        string folderPath = "Assets/KoiData";
        string path = "";

        if (!isOverwrite) 
        {
            // 【新規作成モード】名前が被っていたら番号を振る
            string finalName = baseName;
            int counter = 1;
            while (File.Exists($"{folderPath}/{finalName}.asset"))
            {
                finalName = $"{baseName} {counter}";
                counter++;
            }
            dataToSave.name = finalName; // 重複を避けた新しい名前
            path = $"{folderPath}/{finalName}.asset";
            UnityEditor.AssetDatabase.CreateAsset(dataToSave, path);
        } 
        else 
        {
            // 【編集（上書き）モード】現在のパスを維持しつつ、名前が変わっていればリネーム
            path = UnityEditor.AssetDatabase.GetAssetPath(dataToSave);
            
            if (dataToSave.name != baseName) 
            {
                // 注意：編集画面でわざと「別の既存ファイル名」に変えた場合はリネームが失敗しますが、
                // 基本的には今のファイルを書き換えるので番号は振りません。
                UnityEditor.AssetDatabase.RenameAsset(path, baseName);
                dataToSave.name = baseName;
                path = UnityEditor.AssetDatabase.GetAssetPath(dataToSave); // リネーム後のパスを取得
            }
        }

        // --- 以下、写真の埋め込み処理（変更なし） ---
        if (!string.IsNullOrEmpty(path)) 
        {
            Object[] subAssets = UnityEditor.AssetDatabase.LoadAllAssetsAtPath(path);
            foreach (var sub in subAssets) {
                if (sub is Texture2D && sub != dataToSave.photoTexture) {
                    UnityEditor.AssetDatabase.RemoveObjectFromAsset(sub);
                    DestroyImmediate(sub, true);
                }
            }
            
            if (dataToSave.photoTexture != null) {
                dataToSave.photoTexture.name = "Photo_" + dataToSave.name;
                if (!UnityEditor.AssetDatabase.IsSubAsset(dataToSave.photoTexture)) {
                    UnityEditor.AssetDatabase.AddObjectToAsset(dataToSave.photoTexture, dataToSave);
                }
            }
            
            UnityEditor.EditorUtility.SetDirty(dataToSave);
            UnityEditor.AssetDatabase.SaveAssets();
            UnityEditor.AssetDatabase.ImportAsset(path, UnityEditor.ImportAssetOptions.ForceUpdate);
        }
        
        // 保存が終わったら、今のデータを「編集中」としてセット（連続上書きを可能にする）
        currentEditingAsset = dataToSave;

        Debug.Log($"<color=cyan>【保存完了】</color> {(isOverwrite ? "上書き" : "新規")}保存しました：{dataToSave.name}");
#endif
    }

    void AddSetting(string l, string p, float min, float max) => settings.Add(new SliderSetting { label = l, propName = p, min = min, max = max });
    float GetValue(string key) => currentValues.ContainsKey(key) ? currentValues[key] : 0;
}