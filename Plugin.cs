using System;
using System.Collections.Generic;
using System.Linq;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using TMPro;
using UINavigation;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace HidenDojo;

[BepInPlugin(Guid, "秘传道场", Version)]
public sealed class Plugin : BaseUnityPlugin
{
    public const string Guid = "tong.shogunshowdown.hidendojo";
    public const string Version = "0.3.4";
    internal static Plugin Instance = null!;
    internal ConfigEntry<bool> Enabled = null!, Guard = null!, FreeRefresh = null!, BerserkNoTurn = null!, FreePurchases = null!;
    internal bool GuardUsed;
    internal readonly Dictionary<string, ConfigEntry<int>> Levels = new();
    internal static readonly string[] LevelNames = { "原式", "试炼式", "传承式", "霸道式" };
    Harmony harmony = null!;
    void Awake()
    {
        Instance = this;
        Enabled = Config.Bind("General", "Enabled", true, "启用秘传道场");
        Guard = Config.Bind("General", "护命", true, "每场战斗一次护命");
        FreeRefresh = Config.Bind("General", "万能刷新", true, "商店、武器选择和升级奖励的刷新不消耗金币");
        BerserkNoTurn = Config.Bind("General", "霸道式不耗回合", true, "强化等级为霸道式的武器放入攻击队列时不消耗回合");
        FreePurchases = Config.Bind("General", "免费消费", true, "商店购买、解锁、武器升级及付费服务免除金币、骷髅和生命支付；保留升级槽位限制");
        harmony = new Harmony(Guid);
        harmony.PatchAll();
        Logger.LogInfo("秘传道场 " + Version + " 原生菜单补丁已加载");
    }
    void Update()
    {
        if (DojoMenu.Open)
        {
            if (Gamepad.current?.leftShoulder.wasPressedThisFrame == true) DojoMenu.PageBy(-1);
            if (Gamepad.current?.rightShoulder.wasPressedThisFrame == true) DojoMenu.PageBy(1);
        }
    }
    internal void Log(string s) => Logger.LogInfo(s);
    internal void Error(Exception e) => Logger.LogError(e);
    internal int Level(string key)
    {
        if (!Levels.TryGetValue(key, out var value))
            Levels[key] = value = Config.Bind("武器等级", key, 0, "0 原式 / 1 试炼式 / 2 传承式 / 3 霸道式");
        return Mathf.Clamp(value.Value, 0, 3);
    }
    internal void SetLevel(string key, int value)
    {
        Level(key);
        Levels[key].Value = Mathf.Clamp(value, 0, 3);
        ApplyNumericToCurrentDeck(key);
        Config.Save();
        Log($"强化设置 {key}={Levels[key].Value}");
    }
    internal void ApplyNumericToCurrentDeck(string key)
    {
        var deck = TilesManager.Instance?.Deck;
        if (deck == null) return;
        foreach (var tile in deck.Where(t => t != null && t.Attack != null && t.Attack.AttackEnum.ToString() == key))
        {
            var attack = tile.Attack;
            var level = Level(key);
            attack.Value = attack.BaseValue + (level == 1 ? 1 : level == 2 ? 2 : level == 3 ? 3 : 0);
            NumericCooldown.Apply(tile, level);
            
            tile.Graphics?.UpdateGraphics();
        }
    }
    internal void ResetWeaponLevelsForNewRun()
    {
        foreach (var value in Enum.GetValues(typeof(TileEnums.AttackEnum)))
        {
            string key = value.ToString()!;
            Level(key);
            Levels[key].Value = 0;
        }
        var deck = TilesManager.Instance?.Deck;
        if (deck != null)
        {
            foreach (var key in deck.Where(t => t != null && t.Attack != null)
                         .Select(t => t.Attack.AttackEnum.ToString())
                         .Distinct())
                ApplyNumericToCurrentDeck(key);
        }
        Config.Save();
        Log("新游戏开始：所有武器强化已恢复为原式");
    }
}

