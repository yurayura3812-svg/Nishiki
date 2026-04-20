using UnityEngine;

public class SelectManager : MonoBehaviour
{
    [Header("--- 各パネルの参照 ---")]
    public GameObject crossPanel;         // 交配画面
    public GameObject selectPanel;        // 錦鯉選択リストパネル
    public GameObject editorPanel;        // 詳細作成（スライダー）画面

    [Header("--- 演出・モデル用 ---")]
    public GameObject rotationArea;       // 3D回転用エリア
    public GameObject rotationResetButton;
    public GameObject nishikiModel;       // 撮影用またはプレビュー用の鯉モデル

    [Header("--- 他のマネージャーへの参照 ---")]
    public BreedingUIManager breedingManager; // 交配の進行管理
    public KoiEditorManager editorManager;     // 編集・作成の管理

    void Start()
    {
        // 起動時は「交配画面」をデフォルトにする（お好みで変えてください）
        OnClickCrossButton();
    }

    // ==========================================
    // 1. 【交配】ボタン（中央のボタン）
    // ==========================================
    public void OnClickCrossButton()
    {
        SetAllPanelsInactive();

        crossPanel.SetActive(true);
        // 交配画面では3D回転などは不要ならOFF
        if (nishikiModel != null) nishikiModel.SetActive(false);
    }

    // ==========================================
    // 2. 【詳細作成】ボタン（左のボタン）
    // ==========================================
    public void OnClickNewCreateButton()
    {
        SetAllPanelsInactive();

        editorPanel.SetActive(true);
        rotationArea.SetActive(true);
        rotationResetButton.SetActive(true);
        if (nishikiModel != null) nishikiModel.SetActive(true);

        // ★重要：エディターを「新規作成モード」としてリセットする
        if (editorManager != null)
        {
            editorManager.ResetToNew();
        }
    }

    // ==========================================
    // 3. 【編集】ボタン（右のボタン）
    // ==========================================
    public void OnClickEditModeButton()
    {
        SetAllPanelsInactive();

        // まず「どの鯉を直すか」を選ぶためにリストを開く
        selectPanel.SetActive(true);

        // ★重要：リストを「編集モード(Target 0)」として開くように依頼する
        if (breedingManager != null)
        {
            breedingManager.OpenForEditMode();
        }
    }

    // ==========================================
    // 共通：パネルを一旦全部閉じる
    // ==========================================
    private void SetAllPanelsInactive()
    {
        crossPanel.SetActive(false);
        selectPanel.SetActive(false);
        editorPanel.SetActive(false);
        rotationArea.SetActive(false);
        rotationResetButton.SetActive(false);
    }

    // BreedingUIManagerから「編集対象が決まったからエディタを開いて！」
    // と言われた時に呼び出すための関数です。
    public void OnClickEditorButton()
    {
        // 全パネルを一旦閉じる
        crossPanel.SetActive(false);
        selectPanel.SetActive(false);
        editorPanel.SetActive(false);
        rotationArea.SetActive(false);
        rotationResetButton.SetActive(false);

        // エディタ（詳細作成）パネルを表示
        editorPanel.SetActive(true);
        rotationArea.SetActive(true);
        rotationResetButton.SetActive(true);
        if (nishikiModel != null) nishikiModel.SetActive(true);
    }
}