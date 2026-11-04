using System;
using System.Runtime.CompilerServices;
using HarmonyLib;
using UnityEngine;
using ShopStuff;

namespace HidenDojo;

internal static class NumericCooldown
{
    internal sealed class State { internal int Discount; }
    static readonly ConditionalWeakTable<Attack, State> States = new();
    internal static void Apply(Tile tile, int level)
    {
        var attack = tile.Attack;
        var state = States.GetOrCreateValue(attack);
        int old = attack.Cooldown;
        int restored = old + state.Discount;
        int remaining = Math.Max(0, old - tile.CooldownCharge);
        int discount = level == 3 ? 3 : level == 2 ? 1 : 0;
        int next = Math.Max(0, restored - discount);
        state.Discount = restored - next;
        attack.Cooldown = next;
        if (next != old)
            tile.CooldownCharge = Math.Max(0, next - Math.Min(remaining + state.Discount, next));
    }
    internal static void Save(Tile tile, TileSaveData data)
    {
        if (!States.TryGetValue(tile.Attack, out var state)) return;
        data.cooldown += state.Discount;
        data.cooldownCharge += state.Discount;
    }
}

[HarmonyPatch(typeof(Tile), nameof(Tile.GetTileSaveData))]
internal static class BaseCooldownSavePatch
{
    static void Postfix(Tile __instance, TileSaveData __result) => NumericCooldown.Save(__instance, __result);
}

internal static class FreePrice
{
    internal static bool Applies(Price price)
    {
        var plugin = Plugin.Instance;
        if (plugin == null || !plugin.Enabled.Value) return false;
        if (plugin.FreePurchases.Value) return true;
        return plugin.FreeRefresh.Value && price.buyable is ShopItemUI item &&
            item.shopItemData is ServiceShopItem service && service.shopServiceEnum == ShopServiceEnum.reroll;
    }
    internal static void RefreshAll()
    {
        foreach (var price in UnityEngine.Object.FindObjectsOfType<Price>())
            AccessTools.Method(typeof(Price), "UpdatePrice").Invoke(price, new object[] { 0 });
    }
}

[HarmonyPatch(typeof(Price), nameof(Price.CanAfford), MethodType.Getter)]
internal static class FreeAffordPatch
{
    static void Postfix(Price __instance, ref bool __result)
    {
        if (FreePrice.Applies(__instance)) __result = true;
    }
}
[HarmonyPatch(typeof(Price), nameof(Price.Pay))]
internal static class FreePayPatch
{
    static bool Prefix(Price __instance) => !FreePrice.Applies(__instance);
}
[HarmonyPatch(typeof(Price), "UpdatePrice")]
internal static class FreePriceDisplayPatch
{
    static void Postfix(Price __instance)
    {
        if (!FreePrice.Applies(__instance) || __instance.Value < 0) return;
        if (__instance.priceTextBlack != null) __instance.priceTextBlack.text = "免费";
        if (__instance.priceTextWhite != null) __instance.priceTextWhite.text = "免费";
    }
}