internal sealed class Row
{
    internal OpenOptionsMenuItem Item = null!;
    internal Func<string> Label = null!;
    internal Action<int> Change = null!;
}

internal static class DojoMenu
{
    const int PageSize = 4;
    static int PageCount => Math.Max(1, (Math.Max(1, weaponKeys.Count) + 4 + PageSize - 1) / PageSize);
    static PauseMenuPage? page;
    static OpenOptionsMenuItem? entrance;
    static readonly Dictionary<OpenOptionsMenuItem, Row> rows = new();
    static readonly Dictionary<GameObject, bool> originalActive = new();
    static readonly List<OpenOptionsMenuItem> settings = new();
    static readonly List<string> weaponKeys = new();
    static readonly Dictionary<string, string> weaponNames = new();
    static int pageIndex;
    static string selectedKey = "";
    static TMP_Text? header;
    static string headerBefore = "暂停";
    static TMP_Text? help;
    internal static bool Open;
    static readonly Color Ink = new(0.89f, 0.87f, 0.80f, 1f);
    internal static void Ensure(MenuPage candidate)
    {
        if (!(candidate is PauseMenuPage p) || !Plugin.Instance.Enabled.Value) return;
        if (page != p)
        {
            rows.Clear(); settings.Clear(); weaponKeys.Clear(); weaponNames.Clear(); originalActive.Clear(); entrance = null; help = null; Open = false; page = p;
        }
        if (entrance != null) { Refresh(); return; }
        var template = p.GetComponentsInChildren<OpenOptionsMenuItem>(true).FirstOrDefault();
        if (template == null) return;
        header = p.transform.Find("Canvas/Header")?.GetComponentInChildren<TMP_Text>(true);
        if (header != null) headerBefore = header.text;
        entrance = Clone(template, "HidenDojoEntrance", () => "秘传道场", _ => Show());
        entrance.transform.SetSiblingIndex(template.transform.GetSiblingIndex()+1);
        LayoutAndSelect(entrance, false);
        Plugin.Instance.Log("秘传道场入口已注册到暂停菜单；导航目标=" + p.Targets.Count);
    }
    static OpenOptionsMenuItem Clone(OpenOptionsMenuItem template, string name, Func<string> label, Action<int> change)
    {
        // Instantiate inactive so localisation cannot register callbacks for the copied label.
        var staging = new GameObject("HidenDojoStaging");
        staging.SetActive(false);
        var go = UnityEngine.Object.Instantiate(template.gameObject, staging.transform, false);
        go.name = name;
        foreach (var localize in go.GetComponentsInChildren<LocalizeStringEvent>(true)) UnityEngine.Object.DestroyImmediate(localize);
        go.SetActive(false);
        go.transform.SetParent(template.transform.parent, false);
        UnityEngine.Object.Destroy(staging);
        var item = go.GetComponent<OpenOptionsMenuItem>();
        rows[item] = new Row { Item = item, Label = label, Change = change };
        go.SetActive(true);
        Refresh(item);
        return item;
    }
    internal static bool Handle(OpenOptionsMenuItem item, int direction)
    {
        if (!rows.TryGetValue(item, out var row)) return false;
        try { row.Change(direction); Refresh(); }
        catch (Exception ex) { Plugin.Instance.Error(ex); }
        return true;
    }
    internal static void Refresh(OpenOptionsMenuItem item)
    {
        if (!rows.TryGetValue(item, out var row) || item == null) return;
        var label = item.transform.Find("Button/Text")?.GetComponent<TMP_Text>();
        if (label != null) { label.text = row.Label(); label.color = Ink; }
    }
    static void Refresh()
    {
        foreach (var item in rows.Keys.ToArray()) if (item != null) Refresh(item);
        if (Open && header != null) header.text = "秘传道场";
        UpdateHelp();
    }
    static void Show()
    {
        if (page == null || entrance == null || Open) return;
        Open = true;
        originalActive.Clear();
        var parent = entrance.transform.parent;
        for (int i=0; i<parent.childCount; i++)
        {
            var go = parent.GetChild(i).gameObject;
            originalActive[go] = go.activeSelf;
            go.SetActive(false);
        }
        weaponKeys.Clear();
        weaponNames.Clear();
        var deck = TilesManager.Instance?.Deck;
        if (deck != null)
        {
            foreach (var attack in deck.Where(t => t != null && t.Attack != null)
                         .Select(t => t.Attack)
                         .GroupBy(a => a.AttackEnum.ToString())
                         .Select(g => g.First()))
            {
                var key = attack.AttackEnum.ToString();
                weaponKeys.Add(key);
                weaponNames[key] = attack.Name;
                Plugin.Instance.ApplyNumericToCurrentDeck(key);
            }
        }
        pageIndex = 0;
        selectedKey = weaponKeys.FirstOrDefault() ?? "";
        if (header != null) header.text = "秘传道场";
        if (help == null && header != null)
        {
            help = UnityEngine.Object.Instantiate(header, page.transform.Find("Canvas"), false);
            help.name = "HidenDojoHelp";
            foreach (var localize in help.GetComponentsInChildren<LocalizeStringEvent>(true)) UnityEngine.Object.DestroyImmediate(localize);
            help.rectTransform.anchorMin = help.rectTransform.anchorMax = new Vector2(.5f,.5f);
            help.rectTransform.pivot = new Vector2(.5f,.5f);
            help.rectTransform.anchoredPosition = new Vector2(0, -108);
            help.rectTransform.sizeDelta = new Vector2(520, 58);
            help.fontSize = 9;
            help.enableAutoSizing = false;
            help.color = Ink;
            help.alignment = TextAlignmentOptions.Center;
        }
        if (help != null) help.gameObject.SetActive(true);
        BuildPage(true);
        Refresh();
        Plugin.Instance.Log("秘传设置页打开：" + weaponKeys.Count + " 件当前装备（已去重）");
    }
    static void BuildPage(bool selectFirst)
    {
        if (entrance == null || page == null) return;
        page.SelectedTarget?.Deselect();
        page.SelectedTarget = null;
        foreach (var item in settings)
            if (item != null) { rows.Remove(item); item.gameObject.SetActive(false); UnityEngine.Object.Destroy(item.gameObject); }
        settings.Clear();
        int pageCount = PageCount;
        pageIndex = Mathf.Clamp(pageIndex, 0, pageCount - 1);
        
        int start = pageIndex * PageSize;
        foreach (var key in weaponKeys)
        {
            string capturedKey = key;
            settings.Add(Clone(entrance, "HidenDojoWeapon_" + capturedKey,
                () => (weaponNames.TryGetValue(capturedKey, out var name) ? name : Effects.DisplayName(capturedKey)) + "  ·  " + Plugin.LevelNames[Plugin.Instance.Level(capturedKey)],
                delta => Plugin.Instance.SetLevel(capturedKey, delta == 0 ? (Plugin.Instance.Level(capturedKey) + 1) % 4 : Plugin.Instance.Level(capturedKey) + delta)));
        }
        settings.Add(Clone(entrance, "HidenDojoGuard", () => "护命  ·  " + (Plugin.Instance.Guard.Value ? (Plugin.Instance.GuardUsed ? "本场已用" : "开启") : "关闭"), _ =>
        {
            Plugin.Instance.Guard.Value = !Plugin.Instance.Guard.Value;
            Plugin.Instance.Config.Save();
        }));
        settings.Add(Clone(entrance, "HidenDojoFreeRefresh", () => "万能刷新  ·  " + (Plugin.Instance.FreeRefresh.Value ? "开启" : "关闭"), _ =>
        {
            Plugin.Instance.FreeRefresh.Value = !Plugin.Instance.FreeRefresh.Value;
            FreePrice.RefreshAll();
            Plugin.Instance.Config.Save();
        }));
        settings.Add(Clone(entrance, "HidenDojoBerserkNoTurn", () => "霸道式不耗回合  ·  " + (Plugin.Instance.BerserkNoTurn.Value ? "开启" : "关闭"), _ =>
        {
            Plugin.Instance.BerserkNoTurn.Value = !Plugin.Instance.BerserkNoTurn.Value;
            Plugin.Instance.Config.Save();
        }));
        settings.Add(Clone(entrance, "HidenDojoFreePurchases", () => "免费消费  ·  " + (Plugin.Instance.FreePurchases.Value ? "开启" : "关闭"), _ =>
        {
            Plugin.Instance.FreePurchases.Value = !Plugin.Instance.FreePurchases.Value;
            FreePrice.RefreshAll();
            Plugin.Instance.Config.Save();
        }));
        if (weaponKeys.Count == 0)
            settings.Insert(0, Clone(entrance, "HidenDojoEmpty", () => "当前没有可用秘传武器", _ => { }));
        var visible = settings.Skip(start).Take(PageSize).ToArray();
        foreach (var item in settings) item.gameObject.SetActive(visible.Contains(item));
        if (pageCount > 1)
            settings.Add(Clone(entrance, "HidenDojoPage", () => $"翻页  {pageIndex + 1}/{pageCount}", delta => PageBy(delta < 0 ? -1 : 1)));
        settings.Add(Clone(entrance, "HidenDojoBack", () => "返回暂停菜单", _ => Close()));
        var target = settings.FirstOrDefault(i => i != null && i.gameObject.activeSelf && i.name == "HidenDojoWeapon_" + selectedKey) ?? settings.FirstOrDefault(i => i.gameObject.activeSelf);
        if (target != null) LayoutAndSelect(target, selectFirst || page.SelectedTarget == null);
        Refresh();
    }
    internal static void PageBy(int delta)
    {
        if (!Open) return;
        int pageCount = PageCount;
        if (pageCount <= 1) return;
        pageIndex = (pageIndex + delta + pageCount) % pageCount;
        selectedKey = "";
        BuildPage(true);
    }
    internal static void Close()
    {
        if (!Open) return;
        Open = false;
        if (page != null) { page.SelectedTarget?.Deselect(); page.SelectedTarget = null; }
        foreach (var item in settings)
            if (item != null) { rows.Remove(item); item.gameObject.SetActive(false); UnityEngine.Object.Destroy(item.gameObject); }
        settings.Clear();
        foreach (var entry in originalActive) if (entry.Key != null) entry.Key.SetActive(entry.Value);
        originalActive.Clear();
        if (header != null) header.text = headerBefore;
        if (help != null) help.gameObject.SetActive(false);
        if (entrance != null) LayoutAndSelect(entrance, true);
    }
    static void LayoutAndSelect(OptionsMenuItem target, bool select)
    {
        if (page == null) return;
        var parent = target.transform.parent as RectTransform;
        Canvas.ForceUpdateCanvases();
        if (parent != null) LayoutRebuilder.ForceRebuildLayoutImmediate(parent);
        if (select) { page.SelectedTarget?.Deselect(); page.SelectedTarget = null; }
        page.InitializeNavigation();
        if (select) UINavigationHelper.SelectNewTarget(page, target);
        MenuUINavigationManager.Instance?.SetCurrentNavigationGroup(page);
    }
    internal static void UpdateHelp()
    {
        if (!Open || help == null || page == null) return;
        var selected = page.SelectedTarget as OpenOptionsMenuItem;
        string hint = $"选择装备，左右调整四档强度；A 确认，B 返回。  （第 {pageIndex + 1}/{PageCount} 页）";
        if (selected != null && selected.name.StartsWith("HidenDojoWeapon_"))
        {
            var key = selected.name.Substring("HidenDojoWeapon_".Length);
            selectedKey = key;
            var level = Plugin.Instance.Level(key);
            hint = $"当前：{weaponNames[key]} · {Plugin.LevelNames[level]}\n{Effects.Description(key, level)}\n四档规则：原式原版｜试炼式伤害+1｜传承式伤害+2/冷却-1｜霸道式伤害+3/冷却-3";
        }
        else if (selected != null && selected.name == "HidenDojoFreeRefresh")
            hint = "万能刷新：商店、武器选择和升级奖励中的刷新按钮可以无限免费使用。";
        else if (selected != null && selected.name == "HidenDojoBerserkNoTurn")
            hint = "霸道式不耗回合：等级为霸道式的武器放入攻击队列后，不会触发敌方回合。";
        else if (selected != null && selected.name == "HidenDojoFreePurchases")
            hint = "免费购买、武器升级、解锁和付费服务；不扣金币、骷髅或生命，升级槽位仍按原版。";
        else if (selected != null && selected.name == "HidenDojoGuard")
            hint = "护命：每场战斗一次，受到致命伤时保留 1 点生命。";
        help.text = hint + "\n↑↓ 选择　←→ 调整　LB/RB 翻页　A / 确认：切换　B / 取消：返回";
    }
}

