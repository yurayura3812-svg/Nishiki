using UnityEngine;
using System.Collections.Generic;

public class KoiSpawner : MonoBehaviour
{
    public GameObject koiPrefab;        // 鯉のプレハブ
    public List<KoiPatternData> dataList; // 作成した模様データのリスト

    void Start()
    {
        // リストに登録したデータの数だけ、少しずつ場所をずらして生成
        for (int i = 0; i < dataList.Count; i++)
        {
            Vector3 pos = new Vector3(i * 2.0f, 0, 0); // 横に並べる例
            SpawnKoi(dataList[i], pos);
        }
    }

    void SpawnKoi(KoiPatternData data, Vector3 position)
    {
        GameObject newKoi = Instantiate(koiPrefab, position, Quaternion.identity);
        
        KoiController controller = newKoi.GetComponent<KoiController>();
        if (controller != null)
        {
            controller.patternData = data; // データを渡す
            controller.ApplyDNA();         // 反映
        }
    }
}