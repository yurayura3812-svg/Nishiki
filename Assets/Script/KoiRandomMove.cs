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
    private float eatLunge;
    private KoiController eater;

    void Start()
    {
        mainCamera = Camera.main;
        speedOffset = Random.Range(0f, 100f);
        individualTimer = Random.Range(4.0f, 7.0f);
        individualThreshold = Random.Range(0.5f, 0.65f); // 画面端のガードをさらに内側に
        lastY = 1.0f;

        // float spawnRange = 0.3f; 
        // transform.position = new Vector3(
        //     Random.Range(-spawnRange, spawnRange), 
        //     1.0f, 
        //     Random.Range(-spawnRange, spawnRange)
        // );

        float angle = Random.Range(0f, 360f);
        currentVelocityDirection = new Vector3(Mathf.Sin(angle), 0, Mathf.Cos(angle));
        transform.rotation = Quaternion.LookRotation(currentVelocityDirection);
        eater = GetComponent<KoiController>();
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
        if (closestObstacleDist < 0.6f) crowdMultiplier = 0.1f;
        else if (closestObstacleDist < 1.0f) crowdMultiplier = 0.4f;
        else if (closestObstacleDist < 1.5f) crowdMultiplier = 0.7f;

        bool seekingFood = FeedController.TryAttract(transform, out Vector3 cluster, out _, out _, out bool milling);
        float turnMul = 0.72f;
        if (seekingFood)
        {
            isResting = false;

            Vector3 sep = Vector3.zero;
            float nearestKoi = float.MaxValue;
            foreach (GameObject other in otherKois)
            {
                if (other == null || other == gameObject) continue;
                Vector3 away = transform.position - other.transform.position;
                away.y = 0f;
                float d = away.magnitude;
                if (d < nearestKoi) nearestKoi = d;
                if (d > 0.001f && d < 0.52f)
                    sep += away.normalized * ((0.52f - d) / 0.52f);
            }

            Vector3 slot = FeedController.GatherSlot(transform, cluster);
            slot.y = cluster.y;
            Vector3 toSlot = slot - transform.position;
            toSlot.y = 0f;
            float slotDist = toSlot.magnitude;
            Vector3 inward = cluster - transform.position;
            inward.y = 0f;

            Vector3 foodDir;
            if (slotDist < 0.18f)
            {
                foodDir = inward.sqrMagnitude > 0.0001f ? inward.normalized : transform.forward;
                turnMul = 0.85f;
                milling = true;
            }
            else
            {
                foodDir = toSlot / Mathf.Max(slotDist, 0.001f);
                if (sep.sqrMagnitude > 0.0001f)
                    foodDir = (foodDir + sep.normalized * 1.35f).normalized;
                turnMul = 1.05f;
            }

            if (nearestKoi < 0.38f) crowdMultiplier = 0.22f;
            else if (nearestKoi < 0.55f) crowdMultiplier = Mathf.Min(crowdMultiplier, 0.45f);

            targetDepthOffset = Mathf.Clamp(centerPosition.y - cluster.y, 0.02f, 0.38f);
            currentVelocityDirection = Vector3.Slerp(currentVelocityDirection, foodDir, Time.deltaTime * 1.7f).normalized;

            var bite = FeedController.ClosestPellet(transform.position + transform.forward * 0.38f, 0.36f);
            if (bite != null && eater != null && eater.Nibble(bite))
                eatLunge = 0.12f;
        }

        // --- 2. 緩急：加速を非常にマイルドに ---
        if (!seekingFood && !isResting && Time.frameCount % 300 == 0 && Random.value < 0.25f) {
            burstEnergy = 0.3f;
            targetDepthOffset = Random.Range(0f, 0.4f);
        }
        burstEnergy = Mathf.Lerp(burstEnergy, 0f, Time.deltaTime * 0.25f);
        eatLunge = Mathf.MoveTowards(eatLunge, 0f, Time.deltaTime * 1.4f);
        
        float finalSpeedBase = (isResting) ? moveSpeed * 0.02f : (moveSpeed + burstEnergy) * 1.22f;
        if (seekingFood)
            finalSpeedBase = milling ? moveSpeed * 0.42f : moveSpeed * 2.4f;
        if (eatLunge > 0f)
            finalSpeedBase += 0.22f;
        smoothSpeedMultiplier = Mathf.Lerp(smoothSpeedMultiplier, finalSpeedBase * crowdMultiplier, Time.deltaTime * (seekingFood ? 5.5f : 1.6f));

        transform.Translate(Vector3.forward * smoothSpeedMultiplier * Time.deltaTime);

        // --- 3. 深度とピッチ ---
        currentDepthOffset = Mathf.Lerp(currentDepthOffset, targetDepthOffset, Time.deltaTime * (seekingFood ? 1.6f : 0.4f));
        float waterCap = FeedController.WaterY - 0.14f;
        float currentY = Mathf.Min(centerPosition.y - currentDepthOffset, waterCap);

        float verticalDelta = currentY - lastY;
        float targetPitch = -Mathf.Atan2(verticalDelta, smoothSpeedMultiplier * Time.deltaTime) * Mathf.Rad2Deg;
        targetPitch = Mathf.Clamp(targetPitch, -pitchAmount, pitchAmount);
        currentPitch = Mathf.Lerp(currentPitch, targetPitch, Time.deltaTime * 1.2f);
        lastY = currentY;

        // --- 4. ターンロジック ---
        Vector3 pos = transform.position;
        float relX = pos.x - mainCamera.transform.position.x;
        float relZ = pos.z - mainCamera.transform.position.z;

        if (!seekingFood && (Mathf.Abs(relX) > limitX * individualThreshold || Mathf.Abs(relZ) > limitZ * individualThreshold)) {
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
        transform.rotation = Quaternion.Slerp(transform.rotation, finalRot, rotationSpeed * turnMul * Time.deltaTime);

        // --- 5. タイマー ---
        timer += Time.deltaTime;
        if (timer > individualTimer) {
            if (!seekingFood)
            {
                currentVelocityDirection = Quaternion.Euler(0, Random.Range(-25f, 25f), 0) * currentVelocityDirection;
                isResting = (Random.value < 0.5f);
            }
            timer = 0;
            individualTimer = Random.Range(6.0f, 12.0f); 
        }

        transform.position = new Vector3(transform.position.x, currentY, transform.position.z);
    }
}