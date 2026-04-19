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

        // 【ここを修正】中央に密集させず、少し散らして配置する
        // Range(0.85f)の範囲内で、出現時に重ならないようにランダムな位置へ
        float spawnRange = 2.0f; // 散らす範囲（適宜調整してください）
        transform.position = new Vector3(
            Random.Range(-spawnRange, spawnRange), 
            1.86f, 
            Random.Range(-spawnRange, spawnRange)
        );

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

        // --- 3. 画面端の判定（なめらか誘導版） ---
        Vector3 pos = transform.position;
        float relativeX = pos.x - mainCamera.transform.position.x;
        float relativeZ = pos.z - mainCamera.transform.position.z;

        // 枠の「内側」で、どのくらい端に近いかを 0.0 ～ 1.0 で計算
        // (例：枠の 80% を超えたら徐々に中央を向き始める)
        float threshold = 0.8f; 
        float factorX = Mathf.Abs(relativeX) / limitX;
        float factorZ = Mathf.Abs(relativeZ) / limitZ;

        if (factorX > threshold || factorZ > threshold)
        {
            // 中央へ向かうベクトル
            Vector3 towardCenter = (new Vector3(mainCamera.transform.position.x, centerPosition.y, mainCamera.transform.position.z) - transform.position).normalized;
            
            // 端に行けば行くほど、中央へ戻る力を強くする（重み付け）
            float weight = Mathf.Max(factorX, factorZ); // 0.8 ～ 1.0 以上の値
            float blend = (weight - threshold) / (1.0f - threshold); // 0.0 ～ 1.0 に変換
            
            // 現在の目標方向に中央へのベクトルを混ぜる
            targetDirection = Vector3.Slerp(targetDirection, towardCenter, blend).normalized;
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

    // 範囲を可視化するためのデバッグ表示
    void OnDrawGizmos()
    {
        if (mainCamera == null) mainCamera = Camera.main;
        if (mainCamera == null) return;

        // 計算ロジックをUpdateと同じにする
        float distanceToCamera = Mathf.Abs(mainCamera.transform.position.y - centerPosition.y);
        float frustumHeight = 2.0f * distanceToCamera * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * mainCamera.aspect;
        
        // ここに現在設定している数値をいれる（例: 0.6f）
        float checkLimitX = (frustumWidth / 2.0f) * 0.6f; 
        float checkLimitZ = (frustumHeight / 2.0f) * 0.6f;

        // 四角い枠を描画
        Gizmos.color = Color.red;
        Vector3 corner1 = new Vector3(mainCamera.transform.position.x + checkLimitX, centerPosition.y, mainCamera.transform.position.z + checkLimitZ);
        Vector3 corner2 = new Vector3(mainCamera.transform.position.x - checkLimitX, centerPosition.y, mainCamera.transform.position.z + checkLimitZ);
        Vector3 corner3 = new Vector3(mainCamera.transform.position.x - checkLimitX, centerPosition.y, mainCamera.transform.position.z - checkLimitZ);
        Vector3 corner4 = new Vector3(mainCamera.transform.position.x + checkLimitX, centerPosition.y, mainCamera.transform.position.z - checkLimitZ);

        Gizmos.DrawLine(corner1, corner2);
        Gizmos.DrawLine(corner2, corner3);
        Gizmos.DrawLine(corner3, corner4);
        Gizmos.DrawLine(corner4, corner1);
    }
}