[HarmonyPatch(typeof(MenuPage), nameof(MenuPage.Enable))]
internal static class MenuEnablePatch
{
    static void Postfix(MenuPage __instance) { try { DojoMenu.Ensure(__instance); } catch (Exception e) { Plugin.Instance.Error(e); } }
}
[HarmonyPatch(typeof(OpenOptionsMenuItem), nameof(OpenOptionsMenuItem.OnSubmit))]
internal static class SubmitPatch { static bool Prefix(OpenOptionsMenuItem __instance) => !DojoMenu.Handle(__instance, 0); }
[HarmonyPatch(typeof(OpenOptionsMenuItem), nameof(OpenOptionsMenuItem.OnLeft))]
internal static class LeftPatch { static bool Prefix(OpenOptionsMenuItem __instance) => !DojoMenu.Handle(__instance, -1); }
[HarmonyPatch(typeof(OpenOptionsMenuItem), nameof(OpenOptionsMenuItem.OnRight))]
internal static class RightPatch { static bool Prefix(OpenOptionsMenuItem __instance) => !DojoMenu.Handle(__instance, 1); }
[HarmonyPatch(typeof(OpenOptionsMenuItem), nameof(OpenOptionsMenuItem.UpdateState))]
internal static class LabelPatch { static void Postfix(OpenOptionsMenuItem __instance) => DojoMenu.Refresh(__instance); }
[HarmonyPatch(typeof(MenuPage), nameof(MenuPage.Navigate))]
internal static class NavigatePatch { static void Postfix() => DojoMenu.UpdateHelp(); }
[HarmonyPatch(typeof(MenuUINavigationManager), nameof(MenuUINavigationManager.OnBack))]
internal static class BackPatch
{
    static bool Prefix(UnityEngine.InputSystem.InputAction.CallbackContext context)
    {
        if (!DojoMenu.Open) return true;
        if (context.performed) DojoMenu.Close();
        return false;
    }
}
[HarmonyPatch(typeof(PauseMenu), nameof(PauseMenu.ResumeGame))]
internal static class ResumePatch { static void Prefix() => DojoMenu.Close(); }

