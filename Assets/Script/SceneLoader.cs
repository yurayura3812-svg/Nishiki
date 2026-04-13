using UnityEngine;
using UnityEngine.SceneManagement;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class SceneLoader : MonoBehaviour
{
    [SerializeField] private Object sceneAsset; // シーンをドラッグ

    private string sceneName;

    void Awake()
    {
#if UNITY_EDITOR
        if (sceneAsset != null)
        {
            sceneName = sceneAsset.name;
        }
#endif
    }

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