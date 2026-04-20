using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using System.IO;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class BreedingUIManager : MonoBehaviour
{
    [Header("--- パネルの参照 ---")]
    public GameObject crossPanel;
    public GameObject selectPanel;

    [Header("--- CrossPanel (メイン画面) の参照 ---")]
    public Button parent1Button; 
    public Button parent2Button; 
    public Button crossButton;   
    public InputField nameInput; 
    public Button saveButton;    

    [Header("--- UIテキスト ---")]
    // 中央の「選択してください」の文字（GameObjectとしてON/OFFする）
    public GameObject parent1Placeholder; 
    public GameObject parent2Placeholder;
    public GameObject childPlaceholder;

    // 画像の下に出す「鯉の名前」の文字
    public Text parent1NameText; 
    public Text parent2NameText; 
    public Text childNameText; 
    
    [Header("--- 撮影スタジオの鯉モデル ---")]
    public KoiController parent1KoiModel; 
    public KoiController parent2KoiModel; 
    public KoiController childKoiModel;

    [Header("--- 誕生の演出エフェクト ---")]
    public ParticleSystem birthEffect; 

    [Header("--- SelectPanel (ポップアップ) の参照 ---")]
    public Text selectMessage;   
    public Button closeButton;   
    public Button selectConfirmButton; 
    public Transform scrollViewContent; 
    public GameObject koiListButtonPrefab; 

    [Header("--- プレビュー画像(RawImage) ---")]
    public RawImage parent1RawImage;
    public RawImage parent2RawImage;
    public RawImage childRawImage;

    [Header("--- 使用するレンダーテクスチャ ---")]
    public RenderTexture rtParent1;
    public RenderTexture rtParent2;
    public RenderTexture rtChild;

    [Header("--- 外部マネージャーの参照 ---")]
    public KoiEditorManager editorManager;

    // --- 内部状態の管理 ---
    private int currentSelectingTarget = 1; 
    private KoiPatternData tempSelectedData; 
    
    public KoiPatternData parent1Data { get; private set; }
    public KoiPatternData parent2Data { get; private set; }
    public KoiPatternData childData { get; private set; }

    void Start()
    {
        parent1Button.onClick.AddListener(() => OpenSelectPanel(1));
        parent2Button.onClick.AddListener(() => OpenSelectPanel(2));
        closeButton.onClick.AddListener(CloseSelectPanel);
        selectConfirmButton.onClick.AddListener(ConfirmSelection);
        crossButton.onClick.AddListener(ExecuteBreeding);
        saveButton.onClick.AddListener(SaveChildKoi);

        ResetUI();
        selectPanel.SetActive(false);
    }

    // ==========================================
    // UIの初期化（リセット）とロック機能
    // ==========================================
    void ResetUI()
    {
        parent1Data = null;
        parent2Data = null;
        childData = null;

        crossButton.interactable = false;
        saveButton.interactable = false;

        // ★中央の文字を出し、下の名前を空っぽにする
        if (parent1Placeholder != null) parent1Placeholder.SetActive(true);
        if (parent2Placeholder != null) parent2Placeholder.SetActive(true);
        if (childPlaceholder != null) childPlaceholder.SetActive(true);

        if (parent1NameText != null) parent1NameText.text = "";
        if (parent2NameText != null) parent2NameText.text = "";
        if (childNameText != null) childNameText.text = "";

        nameInput.text = "";

        // ★画像スロットを空（グレー）にする
        Color emptyColor = new Color(0.7f, 0.7f, 0.7f, 1f);
        if (parent1RawImage != null) { parent1RawImage.texture = null; parent1RawImage.color = emptyColor; }
        if (parent2RawImage != null) { parent2RawImage.texture = null; parent2RawImage.color = emptyColor; }
        if (childRawImage != null) { childRawImage.texture = null; childRawImage.color = emptyColor; }

        // ★地下の3Dモデルをお休みさせる（非表示）
        if (parent1KoiModel != null) parent1KoiModel.gameObject.SetActive(false);
        if (parent2KoiModel != null) parent2KoiModel.gameObject.SetActive(false);
        if (childKoiModel != null) childKoiModel.gameObject.SetActive(false);
    }

    // ==========================================
    // UIの開閉と選択フロー
    // ==========================================
    void OpenSelectPanel(int target)
    {
        currentSelectingTarget = target;
        selectMessage.text = $"親{target} を選択してください。";
        tempSelectedData = null; 
        selectConfirmButton.interactable = false; 

        foreach (Transform child in scrollViewContent) Destroy(child.gameObject);

#if UNITY_EDITOR
        string folderPath = "Assets/KoiData";
        if (Directory.Exists(folderPath))
        {
            string[] guids = AssetDatabase.FindAssets("t:KoiPatternData", new[] { folderPath });
            foreach (string guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                KoiPatternData data = AssetDatabase.LoadAssetAtPath<KoiPatternData>(path);
                
                if (data != null)
                {
                    GameObject btnObj = Instantiate(koiListButtonPrefab, scrollViewContent);
                    KoiListButton koiBtn = btnObj.GetComponent<KoiListButton>();
                    if (koiBtn != null) koiBtn.Setup(data, this); 
                }
            }
        }
#endif
        selectPanel.SetActive(true);
    }

    void CloseSelectPanel() { selectPanel.SetActive(false); }

    public void OnClickKoiListItem(KoiPatternData data)
    {
        tempSelectedData = data;
        selectConfirmButton.interactable = true; 
    }

    void ConfirmSelection()
    {
        if (tempSelectedData == null) return;

        // ★追加：編集モード（ターゲットが0）の処理
        if (currentSelectingTarget == 0)
        {
            // エディターマネージャーにデータをロードさせる
            if (editorManager != null)
            {
                editorManager.LoadAssetForEdit(tempSelectedData);
            }

            // UIの切り替え（SelectManagerの関数を呼ぶのが一番確実です）
            SelectManager sm = FindObjectOfType<SelectManager>();
            if (sm != null) sm.OnClickEditorButton();

            CloseSelectPanel();
            return; // 編集モードならここで終了
        }

        // --- 以下、既存の交配用ロジック（親1・親2の選択） ---
        if (currentSelectingTarget == 1)
        {
            parent1Data = tempSelectedData;
            if (parent1Placeholder != null) parent1Placeholder.SetActive(false);
            if (parent1NameText != null) parent1NameText.text = parent1Data.name;

            if (parent1KoiModel != null)
            {
                parent1KoiModel.gameObject.SetActive(true);
                parent1KoiModel.patternData = parent1Data;
                parent1KoiModel.ApplyDNA(); 
            }

            if (parent1RawImage != null)
            {
                parent1RawImage.texture = rtParent1;
                parent1RawImage.color = Color.white;
            }
        }
        else if (currentSelectingTarget == 2)
        {
            parent2Data = tempSelectedData;
            if (parent2Placeholder != null) parent2Placeholder.SetActive(false);
            if (parent2NameText != null) parent2NameText.text = parent2Data.name;

            if (parent2KoiModel != null)
            {
                parent2KoiModel.gameObject.SetActive(true);
                parent2KoiModel.patternData = parent2Data;
                parent2KoiModel.ApplyDNA();
            }

            if (parent2RawImage != null)
            {
                parent2RawImage.texture = rtParent2;
                parent2RawImage.color = Color.white;
            }
        }

        if (parent1Data != null && parent2Data != null) crossButton.interactable = true;
        CloseSelectPanel();
    }

    // ==========================================
    // 交配と保存
    // ==========================================
    void ExecuteBreeding()
    {
        if (parent1Data == null || parent2Data == null) return;

        childData = ScriptableObject.CreateInstance<KoiPatternData>();

        childData.mainColor = (Random.value > 0.5f) ? parent1Data.mainColor : parent2Data.mainColor;
        childData.sub1Color = (Random.value > 0.5f) ? parent1Data.sub1Color : parent2Data.sub1Color;
        childData.sub2Color = (Random.value > 0.5f) ? parent1Data.sub2Color : parent2Data.sub2Color;

        Vector3 inheritedSeed = (Random.value > 0.5f) ? parent1Data.patternSeed : parent2Data.patternSeed;
        childData.patternSeed = inheritedSeed + new Vector3(Random.Range(-5f, 5f), Random.Range(-5f, 5f), 0);

        float blendRate = Random.Range(0.3f, 0.7f); 
        childData.sub1Amount = Mathf.Lerp(parent1Data.sub1Amount, parent2Data.sub1Amount, blendRate);
        childData.sub1Scale  = Mathf.Lerp(parent1Data.sub1Scale,  parent2Data.sub1Scale,  blendRate);
        childData.sub1Detail = Mathf.Lerp(parent1Data.sub1Detail, parent2Data.sub1Detail, blendRate);

        childData.sub2Amount = Mathf.Lerp(parent1Data.sub2Amount, parent2Data.sub2Amount, blendRate);
        childData.sub2Scale  = Mathf.Lerp(parent1Data.sub2Scale,  parent2Data.sub2Scale,  blendRate);
        childData.sub2Detail = Mathf.Lerp(parent1Data.sub2Detail, parent2Data.sub2Detail, blendRate);
        
        childData.bellyLimit = Mathf.Lerp(parent1Data.bellyLimit, parent2Data.bellyLimit, 0.5f);

        if (Random.value < 0.15f)
        {
            childData.sub1Amount = 0f;
            childData.sub2Amount = 0f;
        }

        // ★子供のUI文字を切り替え（名前は空欄）
        if (childPlaceholder != null) childPlaceholder.SetActive(false);
        if (childNameText != null) childNameText.text = "";

        // モデルの起動と適用
        if (childKoiModel != null)
        {
            childKoiModel.gameObject.SetActive(true);
            childKoiModel.patternData = childData;
            childKoiModel.ApplyDNA();
        }

        // テクスチャの反映
        if (childRawImage != null)
        {
            childRawImage.texture = rtChild;
            childRawImage.color = Color.white;
        }

        if (birthEffect != null) birthEffect.Play();
        
        saveButton.interactable = true;
    }

    // --- SelectManagerから呼ばれる「編集モード開始」の窓口を追加 ---
    public void OpenForEditMode()
    {
        // ターゲット0番を「編集用」としてリストを開く
        OpenSelectPanel(0);
        if (selectMessage != null) selectMessage.text = "編集する錦鯉を選択してください。";
    }

    void SaveChildKoi()
    {
        if (childData == null) return;

        // 1. 入力された基本名を取得
        string baseName = nameInput.text;
        if (string.IsNullOrEmpty(baseName)) baseName = "名無しの錦鯉";

#if UNITY_EDITOR
        string folderPath = "Assets/KoiData";
        
        // フォルダがない場合は作成
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
        }

        // 2. ★重複チェックのロジック
        string finalName = baseName;
        int counter = 1;
        
        // すでに「名前.asset」が存在するかチェックし、あれば「名前 1.asset」にする
        while (File.Exists($"{folderPath}/{finalName}.asset"))
        {
            finalName = $"{baseName} {counter}";
            counter++;
        }

        // 3. データに確定した名前を書き込む
        childData.name = finalName; // ScriptableObject内部の名前変数も更新

        // 4. 保存実行
        string path = $"{folderPath}/{finalName}.asset";
        AssetDatabase.CreateAsset(childData, path);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        Debug.Log($"<color=cyan>新種誕生！</color> {finalName} を保存しました！");
#endif
        ResetUI();
    }
}