internal static class FreeRewardPolicy
{
    internal static bool Active => Plugin.Instance.Enabled.Value &&
        (Plugin.Instance.FreeRefresh.Value || Plugin.Instance.FreePurchases.Value);
}
[HarmonyPatch(typeof(RewardRerolling), nameof(RewardRerolling.RerollButtonPressed))]
internal static class FreeRefreshPatch
{
    static bool Prefix(RewardRerolling __instance)
    {
        if (!FreeRewardPolicy.Active) return true;
        if (!__instance.rerollButton.Interactable) return false;
        __instance.rewardsScreen.RerollButtonPressed();
        SoundEffectsManager.Instance.Play("Reroll");
        __instance.UpdateState(false);
        return false;
    }
}
[HarmonyPatch(typeof(RewardRerolling), nameof(RewardRerolling.UpdateState))]
internal static class FreeRefreshStatePatch
{
    static void Postfix(RewardRerolling __instance, bool allowButtonInteraction)
    {
        if (!FreeRewardPolicy.Active) return;
        __instance.price.Value = 0;
        __instance.rerollButton.Interactable = allowButtonInteraction;
    }
}
[HarmonyPatch(typeof(RewardRerolling), "CoinsUpdate")]
internal static class FreeRefreshCoinsPatch
{
    static void Postfix(RewardRerolling __instance, bool ___allowButtonInteraction)
    {
        if (FreeRewardPolicy.Active) __instance.rerollButton.Interactable = ___allowButtonInteraction;
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.HeroPlayedTile))]
internal static class BerserkNoTurnPatch
{
    static void Postfix(CombatManager __instance, Tile tile)
    {
        try
        {
            if (!Plugin.Instance.Enabled.Value || !Plugin.Instance.BerserkNoTurn.Value || tile?.Attack == null) return;
            if (Plugin.Instance.Level(tile.Attack.AttackEnum.ToString()) < 3) return;
            // The game consumes a turn on the next Update after this callback.
            // Clear only that pending flag; the attack itself and its cooldown remain intact.
            Traverse.Create(__instance).Field("heroPlayedTileInThisUpdate").SetValue(false);
        }
        catch (Exception e) { Plugin.Instance.Error(e); }
    }
}

