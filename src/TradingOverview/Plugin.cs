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
    internal const string PluginVersion = "1.5.0";

    private static ManualLogSource log;
    private static bool warned;
    private static bool typographyLogged;

    private const string ExportLabelName = "TradingOverview.Exported";
    private const string ImportLabelName = "TradingOverview.Imported";
    private const string TradeVolumeHeaderName = "TradingOverview.TradeVolumeHeader";

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
        TextMeshProUGUI ____quantityText,
        TextMeshProUGUI ____importText,
        TextMeshProUGUI ____exportText)
    {
        try
        {
            var totals = GetTotals(good, goodData);
            var tradeVolume = GetOrCreateColumn(__instance, ____quantityText, ____importText, ExportLabelName, 0.72f, 0.82f);
            tradeVolume.text = FormatTradeVolume(totals);

            var obsoleteImportColumn = __instance.transform.Find(ImportLabelName);
            if (obsoleteImportColumn != null)
            {
                obsoleteImportColumn.gameObject.SetActive(false);
            }

            PinColumn(____importText.rectTransform, 0.82f, 0.91f);
            PinColumn(____exportText.rectTransform, 0.91f, 1f);
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

    [HarmonyPatch(typeof(CommerceOverseer), "Refresh")]
    [HarmonyPostfix]
    private static void CommerceOverseerRefreshPostfix(
        CommerceOverseer __instance,
        Transform ____rowContainer,
        Dictionary<Good, CommerceRow> ____rowByGood)
    {
        try
        {
            CommerceRow firstRow = null;
            foreach (var row in ____rowByGood.Values)
            {
                if (row != null && row.gameObject.activeSelf)
                {
                    firstRow = row;
                    break;
                }
            }

            if (firstRow == null)
            {
                return;
            }

            LogTypography(__instance);

            var status = firstRow.TradeRuleSelector?.transform as RectTransform;
            var tradeVolume = firstRow.transform.Find(ExportLabelName) as RectTransform;
            if (status == null || tradeVolume == null)
            {
                return;
            }

            var header = FindStatusHeader(__instance, ____rowContainer, status);
            if (header == null)
            {
                return;
            }

            foreach (var text in __instance.GetComponentsInChildren<TextMeshProUGUI>(true))
            {
                if (!text.transform.IsChildOf(____rowContainer) && Math.Abs(text.transform.position.y - header.transform.position.y) < 5f)
                {
                    text.enableAutoSizing = false;
                    text.fontSize = 16f;
                }
            }

            CreateOrUpdateHeader(header, TradeVolumeHeaderName, "Trade Volume (Year / Max)", tradeVolume);
            AlignPriceHeaders(__instance, ____rowContainer, header, firstRow);
        }
        catch (Exception exception)
        {
            if (!warned)
            {
                warned = true;
                log?.LogWarning($"Trading Overview could not update the Commerce headers; the base UI is unchanged: {exception}");
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

    private static TextMeshProUGUI GetOrCreateColumn(
        CommerceRow row,
        TextMeshProUGUI quantityText,
        TextMeshProUGUI priceText,
        string name,
        float anchorMin,
        float anchorMax)
    {
        var existing = row.transform.Find(name);
        if (existing != null)
        {
            return existing.GetComponent<TextMeshProUGUI>();
        }

        var label = Instantiate(quantityText, row.transform);
        label.name = name;
        label.text = string.Empty;
        label.enableWordWrapping = false;
        label.overflowMode = TextOverflowModes.Ellipsis;
        label.raycastTarget = false;
        label.alignment = TextAlignmentOptions.Midline;
        label.enableAutoSizing = priceText.enableAutoSizing;
        label.fontSize = priceText.fontSize;
        label.fontSizeMin = priceText.fontSizeMin;
        label.fontSizeMax = priceText.fontSizeMax;
        label.fontStyle = priceText.fontStyle;

        var labelTransform = label.rectTransform;
        labelTransform.anchorMin = new Vector2(anchorMin, 0f);
        labelTransform.anchorMax = new Vector2(anchorMax, 1f);
        labelTransform.pivot = new Vector2(0.5f, 0.5f);
        labelTransform.offsetMin = new Vector2(2f, 0f);
        labelTransform.offsetMax = new Vector2(-2f, 0f);

        var layout = label.GetComponent<LayoutElement>() ?? label.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
        return label;
    }

    private static string FormatTradeVolume(TradeTotals totals)
    {
        var exported = totals.CanExport
            ? $"Exp {CompactNumber.Format(totals.Exported)} / {CompactNumber.Format(totals.MaxExport)}"
            : string.Empty;
        var imported = totals.CanImport
            ? $"Imp {CompactNumber.Format(totals.Imported)} / {CompactNumber.Format(totals.MaxImport)}"
            : string.Empty;

        if (exported.Length == 0)
        {
            return imported;
        }

        return imported.Length == 0 ? exported : exported + "\n" + imported;
    }

    private static void PinColumn(RectTransform column, float anchorMin, float anchorMax)
    {
        column.anchorMin = new Vector2(anchorMin, 0f);
        column.anchorMax = new Vector2(anchorMax, 1f);
        column.pivot = new Vector2(0.5f, 0.5f);
        column.offsetMin = new Vector2(2f, 0f);
        column.offsetMax = new Vector2(-2f, 0f);
        var layout = column.GetComponent<LayoutElement>() ?? column.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
    }

    private static TextMeshProUGUI FindStatusHeader(
        CommerceOverseer overseer,
        Transform rowContainer,
        RectTransform status)
    {
        foreach (var text in overseer.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (!text.transform.IsChildOf(rowContainer)
                && string.Equals(text.text?.Trim(), "Status", StringComparison.OrdinalIgnoreCase))
            {
                return text;
            }
        }

        TextMeshProUGUI closest = null;
        var distance = float.MaxValue;
        foreach (var text in overseer.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name.StartsWith("TradingOverview.", StringComparison.Ordinal)
                || text.transform.IsChildOf(rowContainer)
                || text.transform.position.y <= status.position.y)
            {
                continue;
            }

            var verticalDistance = text.transform.position.y - status.position.y;
            if (verticalDistance > 150f)
            {
                continue;
            }

            var candidateDistance = Math.Abs(text.transform.position.x - status.position.x);
            if (candidateDistance < distance)
            {
                distance = candidateDistance;
                closest = text;
            }
        }

        return closest;
    }

    private static void CreateOrUpdateHeader(
        TextMeshProUGUI template,
        string name,
        string text,
        RectTransform column)
    {
        var parent = template.transform.parent;
        var existing = parent.Find(name)?.GetComponent<TextMeshProUGUI>();
        var header = existing ?? Instantiate(template, parent);
        header.name = name;
        header.text = text;
        header.fontSize = 16f;
        header.enableAutoSizing = false;
        header.alignment = TextAlignmentOptions.Midline;
        header.raycastTarget = false;
        var layout = header.GetComponent<LayoutElement>() ?? header.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
        header.transform.position = new Vector3(column.position.x, template.transform.position.y, template.transform.position.z);
        header.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, column.rect.width);
    }

    private static void AlignPriceHeaders(
        CommerceOverseer overseer,
        Transform rowContainer,
        TextMeshProUGUI statusHeader,
        CommerceRow row)
    {
        var importText = AccessTools.Field(typeof(CommerceRow), "_importText")?.GetValue(row) as TextMeshProUGUI;
        var exportText = AccessTools.Field(typeof(CommerceRow), "_exportText")?.GetValue(row) as TextMeshProUGUI;
        if (importText == null || exportText == null)
        {
            return;
        }

        var priceHeaders = new List<TextMeshProUGUI>();
        foreach (var text in overseer.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (text.name.StartsWith("TradingOverview.", StringComparison.Ordinal)
                || text.transform.IsChildOf(rowContainer)
                || text.transform.position.x <= statusHeader.transform.position.x
                || Math.Abs(text.transform.position.y - statusHeader.transform.position.y) >= 5f)
            {
                continue;
            }

            priceHeaders.Add(text);
        }

        priceHeaders.Sort((left, right) => left.transform.position.x.CompareTo(right.transform.position.x));
        if (priceHeaders.Count >= 2)
        {
            AlignHeader(priceHeaders[priceHeaders.Count - 2].rectTransform, importText.rectTransform);
            AlignHeader(priceHeaders[priceHeaders.Count - 1].rectTransform, exportText.rectTransform);
        }
    }

    private static void AlignHeader(RectTransform header, RectTransform column)
    {
        var layout = header.GetComponent<LayoutElement>() ?? header.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
        header.position = new Vector3(column.position.x, header.position.y, header.position.z);
        header.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, column.rect.width);
    }

    private static void LogTypography(CommerceOverseer overseer)
    {
        if (typographyLogged)
        {
            return;
        }

        typographyLogged = true;
        log?.LogInfo("Commerce Overseer typography after all refresh patches:");
        foreach (var text in overseer.GetComponentsInChildren<TextMeshProUGUI>(false))
        {
            var value = text.text?.Replace('\n', ' ').Replace('\r', ' ') ?? string.Empty;
            log?.LogInfo(
                $"Typography name='{text.name}', text='{value}', fontSize={text.fontSize:0.##}, "
                + $"autoSize={text.enableAutoSizing}, min={text.fontSizeMin:0.##}, max={text.fontSizeMax:0.##}");
        }
    }
}
