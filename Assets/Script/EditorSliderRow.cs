using UnityEngine;
using UnityEngine.UI;

public class EditorSliderRow : MonoBehaviour
{
    public Text labelText;      // 項目名（例：赤の量）
    public Slider slider;       // スライダー本体
    public Text valueText;      // 現在値の数字表示（例：0.55）

    public string propertyName; // シェーダーの変数名（例：_RedAmount）
    private System.Action<string, float> onUpdate; // 値が変わった時の通知先
    // EditorSliderRow.cs 内に追加
    public void Setup(string label, string prop, float min, float max, float current, System.Action<string, float> callback)
    {
        labelText.text = label;
        propertyName = prop;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = current;
        onUpdate = callback;

        valueText.text = current.ToString("F2");

        // 値が変わった時の処理を登録
        slider.onValueChanged.AddListener(val => {
            valueText.text = val.ToString("F2");
            onUpdate?.Invoke(propertyName, val);
        });
    }

    // 外から値をセットする（ランダムボタン用）
    public void SetValueQuietly(float val)
    {
        slider.SetValueWithoutNotify(val);
        valueText.text = val.ToString("F2");
    }
}