internal static class Effects
{
    internal static string DisplayName(string weapon) => weapon.ToLowerInvariant() switch
    {
        "swirl" => "旋风斩",
        "arrow" => "箭矢",
        "trap" => "陷阱",
        _ => weapon
    };
    internal static string Description(string weapon, int level)
    {
        if (level == 0) return "原式：完全保持原版威力与冷却。";
        var cooldown = level == 2 ? "冷却 -1" : level == 3 ? "冷却 -3" : "冷却不变";
        return $"{DisplayName(weapon)}的{Plugin.LevelNames[level]}：本次出招伤害 +{level}；{cooldown}。";
    }
}

[HarmonyPatch(typeof(CombatManager), nameof(CombatManager.BeginCombat))]
internal static class CombatBeginPatch
{
    static void Prefix() { Plugin.Instance.GuardUsed = false; }
}

[HarmonyPatch(typeof(CombatSceneManager), nameof(CombatSceneManager.BeginRun))]
internal static class NewRunResetPatch
{
    static void Postfix()
    {
        try
        {
            if (Plugin.Instance.Enabled.Value && !Globals.ContinueRun)
                Plugin.Instance.ResetWeaponLevelsForNewRun();
        }
        catch (Exception e) { Plugin.Instance.Error(e); }
    }
}

[HarmonyPatch(typeof(Hero), nameof(Hero.ReceiveAttack))]
internal static class HeroGuardPatch
{
    static void Prefix(Hero __instance, Hit hit)
    {
        try
        {
            var plugin = Plugin.Instance;
            if (!plugin.Enabled.Value || !plugin.Guard.Value || plugin.GuardUsed || hit == null || __instance.AgentStats == null) return;
            int hp = __instance.AgentStats.HP;
            if (hp > 1 && hit.Damage >= hp)
            {
                hit.Damage = hp - 1;
                plugin.GuardUsed = true;
                plugin.Log("护命触发：保留 1 点生命");
            }
        }
        catch (Exception e) { Plugin.Instance.Error(e); }
    }
}

[HarmonyPatch(typeof(Agent), "ExecuteTile")]
internal static class WeaponEnhancementPatch
{
    static void Prefix(Agent __instance, Tile tile)
    {
        try
        {
            if (!Plugin.Instance.Enabled.Value || !(__instance is Hero) || tile == null || tile.Attack == null) return;
            Plugin.Instance.ApplyNumericToCurrentDeck(tile.Attack.AttackEnum.ToString());
        }
        catch (Exception e) { Plugin.Instance.Error(e); }
    }
}
