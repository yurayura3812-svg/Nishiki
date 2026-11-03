using UnityEngine;
using UnityEngine.UI;

public class SelectManager : MonoBehaviour
{
    [Header("--- 各パネルの参照 ---")]
    public GameObject crossPanel;
    public GameObject selectPanel;
    public GameObject editorPanel;
    public GameObject listPanel;

    [Header("--- メニューボタン本体 (押し分け用) ---")]
    public Button btnCreate; // Buttonコンポーネントをアタッチ
    public Button btnCross;  
    public Button btnEdit;   

    [Header("--- タブの色設定 ---")]
    public Color activeTabColor = Color.cyan;
    public Color inactiveTabColor = Color.white;

    [Header("--- 演出・モデル用 ---")]
    public GameObject rotationArea;
    public GameObject rotationResetButton;
    public GameObject nishikiModel;

    [Header("--- 他のマネージャーへの参照 ---")]
    public BreedingUIManager breedingManager;
    public KoiEditorManager editorManager;

    void Start()
    {
        if (listPanel != null) ShowList();
        else OnClickCrossButton();
    }

    public void ShowList()
    {
        SetAllPanelsInactive();
        if (listPanel != null) listPanel.SetActive(true);
        if (nishikiModel != null) nishikiModel.SetActive(false);
        if (breedingManager != null) breedingManager.SetStudioModelsVisible(false);
    }

    // ==========================================
    // 共通処理：ボタンの状態（色と有効化）を更新
    // ==========================================
    private void UpdateButtonStates(Button activeButton)
    {
        // 全ボタンを一旦「白」にして「有効（押せる）」にする
        ResetButton(btnCreate);
        ResetButton(btnCross);
        ResetButton(btnEdit);

        // 選択されたボタンだけ「指定色」にして「無効（押せない）」にする
        if (activeButton != null)
        {
            activeButton.interactable = false;
            // ButtonのTargetGraphic（通常はImage）の色を変える
            if (activeButton.targetGraphic != null)
            {
                activeButton.targetGraphic.color = activeTabColor;
            }
        }
    }

    private void ResetButton(Button btn)
    {
        if (btn == null) return;
        btn.interactable = true;
        if (btn.targetGraphic != null)
        {
            btn.targetGraphic.color = inactiveTabColor;
        }
    }

    // ==========================================
    // 各ボタンのクリックイベント
    // ==========================================
    public void OnClickCrossButton()
    {
        SetAllPanelsInactive();
        UpdateButtonStates(btnCross); // 交配ボタンを無効化

        if (crossPanel != null) crossPanel.SetActive(true);
        if (nishikiModel != null) nishikiModel.SetActive(false);
        if (breedingManager != null) breedingManager.SetStudioModelsVisible(true);
    }

    public void OnClickNewCreateButton()
    {
        SetAllPanelsInactive();
        UpdateButtonStates(btnCreate); // 詳細作成ボタンを無効化

        if (editorPanel != null) editorPanel.SetActive(true);
        if (rotationArea != null) rotationArea.SetActive(true);
        if (rotationResetButton != null) rotationResetButton.SetActive(true);
        if (nishikiModel != null) nishikiModel.SetActive(true);

        if (editorManager != null) editorManager.ResetToNew();
    }

    public void OnClickEditModeButton()
    {
        SetAllPanelsInactive();
        UpdateButtonStates(btnEdit); // 編集ボタンを無効化

        if (selectPanel != null) selectPanel.SetActive(true);
        if (breedingManager != null) breedingManager.OpenForEditMode();
    }

    private void SetAllPanelsInactive()
    {
        if (crossPanel != null) crossPanel.SetActive(false);
        if (selectPanel != null) selectPanel.SetActive(false);
        if (editorPanel != null) editorPanel.SetActive(false);
        if (listPanel != null) listPanel.SetActive(false);
        if (rotationArea != null) rotationArea.SetActive(false);
        if (rotationResetButton != null) rotationResetButton.SetActive(false);
        if (nishikiModel != null) nishikiModel.SetActive(false);
    }

    public void OnClickEditorButton()
    {
        SetAllPanelsInactive();
        // 編集対象を選んだ後も、カテゴリとしては「編集」なのでEditボタンを無効化
        UpdateButtonStates(btnEdit); 

        editorPanel.SetActive(true);
        rotationArea.SetActive(true);
        rotationResetButton.SetActive(true);
        if (nishikiModel != null) nishikiModel.SetActive(true);
    }
}