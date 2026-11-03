using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private Object sceneAsset;
    [SerializeField] private string sceneName;

#if UNITY_EDITOR
    void OnValidate()
    {
        if (sceneAsset != null)
            sceneName = sceneAsset.name;
    }

    void Awake()
    {
        if (sceneAsset != null)
            sceneName = sceneAsset.name;
    }
#endif

    public void LoadScene()
    {
        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("シーンが設定されていません！");
            return;
        }

        SceneManager.LoadScene(sceneName);
    }
}
