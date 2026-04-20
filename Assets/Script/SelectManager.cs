using UnityEngine;

public class SelectManager : MonoBehaviour
{
    [Header("--- 交配 ---")]
    public GameObject crossPanel;
    public GameObject selectPanel;

    [Header("--- 詳細作成 ---")]

    public GameObject edeitorPanel;
    public GameObject rotationArea;
    public GameObject rotationResetButton;
    public GameObject nishikiModel;

    void Start()
    {
        OnClickCrossButton();
    }
    public void OnClickCrossButton()
    {
        crossPanel.SetActive(true);
        //selectPanel.SetActive(true);
        edeitorPanel.SetActive(false);
        rotationArea.SetActive(false);
        rotationResetButton.SetActive(false);
        if (nishikiModel != null) nishikiModel.SetActive(false);
    }

    public void OnClickEditorButton()
    {
        crossPanel.SetActive(false);
        selectPanel.SetActive(false);
        edeitorPanel.SetActive(true);
        rotationArea.SetActive(true);
        rotationResetButton.SetActive(true);
        if (nishikiModel != null) nishikiModel.SetActive(true);
    }

}
