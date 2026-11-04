using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using HarmonyLib;
using TMPro;
using UINavigation;
using UnityEngine;
using UnityEngine.Localization.Components;
using UnityEngine.UI;

namespace HidenDojo;

// Kept outside shopItemUIs: this control is never sold, rerolled, or serialized as merchandise.
internal sealed class ShopRefreshControl : MonoBehaviour
{
    internal MyButton Button = null!;
    ShopRoom room = null!;
    MyButton leave = null!;
    bool busy;
    bool ended;
    internal bool Available => !ended && Button != null && Button.gameObject.activeInHierarchy;
    bool Policy => Plugin.Instance.Enabled.Value && Plugin.Instance.FreeRefresh.Value;
    bool CanRefresh => Available && Policy && !busy && Time.timeScale > 0 &&
        CombatSceneManager.Instance.Room == room && leave.IsOpen && leave.Interactable &&
        room.Shop.Targets.OfType<ShopItemUI>().All(item => item.Interactable);

    internal void Initialize(ShopRoom owner, MyButton source)
    {
        room = owner;
        leave = source;
        // Clone under an inactive parent so copied input binders cannot register a Leave action.
        var staging = new GameObject("HidenDojoButtonStaging");
        staging.SetActive(false);
        Button = UnityEngine.Object.Instantiate(source, staging.transform, false);
        Button.name = "HidenDojoRefreshShop";
        foreach (var binder in Button.GetComponentsInChildren<InputActionButtonBinder>(true))
            UnityEngine.Object.DestroyImmediate(binder);
        foreach (var localizer in Button.GetComponentsInChildren<LocalizeStringEvent>(true))
            UnityEngine.Object.DestroyImmediate(localizer);
        Traverse.Create(Button).Field("notifyHighlighted").SetValue(Array.Empty<GameObject>());
        Traverse.Create(Button).Field("infoBoxActivator").SetValue(null);
        var click = Button.GetComponent<Button>();
        click.onClick = new Button.ButtonClickedEvent();
        click.onClick.AddListener(RequestRefresh);
        // Hide the copied Leave key glyph; selection + A uses the game's shop navigation.
        foreach (var img in Button.GetComponentsInChildren<Image>(true))
            if (img.gameObject != Button.gameObject) img.enabled = false;
        Button.transform.SetParent(source.transform.parent, false);
        var rect = (RectTransform)Button.transform;
        var sourceRect = (RectTransform)source.transform;
        rect.anchoredPosition = sourceRect.anchoredPosition +
            new Vector2(0, sourceRect.rect.height + 14);
        rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Max(120, sourceRect.rect.width));
        var originalLabel = Traverse.Create(Button).Field("text").GetValue<TextMeshProUGUI>();
        originalLabel.enabled = false;
        var sourceLabel = Traverse.Create(source).Field("text").GetValue<TextMeshProUGUI>();
        var label = UnityEngine.Object.Instantiate(sourceLabel, Button.transform, false);
        label.name = "HidenDojoRefreshLabel";
        foreach (var localizer in label.GetComponents<LocalizeStringEvent>())
            UnityEngine.Object.DestroyImmediate(localizer);
        label.gameObject.SetActive(true);
        label.enabled = true;
        label.color = new Color(.89f, .87f, .80f, 1f);
        label.alignment = TextAlignmentOptions.Center;
        label.raycastTarget = false;
        label.enableAutoSizing = false;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.rectTransform.anchorMin = Vector2.zero;
        label.rectTransform.anchorMax = Vector2.one;
        label.rectTransform.offsetMin = new Vector2(4, 0);
        label.rectTransform.offsetMax = new Vector2(-12, 0);
        Traverse.Create(Button).Field("text").SetValue(label);
        Button.gameObject.SetActive(true);
        Button.SetText("刷新商店 · 免费");
        Button.Appear();
        Button.AppearAnimationOver();
        UnityEngine.Object.Destroy(staging);
        Plugin.Instance.Log("商店固定刷新按钮已创建；商品最右侧继续向右选择，A 刷新");
        Plugin.Instance.Log($"刷新按钮文字区域：{label.rectTransform.rect}，字体：{label.font.name} size={label.fontSize} scale={label.transform.localScale}，可用字体：{string.Join(",", Resources.FindObjectsOfTypeAll<TMP_FontAsset>().Select(f => f.name))}");
    }

    void Update()
    {
        if (Button == null) return;
        Button.Interactable = CanRefresh;
        Button.SetText(busy ? "正在刷新…" : Policy ? "刷新商店 · 免费" : "刷新商店 · 已关闭");
    }

    void RequestRefresh()
    {
        if (!CanRefresh) return;
        busy = true;
        StartCoroutine(Refresh());
    }

    IEnumerator Refresh()
    {
        var shop = room.Shop;
        bool wasLeaveInteractable = leave.Interactable;
        leave.Interactable = false;
        Button.Interactable = false;
        shop.SelectedTarget?.Deselect();
        shop.SelectedTarget = null;
        foreach (var item in shop.Targets.OfType<ShopItemUI>()) item.Interactable = false;
        EventsManager.Instance.SetCurrentUINavigationScreenInteractable.Invoke(false);
        try
        {
            // Reuse the original coroutine, including its stock pools, animation and run-save event.
            var routine = (IEnumerator)AccessTools.Method(typeof(Shop), "RerollShop").Invoke(shop, null);
            yield return StartCoroutine(routine);
            Plugin.Instance.Log("固定按钮已免费刷新商店，商品数=" + shop.Targets.OfType<ShopItemUI>().Count());
        }
        finally
        {
            busy = false;
            if (!ended && shop != null && leave != null)
            {
                foreach (var item in shop.Targets.OfType<ShopItemUI>()) item.Interactable = true;
                leave.Interactable = wasLeaveInteractable;
                if (Globals.Options.DiscreteUINavigation) UINavigationHelper.SelectNewTarget(shop, Button);
                EventsManager.Instance.SetCurrentUINavigationScreenInteractable.Invoke(true);
            }
        }
    }

    internal void End()
    {
        ended = true;
        if (Button != null) Button.gameObject.SetActive(false);
    }
    void OnDestroy()
    {
        if (Button != null) UnityEngine.Object.Destroy(Button.gameObject);
    }
    internal static ShopRefreshControl? For(Shop shop)
    {
        var room = CombatSceneManager.Instance?.Room as ShopRoom;
        if (room == null || room.Shop != shop) return null;
        var control = room.GetComponent<ShopRefreshControl>();
        return control != null && control.Available ? control : null;
    }
}

