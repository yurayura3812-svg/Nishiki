using UnityEngine;
using UnityEngine.UI;

public class KoiListButton : MonoBehaviour
{
    [Header("UI参照")]
    public Text nameText; // 鯉の名前を表示するテキスト
    public Button button; // このボタン自体のコンポーネント

    private KoiPatternData myData;
    private BreedingUIManager manager;

    /// <summary>
    /// リスト生成時にマネージャーから呼ばれて、自分のデータをセットする関数
    /// </summary>
    public void Setup(KoiPatternData data, BreedingUIManager mgr)
    {
        myData = data;
        manager = mgr;

        // アセットのファイル名（または設定された名前）を表示
        nameText.text = data.name; 

        // クリックされたら、マネージャーに「自分が選ばれたよ！」と伝える
        button.onClick.RemoveAllListeners(); // 重複登録防止
        button.onClick.AddListener(() => manager.OnClickKoiListItem(myData));
    }
}