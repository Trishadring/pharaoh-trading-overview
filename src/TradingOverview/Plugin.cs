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
    internal const string PluginVersion = "1.3.0";

    private static ManualLogSource log;
    private static bool warned;
    private static bool typographyLogged;
    private static readonly HashSet<int> AdjustedControls = new HashSet<int>();

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
        TMP_Dropdown ____dropdownStatus,
        Button ____openTradeButton)
    {
        try
        {
            var totals = GetTotals(good, goodData);
            var exported = GetOrCreateColumn(__instance, ____quantityText, ____importText, ExportLabelName, 0.36f, 0.44f);
            var imported = GetOrCreateColumn(__instance, ____quantityText, ____importText, ImportLabelName, 0.44f, 0.52f);
            exported.text = totals.CanExport
                ? $"Exp {CompactNumber.Format(totals.Exported)} / {CompactNumber.Format(totals.MaxExport)}"
                : string.Empty;
            imported.text = totals.CanImport
                ? $"Imp {CompactNumber.Format(totals.Imported)} / {CompactNumber.Format(totals.MaxImport)}"
                : string.Empty;
            CompactStatusControl(____dropdownStatus?.transform as RectTransform, 105f);
            CompactStatusControl(____openTradeButton?.transform as RectTransform, 105f);
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
            var exported = firstRow.transform.Find(ExportLabelName) as RectTransform;
            var imported = firstRow.transform.Find(ImportLabelName) as RectTransform;
            if (status == null || exported == null || imported == null)
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

            MoveHeader(header.rectTransform, 105f);
            CreateOrUpdateHeader(header, TradeVolumeHeaderName, "Trade Volume (Year / Max)", exported, imported);
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

    private static void CompactStatusControl(RectTransform control, float shift)
    {
        if (control == null || !AdjustedControls.Add(control.GetInstanceID()))
        {
            return;
        }

        var width = control.rect.width;
        control.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Math.Max(140f, width - (shift * 2f)));
        control.anchoredPosition += new Vector2(shift, 0f);

        foreach (var text in control.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            text.enableAutoSizing = true;
            text.fontSizeMin = 9f;
            text.fontSizeMax = Math.Min(text.fontSizeMax, 14f);
        }
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
        RectTransform firstColumn,
        RectTransform lastColumn)
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
        var centerX = (firstColumn.position.x + lastColumn.position.x) / 2f;
        header.transform.position = new Vector3(centerX, template.transform.position.y, template.transform.position.z);
        header.rectTransform.SetSizeWithCurrentAnchors(
            RectTransform.Axis.Horizontal,
            firstColumn.rect.width + lastColumn.rect.width);
    }

    private static void MoveHeader(RectTransform header, float amount)
    {
        if (!AdjustedControls.Add(header.GetInstanceID()))
        {
            return;
        }

        header.anchoredPosition += new Vector2(amount, 0f);
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
