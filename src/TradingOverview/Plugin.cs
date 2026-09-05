using System;
using System.Collections;
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
    internal const string PluginVersion = "1.7.0-rc.1";

    private static ManualLogSource log;
    private static bool warned;
    private static bool layoutScheduled;
    private static Plugin instance;

    private const string ExportLabelName = "TradingOverview.Exported";
    private const string ImportLabelName = "TradingOverview.Imported";
    private const string TradeVolumeHeaderName = "TradingOverview.TradeVolumeHeader";

    private void Awake()
    {
        instance = this;
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
            ScheduleLayout(__instance, ____rowContainer, ____rowByGood, header);
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
            ? $"Ex {CompactNumber.Format(totals.Exported)} / {CompactNumber.Format(totals.MaxExport)}"
            : string.Empty;
        var imported = totals.CanImport
            ? $"In {CompactNumber.Format(totals.Imported)} / {CompactNumber.Format(totals.MaxImport)}"
            : string.Empty;

        if (exported.Length == 0)
        {
            return imported;
        }

        return imported.Length == 0 ? exported : exported + "\n" + imported;
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

    private static List<TextMeshProUGUI> FindPriceHeaders(
        CommerceOverseer overseer,
        Transform rowContainer,
        TextMeshProUGUI statusHeader)
    {
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
        if (priceHeaders.Count > 2)
        {
            priceHeaders.RemoveRange(0, priceHeaders.Count - 2);
        }

        return priceHeaders;
    }

    private static void PositionInRow(RectTransform row, RectTransform element, float start, float end)
    {
        var layout = element.GetComponent<LayoutElement>() ?? element.gameObject.AddComponent<LayoutElement>();
        layout.ignoreLayout = true;
        var corners = new Vector3[4];
        row.GetWorldCorners(corners);
        var center = Mathf.Lerp(corners[0].x, corners[3].x, (start + end) / 2f);
        element.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, row.rect.width * (end - start));
        element.position = new Vector3(center, element.position.y, element.position.z);
    }

    private static void ScheduleLayout(
        CommerceOverseer overseer,
        Transform rowContainer,
        Dictionary<Good, CommerceRow> rows,
        TextMeshProUGUI statusHeader)
    {
        if (layoutScheduled || instance == null)
        {
            return;
        }

        layoutScheduled = true;
        instance.StartCoroutine(ApplyLayoutAfterFrame(overseer, rowContainer, rows, statusHeader));
    }

    private static IEnumerator ApplyLayoutAfterFrame(
        CommerceOverseer overseer,
        Transform rowContainer,
        Dictionary<Good, CommerceRow> rows,
        TextMeshProUGUI statusHeader)
    {
        yield return new WaitForEndOfFrame();
        Canvas.ForceUpdateCanvases();

        CommerceRow firstRow = null;
        foreach (var row in rows.Values)
        {
            if (row == null)
            {
                continue;
            }

            firstRow = firstRow ?? row;
            var rowRect = row.transform as RectTransform;
            var tradeVolume = row.transform.Find(ExportLabelName) as RectTransform;
            var importText = AccessTools.Field(typeof(CommerceRow), "_importText")?.GetValue(row) as TextMeshProUGUI;
            var exportText = AccessTools.Field(typeof(CommerceRow), "_exportText")?.GetValue(row) as TextMeshProUGUI;
            if (rowRect == null || tradeVolume == null || importText == null || exportText == null)
            {
                continue;
            }

            PositionInRow(rowRect, tradeVolume, 0.73f, 0.82f);
            PositionInRow(rowRect, importText.rectTransform, 0.82f, 0.91f);
            PositionInRow(rowRect, exportText.rectTransform, 0.91f, 1f);
        }

        if (firstRow != null)
        {
            var rowRect = firstRow.transform as RectTransform;
            var tradeHeader = statusHeader.transform.parent.Find(TradeVolumeHeaderName)?.GetComponent<TextMeshProUGUI>();
            var priceHeaders = FindPriceHeaders(overseer, rowContainer, statusHeader);
            if (rowRect != null && tradeHeader != null)
            {
                PositionInRow(rowRect, tradeHeader.rectTransform, 0.73f, 0.82f);
            }
            if (rowRect != null && priceHeaders.Count == 2)
            {
                PositionInRow(rowRect, priceHeaders[0].rectTransform, 0.82f, 0.91f);
                PositionInRow(rowRect, priceHeaders[1].rectTransform, 0.91f, 1f);
            }
        }

        layoutScheduled = false;
    }
}
