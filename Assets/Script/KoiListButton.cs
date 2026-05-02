using UnityEngine;
using UnityEngine.UI;
using System.Collections;
public class KoiListButton : MonoBehaviour
{
    [Header("UI参照")]
    public Text nameText; // 鯉の名前を表示するテキスト
    public Button button; // このボタン自体のコンポーネント
    public RawImage previewImage; // 鯉の写真を表示するRawImage

    private KoiPatternData myData;
    private BreedingUIManager manager;

    /// <summary>
    /// リスト生成時にマネージャーから呼ばれて、自分のデータをセットする関数
    /// </summary>
public void Setup(KoiPatternData data, BreedingUIManager m)
{
    myData = data;
    manager = m;
    nameText.text = data.name;

    if (data.photoTexture != null)
    {
        previewImage.texture = data.photoTexture;
    }
    else
    {
        // ★スタジオに丸投げする
        if (KoiPhotoStudio.Instance != null)
        {
            KoiPhotoStudio.Instance.RequestCapture(data, previewImage);
        }
    }
}
// これがないとマネージャーに通知がいきません
    public void OnClick()
    {
        manager.OnClickKoiListItem(myData);
    }

}