[HarmonyPatch(typeof(ShopRoom), nameof(ShopRoom.Begin))]
internal static class CreateShopRefreshButton
{
    static void Postfix(ShopRoom __instance, MyButton ___goButton)
    {
        try
        {
            if (__instance.GetComponent<ShopRefreshControl>() == null)
                __instance.gameObject.AddComponent<ShopRefreshControl>().Initialize(__instance, ___goButton);
        }
        catch (Exception e) { Plugin.Instance.Error(e); }
    }
}
[HarmonyPatch(typeof(ShopRoom), nameof(ShopRoom.End))]
internal static class HideShopRefreshButton
{
    static void Prefix(ShopRoom __instance) => __instance.GetComponent<ShopRefreshControl>()?.End();
}
[HarmonyPatch(typeof(Shop), nameof(Shop.Targets), MethodType.Getter)]
internal static class ShopRefreshNavigationTarget
{
    static void Postfix(Shop __instance, ref List<INavigationTarget> __result)
    {
        var control = ShopRefreshControl.For(__instance);
        if (control != null) __result.Add(control.Button);
    }
}
[HarmonyPatch(typeof(Shop), nameof(Shop.CanBeNavigatedTo), MethodType.Getter)]
internal static class EmptyShopRefreshNavigation
{
    static void Postfix(Shop __instance, ref bool __result)
    {
        if (ShopRefreshControl.For(__instance) != null) __result = true;
    }
}
[HarmonyPatch(typeof(Shop), nameof(Shop.OnEntry))]
internal static class ShopRefreshEntry
{
    static bool Prefix(Shop __instance)
    {
        if (ShopRefreshControl.For(__instance) == null) return true;
        var targets = __instance.Targets;
        var selected = targets.Contains(__instance.SelectedTarget) ? __instance.SelectedTarget : targets[0];
        UINavigationHelper.SelectNewTarget(__instance, selected);
        return false;
    }
}
[HarmonyPatch(typeof(Shop), nameof(Shop.Navigate))]
internal static class ShopRefreshNavigation
{
    static bool Prefix(Shop __instance, NavigationDirection navigationDirection, ref INavigationGroup __result)
    {
        if (ShopRefreshControl.For(__instance) == null) return true;
        var targets = __instance.Targets;
        int index = targets.IndexOf(__instance.SelectedTarget);
        if (index < 0) index = 0;
        if (navigationDirection == NavigationDirection.Left) index--;
        if (navigationDirection == NavigationDirection.Right) index++;
        if (index < 0 || navigationDirection == NavigationDirection.Up || navigationDirection == NavigationDirection.Down)
            __result = UINavigationHelper.HandleOutOfGroupNavigation(__instance, navigationDirection);
        else
        {
            UINavigationHelper.SelectNewTarget(__instance, targets[Mathf.Clamp(index, 0, targets.Count - 1)]);
            __result = __instance;
        }
        return false;
    }
}
