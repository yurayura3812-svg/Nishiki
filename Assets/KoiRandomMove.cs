using UnityEngine;

public class KoiCameraFrustumMove : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float rotationSpeed = 4.0f;
    
    private Camera mainCamera;
    private Vector3 centerPosition = new Vector3(0f, 1.86f, 0f);
    private Vector3 targetDirection;
    private float timer;

    void Start()
    {
        mainCamera = Camera.main;
        transform.position = centerPosition;
        SetRandomDirection();
    }

    void Update()
    {
        // 1. 現在の高さ（Y=1.86）での画面の端を計算
        float distanceToCamera = Mathf.Abs(mainCamera.transform.position.y - centerPosition.y);
        
        // カメラの視野角から、その距離における画面の半分（高さと幅）を計算
        float frustumHeight = 2.0f * distanceToCamera * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * mainCamera.aspect;

        // 鯉のサイズ分、少し内側にマージンを持たせる（0.9は調整用：1.0で画面端ギリギリ）
        float limitX = (frustumWidth / 2.0f) * 0.9f;
        float limitZ = (frustumHeight / 2.0f) * 0.9f;

        // 2. 移動
        transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);

        // 3. 画面端の判定
        Vector3 pos = transform.position;
        bool isOutOfBounds = false;

        if (Mathf.Abs(pos.x - mainCamera.transform.position.x) > limitX)
        {
            pos.x = mainCamera.transform.position.x + Mathf.Sign(pos.x - mainCamera.transform.position.x) * limitX;
            isOutOfBounds = true;
        }
        if (Mathf.Abs(pos.z - mainCamera.transform.position.z) > limitZ)
        {
            pos.z = mainCamera.transform.position.z + Mathf.Sign(pos.z - mainCamera.transform.position.z) * limitZ;
            isOutOfBounds = true;
        }

        if (isOutOfBounds)
        {
            transform.position = pos;
            // 画面の中央方向へ向きを変える
            targetDirection = (new Vector3(mainCamera.transform.position.x, centerPosition.y, mainCamera.transform.position.z) - transform.position).normalized;
        }

        // 4. 向きの回転
        if (targetDirection != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        }

        // 5. ランダムな方向転換
        timer += Time.deltaTime;
        if (timer > 2.5f && !isOutOfBounds)
        {
            SetRandomDirection();
            timer = 0;
        }

        // 高さ固定
        transform.position = new Vector3(transform.position.x, centerPosition.y, transform.position.z);
    }

    void SetRandomDirection()
    {
        float angle = Random.Range(0f, 360f);
        targetDirection = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
    }
}