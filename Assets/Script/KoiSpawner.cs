using UnityEngine;
using System.Collections.Generic;
using System.IO;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class KoiSpawner : MonoBehaviour
{
    [Header("Settings")]
    public GameObject koiPrefab;        // 鯉のプレハブ

    [Header("Loaded Data")]
    public List<KoiPatternData> dataList = new List<KoiPatternData>(); // ロードされたデータのリスト

    void Start()
    {
        // 1. フォルダ内からアセットを自動ロード
        LoadAllPatternAssets();

        // 2. ロードされたデータの数だけ生成
        foreach (var data in dataList)
        {
            SpawnKoi(data);
        }
    }

    void LoadAllPatternAssets()
    {
#if UNITY_EDITOR
        string folderPath = "Assets/KoiData";
        if (!Directory.Exists(folderPath))
        {
            Directory.CreateDirectory(folderPath);
            return;
        }

        string[] guids = AssetDatabase.FindAssets("t:KoiPatternData", new[] { folderPath });
        dataList.Clear();

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            KoiPatternData data = AssetDatabase.LoadAssetAtPath<KoiPatternData>(path);
            if (data != null) dataList.Add(data);
        }
        Debug.Log($"<color=cyan>KoiSpawner:</color> {dataList.Count}体の鯉データをフォルダから読み込みました。");
#endif
    }

    /// <summary>
    /// KoiNaturalTurn の移動制限範囲（カメラ視界の0.7倍）に合わせてランダム放流
    /// </summary>
    public void SpawnKoi(KoiPatternData data)
{
    Camera mainCamera = Camera.main;
    if (mainCamera == null) {
        Debug.LogError("MainCameraが見つかりません！タグを確認してください。");
        return;
    }

    float targetY = 1.0f;
    float dist = Mathf.Abs(mainCamera.transform.position.y - targetY);
    float h = 2.0f * dist * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
    float w = h * mainCamera.aspect;

    float limitX = (w / 2.0f) * 0.7f;
    float limitZ = (h / 2.0f) * 0.7f;

    float randX = Random.Range(-limitX, limitX);
    float randZ = Random.Range(-limitZ, limitZ);
    
    Vector3 spawnPos = new Vector3(
        mainCamera.transform.position.x + randX, 
        targetY, 
        mainCamera.transform.position.z + randZ
    );

    // デバッグログを出して、実際にどこに生成しようとしているか確認
    Debug.Log($"生成位置を決定: {spawnPos} (範囲: X±{limitX}, Z±{limitZ})");

    CreateKoiInstance(data, spawnPos, Quaternion.Euler(0, Random.Range(0f, 360f), 0));
}

    /// <summary>
    /// 実際にオブジェクトを生成してデータを流し込む（関数名を分けました）
    /// </summary>
    private void CreateKoiInstance(KoiPatternData data, Vector3 position, Quaternion rotation)
    {
        if (koiPrefab == null) return;

        GameObject newKoi = Instantiate(koiPrefab, position, rotation);
        
        KoiController controller = newKoi.GetComponent<KoiController>();
        if (controller != null)
        {
            controller.patternData = data;
            controller.ApplyDNA();
        }
    }

    // --- 範囲の可視化：カメラの高さに合わせて自動で枠が変わります ---
    void OnDrawGizmos()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera == null) return;

        float targetY = 1.0f;
        float dist = Mathf.Abs(mainCamera.transform.position.y - targetY);
        float h = 2.0f * dist * Mathf.Tan(mainCamera.fieldOfView * 0.5f * Mathf.Deg2Rad);
        float w = h * mainCamera.aspect;

        // 移動範囲(0.7倍)を水色の線で描画
        Gizmos.color = Color.cyan;
        Vector3 center = new Vector3(mainCamera.transform.position.x, targetY, mainCamera.transform.position.z);
        Gizmos.DrawWireCube(center, new Vector3(w * 0.7f, 0.1f, h * 0.7f));
    }
}