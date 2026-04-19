using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using System.Collections.Generic; // これを追加

public class ObjectRotator : MonoBehaviour
{
    public float rotationSpeed = 0.2f;
    // ★インスペクターから、回転エリアにしたいPanelをドラッグ＆ドロップする
    public GameObject rotationAreaPanel;

    private Quaternion initialRotation;
    private bool isRotating = false;

    void Start()
    {
        initialRotation = transform.rotation;
    }

    void Update()
    {
        var mouse = Mouse.current;
        if (mouse == null) return;

        if (mouse.leftButton.wasPressedThisFrame)
        {
            // 今触っているのが指定したパネルかどうかを判定
            if (IsPointerOverRotationArea())
            {
                isRotating = true;
            }
        }

        if (mouse.leftButton.wasReleasedThisFrame)
        {
            isRotating = false;
        }

        if (isRotating)
        {
            Vector2 delta = mouse.delta.ReadValue();
            transform.Rotate(Vector3.up, -delta.x * rotationSpeed, Space.World);
            transform.Rotate(Vector3.right, delta.y * rotationSpeed, Space.World);
        }
    }

    // ★今触っているUIが、指定したパネルかどうかをチェックする関数
    private bool IsPointerOverRotationArea()
    {
        PointerEventData eventData = new PointerEventData(EventSystem.current);
        eventData.position = Mouse.current.position.ReadValue();

        List<RaycastResult> results = new List<RaycastResult>();
        EventSystem.current.RaycastAll(eventData, results);

        foreach (var result in results)
        {
            // 触っているUIの中に、指定したパネルが含まれているか
            if (result.gameObject == rotationAreaPanel) return true;
        }
        return false;
    }

    public void ResetRotation() => transform.rotation = initialRotation;
}