using System;
using System.Collections.Generic;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace TradingOverview;

[BepInPlugin(PluginGuid, PluginName, PluginVersion)]
public sealed class Plugin : BaseUnityPlugin
{
    internal const string PluginGuid = "net.tdring.pharaoh.tradingoverview";
    internal const string PluginName = "Trading Overview";
    internal const string PluginVersion = "1.0.0";

    private static ManualLogSource log;
    private static bool warned;

    private void Awake()
    {
        log = Logger;
        try
        {
            Harmony.CreateAndPatchAll(typeof(Plugin));
            log.LogInfo($"{PluginName} {PluginVersion} loaded.");
        }
        catch (Exception exception)
        {
            log.LogError($"{PluginName} could not install its patches: {exception}");
        }
    }

    [HarmonyPatch(typeof(CommerceRow), "UpdateData")]
    [HarmonyPostfix]
    private static void CommerceRowUpdateDataPostfix(
        CommerceRow __instance,
        Good good,
        GoodData goodData,
        TextMeshProUGUI ____quantityText)
    {
        try
        {
            var totals = GetTotals(good, goodData);
            var label = GetOrCreateLabel(__instance, ____quantityText);
            label.text = $"Exported: {totals.Exported:N0} / {totals.MaxExport:N0}    Imported: {totals.Imported:N0} / {totals.MaxImport:N0}";
        }
        catch (Exception exception)
        {
            if (!warned)
            {
                warned = true;
                log?.LogWarning($"Trading Overview could not update a Commerce row; the base UI is unchanged: {exception}");
            }
        }
    }

    private static TradeTotals GetTotals(Good good, GoodData goodData)
    {
        var routes = new List<RouteTrade>();
        var commerce = CommerceManager.Instance;
        var level = GlobalAccessor.Level;

        if (commerce != null && level != null)
        {
            foreach (var city in level.MapCityStates.Values)
            {
                if (city == null || city.Status == CityStatus.MyCity || !city.CanTrade || commerce.GetTradeRouteForCity(city) == null)
                {
                    continue;
                }

                foreach (var merchandise in city.TradeMerchandises)
                {
                    if (merchandise == null || merchandise.Good != good)
                    {
                        continue;
                    }

                    var volume = merchandise.TradeVolume;
                    if (commerce.ExportVolumeModifier > 0)
                    {
                        volume = volume.GetIncrease();
                    }
                    else if (commerce.ExportVolumeModifier < 0)
                    {
                        volume = volume.GetDecrease();
                    }

                    routes.Add(new RouteTrade(
                        good.ToString(),
                        merchandise.TradeMode == TradeMode.CityExport,
                        (int)volume));
                }
            }
        }

        return TradeTotals.Calculate(
            good.ToString(),
            goodData?.ThisYearImportedQuantity ?? 0,
            goodData?.ThisYearExportedQuantity ?? 0,
            Merchandise.IsIndividualUnit(good),
            routes);
    }

    private static TextMeshProUGUI GetOrCreateLabel(CommerceRow row, TextMeshProUGUI quantityText)
    {
        const string labelName = "TradingOverview.TradeTotals";
        var container = quantityText.transform.parent;
        var existing = container.Find(labelName);
        if (existing != null)
        {
            return existing.GetComponent<TextMeshProUGUI>();
        }

        var label = Instantiate(quantityText, container);
        label.name = labelName;
        label.text = string.Empty;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Overflow;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.MidlineLeft;
        label.enableAutoSizing = true;
        label.fontSizeMin = 10f;

        var labelTransform = label.rectTransform;
        labelTransform.SetSiblingIndex(quantityText.rectTransform.GetSiblingIndex() + 1);
        labelTransform.sizeDelta = new Vector2(360f, labelTransform.sizeDelta.y);

        var layout = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
        layout.minWidth = 300f;
        layout.preferredWidth = 360f;
        layout.flexibleWidth = 0f;
        return label;
    }
}
