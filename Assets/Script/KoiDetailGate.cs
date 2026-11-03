using UnityEngine.SceneManagement;

/// <summary>
/// 個体詳細シーンへ渡す引数。品評会の出品経由のときだけ出品ボタンを出す。
/// </summary>
public static class KoiDetailGate
{
    public const string SceneName = "KoiDetailScene";

    public static string KoiKey;
    public static bool EnterJudging;
    public static string ReturnScene = "KoiScene";
    public static string EnteredKoiKey;

    public static void Open(KoiPatternData koi, bool enterJudging, string returnScene)
    {
        KoiKey = koi == null ? null : KoiSaveStore.KeyOf(koi);
        EnterJudging = enterJudging;
        ReturnScene = string.IsNullOrEmpty(returnScene) ? "KoiScene" : returnScene;
        SceneManager.LoadScene(SceneName);
    }

    public static void OpenListForJudging()
    {
        EnterJudging = true;
        ReturnScene = "HinpyokaiScene";
        SceneManager.LoadScene("KoiScene");
    }

    public static void Close()
    {
        var back = string.IsNullOrEmpty(ReturnScene) ? "KoiScene" : ReturnScene;
        EnterJudging = false;
        SceneManager.LoadScene(back);
    }

    public static void SubmitCurrent()
    {
        EnteredKoiKey = KoiKey;
        HinpyokaiEntryStore.Submit(KoiKey);
        EnterJudging = false;
        ReturnScene = "KoiScene";
        SceneManager.LoadScene("HinpyokaiScene");
    }
}
