using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

public class KoiColorPicker : MonoBehaviour, IPointerClickHandler, IDragHandler
{
    public RawImage colorWheel; // 虹色を表示する画像枠
    public System.Action<Color> OnColorSelected; // 色が決まった時の通知

    void Start() => CreateWheel();

    // 虹色の円形テクスチャをプログラムで生成
    void CreateWheel()
    {
        int size = 128;
        Texture2D tex = new Texture2D(size, size);
        for (int y = 0; y < size; y++) {
            for (int x = 0; x < size; x++) {
                float dx = (x - size / 2f) / (size / 2f);
                float dy = (y - size / 2f) / (size / 2f);
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                if (dist <= 1f) {
                    float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
                    if (angle < 0) angle += 360;
                    tex.SetPixel(x, y, Color.HSVToRGB(angle / 360f, dist, 1f));
                } else {
                    tex.SetPixel(x, y, new Color(0, 0, 0, 0));
                }
            }
        }
        tex.Apply();
        colorWheel.texture = tex;
    }

    public void OnPointerClick(PointerEventData eventData) => PickColor(eventData);
    public void OnDrag(PointerEventData eventData) => PickColor(eventData);

    void PickColor(PointerEventData eventData)
    {
        Vector2 localPos;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(colorWheel.rectTransform, eventData.position, eventData.pressEventCamera, out localPos);
        
        // 座標から色を計算（テクスチャを読み取るより計算の方が正確で速い）
        float dx = localPos.x / (colorWheel.rectTransform.rect.width / 2f);
        float dy = localPos.y / (colorWheel.rectTransform.rect.height / 2f);
        float dist = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy));
        float angle = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg;
        if (angle < 0) angle += 360;

        Color selected = Color.HSVToRGB(angle / 360f, dist, 1f);
        OnColorSelected?.Invoke(selected);
    }
}