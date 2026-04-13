using UnityEngine;

public class KoiNaturalTurn : MonoBehaviour
{
    [SerializeField] private float moveSpeed = 1.5f;
    [SerializeField] private float rotationSpeed = 2.0f; // 少し落とすと大きな円で曲がります
    [SerializeField] private float bankAmount = 30.0f;   // 曲がる時の傾き（度）
    [SerializeField] private float bankSpeed = 5.0f;     // 傾くスピード

    private Camera mainCamera;
    private Vector3 centerPosition = new Vector3(0f, 1.0f, 0f);
    private Vector3 targetDirection;
    private float timer;
    private float currentRoll = 0f;

    void Start()
    {
        mainCamera = Camera.main;
        transform.position = centerPosition;
        SetRandomDirection();
    }

    void Update()
    {
        // 1. 画面端の計算（前回と同じ）
        float distanceToCamera = Mathf.Abs(mainCamera.transform.position.y - centerPosition.y);
        float frustumHeight = 2.0f * distanceToCamera * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * mainCamera.aspect;
        float limitX = (frustumWidth / 2.0f) * 0.85f; // 少し余裕を持たせる
        float limitZ = (frustumHeight / 2.0f) * 0.85f;

        // 2. 移動
        transform.Translate(Vector3.forward * moveSpeed * Time.deltaTime);

        // 3. 画面端の判定
        Vector3 pos = transform.position;
        if (Mathf.Abs(pos.x - mainCamera.transform.position.x) > limitX || Mathf.Abs(pos.z - mainCamera.transform.position.z) > limitZ)
        {
            // 画面端に来たら、中央の少し先を狙ってなだらかに曲がらせる
            targetDirection = (new Vector3(mainCamera.transform.position.x, centerPosition.y, mainCamera.transform.position.z) - transform.position).normalized;
        }

        // 4. なめらかな旋回とバンク（傾き）の計算
        Quaternion targetRotation = Quaternion.LookRotation(targetDirection);
        
        // 現在の向きとターゲットの向きの差（角度）を求める
        float angleDiff = Vector3.SignedAngle(transform.forward, targetDirection, Vector3.up);
        
        // 曲がる方向に合わせてロール（傾き）の目標値を決める
        float targetRoll = -angleDiff * (bankAmount / 90f); 
        targetRoll = Mathf.Clamp(targetRoll, -bankAmount, bankAmount);
        
        // 傾きを滑らかに補間
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * bankSpeed);

        // 最終的な回転を適用
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, rotationSpeed * Time.deltaTime);
        transform.Rotate(Vector3.forward, currentRoll - transform.localEulerAngles.z, Space.Self);

        // 5. ランダムな方向転換
        timer += Time.deltaTime;
        if (timer > 4.0f) // 少し長めにして直進距離を増やす
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