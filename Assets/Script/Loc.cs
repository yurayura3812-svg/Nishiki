using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// 製品 UI の文言。日本語と英語。キーで参照する。
/// </summary>
public static class Loc
{
    public const string Ja = "ja";
    public const string En = "en";

    static string lang;
    public static event Action Changed;

    public static string Language
    {
        get
        {
            if (string.IsNullOrEmpty(lang))
            {
                lang = AppSettingsStore.Load().language;
                if (lang != En) lang = Ja;
            }
            return lang;
        }
    }

    public static void SetLanguage(string code)
    {
        if (code != En) code = Ja;
        if (Language == code) return;
        lang = code;
        var save = AppSettingsStore.Load();
        save.language = code;
        AppSettingsStore.Save(save);
        ApplyScene();
        Changed?.Invoke();
    }

    public static string Get(string key)
    {
        var table = Language == En ? EnTable : JaTable;
        if (table.TryGetValue(key, out var v)) return v;
        if (JaTable.TryGetValue(key, out v)) return v;
        return key;
    }

    public static string Format(string key, params object[] args)
    {
        return string.Format(Get(key), args);
    }

    public static string KoiName(string name)
    {
        return string.IsNullOrEmpty(name) ? Get("koi.fallback") : name;
    }

    /// <summary>
    /// シーン／メニュー上の固定 Text を、オブジェクト名から差し替える。
    /// </summary>
    public static void ApplyScene()
    {
        var scene = SceneManager.GetActiveScene();
        foreach (var root in scene.GetRootGameObjects())
            Apply(root.transform);
    }

    public static void Apply(Transform root)
    {
        if (root == null) return;
        var texts = root.GetComponentsInChildren<Text>(true);
        for (int i = 0; i < texts.Length; i++)
        {
            var text = texts[i];
            if (text == null) continue;
            if (!TryKey(text.transform, out var key)) continue;
            text.text = Get(key);
        }
    }

    static bool TryKey(Transform t, out string key)
    {
        key = null;
        if (Under(t, "SettingsOverlay") || Under(t, "DeleteConfirm") || Under(t, "DetailOverlay"))
            return false;

        Transform p = t;
        while (p != null)
        {
            if (ByName.TryGetValue(p.name, out key)) return true;
            p = p.parent;
        }

        if (t.parent != null && (t.name == "Text" || t.name == "Text (Legacy)"))
        {
            var pn = t.parent.name;
            if (pn == "Parent1") { key = "breed.parent1"; return true; }
            if (pn == "Parent2") { key = "breed.parent2"; return true; }
            if (pn == "ChildArea") { key = "breed.child"; return true; }
        }

        if (t.name == "Title" || (t.parent != null && t.parent.name == "Title"))
        {
            var parent = t.name == "Title" ? t.parent : t.parent.parent;
            if (parent != null && (parent.name == "KoiPanel" || parent.name == "FloorPanel"))
                return false;
            var scene = SceneManager.GetActiveScene().name;
            if (scene == "ShopScene") { key = "custom.screen"; return true; }
            if (scene == "HinpyokaiScene") { key = "judging.title"; return true; }
        }
        return false;
    }

    static bool Under(Transform t, string name)
    {
        while (t != null)
        {
            if (t.name == name) return true;
            t = t.parent;
        }
        return false;
    }

    static readonly Dictionary<string, string> ByName = new Dictionary<string, string>
    {
        { "IkeButton", "menu.pond" },
        { "KoiButton", "menu.koi" },
        { "CustomButton", "menu.custom" },
        { "HinpyokaiButton", "menu.judging" },
        { "EsaButton", "menu.feed" },
        { "TabList", "list.tab" },
        { "TabBreed", "list.breed" },
        { "TabCreate", "list.create" },
        { "TabEdit", "list.edit" },
        { "JoinButton", "judging.join" },
        { "CategoryTab", "judging.overall" },
        { "EnterButton", "detail.submit" },
        { "DeleteButton", "detail.delete" },
        { "MessageText", "breed.pick" },
        { "MassageText", "breed.press" },
        { "SelectButton", "breed.select" },
        { "CrossButton", "breed.do" },
        { "SaveButton", "common.save" }
    };

