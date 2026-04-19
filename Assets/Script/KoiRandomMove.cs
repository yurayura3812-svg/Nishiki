using UnityEngine;

public class KoiNaturalTurn : MonoBehaviour
{
    [Header("基本設定")]
    [SerializeField] private float moveSpeed = 0.9f; // さらに少し落として「漂い」を強調
    [SerializeField] private float rotationSpeed = 1.1f; 
    [SerializeField] private float bankAmount = 20.0f;   
    [SerializeField] private float bankSpeed = 2.0f;
    [SerializeField] private float pitchAmount = 18.0f;

    [Header("視界の設定")]
    [SerializeField] private float viewDistance = 1.5f; // 手前から認識して貫通感を防ぐ
    [SerializeField] private float viewAngle = 110f;    

    private Camera mainCamera;
    private Vector3 centerPosition = new Vector3(0f, 1.0f, 0f); 
    private Vector3 currentVelocityDirection; 
    private float timer;
    private float currentRoll = 0f;
    private float currentPitch = 0f; 

    private float smoothSpeedMultiplier = 1.0f;
    private bool isResting = false;
    private float speedOffset;
    private float individualTimer;
    private float individualThreshold;

    private float burstEnergy = 0f;
    private float targetDepthOffset = 0f;
    private float currentDepthOffset = 0f;
    private float lastY;

    void Start()
    {
        mainCamera = Camera.main;
        speedOffset = Random.Range(0f, 100f);
        individualTimer = Random.Range(4.0f, 7.0f);
        individualThreshold = Random.Range(0.5f, 0.65f); // 画面端のガードをさらに内側に
        lastY = 1.0f;

        float spawnRange = 0.3f; 
        transform.position = new Vector3(
            Random.Range(-spawnRange, spawnRange), 
            1.0f, 
            Random.Range(-spawnRange, spawnRange)
        );

        float angle = Random.Range(0f, 360f);
        currentVelocityDirection = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
        transform.rotation = Quaternion.LookRotation(currentVelocityDirection);
    }

    void Update()
    {
        float distanceToCamera = Mathf.Abs(mainCamera.transform.position.y - centerPosition.y);
        float frustumHeight = 2.0f * distanceToCamera * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float frustumWidth = frustumHeight * mainCamera.aspect;
        
        float limitX = (frustumWidth / 2.0f) * 0.7f; // さらに内側に絞る
        float limitZ = (frustumHeight / 2.0f) * 0.7f;

        // --- 1. 前方の密集判定（貫通感防止のため、より手前でマイルドに減速） ---
        float crowdMultiplier = 1.0f; 
        GameObject[] otherKois = GameObject.FindGameObjectsWithTag("Koi");
        float closestObstacleDist = float.MaxValue;
        foreach (GameObject other in otherKois) {
            if (other == this.gameObject) continue;
            Vector3 toOther = other.transform.position - transform.position;
            float dist = toOther.magnitude;
            if (dist < viewDistance) {
                if (Vector3.Angle(transform.forward, toOther) < viewAngle * 0.5f) {
                    if (dist < closestObstacleDist) closestObstacleDist = dist;
                }
            }
        }
        // 段階的にブレーキをかける
        if (closestObstacleDist < 0.6f) crowdMultiplier = 0.1f; 
        else if (closestObstacleDist < 1.0f) crowdMultiplier = 0.4f;
        else if (closestObstacleDist < 1.5f) crowdMultiplier = 0.7f;

        // --- 2. 緩急：加速を非常にマイルドに ---
        if (!isResting && Time.frameCount % 300 == 0 && Random.value < 0.25f) {
            burstEnergy = 0.3f; // 0.6f から 0.3f へ。ゆったりした動きに
            targetDepthOffset = Random.Range(0f, 0.4f); // 深度の幅を広げて重なりを回避
        }
        burstEnergy = Mathf.Lerp(burstEnergy, 0f, Time.deltaTime * 0.25f); // 減衰をさらに遅く
        
        float finalSpeedBase = (isResting) ? moveSpeed * 0.02f : (moveSpeed + burstEnergy);
        smoothSpeedMultiplier = Mathf.Lerp(smoothSpeedMultiplier, finalSpeedBase * crowdMultiplier, Time.deltaTime * 0.8f);

        transform.Translate(Vector3.forward * smoothSpeedMultiplier * Time.deltaTime);

        // --- 3. 深度とピッチ ---
        currentDepthOffset = Mathf.Lerp(currentDepthOffset, targetDepthOffset, Time.deltaTime * 0.4f);
        float currentY = centerPosition.y - currentDepthOffset;

        float verticalDelta = currentY - lastY;
        float targetPitch = -Mathf.Atan2(verticalDelta, smoothSpeedMultiplier * Time.deltaTime) * Mathf.Rad2Deg;
        targetPitch = Mathf.Clamp(targetPitch, -pitchAmount, pitchAmount);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * 1.2f);
        lastY = currentY;

        // --- 4. ターンロジック ---
        Vector3 pos = transform.position;
        float relX = pos.x - mainCamera.transform.position.x;
        float relZ = pos.z - mainCamera.transform.position.z;

        if (Mathf.Abs(relX) > limitX * individualThreshold || Mathf.Abs(relZ) > limitZ * individualThreshold) {
            Vector3 towardCenter = (new Vector3(mainCamera.transform.position.x, centerPosition.y, mainCamera.transform.position.z) - transform.position).normalized;
            currentVelocityDirection = Vector3.Slerp(currentVelocityDirection, towardCenter, Time.deltaTime * 0.8f).normalized;
            isResting = false;
        }

        Quaternion baseYaw = Quaternion.LookRotation(currentVelocityDirection);
        float angleDiff = Vector3.SignedAngle(transform.forward, currentVelocityDirection, Vector3.up);
        if (isResting) angleDiff += Mathf.Sin(Time.time * 1.0f) * 2.0f; 
        
        float targetRoll = -angleDiff * (bankAmount / 90f); 
        currentRoll = Mathf.Lerp(currentRoll, targetRoll, Time.deltaTime * bankSpeed);

        Quaternion finalRot = baseYaw * Quaternion.Euler(currentPitch, 0, currentRoll);
        transform.rotation = Quaternion.Slerp(transform.rotation, finalRot, rotationSpeed * Time.deltaTime);

        // --- 5. タイマー ---
        timer += Time.deltaTime;
        if (timer > individualTimer) {
            currentVelocityDirection = Quaternion.Euler(0, Random.Range(-25f, 25f), 0) * currentVelocityDirection;
            isResting = (Random.value < 0.5f); // 停滞確率をさらにアップ
            timer = 0;
            individualTimer = Random.Range(6.0f, 12.0f); 
        }

        transform.position = new Vector3(transform.position.x, currentY, transform.position.z);
    }
}