    static readonly Dictionary<string, string> JaTable = new Dictionary<string, string>
    {
        { "menu.pond", "池" },
        { "menu.koi", "鯉" },
        { "menu.custom", "カスタム" },
        { "menu.judging", "品評会" },
        { "menu.feed", "えさやり" },
        { "custom.screen", "池のカスタム" },
        { "custom.koi", "泳がせる鯉" },
        { "custom.floor", "背景" },
        { "custom.float", "浮いてるもの" },
        { "custom.none", "なし" },
        { "custom.detail", "詳細" },
        { "judging.title", "総合品評会  暫定順位" },
        { "judging.overall", "総合" },
        { "judging.join", "品評会に参加する" },
        { "judging.empty", "出す鯉がありません" },
        { "judging.breed", "品種" },
        { "judging.gloss", "艶" },
        { "judging.pattern", "模様" },
        { "judging.body", "体格" },
        { "judging.rank", "{0}位" },
        { "judging.rank_name", "{0}位  {1}" },
        { "judging.pts", "{0}点" },
        { "judging.gloss_pts", "艶  {0}点" },
        { "judging.pattern_pts", "模様  {0}点" },
        { "judging.body_pts", "体格  {0}点" },
        { "detail.name_fallback", "なまえ" },
        { "detail.breed", "品種" },
        { "detail.born", "生まれた日" },
        { "detail.awards", "入賞回数" },
        { "detail.pattern_pts", "模様          {0} 点" },
        { "detail.gloss_pts", "艶            {0} 点" },
        { "detail.delete", "削除" },
        { "detail.submit", "出品する" },
        { "detail.confirm", "この鯉を本当に消しますか？" },
        { "detail.cancel", "やめる" },
        { "detail.ok", "消す" },
        { "list.tab", "鯉一覧" },
        { "list.breed", "交配" },
        { "list.create", "詳細作成" },
        { "list.edit", "詳細編集" },
        { "koi.fallback", "鯉" },
        { "koi.unnamed", "名無しの錦鯉" },
        { "settings.title", "設定" },
        { "settings.language", "言語" },
        { "settings.admin", "管理者ツール" },
        { "settings.on", "オン" },
        { "settings.off", "オフ" },
        { "settings.close", "閉じる" },
        { "breed.parent1", "親1" },
        { "breed.parent2", "親2" },
        { "breed.child", "子供" },
        { "breed.pick", "選択してください" },
        { "breed.press", "交配ボタンを\n押してください" },
        { "breed.do", "交配" },
        { "breed.select", "選択" },
        { "breed.pick_parent", "親{0} を選択してください。" },
        { "breed.pick_edit", "編集する錦鯉を選択してください。" },
        { "common.save", "保存" }
    };

    static readonly Dictionary<string, string> EnTable = new Dictionary<string, string>
    {
        { "menu.pond", "Pond" },
        { "menu.koi", "Koi" },
        { "menu.custom", "Custom" },
        { "menu.judging", "Show" },
        { "menu.feed", "Feed" },
        { "custom.screen", "Pond custom" },
        { "custom.koi", "Koi in pond" },
        { "custom.floor", "Background" },
        { "custom.float", "Floating" },
        { "custom.none", "None" },
        { "custom.detail", "Details" },
        { "judging.title", "General show  Provisional ranking" },
        { "judging.overall", "Overall" },
        { "judging.join", "Enter the show" },
        { "judging.empty", "No koi entered" },
        { "judging.breed", "Variety" },
        { "judging.gloss", "Luster" },
        { "judging.pattern", "Pattern" },
        { "judging.body", "Form" },
        { "judging.rank", "#{0}" },
        { "judging.rank_name", "#{0}  {1}" },
        { "judging.pts", "{0} pts" },
        { "judging.gloss_pts", "Luster  {0}" },
        { "judging.pattern_pts", "Pattern  {0}" },
        { "judging.body_pts", "Form  {0}" },
        { "detail.name_fallback", "Name" },
        { "detail.breed", "Variety" },
        { "detail.born", "Date of birth" },
        { "detail.awards", "Awards" },
        { "detail.pattern_pts", "Pattern          {0}" },
        { "detail.gloss_pts", "Luster            {0}" },
        { "detail.delete", "Delete" },
        { "detail.submit", "Enter" },
        { "detail.confirm", "Delete this koi?" },
        { "detail.cancel", "Cancel" },
        { "detail.ok", "Delete" },
        { "list.tab", "List" },
        { "list.breed", "Breed" },
        { "list.create", "Create" },
        { "list.edit", "Edit" },
        { "koi.fallback", "Koi" },
        { "koi.unnamed", "Unnamed koi" },
        { "settings.title", "Settings" },
        { "settings.language", "Language" },
        { "settings.admin", "Admin tools" },
        { "settings.on", "On" },
        { "settings.off", "Off" },
        { "settings.close", "Close" },
        { "breed.parent1", "Parent 1" },
        { "breed.parent2", "Parent 2" },
        { "breed.child", "Child" },
        { "breed.pick", "Please select" },
        { "breed.press", "Tap Breed" },
        { "breed.do", "Breed" },
        { "breed.select", "Select" },
        { "breed.pick_parent", "Choose parent {0}." },
        { "breed.pick_edit", "Choose a koi to edit." },
        { "common.save", "Save" }
    };
}
