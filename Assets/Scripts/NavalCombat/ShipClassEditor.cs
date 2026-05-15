using UnityEngine;
using UnityEngine.UIElements;
using System.Collections.Generic;
using System.Xml.Serialization;
using System.Xml;
using System.IO;
using System.Linq;
using Unity.Properties;

using NavalCombatCore;
using CoreUtils;
using System;
using YYZ;

public class BatteryFigurePoint
{
    public float distanceYards;
    public float verticalPenetrationInches;
    public float horizontalPenetrationInches;
    public float fireControlValue;
}

[UxmlElement]
public partial class BatteryPenetrationFireControlChart : VisualElement
{
    const float LeftPadding = 42f;
    const float RightPadding = 42f;
    const float TopPadding = 20f;
    const float BottomPadding = 34f;

    readonly VisualElement labelLayer = new();
    List<BatteryFigurePoint> points = new();
    float? rangeYards;
    float? mainBeltEffectiveInches;

    static readonly Color VerticalPenetrationColor = new(0.75f, 0.2f, 0.18f, 1f);
    static readonly Color HorizontalPenetrationColor = new(0.16f, 0.42f, 0.78f, 1f);
    static readonly Color FireControlColor = new(0.12f, 0.6f, 0.24f, 1f);
    static readonly Color GridColor = new(0f, 0f, 0f, 0.18f);
    static readonly Color RangeLineColor = new(0.85f, 0.85f, 0.85f, 0.9f);
    static readonly Color ImmuneZoneColor = new(0.0f, 0.72f, 0.72f, 1f);
    static readonly Color VulnerableZoneColor = new(0.9f, 0.6f, 0.12f, 1f);

    public BatteryPenetrationFireControlChart()
    {
        style.position = Position.Relative;

        labelLayer.style.position = Position.Absolute;
        labelLayer.style.left = 0;
        labelLayer.style.top = 0;
        labelLayer.style.right = 0;
        labelLayer.style.bottom = 0;
        labelLayer.pickingMode = PickingMode.Ignore;
        Add(labelLayer);

        generateVisualContent += OnGenerateVisualContent;
        RegisterCallback<GeometryChangedEvent>(_ => RebuildLabels());
    }

    public void SetPoints(IEnumerable<BatteryFigurePoint> newPoints)
    {
        points = newPoints?.Where(point => point != null).OrderBy(point => point.distanceYards).ToList() ?? new();
        RebuildLabels();
        MarkDirtyRepaint();
    }

    public void SetRangeYards(float? value)
    {
        rangeYards = value;
        RebuildLabels();
        MarkDirtyRepaint();
    }

    public void SetMainBeltEffectiveInches(float? value)
    {
        mainBeltEffectiveInches = value.HasValue && value.Value > 0f ? value.Value : null;
        RebuildLabels();
        MarkDirtyRepaint();
    }

    void OnGenerateVisualContent(MeshGenerationContext context)
    {
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0)
            return;

        var painter = context.painter2D;
        painter.lineWidth = 1f;
        painter.lineCap = LineCap.Butt;
        painter.strokeColor = Color.black;

        DrawAxes(painter, chartRect);

        if (points.Count == 0)
            return;

        var leftMax = GetLeftAxisMax();
        var rightMax = GetRightAxisMax();
        var (minDistance, maxDistance) = GetDistanceBounds();

        DrawRangeLine(painter, chartRect, minDistance, maxDistance);
        DrawMainBeltEffectiveLine(painter, chartRect, minDistance, maxDistance, leftMax);

        DrawSeries(
            painter,
            chartRect,
            points.Select(point => MapPoint(chartRect, point.distanceYards, point.verticalPenetrationInches, minDistance, maxDistance, leftMax)),
            VerticalPenetrationColor);
        DrawSeries(
            painter,
            chartRect,
            points.Select(point => MapPoint(chartRect, point.distanceYards, point.horizontalPenetrationInches, minDistance, maxDistance, leftMax)),
            HorizontalPenetrationColor);
        DrawSeries(
            painter,
            chartRect,
            points.Select(point => MapPoint(chartRect, point.distanceYards, point.fireControlValue, minDistance, maxDistance, rightMax)),
            FireControlColor);
    }

    void DrawAxes(Painter2D painter, Rect chartRect)
    {
        painter.strokeColor = Color.black;
        painter.BeginPath();
        painter.MoveTo(new Vector2(chartRect.xMin, chartRect.yMin));
        painter.LineTo(new Vector2(chartRect.xMin, chartRect.yMax));
        painter.LineTo(new Vector2(chartRect.xMax, chartRect.yMax));
        painter.LineTo(new Vector2(chartRect.xMax, chartRect.yMin));
        painter.Stroke();

        var leftMax = GetLeftAxisMax();
        var rightMax = GetRightAxisMax();
        foreach (var ratio in GetTickRatios())
        {
            var y = Mathf.Lerp(chartRect.yMax, chartRect.yMin, ratio);
            painter.strokeColor = GridColor;
            painter.BeginPath();
            painter.MoveTo(new Vector2(chartRect.xMin, y));
            painter.LineTo(new Vector2(chartRect.xMax, y));
            painter.Stroke();

            painter.strokeColor = Color.black;
            painter.BeginPath();
            painter.MoveTo(new Vector2(chartRect.xMin - 4f, y));
            painter.LineTo(new Vector2(chartRect.xMin, y));
            painter.MoveTo(new Vector2(chartRect.xMax, y));
            painter.LineTo(new Vector2(chartRect.xMax + 4f, y));
            painter.Stroke();
        }

        if (points.Count == 0)
            return;

        var (minDistance, maxDistance) = GetDistanceBounds();

        foreach (var tickDistance in GetDistanceLabelDistances())
        {
            var x = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(minDistance, maxDistance, tickDistance));
            painter.strokeColor = Color.black;
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, chartRect.yMax));
            painter.LineTo(new Vector2(x, chartRect.yMax + 4f));
            painter.Stroke();
        }
    }

    void RebuildLabels()
    {
        labelLayer.Clear();
        var chartRect = GetChartRect();
        if (chartRect.width <= 0 || chartRect.height <= 0)
            return;

        labelLayer.Add(BuildOverlayLabel(
            "(in)",
            0f,
            2f,
            LeftPadding - 6f,
            14f,
            TextAnchor.UpperRight));
        labelLayer.Add(BuildOverlayLabel(
            "(fc)",
            contentRect.width - RightPadding + 6f,
            2f,
            RightPadding - 6f,
            14f,
            TextAnchor.UpperLeft));
        if (rangeYards.HasValue)
        {
            var (rangeMinDistance, rangeMaxDistance) = GetDistanceBounds();
            var rangeX = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(rangeMinDistance, rangeMaxDistance, rangeYards.Value));
            labelLayer.Add(BuildOverlayLabel(
                rangeYards.Value.ToString("0"),
                rangeX - 32f,
                2f,
                64f,
                14f,
                TextAnchor.UpperCenter,
                RangeLineColor));
        }

        var leftMax = GetLeftAxisMax();
        var rightMax = GetRightAxisMax();
        foreach (var ratio in GetTickRatios())
        {
            var y = Mathf.Lerp(chartRect.yMax, chartRect.yMin, ratio) - 8f;
            labelLayer.Add(BuildOverlayLabel(
                Mathf.Lerp(0f, leftMax, ratio).ToString("0.0"),
                0f,
                y,
                LeftPadding - 6f,
                16f,
                TextAnchor.MiddleRight));
            labelLayer.Add(BuildOverlayLabel(
                Mathf.Lerp(0f, rightMax, ratio).ToString("0.0"),
                chartRect.xMax + 6f,
                y,
                RightPadding - 6f,
                16f,
                TextAnchor.MiddleLeft));
        }

        if (points.Count == 0)
            return;

        var (minDistance, maxDistance) = GetDistanceBounds();

        var distanceLabelWidth = GetDistanceLabelWidth(chartRect.width);
        foreach (var tickDistance in GetDistanceLabelDistances())
        {
            var x = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(minDistance, maxDistance, tickDistance));
            labelLayer.Add(BuildOverlayLabel(
                tickDistance.ToString("0"),
                x - distanceLabelWidth * 0.5f,
                chartRect.yMax + 6f,
                distanceLabelWidth,
                18f,
                TextAnchor.UpperCenter));
        }
    }

    Rect GetChartRect()
    {
        var width = Mathf.Max(0f, contentRect.width - LeftPadding - RightPadding);
        var height = Mathf.Max(0f, contentRect.height - TopPadding - BottomPadding);
        return new Rect(LeftPadding, TopPadding, width, height);
    }

    float GetLeftAxisMax()
    {
        var maxValue = points.Count == 0
            ? 1f
            : Mathf.Max(
                points.Max(point => point.verticalPenetrationInches),
                points.Max(point => point.horizontalPenetrationInches));
        if (mainBeltEffectiveInches.HasValue)
            maxValue = Mathf.Max(maxValue, mainBeltEffectiveInches.Value);
        return Mathf.Max(1f, Mathf.Ceil(maxValue));
    }

    float GetRightAxisMax()
    {
        var maxValue = points.Count == 0 ? 1f : points.Max(point => point.fireControlValue);
        return Mathf.Max(1f, Mathf.Ceil(maxValue));
    }

    (float minDistance, float maxDistance) GetDistanceBounds()
    {
        if (points.Count == 0)
            return (0f, 1f);

        var minDistance = points.Min(point => point.distanceYards);
        var maxDistance = points.Max(point => point.distanceYards);
        if (rangeYards.HasValue)
        {
            minDistance = Mathf.Min(minDistance, rangeYards.Value);
            maxDistance = Mathf.Max(maxDistance, rangeYards.Value);
        }

        if (Mathf.Approximately(minDistance, maxDistance))
        {
            minDistance -= 1f;
            maxDistance += 1f;
        }

        return (minDistance, maxDistance);
    }

    static IEnumerable<float> GetTickRatios()
    {
        yield return 0f;
        yield return 0.25f;
        yield return 0.5f;
        yield return 0.75f;
        yield return 1f;
    }

    IEnumerable<float> GetDistanceLabelDistances()
    {
        return points
            .Select(point => point.distanceYards)
            .Distinct()
            .OrderBy(distance => distance)
            .ToList();
    }

    float GetDistanceLabelWidth(float chartWidth)
    {
        var count = Mathf.Max(1, GetDistanceLabelDistances().Count());
        return Mathf.Max(32f, chartWidth / count + 12f);
    }

    static Vector2 MapPoint(Rect chartRect, float distanceYards, float value, float minDistance, float maxDistance, float axisMax)
    {
        var x = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(minDistance, maxDistance, distanceYards));
        var y = Mathf.Lerp(chartRect.yMax, chartRect.yMin, Mathf.InverseLerp(0f, Mathf.Max(1f, axisMax), value));
        return new Vector2(x, y);
    }

    static void DrawSeries(Painter2D painter, Rect chartRect, IEnumerable<Vector2> seriesPoints, Color color)
    {
        var pointList = seriesPoints.ToList();
        if (pointList.Count == 0)
            return;

        painter.strokeColor = color;
        painter.fillColor = color;
        painter.lineWidth = 2f;
        if (pointList.Count >= 2)
        {
            painter.BeginPath();
            painter.MoveTo(pointList[0]);
            for (int i = 1; i < pointList.Count; i++)
            {
                painter.LineTo(pointList[i]);
            }
            painter.Stroke();
        }

        foreach (var point in pointList)
        {
            if (!chartRect.Contains(point))
                continue;

            painter.BeginPath();
            painter.Arc(point, 2.5f, 0f, 360f);
            painter.ClosePath();
            painter.Fill();
        }
    }

    static void DrawRangeLine(Painter2D painter, Rect chartRect, float minDistance, float maxDistance, float rangeYards)
    {
        var x = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(minDistance, maxDistance, rangeYards));
        if (x < chartRect.xMin || x > chartRect.xMax)
            return;

        painter.strokeColor = RangeLineColor;
        painter.lineWidth = 1f;
        const float dashLength = 6f;
        const float gapLength = 4f;
        var y = chartRect.yMin;
        while (y < chartRect.yMax)
        {
            var endY = Mathf.Min(y + dashLength, chartRect.yMax);
            painter.BeginPath();
            painter.MoveTo(new Vector2(x, y));
            painter.LineTo(new Vector2(x, endY));
            painter.Stroke();
            y += dashLength + gapLength;
        }
    }

    void DrawRangeLine(Painter2D painter, Rect chartRect, float minDistance, float maxDistance)
    {
        if (rangeYards.HasValue)
        {
            DrawRangeLine(painter, chartRect, minDistance, maxDistance, rangeYards.Value);
        }
    }

    void DrawMainBeltEffectiveLine(Painter2D painter, Rect chartRect, float minDistance, float maxDistance, float leftAxisMax)
    {
        if (!mainBeltEffectiveInches.HasValue || points.Count == 0)
            return;

        var armorValue = mainBeltEffectiveInches.Value;
        var y = Mathf.Lerp(chartRect.yMax, chartRect.yMin, Mathf.InverseLerp(0f, Mathf.Max(1f, leftAxisMax), armorValue));
        if (y < chartRect.yMin || y > chartRect.yMax)
            return;

        var splitDistances = new List<float> { minDistance, maxDistance };
        for (int i = 0; i < points.Count - 1; i++)
        {
            AddCrossingDistance(splitDistances, points[i].distanceYards, points[i + 1].distanceYards, points[i].verticalPenetrationInches, points[i + 1].verticalPenetrationInches, armorValue);
            AddCrossingDistance(splitDistances, points[i].distanceYards, points[i + 1].distanceYards, points[i].horizontalPenetrationInches, points[i + 1].horizontalPenetrationInches, armorValue);
        }

        splitDistances = splitDistances
            .Distinct()
            .Where(distance => distance >= minDistance && distance <= maxDistance)
            .OrderBy(distance => distance)
            .ToList();

        painter.lineWidth = 1f;
        const float dashLength = 6f;
        const float gapLength = 4f;

        for (int i = 0; i < splitDistances.Count - 1; i++)
        {
            var startDistance = splitDistances[i];
            var endDistance = splitDistances[i + 1];
            if (endDistance <= startDistance)
                continue;

            var midDistance = (startDistance + endDistance) * 0.5f;
            painter.strokeColor = IsImmuneAtDistance(midDistance, armorValue) ? ImmuneZoneColor : VulnerableZoneColor;

            var startX = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(minDistance, maxDistance, startDistance));
            var endX = Mathf.Lerp(chartRect.xMin, chartRect.xMax, Mathf.InverseLerp(minDistance, maxDistance, endDistance));

            var x = startX;
            while (x < endX)
            {
                var dashEnd = Mathf.Min(x + dashLength, endX);
                painter.BeginPath();
                painter.MoveTo(new Vector2(x, y));
                painter.LineTo(new Vector2(dashEnd, y));
                painter.Stroke();
                x += dashLength + gapLength;
            }
        }
    }

    bool IsImmuneAtDistance(float distanceYards, float armorValue)
    {
        var verticalPenetration = EvaluatePenetration(distanceYards, point => point.verticalPenetrationInches);
        var horizontalPenetration = EvaluatePenetration(distanceYards, point => point.horizontalPenetrationInches);
        return verticalPenetration < armorValue && horizontalPenetration < armorValue;
    }

    float EvaluatePenetration(float distanceYards, Func<BatteryFigurePoint, float> selector)
    {
        if (points.Count == 0)
            return 0f;
        if (points.Count == 1)
            return selector(points[0]);

        if (distanceYards <= points[0].distanceYards)
            return selector(points[0]);
        if (distanceYards >= points[^1].distanceYards)
            return selector(points[^1]);

        for (int i = 0; i < points.Count - 1; i++)
        {
            var start = points[i];
            var end = points[i + 1];
            if (distanceYards < start.distanceYards || distanceYards > end.distanceYards)
                continue;

            var t = Mathf.InverseLerp(start.distanceYards, end.distanceYards, distanceYards);
            return Mathf.Lerp(selector(start), selector(end), t);
        }

        return selector(points[^1]);
    }

    static void AddCrossingDistance(List<float> splitDistances, float startDistance, float endDistance, float startValue, float endValue, float threshold)
    {
        var startDelta = startValue - threshold;
        var endDelta = endValue - threshold;

        if (Mathf.Approximately(startDelta, 0f))
            splitDistances.Add(startDistance);
        if (Mathf.Approximately(endDelta, 0f))
            splitDistances.Add(endDistance);
        if (Mathf.Approximately(startDelta, 0f) || Mathf.Approximately(endDelta, 0f) || Mathf.Sign(startDelta) == Mathf.Sign(endDelta))
            return;

        var t = Mathf.InverseLerp(startValue, endValue, threshold);
        splitDistances.Add(Mathf.Lerp(startDistance, endDistance, t));
    }

    static Label BuildOverlayLabel(string text, float x, float y, float width, float height, TextAnchor textAnchor, Color? color = null)
    {
        var label = new Label(text);
        label.pickingMode = PickingMode.Ignore;
        label.style.position = Position.Absolute;
        label.style.left = x;
        label.style.top = y;
        label.style.width = width;
        label.style.height = height;
        label.style.fontSize = 10;
        label.style.unityTextAlign = textAnchor;
        label.style.whiteSpace = WhiteSpace.NoWrap;
        if (color.HasValue)
        {
            label.style.color = color.Value;
        }
        return label;
    }
}


public class ShipClassEditor : LeftObjectPickerRightEditor<ShipClassEditor, ShipClass>
{
    const string ArmorTypeMarkdown = @"Armor Type Factor is derived from Okun's [Table of Metallurgical Properties of Naval Armor and Construction Materials](http://www.navweaps.com/index_nathan/metalprpsept2009.php) 


| Armor Type                             | SK5 Armor Factor | Okun Material / Rule                                     | Okun Field       | Okun Value               | Derivation              |
| -------------------------------------- | ---------------: | -------------------------------------------------------- | ---------------- | ------------------------ | ----------------------- |
| No Armor                               |                0 | -                                                        | -                | -                        | game placeholder        |
| Wrought Iron                           |             0.60 | WROUGHT IRON ARMOR                                       | Average Quality  | approx. 0.55-0.60        | selected / rounded      |
| Mild Steel                             |             0.75 | AVE. “MILD/MEDIUM” STEEL                                 | Average Quality  | 0.75                     | direct                  |
| Compound Hard Steel Faced Wrought Iron |             0.68 | “COMPOUND” HARD-STEEL-FACED WROUGHT IRON                 | Q / QD           | Q=0.75; QD=0.60          | mean / rounded          |
| Nickel Steel                           |             0.90 | AVE. NICKEL-STEEL ARMOR                                  | Average Quality  | 0.90                     | direct                  |
| Harvey Mild Steel                      |             0.74 | AVE. HARVEYIZED MILD STEEL                               | Q / QD           | Q=0.78; QD=0.70          | mean(Q,QD)              |
| Harvey Nickel Steel                    |             0.78 | AVE. HARVEYIZED NICKEL-STEEL                             | Q / QD           | Q≈0.805; QD≈0.75         | mean / rounded          |
| Krupp Chrome Nickel Steel              |             0.95 | Krupp “HIGH-%” Nickel-Steel                              | Average Quality  | 0.95                     | direct                  |
| Krupp Cemented 1894                    |             0.83 | Original KC / KC a/A                                     | Q                | Q=0.828                  | rounded                 |
| High Tensile Steel                     |             0.82 | AVE. HIGH-TENSILE STEEL                                  | Average Quality  | 1895=0.80; post-WWI=0.85 | interpolated / selected |
| Class A Armor 1900                     |             0.83 | AVE. WWI-ERA CLASS “A” ARMOR / early Class A rule        | Q                | Q≈0.828                  | rounded                 |
| Krupp Nickel Steel                     |             0.83 | likely KC a/A or default pre-1911 FH armor               | Q                | Q≈0.828                  | rounded                 |
| Krupp Non-Cemented                     |             0.95 | KRUPP NON-CEMENTED / KNC-like homogeneous armor          | Average Quality  | 0.95                     | direct                  |
| Krupp Cemented WW1 Era 1905            |             0.83 | default face-hardened armor through 1910                 | Q                | Q=0.828                  | rounded                 |
| Witkowitzer KC                         |             0.95 | IMPROVED AUSTRO-HUNGARIAN WITKOWITZER KC                 | Q                | Q=0.947                  | rounded                 |
| Class A Armor Midvale Non-Cemented     |             0.88 | MIDVALE NON-CEMENTED CLASS “A”                           | Q / special case | Q=0.889                  | truncated               |
| Class B Armor 1910                     |             0.95 | early STS / WWI-era Class “B” homogeneous armor          | Average Quality  | 0.95                     | direct                  |
| Special Treatment Steel                |             1.00 | SPECIAL TREATMENT STEEL / STS                            | Average Quality  | 1.00                     | direct                  |
| Class A Armor 1911                     |             0.89 | AVE. WWI-ERA CLASS “A” ARMOR                             | Q                | Q=0.889                  | rounded                 |
| Krupp Cemented WW1 Era 1911            |             0.85 | default FH armor 1911–1921                               | Q                | Q=0.850                  | direct                  |
| Krupp Wotan Hard Nickel Steel          |             1.00 | German WOTAN HART / Wh / Wotan Härte                     | Average Quality  | 1.00                     | direct                  |
| D Silicon Manganese HT Steel           |             0.90 | AVE. EXTRA-HIGH-STRENGTH “D” SILICON-MANGANESE HT STEELS | Average Quality  | 0.90                     | direct                  |
| New Vickers Non-Cemented               |             0.95 | NEW VICKERS NON-CEMENTED / NVNC                          | Average Quality  | 0.95                     | direct                  |
| Non-Cemented Armor                     |             1.00 | AVE. NON-CEMENTED ARMOR / NCA                            | Average Quality  | 1.00                     | direct                  |
| Krupp Cemented 1928                    |             1.00 | Krupp Cemented new type / KC n/A                         | Q                | Q=1.00                   | direct                  |
| PO Homogenous Plate                    |             1.00 | PIASTRE OMOGENEE / PO                                    | Average Quality  | 1.00 estimated           | direct                  |
| Italian WW2 Era Krupp Cemented         |             1.00 | ITALIAN WWII KRUPP CEMENTED                              | Q                | Q=1.00 estimated         | direct                  |
| British Cemented Armor                 |             1.00 | BRITISH CEMENTED ARMOR / CA                              | Q                | Q=1.00                   | direct                  |
| Class B Armor 1933                     |             1.00 | WWII-era CLASS “B” ARMOR                                 | Average Quality  | 1.00                     | direct                  |
| Class A Armor 1933                     |             1.00 | improved USN CLASS “A” ARMOR / post-1921 FH default      | Q                | 1.00                     | direct / default        |
| Vickers Non-Cemented                   |             0.84 | VICKERS HARDENED NON-CEMENTED / VH                       | Q                | Q=0.839                  | rounded                 |
| Molybdenum Non-Cemented                |             0.97 | MOLYBDENUM NON-CEMENTED / MNC                            | Average Quality  | 0.97                     | direct                  |";

    const string TorpedoDamageClassMarkdown = @"| Damage Class | Type                                                                                                   | Firing Ship Example                                                                                                                           | Year | warhead (lb) | diameter (inch) |
| ------------ | ------------------------------------------------------------------------------------------------------ | --------------------------------------------------------------------------------------------------------------------------------------------- | ---- | ------------ | --------------- |
| A            | [61cm Type 93 M1-2](https://en.wikipedia.org/wiki/Type_93_torpedo)                                     | JAPAN / DD / [Kawakaze (江風)](https://en.wikipedia.org/wiki/Japanese_destroyer_Kawakaze_(1936))                                                | 1933 | 1080         | 24              |
| B            | [61cm Type 90](https://en.wikipedia.org/wiki/61_cm_Type_90_torpedo)                                    | JAPAN / DD / [Shikinami (敷波)](https://en.wikipedia.org/wiki/Japanese_destroyer_Shikinami_(1929))                                              | 1933 | 880          | 24              |
| C            | [53cm G7a T1](https://en.wikipedia.org/wiki/G7a_torpedo)                                               | GERMANY / SS / [U-47](https://en.wikipedia.org/wiki/German_submarine_U-47_(1938))                                                             | 1934 | 661          | 21              |
| D            | [53cm Si270I](https://regiamarina.net/torpedoes/)                                                      | ITALY / SS / [Axum](https://en.wikipedia.org/wiki/Italian_submarine_Axum) ( [Adua](https://en.wikipedia.org/wiki/Adua-class_submarine)-class) | 1936 | 595          | 21              |
| E            | [53cm G7e T2/3](https://en.wikipedia.org/wiki/G7e_torpedo)                                             | GERMANY / SS / [U-29](https://en.wikipedia.org/wiki/German_submarine_U-29_(1936))                                                             | 1936 | 660          | 21.04           |
| F            | [21"" Mk I](https://en.wikipedia.org/wiki/British_21-inch_torpedo)                                      | GREAT BRITAIN / DD / [Onslaught](https://en.wikipedia.org/wiki/HMS_Onslaught_(1915))                                                          | 1910 | 200          | 21              |
| G            | [50cm G6](http://www.navweaps.com/Weapons/WTGER_PreWWII.php#50_cm_%2819.7%22%29_G%2F6_and_G%2F6D)      | GERMANY / SS / [U-21](https://en.wikipedia.org/wiki/SM_U-21_(Germany))  ([U-19](https://en.wikipedia.org/wiki/Type_U_19_submarine)-class)     | 1911 | 353          | 19.7            |
| H            | [45cm C/06](http://www.navweaps.com/Weapons/WTGER_PreWWII.php#45_cm_%2817.7%22%29_C%2F06_and_C%2F06_D) | GERMANY / SS / [U-9](https://en.wikipedia.org/wiki/SM_U-9)                                                                                    | 1907 | 270          | 17.7            |
| I            | [14"" Whitehead](https://en.wikipedia.org/wiki/Whitehead_torpedo)                                       | CHILE / TB / [Almirante Lynch](https://en.wikipedia.org/wiki/Chilean_torpedo_gunboat_Almirante_Lynch)                                         | 1868 | 118          | 14              |";

    static readonly GlobalString Sk5CodeHelpMessage = new()
    {
        english = "Fire Control Code, which denotes a specific combination of Fire Control Components in SK5 data, and Component only serve to set the inferred values of the Fire Control Table when the 'Reset Fire Control Table' button is clicked. They also act as a form of remark/annotation. Their values themselves do not affect resolution because their effects are already fully captured by the Fire Control Table, which acts as a sufficient statistic.",
        japanese = "射撃統制コード（SK5データにおける射撃統制構成要素の特定の組み合わせを表す）と構成要素は、「射撃統制表をリセット」ボタンがクリックされたときに、射撃統制表の推定値を設定するためだけに機能します。また、注釈／メモとしての役割も果たします。それらの値自体は解決に影響しません。なぜなら、その影響は十分統計量として機能する射撃統制表にすでに完全に反映されているからです。",
        chineseSimplified = "火控码（表示 SK5 数据中火控具体组成的某种特定组合）和具体组成仅用于在点击“重置火控表”按钮时设置火控表的推断值，同时也起到备注／注释的作用。它们的值本身不会影响结算，因为其影响已经完整体现在火控表中，而火控表起到了充分统计量的作用。",
        chineseTraditional = "火控碼（表示 SK5 資料中火控具體組成的某種特定組合）和具體組成僅用於在點擊「重置火控表」按鈕時設定火控表的推斷值，同時也起到備註／註釋的作用。它們的值本身不會影響結算，因為其影響已經完整體現在火控表中，而火控表起到了充分統計量的作用。",
    };

    ListView batteryRecordsListView;
    VisualElement portraitTopPreview;
    VisualElement portraitIconPreview;
    VisualElement graphicTabContent;
    VisualElement sectorArcsTabContent;
    VisualElement batterySectorArcsContainer;
    VisualElement batteryFigureChartsContainer;
    Label torpedoSectorTitleLabel;
    Image defaultPlaceholderPreviewImage;
    Texture2D defaultPlaceholderPreviewTexture;
    string lastDefaultPlaceholderSignature;
    string lastDefaultPlaceholderShipObjectId;
    string lastSectorArcSignature;
    string lastSectorArcShipObjectId;

    SectorArcIndicatorBinder torpedoSectorArcIndicatorBinder = new();

    protected override string ObjectListViewElementName => "ShipClassListView";

    public ListView shipClassListView => objectListView;

    [CreateProperty]
    public ShipClass selectedShipClass => selectedObject;

    public ShipClass SelectedShipClassProvider()
    {
        return selectedObject;
    }

    // protected override void Awake()
    protected override void OnEnable()
    {
        base.OnEnable();

        torpedoSectorArcIndicatorBinder.BindUI(root.Q<VisualElement>("TorpedoSectorArcIndicator"));
        sectorArcsTabContent = root.Q<VisualElement>("SectorArcsTabContent");
        batterySectorArcsContainer = root.Q<VisualElement>("BatterySectorArcsContainer");
        batteryFigureChartsContainer = root.Q<VisualElement>("BatteryFigureChartsContainer");
        torpedoSectorTitleLabel = root.Q<Label>("TorpedoSectorTitleLabel");
        sectorArcsTabContent?.RegisterCallback<GeometryChangedEvent>(_ => RequestSectorArcRefresh());

        shipClassListView.selectionChanged += (objs) =>
        {
            // Debug.Log($"selectionChanged: {objs}");
            var currentShipClass = objs.FirstOrDefault() as ShipClass;
            if (currentShipClass != null)
            {
                Debug.Log($"currentShipClass: {currentShipClass}");
            }

            RequestSectorArcRefresh(currentShipClass, true);
            RequestDefaultPlaceholderPreviewRefresh(currentShipClass, true);
        };

        var speedIncreaseMultiColumnListView = root.Q<MultiColumnListView>("SpeedIncreaseMultiColumnListView");
        // speedIncreaseMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<SpeedIncreaseRecord>(speedIncreaseMultiColumnListView);
        Utils.BindItemsAddedRemoved<SpeedIncreaseRecord>(speedIncreaseMultiColumnListView, SelectedShipClassProvider);

        var inferSpeedIncreaseButton = root.Q<Button>("InferSpeedIncreaseButton");
        if (inferSpeedIncreaseButton != null)
        {
            inferSpeedIncreaseButton.clicked += () =>
            {
                var shipClass = selectedShipClass;
                if (shipClass == null)
                {
                    DialogRoot.Instance.PopupMessageDialog(Localize("No ship class is selected."));
                    return;
                }

                shipClass.InferSpeedIncreaseRecord();
                shipClass.InferTurnRate();
                shipClass.InferMachineryHitSpeedLimits();
            };
        }

        var armorTypeHelpButton = root.Q<Button>("ArmorTypeHelpButton");
        if (armorTypeHelpButton != null)
        {
            armorTypeHelpButton.clicked += () =>
            {
                DialogRoot.Instance.PopupMarkdownDialog(ArmorTypeMarkdown, Localize("Armor Type"));
            };
        }

        var torpedoDamageClassHelpButton = root.Q<Button>("TorpedoDamageClassHelpButton");
        if (torpedoDamageClassHelpButton != null)
        {
            torpedoDamageClassHelpButton.clicked += () =>
            {
                DialogRoot.Instance.PopupMarkdownDialog(TorpedoDamageClassMarkdown, Localize("Torpedo Damage Class"));
            };
        }

        batteryRecordsListView = root.Q<ListView>("BatteryRecordsListView");
        // batteryRecordsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<BatteryRecord>(batteryRecordsListView);
        Utils.BindItemsAddedRemoved<BatteryRecord>(batteryRecordsListView, SelectedShipClassProvider);
        batteryRecordsListView.makeItem = () =>
        {
            var el = batteryRecordsListView.itemTemplate.CloneTree();
            Utils.BindItemsSourceRecursive(el);

            var fireControlTableMultiColumnListView = el.Q<MultiColumnListView>("FireControlTableMultiColumnListView");
            var penetrationTableMultiColumnListView = el.Q<MultiColumnListView>("PenetrationTableMultiColumnListView");
            var mountsListView = el.Q<ListView>("MountsListView");
            var sk5CodeHelpButton = el.Q<Button>("Sk5CodeHelpButton");
            var fireControlModelComparisonButton = el.Q<Button>("FireControlModelComparisonButton");
            var batteryRecordMetaInfoButton = el.Q<Button>("BatteryRecordMetaInfoButton");
            var batteryRecordMetaInfoMcCoyOkunButton = el.Q<Button>("BatteryRecordMetaInfoMcCoyOkunButton");
            if (sk5CodeHelpButton != null)
            {
                sk5CodeHelpButton.clicked += () =>
                {
                    DialogRoot.Instance.PopupMessageDialog(Sk5CodeHelpMessage.GetShortName(), Localize("SK5 Code"));
                };
            }

            if (batteryRecordMetaInfoButton != null)
            {
                batteryRecordMetaInfoButton.clicked += () =>
                {
                    if (!Utils.TryResolveCurrentValueForBinding(batteryRecordMetaInfoButton, out BatteryRecord batteryRecord))
                    {
                        DialogRoot.Instance.PopupMessageDialog(Localize("No battery record is selected."));
                        return;
                    }

                    DialogRoot.Instance.PopupBatteryRecordMetaInfoDialog(batteryRecord, () =>
                    {
                        penetrationTableMultiColumnListView?.RefreshItems();
                        RequestSectorArcRefresh(true);
                    });
                };
            }

            if (batteryRecordMetaInfoMcCoyOkunButton != null)
            {
                batteryRecordMetaInfoMcCoyOkunButton.clicked += () =>
                {
                    if (!Utils.TryResolveCurrentValueForBinding(batteryRecordMetaInfoMcCoyOkunButton, out BatteryRecord batteryRecord))
                    {
                        DialogRoot.Instance.PopupMessageDialog(Localize("No battery record is selected."));
                        return;
                    }

                    DialogRoot.Instance.PopupBatteryRecordMetaInfoMcCoyOkunDialog(batteryRecord, () =>
                    {
                        penetrationTableMultiColumnListView?.RefreshItems();
                        RequestSectorArcRefresh(true);
                    });
                };
            }

            if (fireControlModelComparisonButton != null)
            {
                fireControlModelComparisonButton.clicked += () =>
                {
                    if (!Utils.TryResolveCurrentValueForBinding(fireControlModelComparisonButton, out BatteryRecord batteryRecord))
                    {
                        DialogRoot.Instance.PopupMessageDialog(Localize("No battery record is selected."));
                        return;
                    }

                    PopupFireControlModelComparisonDialog(batteryRecord);
                };
            }

            var resetFireControlTableButton = el.Q<Button>("ResetFireControlTableButton");
            if (resetFireControlTableButton != null)
            {
                resetFireControlTableButton.clicked += () =>
                {
                    if (!Utils.TryResolveCurrentValueForBinding(resetFireControlTableButton, out BatteryRecord batteryRecord))
                    {
                        DialogRoot.Instance.PopupMessageDialog(Localize("No battery record is selected."));
                        return;
                    }

                    void ResetFromStandardCode()
                    {
                        if (!ResetFireControlTableFromStandardCode(batteryRecord))
                        {
                            DialogRoot.Instance.PopupMessageDialog(
                                Localize("No standard fire control table is available for this Role/Code/Era combination."),
                                Localize("Reset Fire Control Table"));
                            return;
                        }

                        fireControlTableMultiColumnListView?.RefreshItems();
                        DialogRoot.Instance.PopupMessageDialog(
                            Localize("Fire control table reset from standard code table."),
                            Localize("Reset Fire Control Table"));
                    }

                    if (batteryRecord.customFireControlTable)
                    {
                        DialogRoot.Instance.PopupConfirmDialog(
                            Localize("This battery uses a custom fire control table. Resetting will replace it with the standard table generated from Role/Code/Era and clear the Custom flag."),
                            ResetFromStandardCode,
                            Localize("Reset Fire Control Table"));
                    }
                    else
                    {
                        ResetFromStandardCode();
                    }
                };
            }

            var resetPenetrationTableButton = el.Q<Button>("ResetPenetrationTableButton");
            if (resetPenetrationTableButton != null)
            {
                resetPenetrationTableButton.clicked += () =>
                {
                    if (!Utils.TryResolveCurrentValueForBinding(resetPenetrationTableButton, out BatteryRecord batteryRecord))
                    {
                        DialogRoot.Instance.PopupMessageDialog(Localize("No battery record is selected."));
                        return;
                    }

                    var rowCount = ResetPenetrationTableFromModel(batteryRecord);
                    penetrationTableMultiColumnListView?.RefreshItems();
                    RequestSectorArcRefresh(true);
                    DialogRoot.Instance.PopupMessageDialog(
                        Localize("Penetration table reset from model with {0} rows.", rowCount),
                        Localize("Reset Penetration Table"));
                };
            }

            // fireControlTableMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<FireControlTableRecord>(fireControlTableMultiColumnListView);
            // penetrationTableMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<PenetrationTableRecord>(penetrationTableMultiColumnListView);
            // mountsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountLocationRecord>(mountsListView);
            Utils.BindItemsAddedRemoved<FireControlTableRecord>(fireControlTableMultiColumnListView, SelectedShipClassProvider);
            Utils.BindItemsAddedRemoved<PenetrationTableRecord>(penetrationTableMultiColumnListView, SelectedShipClassProvider);
            Utils.BindItemsAddedRemoved<MountLocationRecord>(mountsListView, SelectedShipClassProvider);

            mountsListView.makeItem = () =>
            {
                var el2 = mountsListView.itemTemplate.CloneTree();

                var mountsArcsMultiColumnsListView = el2.Q<MultiColumnListView>("MountArcsMultiColumnListView");
                // mountsArcsMultiColumnsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountArcRecord>(mountsArcsMultiColumnsListView);
                Utils.BindItemsAddedRemoved<MountArcRecord>(mountsArcsMultiColumnsListView, SelectedShipClassProvider);

                Utils.BindItemsSourceRecursive(el2);

                return el2;
            };

            return el;
        };

        var torpedoSettingsMultiColumnListView = root.Q<MultiColumnListView>("TorpedoSettingsMultiColumnListView");
        // torpedoSettingsMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<TorpedoSetting>(torpedoSettingsMultiColumnListView);
        Utils.BindItemsAddedRemoved<TorpedoSetting>(torpedoSettingsMultiColumnListView, SelectedShipClassProvider);

        var torpedoMountsListView = root.Q<ListView>("TorpedoMountsListView");
        // torpedoMountsListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountLocationRecord>(torpedoMountsListView);
        Utils.BindItemsAddedRemoved<MountLocationRecord>(torpedoMountsListView, SelectedShipClassProvider);
        torpedoMountsListView.makeItem = () =>
        {
            var el = torpedoMountsListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(el);
            var mountArcsMultiColumnListView = el.Q<MultiColumnListView>("MountArcsMultiColumnListView");
            // mountArcsMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<MountArcRecord>(mountArcsMultiColumnListView);
            Utils.BindItemsAddedRemoved<MountArcRecord>(mountArcsMultiColumnListView, SelectedShipClassProvider);

            return el;
        };

        var torpedoSectorMetaInfoButton = root.Q<Button>("TorpedoSectorMetaInfoSetButton");
        if (torpedoSectorMetaInfoButton != null)
        {
            torpedoSectorMetaInfoButton.clicked += () =>
            {
                if (!Utils.TryResolveCurrentValueForBinding(torpedoSectorMetaInfoButton, out TorpedoSector torpedoSector))
                    return;

                DialogRoot.Instance.PopupTorpedoSectorMetaInfoDialog(torpedoSector, null);
            };
        }

        var rapidFireBatteryListView = root.Q<ListView>("RapidFireBatteryListView");
        // rapidFireBatteryListView.itemsAdded += Utils.MakeCallbackForItemsAdded<RapidFireBatteryRecord>(rapidFireBatteryListView);
        Utils.BindItemsAddedRemoved<RapidFireBatteryRecord>(rapidFireBatteryListView, SelectedShipClassProvider);

        rapidFireBatteryListView.makeItem = () =>
        {
            var el = rapidFireBatteryListView.itemTemplate.CloneTree();

            Utils.BindItemsSourceRecursive(el);
            var fireControlLevelMultiColumnListView = el.Q<MultiColumnListView>("FireControlLevelMultiColumnListView");
            // fireControlLevelMultiColumnListView.itemsAdded += Utils.MakeCallbackForItemsAdded<RapidFireBatteryFireControlLevelRecord>(fireControlLevelMultiColumnListView);
            Utils.BindItemsAddedRemoved<RapidFireBatteryFireControlLevelRecord>(fireControlLevelMultiColumnListView, SelectedShipClassProvider);

            var metaInfoButton = el.Q<Button>("RapidFireBatteryMetaInfoSetButton");
            metaInfoButton.clicked += () =>
            {
                if (!Utils.TryResolveCurrentValueForBinding(metaInfoButton, out RapidFireBatteryRecord rapidFireBatteryRecord))
                    return;

                DialogRoot.Instance.PopupRapidFireBatteryRecordMetaInfoDialog(rapidFireBatteryRecord, null);
            };

            return el;
        };

        var exportButton = root.Q<Button>("ExportButton");
        exportButton.clicked += () =>
        {
            var gameState = SuperGameState.Instance.GetCurrentGameState();
            var content = gameState.ShipClassesToXML();
            IOManager.Instance.SaveTextFile(content, "ShipClasses", "xml");
        };

        var importButton = root.Q<Button>("ImportButton");
        importButton.clicked += () =>
        {
            // IOManager.Instance.textLoaded += OnShipClassesXMLLoaded;
            IOManager.Instance.LoadTextFile(OnShipClassesXMLLoaded, "xml");
        };

        var exportSelectedBatteryButton = root.Q<Button>("ExportSelectedBatteryButton");
        var importToSelectedBatteryButton = root.Q<Button>("ImportToSelectedBatteryButton");

        exportSelectedBatteryButton.clicked += () =>
        {
            var battryRecord = batteryRecordsListView.selectedItem as BatteryRecord;
            if (battryRecord != null)
            {
                var content = battryRecord.ToXML();
                IOManager.Instance.SaveTextFile(content, "battery", "xml");
            }
        };

        importToSelectedBatteryButton.clicked += () =>
        {
            var idx = batteryRecordsListView.selectedIndex;
            if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
            {
                // IOManager.Instance.textLoaded += OnBatteryXMLLoaded;
                IOManager.Instance.LoadTextFile(OnBatteryXMLLoaded, "xml");
            }
        };

        var setSelectedByBatterySelectorButton = root.Q<Button>("SetSelectedByBatterySelectorButton");
        setSelectedByBatterySelectorButton.clicked += () =>
        {
            // Debug.Log("setSelectedByBatterySelectorButton clicked");

            DialogRoot.Instance.PopupBatteryRecordSelectorDialog(_batteryRecord =>
            {
                var batteryRecord = XmlUtils.FromXML<BatteryRecord>(XmlUtils.ToXML(_batteryRecord));
                ((IObjectIdLabeled)batteryRecord).ResetObjectId();

                var idx = batteryRecordsListView.selectedIndex;
                if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
                {
                    batteryRecordsListView.itemsSource[idx] = batteryRecord;
                }
                else
                {
                    batteryRecordsListView.itemsSource.Add(batteryRecord);
                }

                var gameState = SuperGameState.Instance.GetCurrentGameState();
                gameState.ResetAndRegisterAll(); // Assign a new guid to new copied battery record
            });
        };

        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PortraitTopReferenceField"));
        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PortraitReferenceField"));
        PathReferenceBinder.BindPictureReference(root.Q<VisualElement>("PortraitIconReferenceField"));
        portraitTopPreview = root.Q<VisualElement>("PortraitTopPreview");
        portraitIconPreview = root.Q<VisualElement>("PortraitIconPreview");
        graphicTabContent = root.Q<VisualElement>("GraphicTabContent");
        defaultPlaceholderPreviewImage = root.Q<Image>("DefaultPlaceholderPreviewImage");

        graphicTabContent?.RegisterCallback<GeometryChangedEvent>(_ => RequestDefaultPlaceholderPreviewRefresh());

        root.Q<Button>("GeneratePlaceholderImageButton").clicked += () =>
        {
            if (selectedShipClass != null)
            {
                DialogRoot.Instance.PopupShipClassPlaceholderGeneratorDialog(selectedShipClass);
            }
        };

        root.Q<Button>("GeneratePlaceholderImageForAllPlaceholderButton").clicked += () =>
        {
            var placeholders = SuperGameState.Instance.GetCurrentGameState().shipClasses.Where(x => x.isGraphicPlaceholder).ToList();
            var count = placeholders.Count;
            if (count == 0)
            {
                DialogRoot.Instance.PopupMessageDialog("No ship class is marked as graphic placeholder.");
                return;
            }

            DialogRoot.Instance.PopupConfirmDialog(
                $"Generate placeholder images for {count} ship class? If confirm, {count} x 2 images would be generated in the game folder and binding would be reset to those image.\n\n Warning: This will modify files in the disk.",
                () =>
                {
                    var result = ShipClassPlaceholderImageGenerator.GenerateAndBindAllMarked(placeholders);
                    UnityWebRequestImageReader.Instance.Reset();
                    RefreshGraphicBindings();

                    var message = $"Generated placeholder images for {result.generatedShipClasses.Count} ship class.";
                    if (result.skippedMessages.Count > 0)
                    {
                        message += "\nSkipped:\n" + string.Join("\n", result.skippedMessages);
                    }
                    DialogRoot.Instance.PopupMessageDialog(message);
                });
        };

        var batteryArcIndicatorDialogButton = root.Q<Button>("BatteryArcIndicatorDialogButton");
        batteryArcIndicatorDialogButton.clicked += () =>
        {
            if (Utils.TryResolveCurrentValueForBinding(batteryArcIndicatorDialogButton, out ShipClass shipClass))
            {
                DialogRoot.Instance.PopupBatteryArcIndicatorDialog(shipClass);
            }
        };

        root.Q<Button>("SetSelectedByRapidFireBatterySelectorButton").clicked += () =>
        {
            Debug.Log("SetSelectedByRapidFireBatterySelectorButton clicked");

            DialogRoot.Instance.PopupRapidFireBatteryRecordSelectorDialog(_rapidFireBatteryRecord =>
            {
                var rapidFireBatteryRecord = XmlUtils.FromXML<RapidFireBatteryRecord>(XmlUtils.ToXML(_rapidFireBatteryRecord));
                // ((IObjectIdLabeled)rapidFireBatteryRecord).ResetObjectId();

                var idx = rapidFireBatteryListView.selectedIndex;
                if (idx >= 0 && idx < rapidFireBatteryListView.itemsSource.Count) // TODO: Notify invalid 
                {
                    rapidFireBatteryListView.itemsSource[idx] = rapidFireBatteryRecord;
                }
                else
                {
                    rapidFireBatteryListView.itemsSource.Add(rapidFireBatteryRecord);
                }

                // var gameState = SuperGameState.Instance.GetCurrentGameState();
                // gameState.ResetAndRegisterAll(); // Assign a new guid to new copied battery record
            });
        };
        
        var setByTorpedoSelectorButton = root.Q<Button>("SetByTorpedoSelectorButton");
        setByTorpedoSelectorButton.clicked += () =>
        {
            Debug.Log("SetByTorpedoSelectorButton clicked");

            DialogRoot.Instance.PopupTorpedoSectorSelectorDialog(_shipClass =>
            {
                var _torpedoSector = _shipClass.torpedoSector;
                var torpedoSector = XmlUtils.FromXML<TorpedoSector>(XmlUtils.ToXML(_torpedoSector));
                foreach (var mountLocationRecord in torpedoSector.mountLocationRecords)
                {
                    mountLocationRecord.objectId = null;
                }

                if(Utils.TryResolveCurrentValueForBinding<ShipClass>(setByTorpedoSelectorButton, out var shipClass))
                {
                    shipClass.torpedoSector = torpedoSector;
                    SuperGameState.Instance.GetCurrentGameState().ResetAndRegisterAll();
                }
            });
        };
    }

    void OnDisable()
    {
        ClearSectorArcState();
        DisposeDefaultPlaceholderPreviewTexture();
    }

    public EventHandler shown;
    public EventHandler hidden;

    protected override void OnShow()
    {
        RequestDefaultPlaceholderPreviewRefresh();
        shown?.Invoke(this, EventArgs.Empty);
    }

    protected override void OnHidden()
    {
        ClearDefaultPlaceholderPreviewState();
        hidden?.Invoke(this, EventArgs.Empty);
    }

    public void OnBatteryXMLLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnBatteryXMLLoaded;

        var idx = batteryRecordsListView.selectedIndex;
        if (idx >= 0 && idx < batteryRecordsListView.itemsSource.Count) // TODO: Notify invalid 
        {
            var battryRecord = BatteryRecord.FromXml(text);
            batteryRecordsListView.itemsSource[idx] = battryRecord;
        }

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.ResetAndRegisterAll(); // re-duplicate object id // FIXME: Correctness is questionable though
    }

    public void OnShipClassesXMLLoaded(string text)
    {
        // IOManager.Instance.textLoaded -= OnShipClassesXMLLoaded;

        var gameState = SuperGameState.Instance.GetCurrentGameState();
        gameState.ShipClassesFromXML(text);
        gameState.ResetAndRegisterAll();
        GetFullObjects();
        RefreshFilter();
        RequestDefaultPlaceholderPreviewRefresh(selectedShipClass, true);
    }

    [CreateProperty]
    public AbstractGameState currentGameState => SuperGameState.Instance.GetCurrentGameState();

    [CreateProperty]
    public bool isInEditMode => GamePreference.Instance.isInEditMode;

    protected override void GetFullObjects()
    {
        fullObjects = currentGameState.shipClasses;
    }

    protected override void ProcessRemovedOne(ShipClass removeObj)
    {
        EntityManager.Instance.Unregister(removeObj);
    }

    protected override void OnAddObjectButtonClicked()
    {
        var newObj = new ShipClass();
        EntityManager.Instance.Register(newObj, null);
        fullObjects.Add(newObj);

        ProcessAddedOne(newObj);

        RefreshFilter();
        SelectObject(newObj);
    }

    void RefreshGraphicBindings()
    {
        shipClassListView?.RefreshItems();
        if (selectedShipClass == null)
            return;

        RefreshPictureField(root.Q<VisualElement>("PortraitTopReferenceField"), selectedShipClass.portraitTopReference);
        RefreshPictureField(root.Q<VisualElement>("PortraitIconReferenceField"), selectedShipClass.portraitIconReference);

        if (portraitTopPreview != null)
            portraitTopPreview.style.backgroundImage = selectedShipClass.portraitTopReference.pictureStyleBackground;

        if (portraitIconPreview != null)
            portraitIconPreview.style.backgroundImage = selectedShipClass.portraitIconReference.pictureStyleBackground;

        RequestDefaultPlaceholderPreviewRefresh();
    }

    void RequestSectorArcRefresh(bool force = false)
    {
        RequestSectorArcRefresh(selectedShipClass, force);
    }

    void RequestSectorArcRefresh(ShipClass shipClass, bool force = false)
    {
        if (sectorArcsTabContent == null || batterySectorArcsContainer == null || batteryFigureChartsContainer == null || !IsElementActuallyVisible(sectorArcsTabContent))
            return;

        if (shipClass == null)
        {
            ClearSectorArcState();
            return;
        }

        if (lastSectorArcShipObjectId != shipClass.objectId)
        {
            ClearSectorArcState();
            lastSectorArcShipObjectId = shipClass.objectId;
        }

        var signature = BuildSectorArcSignature(shipClass);
        if (!force && signature == lastSectorArcSignature)
            return;

        RebuildBatterySectorArcCards(shipClass);
        RebuildBatteryFigureCharts(shipClass);
        torpedoSectorArcIndicatorBinder.BindTorpedoData(shipClass);
        if (torpedoSectorTitleLabel != null)
        {
            torpedoSectorTitleLabel.text = shipClass?.torpedoSector?.name?.GetShortName() ?? "";
        }
        lastSectorArcShipObjectId = shipClass.objectId;
        lastSectorArcSignature = signature;
    }

    void RequestDefaultPlaceholderPreviewRefresh(bool force = false)
    {
        RequestDefaultPlaceholderPreviewRefresh(selectedShipClass, force);
    }

    void RequestDefaultPlaceholderPreviewRefresh(ShipClass shipClass, bool force = false)
    {
        if (graphicTabContent == null || defaultPlaceholderPreviewImage == null || !IsElementActuallyVisible(graphicTabContent))
            return;

        if (shipClass == null)
        {
            ClearDefaultPlaceholderPreviewState();
            return;
        }

        if (lastDefaultPlaceholderShipObjectId != shipClass.objectId)
        {
            ClearDefaultPlaceholderPreviewState();
            lastDefaultPlaceholderShipObjectId = shipClass.objectId;
        }

        var signature = ShipClassPlaceholderImageGenerator.BuildDefaultPreviewSignature(shipClass);
        if (!force && signature == lastDefaultPlaceholderSignature && defaultPlaceholderPreviewTexture != null)
            return;

        if (!ShipClassPlaceholderImageGenerator.TryRenderDefaultPreview(shipClass, out var renderResult))
        {
            ClearDefaultPlaceholderPreviewState();
            lastDefaultPlaceholderShipObjectId = shipClass.objectId;
            lastDefaultPlaceholderSignature = signature;
            return;
        }

        DisposeDefaultPlaceholderPreviewTexture();
        defaultPlaceholderPreviewTexture = renderResult.previewTexture;
        defaultPlaceholderPreviewImage.image = defaultPlaceholderPreviewTexture;
        lastDefaultPlaceholderShipObjectId = shipClass.objectId;
        lastDefaultPlaceholderSignature = signature;

        if (renderResult.topTexture != null)
            Destroy(renderResult.topTexture);
        if (renderResult.iconTexture != null)
            Destroy(renderResult.iconTexture);
    }

    void RebuildBatterySectorArcCards(ShipClass shipClass)
    {
        batterySectorArcsContainer.Clear();

        if (shipClass?.batteryRecords == null)
            return;

        for (int i = 0; i < shipClass.batteryRecords.Count; i++)
        {
            batterySectorArcsContainer.Add(BuildBatterySectorArcCard(shipClass.batteryRecords[i], i));
        }
    }

    void RebuildBatteryFigureCharts(ShipClass shipClass)
    {
        batteryFigureChartsContainer.Clear();

        if (shipClass?.batteryRecords == null)
            return;

        for (int i = 0; i < shipClass.batteryRecords.Count; i++)
        {
            batteryFigureChartsContainer.Add(BuildBatteryFigureChartCard(shipClass, shipClass.batteryRecords[i], i));
        }
    }

    VisualElement BuildBatterySectorArcCard(BatteryRecord batteryRecord, int batteryIndex)
    {
        var card = new VisualElement();
        card.style.width = 220;
        card.style.minWidth = 220;
        card.style.alignItems = Align.Center;
        card.style.marginRight = 8;
        card.style.marginBottom = 8;
        card.style.paddingTop = 6;
        card.style.paddingRight = 6;
        card.style.paddingBottom = 6;
        card.style.paddingLeft = 6;
        card.style.borderTopWidth = 1;
        card.style.borderRightWidth = 1;
        card.style.borderBottomWidth = 1;
        card.style.borderLeftWidth = 1;
        card.style.borderTopColor = Color.black;
        card.style.borderRightColor = Color.black;
        card.style.borderBottomColor = Color.black;
        card.style.borderLeftColor = Color.black;

        var titleLabel = new Label(GetBatterySectorArcTitle(batteryRecord, batteryIndex));
        titleLabel.style.width = Length.Percent(100);
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.whiteSpace = WhiteSpace.Normal;
        titleLabel.style.marginBottom = 6;
        card.Add(titleLabel);

        var indicatorRoot = CreateSectorArcIndicatorLayout();
        var binder = new SectorArcIndicatorBinder();
        binder.BindUI(indicatorRoot);
        binder.BindBatteryData(batteryRecord);
        card.Add(indicatorRoot);

        return card;
    }

    VisualElement BuildBatteryFigureChartCard(ShipClass shipClass, BatteryRecord batteryRecord, int batteryIndex)
    {
        var card = new VisualElement();
        card.style.flexDirection = FlexDirection.Column;
        card.style.alignItems = Align.Stretch;
        card.style.marginBottom = 8;
        card.style.paddingTop = 6;
        card.style.paddingRight = 6;
        card.style.paddingBottom = 6;
        card.style.paddingLeft = 6;
        card.style.borderTopWidth = 1;
        card.style.borderRightWidth = 1;
        card.style.borderBottomWidth = 1;
        card.style.borderLeftWidth = 1;
        card.style.borderTopColor = Color.black;
        card.style.borderRightColor = Color.black;
        card.style.borderBottomColor = Color.black;
        card.style.borderLeftColor = Color.black;

        var titleLabel = new Label(GetBatterySectorArcTitle(batteryRecord, batteryIndex));
        titleLabel.style.width = Length.Percent(100);
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.whiteSpace = WhiteSpace.Normal;
        titleLabel.style.marginBottom = 6;
        card.Add(titleLabel);

        var chartRow = new VisualElement();
        chartRow.style.flexDirection = FlexDirection.Row;
        chartRow.style.alignItems = Align.FlexStart;
        var chart = new BatteryPenetrationFireControlChart();
        chart.style.flexGrow = 1;
        chart.style.minWidth = 560;
        chart.style.height = 220;
        chart.style.minHeight = 220;
        chart.SetPoints(BuildBatteryFigurePoints(batteryRecord));
        chart.SetRangeYards(batteryRecord?.rangeYards);
        chart.SetMainBeltEffectiveInches(shipClass?.armorRating?.mainBelt?.effectInch);
        chartRow.Add(chart);
        chartRow.Add(BuildBatteryFigureLegend());
        card.Add(chartRow);

        return card;
    }

    VisualElement CreateSectorArcIndicatorLayout()
    {
        var indicatorRoot = new VisualElement();
        indicatorRoot.style.flexGrow = 0;
        indicatorRoot.style.alignItems = Align.Center;
        indicatorRoot.Add(CreateSectorArcIndicatorRow("PortForward", "Forward", "StarboardForward"));
        indicatorRoot.Add(CreateSectorArcIndicatorRow("PortMidship", "Midship", "StarboardMidship"));
        indicatorRoot.Add(CreateSectorArcIndicatorRow("PortAfter", "After", "StarboardAfter"));
        return indicatorRoot;
    }

    VisualElement CreateSectorArcIndicatorRow(params string[] indicatorNames)
    {
        var row = new VisualElement();
        row.style.flexGrow = 0;
        row.style.flexDirection = FlexDirection.Row;

        foreach (var indicatorName in indicatorNames)
        {
            var indicator = new BatteryArcIndicator();
            indicator.name = indicatorName;
            indicator.style.justifyContent = Justify.Center;
            row.Add(indicator);
        }

        return row;
    }

    void ClearSectorArcState()
    {
        batterySectorArcsContainer?.Clear();
        batteryFigureChartsContainer?.Clear();
        torpedoSectorArcIndicatorBinder.BindTorpedoData((ShipClass)null);
        if (torpedoSectorTitleLabel != null)
        {
            torpedoSectorTitleLabel.text = string.Empty;
        }
        lastSectorArcSignature = null;
        lastSectorArcShipObjectId = null;
    }

    string GetBatterySectorArcTitle(BatteryRecord batteryRecord, int batteryIndex)
    {
        var shortName = batteryRecord?.name?.GetShortName();
        return string.IsNullOrWhiteSpace(shortName) ? Localize("Battery {0}", batteryIndex + 1) : shortName;
    }

    string BuildSectorArcSignature(ShipClass shipClass)
    {
        if (shipClass == null)
            return null;

        var batterySignature = string.Join(";",
            (shipClass.batteryRecords ?? new List<BatteryRecord>())
                .Select(batteryRecord => string.Join("~", new[]
                {
                    batteryRecord?.name?.GetShortName() ?? "",
                    BuildMountLocationSignature(batteryRecord?.mountLocationRecords),
                    BuildPenetrationSignature(batteryRecord?.penetrationTableRecords),
                    BuildFireControlSignature(batteryRecord?.fireControlTableRecords)
                })));

        return string.Join("|", new[]
        {
            shipClass.objectId ?? "",
            $"{shipClass.armorRating?.mainBelt?.effectInch:0.###}",
            batterySignature,
            BuildMountLocationSignature(shipClass.torpedoSector?.mountLocationRecords)
        });
    }

    static string BuildMountLocationSignature(IEnumerable<MountLocationRecord> mountLocationRecords)
    {
        return string.Join(";",
            (mountLocationRecords ?? Enumerable.Empty<MountLocationRecord>())
                .Select(record => string.Join(":", new[]
                {
                    record.mountLocation.ToString(),
                    BuildMountArcSignature(record.mountArcs)
                })));
    }

    static string BuildMountArcSignature(IEnumerable<MountArcRecord> mountArcs)
    {
        return string.Join(",",
            (mountArcs ?? Enumerable.Empty<MountArcRecord>())
                .Select(arc => $"{arc.startDeg:0.###}-{arc.CoverageDeg:0.###}"));
    }

    static string BuildPenetrationSignature(IEnumerable<PenetrationTableRecord> penetrationTableRecords)
    {
        return string.Join(",",
            (penetrationTableRecords ?? Enumerable.Empty<PenetrationTableRecord>())
                .Select(record => $"{record.distanceYards:0.###}:{record.verticalPenetrationInchs:0.###}:{record.horizontalPenetrationInchs:0.###}:{record.rateOfFire:0.###}:{record.rangeBand}"));
    }

    static readonly FireControlComparisonColumn[] FireControlComparisonColumns =
    {
        new("S/B", RangeBand.Short, TargetAspect.Broad),
        new("S/N", RangeBand.Short, TargetAspect.Narrow),
        new("M/B", RangeBand.Medium, TargetAspect.Broad),
        new("M/N", RangeBand.Medium, TargetAspect.Narrow),
        new("L/B", RangeBand.Long, TargetAspect.Broad),
        new("L/N", RangeBand.Long, TargetAspect.Narrow),
        new("E/B", RangeBand.Extreme, TargetAspect.Broad),
        new("E/N", RangeBand.Extreme, TargetAspect.Narrow),
    };

    void PopupFireControlModelComparisonDialog(BatteryRecord batteryRecord)
    {
        if (batteryRecord == null)
            return;

        if ((batteryRecord.fireControlTableRecords == null || batteryRecord.fireControlTableRecords.Count == 0) &&
            (batteryRecord.penetrationTableRecords == null || batteryRecord.penetrationTableRecords.Count == 0))
        {
            DialogRoot.Instance.PopupMessageDialog(Localize("Fire control and penetration tables are empty."), Localize("Model Comparison"));
            return;
        }

        DialogRoot.Instance.PopupModelComparisonDialog(
            Localize("Model Comparison"),
            () => BuildModelComparisonContent(batteryRecord),
            Localize("Close")
        );
    }

    VisualElement BuildModelComparisonContent(BatteryRecord batteryRecord)
    {
        var tabView = new TabView
        {
            name = "ModelComparisonTabView",
            style =
            {
                flexGrow = 1,
                flexShrink = 1,
            }
        };

        tabView.Add(BuildModelComparisonTab(Localize("Fire Control"), BuildFireControlModelComparisonContent(batteryRecord)));
        tabView.Add(BuildModelComparisonTab(Localize("Penetration"), BuildPenetrationModelComparisonContent(batteryRecord)));
        return tabView;
    }

    Tab BuildModelComparisonTab(string label, VisualElement content)
    {
        var tab = new Tab
        {
            label = label,
            style =
            {
                flexGrow = 1,
            }
        };
        tab.Add(content);
        return tab;
    }

    VisualElement BuildFireControlModelComparisonContent(BatteryRecord batteryRecord)
    {
        if (batteryRecord.fireControlTableRecords == null || batteryRecord.fireControlTableRecords.Count == 0)
        {
            var empty = new Label(Localize("Fire control table is empty."));
            empty.style.whiteSpace = WhiteSpace.Normal;
            return empty;
        }

        var records = batteryRecord.fireControlTableRecords
            .OrderBy(record => record.speedThresholdKnot)
            .ToList();
        var hasStandardTable = TryGetStandardFireControlTableRecords(batteryRecord, out var standardCode, out var standardRecords);
        var hasLatentModel = TryGetLatentFireControlTableRecords(
            batteryRecord,
            out var latentCode,
            out var latentBase,
            out _,
            out var latentRecords);
        var roundedLatentRecords = hasLatentModel ? RoundFireControlTableRecords(latentRecords) : null;
        var bestStandards = FindBestMatchingStandardFireControlCodes(records);

        var scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;
        scrollView.style.flexShrink = 1;

        var summary = new Label();
        summary.style.whiteSpace = WhiteSpace.Normal;
        summary.style.marginBottom = 10;
        scrollView.Add(summary);

        if (batteryRecord.customFireControlTable)
        {
            var customNotice = new Label(Localize("Custom Fire Control Table: differences from the standard code table are expected."));
            customNotice.style.whiteSpace = WhiteSpace.Normal;
            customNotice.style.marginBottom = 10;
            scrollView.Add(customNotice);
        }

        if (!hasStandardTable)
        {
            var warning = new Label(Localize("No standard table is available for the current Role/Code/Era."));
            warning.style.whiteSpace = WhiteSpace.Normal;
            warning.style.marginBottom = 10;
            scrollView.Add(warning);
        }

        if (!hasLatentModel)
        {
            var warning = new Label(Localize("No latent variable model parameters are available for the current Role/Code/Era."));
            warning.style.whiteSpace = WhiteSpace.Normal;
            warning.style.marginBottom = 10;
            scrollView.Add(warning);
        }

        var tablesContainer = new VisualElement();
        scrollView.Add(tablesContainer);

        void RefreshComparison()
        {
            var standardStats = hasStandardTable ? CalculateFireControlComparisonStats(records, standardRecords) : new FireControlErrorStats();
            var latentStats = hasLatentModel ? CalculateFireControlComparisonStats(records, roundedLatentRecords) : new FireControlErrorStats();
            var standardStatus = hasStandardTable && standardStats.exact == standardStats.count
                ? Localize("Matches standard fire control table.")
                : Localize("Does not match the standard code table. Review the table/code or mark it as Custom.");

            var summaryLines = new List<string>
            {
                $"{Localize("Overall Error")}",
            };
            if (hasStandardTable)
            {
                summaryLines.Add($"{Localize("Current standard code")} ({standardCode}): {FormatFireControlErrorStats(standardStats)}");
                summaryLines.Add(standardStatus);
            }
            else
            {
                summaryLines.Add(Localize("No standard table is available for the current Role/Code/Era."));
            }
            if (bestStandards.codes.Count > 0)
                summaryLines.Add($"{Localize("Best matching standard codes")} ({FormatStandardCodeList(bestStandards.codes)}): {FormatFireControlErrorStats(bestStandards.stats)}");
            if (hasLatentModel)
                summaryLines.Add(Localize(
                    "Latent variable model code ({0}, base {1}-{2}, midpoint {3}): {4}",
                    latentCode,
                    $"{latentBase.min:0.00}",
                    $"{latentBase.max:0.00}",
                    $"{latentBase.mid:0.00}",
                    FormatFireControlErrorStats(latentStats)));
            summary.text = string.Join("\n", summaryLines);

            tablesContainer.Clear();
            if (hasStandardTable)
            {
                tablesContainer.Add(BuildFireControlStandardComparisonTable(
                    Localize("Standard Code Table"),
                    Localize("Uses the standard SK5 table generated from the current Role, Code, and Era."),
                    records,
                    standardRecords
                ));
            }
            if (hasLatentModel)
            {
                tablesContainer.Add(BuildFireControlLatentComparisonTable(
                    Localize("Latent Variable Model Table"),
                    Localize("Uses midpoint latent base and midpoint range/speed/aspect multipliers from the SK5 fire-control latent variable model."),
                    records,
                    latentRecords
                ));
            }
        }

        RefreshComparison();

        return scrollView;
    }

    VisualElement BuildPenetrationModelComparisonContent(BatteryRecord batteryRecord)
    {
        var scrollView = new ScrollView(ScrollViewMode.Vertical);
        scrollView.style.flexGrow = 1;
        scrollView.style.flexShrink = 1;

        var records = (batteryRecord.penetrationTableRecords ?? new List<PenetrationTableRecord>())
            .GroupBy(record => record.distanceYards)
            .ToDictionary(group => group.Key, group => group.OrderBy(record => record.distanceYards).First());
        var expectedDistances = GetExpectedPenetrationTableDistances(batteryRecord.rangeYards).ToList();

        var summary = new Label();
        summary.style.whiteSpace = WhiteSpace.Normal;
        summary.style.marginBottom = 10;
        scrollView.Add(summary);

        var stats = new PenetrationComparisonStats();
        foreach (var distanceYards in expectedDistances)
        {
            if (!records.TryGetValue(distanceYards, out var record))
            {
                stats.missingRows++;
                continue;
            }

            var prediction = PredictPenetrationRecord(batteryRecord, distanceYards);
            stats.AddRateOfFire(record.rateOfFire, prediction.rateOfFire);
            stats.AddVertical(record.verticalPenetrationInchs, prediction.verticalPenetrationInches);
            stats.AddHorizontal(record.horizontalPenetrationInchs, prediction.horizontalPenetrationInches);
            stats.AddRangeBand(record.rangeBand, prediction.rangeBand);
        }

        var extraRows = records.Keys.Count(distance => !expectedDistances.Contains(distance));
        summary.text =
            $"{Localize("Expected rows")}: {expectedDistances.Count}, {Localize("current rows")}: {records.Count}, {Localize("missing")}: {stats.missingRows}, {Localize("extra")}: {extraRows}\n" +
            $"{Localize("Rate of Fire")}: {FormatFireControlErrorStats(stats.rateOfFire)}\n" +
            $"{Localize("Vertical Penetration")}: {FormatFireControlErrorStats(stats.verticalPenetration)}\n" +
            $"{Localize("Horizontal Penetration")}: {FormatFireControlErrorStats(stats.horizontalPenetration)}\n" +
            $"{Localize("Range Band")}: exact {stats.rangeBandExact}/{stats.rangeBandCount} ({FormatPercent(stats.rangeBandCount == 0 ? 0f : (float)stats.rangeBandExact / stats.rangeBandCount)})";

        var description = new Label(Localize("Predictions use Battery-level fields and distance only. Expected penetration rows use fixed yard marks up to the first mark that covers the battery range."));
        description.style.whiteSpace = WhiteSpace.Normal;
        description.style.marginBottom = 8;
        scrollView.Add(description);

        scrollView.Add(BuildPenetrationComparisonTable(batteryRecord, expectedDistances, records));
        return scrollView;
    }

    VisualElement BuildPenetrationComparisonTable(BatteryRecord batteryRecord, List<float> expectedDistances, Dictionary<float, PenetrationTableRecord> records)
    {
        var section = new VisualElement();
        section.style.minWidth = 900;

        var table = new VisualElement();
        table.style.flexDirection = FlexDirection.Column;
        section.Add(table);

        var header = BuildFireControlComparisonTableRow();
        header.Add(BuildFireControlComparisonCell(Localize("Distance"), true, 82));
        header.Add(BuildFireControlComparisonCell(Localize("ROF"), true, 120));
        header.Add(BuildFireControlComparisonCell(Localize("Band"), true, 128));
        header.Add(BuildFireControlComparisonCell(Localize("Vert Pen"), true, 128));
        header.Add(BuildFireControlComparisonCell(Localize("Hor Pen"), true, 128));
        header.Add(BuildFireControlComparisonCell(Localize("Status"), true, 160));
        table.Add(header);

        foreach (var distanceYards in expectedDistances)
        {
            var row = BuildFireControlComparisonTableRow();
            var prediction = PredictPenetrationRecord(batteryRecord, distanceYards);
            row.Add(BuildFireControlComparisonCell($"{distanceYards:0}", true, 82));

            if (records.TryGetValue(distanceYards, out var record))
            {
                row.Add(BuildFireControlComparisonCell(FormatPenetrationActualPredicted(record.rateOfFire, prediction.rateOfFire), false, 120));
                row.Add(BuildFireControlComparisonCell($"{record.rangeBand} / {prediction.rangeBand}\n{FormatRangeBandDiff(record.rangeBand, prediction.rangeBand)}", false, 128));
                row.Add(BuildFireControlComparisonCell(FormatPenetrationActualPredicted(record.verticalPenetrationInchs, prediction.verticalPenetrationInches), false, 128));
                row.Add(BuildFireControlComparisonCell(FormatPenetrationActualPredicted(record.horizontalPenetrationInchs, prediction.horizontalPenetrationInches), false, 128));
                row.Add(BuildFireControlComparisonCell(Localize("Current row"), false, 160));
            }
            else
            {
                row.Add(BuildFireControlComparisonCell($"{Localize("Missing")} / {prediction.rateOfFire:0.0}", false, 120));
                row.Add(BuildFireControlComparisonCell($"{Localize("Missing")} / {prediction.rangeBand}", false, 128));
                row.Add(BuildFireControlComparisonCell($"{Localize("Missing")} / {prediction.verticalPenetrationInches:0.0}", false, 128));
                row.Add(BuildFireControlComparisonCell($"{Localize("Missing")} / {prediction.horizontalPenetrationInches:0.0}", false, 128));
                row.Add(BuildFireControlComparisonCell(Localize("Expected by range coverage"), false, 160));
            }

            table.Add(row);
        }

        var legend = new Label(Localize("Each cell is shown as current / model, then model-current delta. Missing rows show only model values."));
        legend.style.whiteSpace = WhiteSpace.Normal;
        legend.style.marginTop = 4;
        section.Add(legend);

        return section;
    }

    public static readonly float[] PenetrationTableDistanceYards =
    {
        2000f, 4000f, 6000f, 8000f, 10000f, 12000f, 15000f,
        18000f, 21000f, 24000f, 27000f, 30000f, 33000f, 36000f
    };

    static IEnumerable<float> GetExpectedPenetrationTableDistances(float rangeYards)
    {
        if (rangeYards <= 0f)
        {
            yield return PenetrationTableDistanceYards[0];
            yield break;
        }

        foreach (var distance in PenetrationTableDistanceYards)
        {
            yield return distance;
            if (distance >= rangeYards)
                yield break;
        }
    }

    static PenetrationPrediction PredictPenetrationRecord(BatteryRecord batteryRecord, float distanceYards)
    {
        return new PenetrationPrediction
        {
            distanceYards = distanceYards,
            rateOfFire = PredictPenetrationRateOfFire(batteryRecord, distanceYards),
            rangeBand = PredictPenetrationRangeBand(batteryRecord, distanceYards),
            verticalPenetrationInches = PredictVerticalPenetrationInches(batteryRecord, distanceYards),
            horizontalPenetrationInches = PredictHorizontalPenetrationInches(batteryRecord, distanceYards),
        };
    }

    static List<PenetrationTableRecord> BuildModelPenetrationTableRecords(BatteryRecord batteryRecord)
    {
        return GetExpectedPenetrationTableDistances(batteryRecord?.rangeYards ?? 0f)
            .Select(distanceYards =>
            {
                var prediction = PredictPenetrationRecord(batteryRecord, distanceYards);
                return new PenetrationTableRecord
                {
                    distanceYards = prediction.distanceYards,
                    rateOfFire = prediction.rateOfFire,
                    rangeBand = prediction.rangeBand,
                    horizontalPenetrationInchs = prediction.horizontalPenetrationInches,
                    verticalPenetrationInchs = prediction.verticalPenetrationInches,
                };
            })
            .ToList();
    }

    static int ResetPenetrationTableFromModel(BatteryRecord batteryRecord)
    {
        if (batteryRecord == null)
            return 0;

        var modelRecords = BuildModelPenetrationTableRecords(batteryRecord);
        batteryRecord.penetrationTableRecords ??= new List<PenetrationTableRecord>();
        batteryRecord.penetrationTableRecords.Clear();
        batteryRecord.penetrationTableRecords.AddRange(modelRecords);
        return modelRecords.Count;
    }

    static float PredictPenetrationRateOfFire(BatteryRecord batteryRecord, float distanceYards)
    {
        const float fixedProcessSeconds = 9.090133f;
        const float equivalentVelocityYardsPerSecond = 371.07068f;
        var cap = 120f / (fixedProcessSeconds + distanceYards / equivalentVelocityYardsPerSecond);
        var inherent = Mathf.Max(0f, (batteryRecord?.maxRateOfFireShootPerMin ?? 0f) * 2f);
        return RoundTenth(Mathf.Min(inherent, cap));
    }

    static RangeBand PredictPenetrationRangeBand(BatteryRecord batteryRecord, float distanceYards)
    {
        var rangeYards = batteryRecord?.rangeYards ?? 0f;
        if (rangeYards <= 0f)
            return RangeBand.Short;

        var rel = distanceYards / rangeYards;
        var shellSize = batteryRecord?.shellSizeInch ?? 0f;

        var shortToMedium = 0.56f;
        var mediumToLong = 0.90f;
        var longToExtreme = 1.05f;

        if (rangeYards <= 5900f)
        {
            shortToMedium -= 0.08f;
            mediumToLong -= 0.10f;
        }

        if (shellSize >= 12f)
        {
            shortToMedium += 0.08f;
            mediumToLong += 0.08f;
            longToExtreme += 0.08f;
        }

        if (rel < shortToMedium)
            return RangeBand.Short;
        if (rel < mediumToLong)
            return RangeBand.Medium;
        if (rel < longToExtreme)
            return RangeBand.Long;
        return RangeBand.Extreme;
    }

    static float PredictVerticalPenetrationInches(BatteryRecord batteryRecord, float distanceYards)
    {
        var shellSize = Mathf.Max(0.1f, batteryRecord?.shellSizeInch ?? 0f);
        var shellWeight = Mathf.Max(0.1f, batteryRecord?.shellWeightPounds ?? 0f);
        var rangeYards = Mathf.Max(1f, batteryRecord?.rangeYards ?? 0f);
        var maxRof = batteryRecord?.maxRateOfFireShootPerMin ?? 0f;
        var distanceKyd = distanceYards / 1000f;
        var logShellSize = Mathf.Log(shellSize);
        var logRange = Mathf.Log(rangeYards);
        var logValue = -7.19567f
            - 0.596694f * logShellSize
            + 0.702142f * Mathf.Log(shellWeight)
            + 0.733421f * logRange
            + 0.0331102f * maxRof
            + 0.402345f * distanceKyd
            + 0.00675885f * distanceKyd * distanceKyd
            + 0.0367314f * logShellSize * distanceKyd
            - 0.0718001f * logRange * distanceKyd;

        return RoundTenth(Mathf.Exp(logValue));
    }

    static float PredictHorizontalPenetrationInches(BatteryRecord batteryRecord, float distanceYards)
    {
        var shellSize = Mathf.Max(0.1f, batteryRecord?.shellSizeInch ?? 0f);
        var shellWeight = Mathf.Max(0.1f, batteryRecord?.shellWeightPounds ?? 0f);
        var rangeYards = Mathf.Max(1f, batteryRecord?.rangeYards ?? 0f);
        var maxRof = batteryRecord?.maxRateOfFireShootPerMin ?? 0f;
        var rel = distanceYards / rangeYards;
        var logValue = -13.5807f
            - 0.404477f * Mathf.Log(shellSize)
            + 0.492548f * Mathf.Log(shellWeight)
            + 1.01641f * Mathf.Log(rangeYards)
            - 0.0211344f * maxRof
            + 3.84280f * rel
            - 1.27663f * rel * rel;

        return RoundTenth(Mathf.Exp(logValue));
    }

    static string FormatPenetrationActualPredicted(float actual, float predicted)
    {
        return $"{actual:0.0} / {predicted:0.0}\n{FormatFireControlDiff(predicted - actual, true)}";
    }

    static string FormatRangeBandDiff(RangeBand actual, RangeBand predicted)
    {
        return actual == predicted ? "0" : $"{(int)predicted - (int)actual:+0;-0;0}";
    }

    static string FormatPercent(float value)
    {
        return $"{100f * value:0.#}%";
    }

    static readonly float[] StandardFireControlSpeedThresholds =
    {
        9f, 18f, 27f, 36f, 45f
    };

    static readonly Dictionary<string, string> StandardFireControlTableData = new()
    {
        { "1Q1", "14,8,8,5,6,4,5,3;9,6,6,3,4,2,3,2;7,4,4,3,3,2,3,2;6,4,4,2,3,2,2,1;5,3,3,2,2,1,2,1" },
        { "2Q1", "11,6,7,4,5,3,4,2;7,4,4,3,3,2,2,1;6,3,3,2,2,1,2,1;5,3,3,2,2,1,2,1;4,2,3,2,2,1,1,1" },
        { "1R1", "11,7,7,4,5,3,4,2;8,5,5,3,3,2,3,2;6,4,4,2,3,2,2,1;5,3,3,2,2,1,2,1;4,3,3,2,2,1,1,1" },
        { "2R1", "9,5,5,3,4,2,3,2;6,4,4,2,3,2,2,1;5,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1;3,2,2,1,1,1,1,1" },
        { "1S1", "12,7,7,4,5,3,4,3;8,5,5,3,3,2,3,2;6,4,4,2,3,2,2,1;5,3,3,2,2,1,2,1;5,3,3,2,2,1,2,1" },
        { "2S1", "9,6,6,3,4,2,3,2;6,4,4,2,3,2,2,1;5,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1;4,2,2,1,2,1,1,1" },
        { "1T1", "10,6,6,4,5,3,4,2;7,4,4,3,3,2,2,1;5,3,3,2,2,1,2,1;4,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1" },
        { "2T1", "8,5,5,3,4,2,3,2;5,3,3,2,2,1,2,1;4,3,3,2,2,1,1,1;3,2,2,1,1,1,1,1;3,2,2,1,1,1,1,1" },
        { "1U1", "9,5,5,3,4,2,3,2;6,3,3,2,2,1,2,1;5,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1;3,2,2,1,1,1,1,1" },
        { "2U1", "7,4,4,2,3,2,2,1;4,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1;3,2,2,1,1,1,1,1;3,2,2,1,1,1,1,1" },
        { "1V1", "13,8,8,5,5,3,4,3;8,5,5,3,4,2,3,2;7,4,4,2,3,2,2,1;5,3,3,2,2,1,2,1;5,3,3,2,2,1,2,1" },
        { "1W1", "11,6,7,4,5,3,4,2;7,4,4,3,3,2,2,1;6,3,3,2,2,1,2,1;5,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1" },
        { "2W1", "8,5,5,3,4,2,3,2;5,3,3,2,2,1,2,1;4,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1;3,2,2,1,1,1,1,1" },
        { "1X1", "11,6,7,4,5,3,4,2;7,4,4,3,3,2,2,1;6,3,3,2,2,1,2,1;5,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1" },
        { "2X1", "8,5,5,3,4,2,3,2;5,3,3,2,2,1,2,1;4,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1;3,2,2,1,1,1,1,1" },
        { "1Y1", "9,5,5,3,4,2,3,2;6,4,4,2,3,2,2,1;5,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1;3,2,2,1,1,1,1,1" },
        { "2Y1", "7,4,4,3,3,2,2,1;5,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1;3,2,2,1,1,1,1,1;3,2,2,1,1,1,1,1" },
        { "1Z1", "7,4,4,3,3,2,2,1;5,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1;3,2,2,1,1,1,1,1;3,2,2,1,1,1,1,1" },
        { "2Z1", "6,3,3,2,2,1,2,1;4,2,2,1,2,1,1,1;3,2,2,1,1,1,1,1;2,1,1,1,1,1,1,0;2,1,1,1,1,1,1,0" },
        { "1G2", "21,13,15,10,12,8,10,7;15,9,11,7,9,5,7,5;12,8,9,6,7,4,6,4;10,7,7,5,6,4,5,3;9,6,7,4,5,3,5,3" },
        { "2G2", "16,10,12,7,9,6,8,5;12,7,8,5,7,4,6,4;10,6,7,4,5,3,5,3;8,5,6,4,5,3,4,2;7,5,5,3,4,3,4,2" },
        { "1H2", "23,14,16,10,13,8,11,7;17,11,12,8,10,6,9,5;15,9,11,7,9,5,7,5;13,8,9,6,7,5,6,4;12,8,9,5,7,4,6,4" },
        { "2H2", "17,11,12,8,10,6,8,5;13,8,9,6,7,5,6,4;11,7,8,5,6,4,5,3;10,6,7,4,6,4,5,3;9,6,7,4,5,3,4,3" },
        { "1J2", "19,12,14,9,11,7,9,6;13,8,10,6,8,5,7,4;11,7,8,5,6,4,5,3;9,6,7,4,5,3,5,3;8,5,6,4,5,3,4,3" },
        { "2J2", "15,9,11,7,8,5,7,5;10,7,7,5,6,4,5,3;9,5,6,4,5,3,4,3;7,5,5,3,4,3,4,2;7,4,5,3,4,2,3,2" },
        { "1K2", "18,11,13,8,10,7,9,6;13,8,9,6,7,5,6,4;11,7,8,5,6,4,5,3;9,6,6,4,5,3,4,3;8,5,6,4,5,3,4,3" },
        { "1M2", "16,10,11,7,8,5,7,4;11,7,8,5,6,4,5,3;9,6,6,4,5,3,4,2;8,5,5,3,4,2,3,2;7,5,5,3,4,2,3,2" },
        { "2M2", "13,8,8,5,6,4,5,3;9,6,6,4,4,3,4,2;7,5,5,3,4,2,3,2;6,4,4,3,3,2,3,2;6,4,4,2,3,2,2,1" },
        { "1N2", "16,10,12,7,9,6,8,5;11,7,8,5,7,4,6,4;9,6,7,4,5,3,5,3;8,5,6,4,5,3,4,2;7,5,5,3,4,3,4,2" },
        { "2N2", "13,8,9,6,7,5,6,4;9,6,6,4,5,3,4,3;7,5,5,3,4,3,4,2;6,4,4,3,4,2,3,2;6,4,4,3,3,2,3,2" },
        { "1Q2", "14,9,9,6,7,4,6,4;10,6,7,4,5,3,4,3;8,5,5,3,4,3,3,2;7,4,5,3,3,2,3,2;6,4,4,3,3,2,3,2" },
        { "2Q2", "11,7,7,5,5,3,4,3;8,5,5,3,4,2,3,2;6,4,4,3,3,2,3,2;5,3,4,2,3,2,2,1;5,3,3,2,2,2,2,1" },
        { "1R2", "11,7,8,5,6,4,5,3;8,5,5,3,4,3,3,2;7,4,4,3,3,2,3,2;6,4,4,2,3,2,2,1;5,3,3,2,3,2,2,1" },
        { "1S2", "12,8,8,5,6,4,5,3;9,5,6,4,4,3,4,2;7,4,5,3,4,2,3,2;6,4,4,2,3,2,2,2;5,3,4,2,3,2,2,1" },
        { "2S2", "9,6,6,4,5,3,4,2;7,4,4,3,3,2,3,2;5,3,4,2,3,2,2,1;5,3,3,2,2,1,2,1;4,3,3,2,2,1,2,1" },
        { "1T2", "10,7,7,4,5,3,4,3;7,5,5,3,4,2,3,2;6,4,4,3,3,2,3,2;5,3,3,2,3,2,2,1;5,3,3,2,2,1,2,1" },
        { "2T2", "8,5,5,3,4,3,3,2;6,4,4,2,3,2,2,1;5,3,3,2,2,1,2,1;4,3,3,2,2,1,2,1;4,2,2,2,2,1,1,1" },
        { "1V2", "13,8,8,5,6,4,5,3;9,6,6,4,4,3,4,2;7,5,5,3,4,2,3,2;6,4,4,3,3,2,3,2;6,4,4,2,3,2,2,1" },
        { "1W2", "11,7,7,4,5,3,4,3;8,5,5,3,4,2,3,2;6,4,4,3,3,2,3,2;5,3,3,2,3,2,2,1;5,3,3,2,2,2,2,1" },
        { "2W2", "8,5,6,3,4,3,3,2;6,4,4,2,3,2,2,2;5,3,3,2,2,2,2,1;4,3,3,2,2,1,2,1;4,2,2,2,2,1,2,1" },
        { "1X2", "11,7,7,4,5,3,4,3;8,5,5,3,4,2,3,2;6,4,4,3,3,2,3,2;5,3,3,2,3,2,2,1;5,3,3,2,2,2,2,1" },
        { "2X2", "8,5,6,3,4,3,3,2;6,4,4,2,3,2,2,2;5,3,3,2,2,2,2,1;4,3,3,2,2,1,2,1;4,2,2,2,2,1,2,1" },
        { "2Y2", "7,4,5,3,3,2,3,2;5,3,3,2,2,2,2,1;4,3,3,2,2,1,2,1;3,2,2,1,2,1,1,1;3,2,2,1,2,1,1,1" },
        { "1A3", "29,20,23,16,19,14,17,12;26,18,20,14,17,12,15,10;24,17,18,13,16,11,14,10;22,15,17,12,15,10,13,9;21,15,17,12,14,10,12,9" },
        { "2A3", "22,15,17,12,14,10,13,9;19,13,15,10,13,9,11,8;18,12,14,10,12,8,10,7;16,12,13,9,11,8,10,7;16,11,12,9,10,7,9,7" },
        { "1B3", "28,20,22,15,18,13,16,11;23,16,18,12,15,10,13,9;20,14,16,11,13,9,12,8;18,13,14,10,12,8,11,7;17,12,13,9,11,8,10,7" },
        { "2B3", "21,15,16,12,14,10,12,9;17,12,13,9,11,8,10,7;15,11,12,8,10,7,9,6;14,10,11,8,9,6,8,6;13,9,10,7,9,6,8,5" },
        { "1C3", "26,18,20,14,17,12,15,11;20,14,15,11,13,9,12,8;17,12,13,9,11,8,10,7;15,10,12,8,10,7,9,6;14,10,11,7,9,6,8,6" },
        { "1D3", "26,18,21,14,17,12,16,11;23,16,18,13,15,11,14,9;21,15,17,12,14,10,13,9;20,14,16,11,13,9,12,8;19,13,15,10,13,9,11,8" },
        { "1E3", "25,17,20,14,16,12,15,10;20,14,16,11,13,9,12,8;18,13,14,10,12,8,11,7;16,11,13,9,11,8,10,7;15,11,12,8,10,7,9,6" },
        { "2E3", "19,13,15,10,12,9,11,8;15,11,12,8,10,7,9,6;14,10,11,7,9,6,8,6;12,9,10,7,8,6,7,5;12,8,9,6,8,5,7,5" },
        { "1F3", "23,16,18,13,15,11,14,10;18,12,14,10,12,8,10,7;15,11,12,8,10,7,9,6;13,9,10,7,9,6,8,5;12,9,10,7,8,6,7,5" },
        { "2F3", "18,13,14,10,12,8,11,7;14,10,11,7,9,6,8,6;12,8,9,6,8,5,7,5;10,7,8,6,7,5,6,4;10,7,7,5,6,4,6,4" },
        { "1G3", "21,14,16,11,14,9,12,8;16,11,12,8,11,7,9,6;14,9,11,7,9,6,8,5;12,8,9,6,8,5,7,5;11,7,9,6,7,5,7,4" },
        { "2G3", "16,11,13,9,11,7,10,6;12,8,10,6,8,5,7,5;11,7,8,6,7,5,6,4;9,6,7,5,6,4,5,4;9,6,7,4,6,4,5,3" },
        { "1H3", "23,15,18,12,15,10,13,9;19,12,15,10,12,8,11,7;17,11,13,9,11,7,10,6;15,10,12,8,10,7,9,6;14,9,11,7,9,6,8,6" },
        { "2H3", "17,11,13,9,11,8,10,7;14,9,11,7,9,6,8,5;12,8,10,6,8,5,7,5;11,7,9,6,7,5,7,4;11,7,8,6,7,5,6,4" },
        { "1J3", "19,13,15,10,12,8,11,7;14,10,11,7,9,6,8,6;12,8,10,6,8,5,7,5;11,7,8,6,7,5,6,4;10,7,8,5,7,4,6,4" },
        { "2J3", "15,10,11,8,10,6,9,6;11,7,9,6,7,5,7,4;10,6,7,5,6,4,6,4;8,6,6,4,5,4,5,3;8,5,6,4,5,3,5,3" },
        { "1K3", "18,12,14,10,12,8,11,7;14,9,11,7,9,6,8,5;12,8,9,6,8,5,7,5;10,7,8,5,7,5,6,4;10,6,7,5,6,4,6,4" },
        { "1M3", "16,11,12,8,9,6,8,5;12,8,9,6,7,5,6,4;10,7,8,5,6,4,5,3;9,6,7,4,5,3,4,3;8,6,6,4,5,3,4,3" },
        { "1N3", "16,11,13,8,11,7,9,6;12,8,10,6,8,5,7,5;10,7,8,5,7,5,6,4;9,6,7,5,6,4,5,4;8,6,7,4,6,4,5,3" },
        { "1Q3", "14,9,10,7,8,5,7,5;11,7,8,5,6,4,5,3;9,6,7,4,5,3,4,3;8,5,6,4,5,3,4,3;7,5,5,3,4,3,4,2" },
        { "1S3", "12,8,9,6,7,5,6,4;9,6,7,4,5,4,5,3;8,5,6,4,5,3,4,3;7,5,5,3,4,3,3,2;6,4,5,3,4,2,3,2" },
    };

    static bool TryGetStandardFireControlTableRecords(BatteryRecord batteryRecord, out string fullCode, out List<FireControlTableRecord> records)
    {
        fullCode = BuildFireControlFullCode(batteryRecord);
        records = null;
        if (string.IsNullOrEmpty(fullCode) || !StandardFireControlTableData.TryGetValue(fullCode, out var tableData))
            return false;

        records = ParseStandardFireControlTable(tableData);
        return true;
    }

    readonly struct LatentFireControlBase
    {
        public readonly float min;
        public readonly float max;
        public readonly float mid;

        public LatentFireControlBase(float min, float max, float mid)
        {
            this.min = min;
            this.max = max;
            this.mid = mid;
        }
    }

    readonly struct LatentFireControlMultipliers
    {
        public readonly float medium;
        public readonly float longRange;
        public readonly float extreme;
        public readonly float speed18;
        public readonly float speed27;
        public readonly float speed36;
        public readonly float speed45;
        public readonly float narrow;

        public LatentFireControlMultipliers(
            float medium,
            float longRange,
            float extreme,
            float speed18,
            float speed27,
            float speed36,
            float speed45,
            float narrow)
        {
            this.medium = medium;
            this.longRange = longRange;
            this.extreme = extreme;
            this.speed18 = speed18;
            this.speed27 = speed27;
            this.speed36 = speed36;
            this.speed45 = speed45;
            this.narrow = narrow;
        }

        public float GetRangeMultiplier(RangeBand rangeBand) => rangeBand switch
        {
            RangeBand.Short => 1f,
            RangeBand.Medium => medium,
            RangeBand.Long => longRange,
            RangeBand.Extreme => extreme,
            _ => 1f
        };

        public float GetSpeedMultiplier(float speedThresholdKnot)
        {
            if (speedThresholdKnot <= 9.001f)
                return 1f;
            if (speedThresholdKnot <= 18.001f)
                return speed18;
            if (speedThresholdKnot <= 27.001f)
                return speed27;
            if (speedThresholdKnot <= 36.001f)
                return speed36;
            return speed45;
        }

        public float GetAspectMultiplier(TargetAspect targetAspect) =>
            targetAspect == TargetAspect.Narrow ? narrow : 1f;
    }

    static readonly Dictionary<(FireControlSystemRole role, FCSCode code), LatentFireControlBase> LatentFireControlBases = new()
    {
        { (FireControlSystemRole.Primary, FCSCode.A), new(28.78f, 29.41f, 29.10f) },
        { (FireControlSystemRole.Secondary, FCSCode.A), new(21.50f, 22.02f, 21.76f) },
        { (FireControlSystemRole.Primary, FCSCode.B), new(27.50f, 28.50f, 28.00f) },
        { (FireControlSystemRole.Secondary, FCSCode.B), new(20.74f, 21.50f, 21.12f) },
        { (FireControlSystemRole.Primary, FCSCode.C), new(25.50f, 26.50f, 26.00f) },
        { (FireControlSystemRole.Primary, FCSCode.D), new(25.50f, 26.50f, 26.00f) },
        { (FireControlSystemRole.Primary, FCSCode.E), new(24.70f, 25.11f, 24.90f) },
        { (FireControlSystemRole.Secondary, FCSCode.E), new(18.50f, 19.02f, 18.76f) },
        { (FireControlSystemRole.Primary, FCSCode.F), new(22.59f, 23.50f, 23.05f) },
        { (FireControlSystemRole.Secondary, FCSCode.F), new(17.50f, 18.50f, 18.00f) },
        { (FireControlSystemRole.Primary, FCSCode.G), new(20.52f, 21.49f, 21.00f) },
        { (FireControlSystemRole.Secondary, FCSCode.G), new(16.06f, 16.50f, 16.28f) },
        { (FireControlSystemRole.Primary, FCSCode.H), new(22.50f, 23.32f, 22.91f) },
        { (FireControlSystemRole.Secondary, FCSCode.H), new(16.71f, 17.46f, 17.09f) },
        { (FireControlSystemRole.Primary, FCSCode.J), new(18.50f, 19.50f, 19.00f) },
        { (FireControlSystemRole.Secondary, FCSCode.J), new(14.50f, 15.13f, 14.82f) },
        { (FireControlSystemRole.Primary, FCSCode.K), new(17.61f, 18.50f, 18.06f) },
        { (FireControlSystemRole.Primary, FCSCode.M), new(15.61f, 16.50f, 16.06f) },
        { (FireControlSystemRole.Secondary, FCSCode.M), new(12.50f, 13.03f, 12.77f) },
        { (FireControlSystemRole.Primary, FCSCode.N), new(15.50f, 16.50f, 16.00f) },
        { (FireControlSystemRole.Secondary, FCSCode.N), new(12.50f, 13.27f, 12.88f) },
        { (FireControlSystemRole.Primary, FCSCode.Q), new(13.50f, 14.17f, 13.83f) },
        { (FireControlSystemRole.Secondary, FCSCode.Q), new(10.50f, 11.06f, 10.78f) },
        { (FireControlSystemRole.Primary, FCSCode.R), new(10.83f, 11.50f, 11.17f) },
        { (FireControlSystemRole.Secondary, FCSCode.R), new(8.50f, 9.33f, 8.91f) },
        { (FireControlSystemRole.Primary, FCSCode.S), new(11.59f, 12.50f, 12.05f) },
        { (FireControlSystemRole.Secondary, FCSCode.S), new(8.87f, 9.50f, 9.19f) },
        { (FireControlSystemRole.Primary, FCSCode.T), new(9.97f, 10.50f, 10.23f) },
        { (FireControlSystemRole.Secondary, FCSCode.T), new(7.62f, 8.50f, 8.06f) },
        { (FireControlSystemRole.Primary, FCSCode.U), new(8.50f, 9.50f, 9.00f) },
        { (FireControlSystemRole.Secondary, FCSCode.U), new(6.50f, 7.50f, 7.00f) },
        { (FireControlSystemRole.Primary, FCSCode.V), new(12.50f, 13.36f, 12.93f) },
        { (FireControlSystemRole.Primary, FCSCode.W), new(10.50f, 11.21f, 10.86f) },
        { (FireControlSystemRole.Secondary, FCSCode.W), new(8.09f, 8.50f, 8.29f) },
        { (FireControlSystemRole.Primary, FCSCode.X), new(10.50f, 11.21f, 10.86f) },
        { (FireControlSystemRole.Secondary, FCSCode.X), new(8.09f, 8.50f, 8.29f) },
        { (FireControlSystemRole.Primary, FCSCode.Y), new(8.50f, 9.50f, 9.00f) },
        { (FireControlSystemRole.Secondary, FCSCode.Y), new(6.50f, 7.50f, 7.00f) },
        { (FireControlSystemRole.Primary, FCSCode.Z), new(6.50f, 7.50f, 7.00f) },
        { (FireControlSystemRole.Secondary, FCSCode.Z), new(5.50f, 6.06f, 5.78f) },
    };

    static readonly LatentFireControlMultipliers PredreadnoughtLatentFireControlMultipliers =
        new(0.6043f, 0.4343f, 0.3467f, 0.6661f, 0.5100f, 0.4202f, 0.3765f, 0.5990f);

    static readonly Dictionary<(FireControlSystemEra era, FCSCode code), LatentFireControlMultipliers> LatentFireControlMultiplierTable = new()
    {
        { (FireControlSystemEra.WorldWarI, FCSCode.G), new(0.7181f, 0.5637f, 0.4929f, 0.7181f, 0.5928f, 0.4929f, 0.4460f, 0.6331f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.H), new(0.7088f, 0.5742f, 0.4984f, 0.7585f, 0.6603f, 0.5714f, 0.5378f, 0.6222f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.J), new(0.7342f, 0.5709f, 0.4975f, 0.6992f, 0.5837f, 0.4936f, 0.4394f, 0.6331f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.K), new(0.7178f, 0.5647f, 0.4897f, 0.7178f, 0.6073f, 0.4897f, 0.4571f, 0.6381f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.M), new(0.6661f, 0.4991f, 0.4170f, 0.6971f, 0.5692f, 0.4873f, 0.4478f, 0.6387f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.N), new(0.7285f, 0.5826f, 0.4991f, 0.7117f, 0.5692f, 0.4968f, 0.4450f, 0.6261f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.Q), new(0.6700f, 0.4892f, 0.4066f, 0.7310f, 0.5873f, 0.4987f, 0.4364f, 0.6421f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.R), new(0.6722f, 0.5084f, 0.4229f, 0.6928f, 0.5826f, 0.5169f, 0.4290f, 0.6288f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.S), new(0.6643f, 0.5015f, 0.4115f, 0.7310f, 0.5928f, 0.5146f, 0.4450f, 0.6235f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.T), new(0.6667f, 0.5111f, 0.4066f, 0.7183f, 0.6199f, 0.4965f, 0.4835f, 0.6557f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.V), new(0.6400f, 0.4933f, 0.4123f, 0.7035f, 0.5692f, 0.4873f, 0.4659f, 0.6400f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.W), new(0.6807f, 0.4892f, 0.4066f, 0.7308f, 0.5873f, 0.4892f, 0.4416f, 0.6182f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.X), new(0.6807f, 0.4892f, 0.4066f, 0.7308f, 0.5873f, 0.4892f, 0.4416f, 0.6182f) },
        { (FireControlSystemEra.WorldWarI, FCSCode.Y), new(0.6889f, 0.4835f, 0.4359f, 0.6889f, 0.5795f, 0.4835f, 0.4835f, 0.6462f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.A), new(0.7773f, 0.6594f, 0.5879f, 0.8857f, 0.8094f, 0.7555f, 0.7283f, 0.7056f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.B), new(0.7805f, 0.6512f, 0.5838f, 0.8207f, 0.7212f, 0.6545f, 0.6140f, 0.7056f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.C), new(0.7684f, 0.6545f, 0.5844f, 0.7684f, 0.6545f, 0.5844f, 0.5404f, 0.6959f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.D), new(0.7979f, 0.6637f, 0.6033f, 0.8757f, 0.8084f, 0.7699f, 0.7281f, 0.6906f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.E), new(0.7954f, 0.6549f, 0.5987f, 0.8007f, 0.7203f, 0.6549f, 0.6163f, 0.7056f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.F), new(0.7747f, 0.6569f, 0.5872f, 0.7612f, 0.6553f, 0.5742f, 0.5345f, 0.6971f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.G), new(0.7824f, 0.6700f, 0.5928f, 0.7568f, 0.6582f, 0.5569f, 0.5378f, 0.6656f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.H), new(0.7848f, 0.6463f, 0.5826f, 0.8247f, 0.7306f, 0.6593f, 0.6222f, 0.6639f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.J), new(0.7747f, 0.6474f, 0.5921f, 0.7615f, 0.6443f, 0.5587f, 0.5438f, 0.6697f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.K), new(0.7596f, 0.6529f, 0.5986f, 0.7696f, 0.6627f, 0.5600f, 0.5345f, 0.6697f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.M), new(0.7604f, 0.5712f, 0.4920f, 0.7517f, 0.6387f, 0.5600f, 0.5110f, 0.6697f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.N), new(0.8178f, 0.6931f, 0.5712f, 0.7551f, 0.6282f, 0.5576f, 0.5149f, 0.6635f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.Q), new(0.7310f, 0.5742f, 0.4952f, 0.7851f, 0.6557f, 0.5795f, 0.5111f, 0.6557f) },
        { (FireControlSystemEra.WorldWarII, FCSCode.S), new(0.7592f, 0.5928f, 0.5061f, 0.7592f, 0.6696f, 0.5782f, 0.5195f, 0.6643f) },
    };

    static string BuildFireControlFullCode(BatteryRecord batteryRecord)
    {
        var fcs = batteryRecord?.fireControlType;
        if (fcs == null || fcs.code == FCSCode.Custom)
            return null;

        var rolePrefix = fcs.role switch
        {
            FireControlSystemRole.Primary => "1",
            FireControlSystemRole.Secondary => "2",
            _ => null
        };
        var eraSuffix = fcs.era switch
        {
            FireControlSystemEra.Predreadnought => "1",
            FireControlSystemEra.WorldWarI => "2",
            FireControlSystemEra.WorldWarII => "3",
            _ => null
        };

        return rolePrefix == null || eraSuffix == null ? null : $"{rolePrefix}{fcs.code}{eraSuffix}";
    }

    static bool TryGetLatentFireControlTableRecords(
        BatteryRecord batteryRecord,
        out string fullCode,
        out LatentFireControlBase latentBase,
        out LatentFireControlMultipliers multipliers,
        out List<FireControlTableRecord> records)
    {
        fullCode = BuildFireControlFullCode(batteryRecord);
        latentBase = default;
        multipliers = default;
        records = null;

        var fcs = batteryRecord?.fireControlType;
        if (fcs == null || fcs.code == FCSCode.Custom)
            return false;

        if (!LatentFireControlBases.TryGetValue((fcs.role, fcs.code), out latentBase))
            return false;

        if (!TryGetLatentFireControlMultipliers(fcs.era, fcs.code, out multipliers))
            return false;

        records = BuildLatentFireControlTableRecords(latentBase.mid, multipliers);
        return true;
    }

    static bool TryGetLatentFireControlMultipliers(FireControlSystemEra era, FCSCode code, out LatentFireControlMultipliers multipliers)
    {
        if (era == FireControlSystemEra.Predreadnought)
        {
            multipliers = PredreadnoughtLatentFireControlMultipliers;
            return true;
        }

        return LatentFireControlMultiplierTable.TryGetValue((era, code), out multipliers);
    }

    static List<FireControlTableRecord> BuildLatentFireControlTableRecords(float latentBase, LatentFireControlMultipliers multipliers)
    {
        return StandardFireControlSpeedThresholds
            .Select(speedThresholdKnot => new FireControlTableRecord
            {
                speedThresholdKnot = speedThresholdKnot,
                shortBroad = PredictLatentFireControlValue(latentBase, multipliers, speedThresholdKnot, RangeBand.Short, TargetAspect.Broad),
                shortNarrow = PredictLatentFireControlValue(latentBase, multipliers, speedThresholdKnot, RangeBand.Short, TargetAspect.Narrow),
                mediumBroad = PredictLatentFireControlValue(latentBase, multipliers, speedThresholdKnot, RangeBand.Medium, TargetAspect.Broad),
                mediumNarrow = PredictLatentFireControlValue(latentBase, multipliers, speedThresholdKnot, RangeBand.Medium, TargetAspect.Narrow),
                longBroad = PredictLatentFireControlValue(latentBase, multipliers, speedThresholdKnot, RangeBand.Long, TargetAspect.Broad),
                longNarrow = PredictLatentFireControlValue(latentBase, multipliers, speedThresholdKnot, RangeBand.Long, TargetAspect.Narrow),
                extremeBroad = PredictLatentFireControlValue(latentBase, multipliers, speedThresholdKnot, RangeBand.Extreme, TargetAspect.Broad),
                extremeNarrow = PredictLatentFireControlValue(latentBase, multipliers, speedThresholdKnot, RangeBand.Extreme, TargetAspect.Narrow),
            })
            .ToList();
    }

    static float PredictLatentFireControlValue(
        float latentBase,
        LatentFireControlMultipliers multipliers,
        float speedThresholdKnot,
        RangeBand rangeBand,
        TargetAspect targetAspect)
    {
        return latentBase
            * multipliers.GetRangeMultiplier(rangeBand)
            * multipliers.GetSpeedMultiplier(speedThresholdKnot)
            * multipliers.GetAspectMultiplier(targetAspect);
    }

    static List<FireControlTableRecord> RoundFireControlTableRecords(IReadOnlyList<FireControlTableRecord> records)
    {
        return records
            .Select(record => new FireControlTableRecord
            {
                speedThresholdKnot = record.speedThresholdKnot,
                shortBroad = RoundFireControlValue(record.shortBroad),
                shortNarrow = RoundFireControlValue(record.shortNarrow),
                mediumBroad = RoundFireControlValue(record.mediumBroad),
                mediumNarrow = RoundFireControlValue(record.mediumNarrow),
                longBroad = RoundFireControlValue(record.longBroad),
                longNarrow = RoundFireControlValue(record.longNarrow),
                extremeBroad = RoundFireControlValue(record.extremeBroad),
                extremeNarrow = RoundFireControlValue(record.extremeNarrow),
            })
            .ToList();
    }

    static float RoundFireControlValue(float value) => Mathf.Floor(value + 0.5f);

    static List<FireControlTableRecord> ParseStandardFireControlTable(string tableData)
    {
        var rows = tableData.Split(';');
        var records = new List<FireControlTableRecord>(rows.Length);
        for (var i = 0; i < rows.Length && i < StandardFireControlSpeedThresholds.Length; i++)
        {
            var cells = rows[i].Split(',').Select(int.Parse).ToArray();
            records.Add(new FireControlTableRecord
            {
                speedThresholdKnot = StandardFireControlSpeedThresholds[i],
                shortBroad = cells[0],
                shortNarrow = cells[1],
                mediumBroad = cells[2],
                mediumNarrow = cells[3],
                longBroad = cells[4],
                longNarrow = cells[5],
                extremeBroad = cells[6],
                extremeNarrow = cells[7],
            });
        }
        return records;
    }

    static (List<string> codes, FireControlErrorStats stats) FindBestMatchingStandardFireControlCodes(List<FireControlTableRecord> records)
    {
        FireControlErrorStats bestStats = null;
        var bestCodes = new List<string>();
        foreach (var (code, tableData) in StandardFireControlTableData)
        {
            var stats = CalculateFireControlComparisonStats(records, ParseStandardFireControlTable(tableData));
            if (bestStats == null
                || stats.sumAbs < bestStats.sumAbs
                || (Mathf.Approximately(stats.sumAbs, bestStats.sumAbs) && stats.maxAbs < bestStats.maxAbs)
                || (Mathf.Approximately(stats.sumAbs, bestStats.sumAbs) && Mathf.Approximately(stats.maxAbs, bestStats.maxAbs) && stats.exact > bestStats.exact))
            {
                bestStats = stats;
                bestCodes.Clear();
                bestCodes.Add(code);
            }
            else if (IsSameFireControlError(stats, bestStats))
            {
                bestCodes.Add(code);
            }
        }

        return (bestCodes, bestStats);
    }

    static bool IsSameFireControlError(FireControlErrorStats a, FireControlErrorStats b)
    {
        return b != null
            && Mathf.Approximately(a.sumAbs, b.sumAbs)
            && Mathf.Approximately(a.maxAbs, b.maxAbs)
            && a.exact == b.exact;
    }

    static string FormatStandardCodeList(IReadOnlyList<string> codes)
    {
        const int maxVisible = 6;
        if (codes == null || codes.Count == 0)
            return "";
        if (codes.Count <= maxVisible)
            return string.Join(", ", codes);
        return $"{string.Join(", ", codes.Take(maxVisible))}, +{codes.Count - maxVisible}";
    }

    static bool ResetFireControlTableFromStandardCode(BatteryRecord batteryRecord)
    {
        if (!TryGetStandardFireControlTableRecords(batteryRecord, out _, out var standardRecords))
            return false;

        batteryRecord.fireControlTableRecords ??= new List<FireControlTableRecord>();
        batteryRecord.fireControlTableRecords.Clear();
        batteryRecord.fireControlTableRecords.AddRange(standardRecords);
        batteryRecord.customFireControlTable = false;
        return true;
    }

    VisualElement BuildFireControlStandardComparisonTable(string title, string description, List<FireControlTableRecord> records, IReadOnlyList<FireControlTableRecord> standardRecords)
    {
        var section = new VisualElement();
        section.style.marginTop = 10;
        section.style.marginBottom = 12;

        var titleLabel = new Label(title);
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 2;
        section.Add(titleLabel);

        var descriptionLabel = new Label(description);
        descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
        descriptionLabel.style.marginBottom = 6;
        section.Add(descriptionLabel);

        var table = new VisualElement();
        table.style.flexDirection = FlexDirection.Column;
        table.style.minWidth = 920;
        section.Add(table);

        var header = BuildFireControlComparisonTableRow();
        header.Add(BuildFireControlComparisonCell(Localize("Tgt Spd"), true, 74));
        foreach (var column in FireControlComparisonColumns)
        {
            header.Add(BuildFireControlComparisonCell(column.label, true));
        }
        table.Add(header);

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var standardRecord = i < standardRecords.Count ? standardRecords[i] : null;
            var row = BuildFireControlComparisonTableRow();
            row.Add(BuildFireControlComparisonCell($"{record.speedThresholdKnot:0.#} kt", true, 74));
            foreach (var column in FireControlComparisonColumns)
            {
                var actual = record.GetValue(column.rangeBand, column.targetAspect);
                if (standardRecord == null)
                {
                    row.Add(BuildFireControlComparisonCell($"{actual:0.#} / {Localize("Missing")}", false));
                    continue;
                }

                var predicted = standardRecord.GetValue(column.rangeBand, column.targetAspect);
                var diff = predicted - actual;
                row.Add(BuildFireControlComparisonCell($"{actual:0.#} / {predicted:0.#}\n{FormatFireControlDiff(diff, true)}", false));
            }
            table.Add(row);
        }

        var legend = new Label(Localize("Each cell is shown as current / model, then model-current delta."));
        legend.style.whiteSpace = WhiteSpace.Normal;
        legend.style.marginTop = 4;
        section.Add(legend);

        return section;
    }

    VisualElement BuildFireControlLatentComparisonTable(string title, string description, List<FireControlTableRecord> records, IReadOnlyList<FireControlTableRecord> latentRecords)
    {
        var section = new VisualElement();
        section.style.marginTop = 10;
        section.style.marginBottom = 12;

        var titleLabel = new Label(title);
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 2;
        section.Add(titleLabel);

        var descriptionLabel = new Label(description);
        descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
        descriptionLabel.style.marginBottom = 6;
        section.Add(descriptionLabel);

        var table = new VisualElement();
        table.style.flexDirection = FlexDirection.Column;
        table.style.minWidth = 920;
        section.Add(table);

        var header = BuildFireControlComparisonTableRow();
        header.Add(BuildFireControlComparisonCell(Localize("Tgt Spd"), true, 74));
        foreach (var column in FireControlComparisonColumns)
        {
            header.Add(BuildFireControlComparisonCell(column.label, true));
        }
        table.Add(header);

        for (var i = 0; i < records.Count; i++)
        {
            var record = records[i];
            var latentRecord = i < latentRecords.Count ? latentRecords[i] : null;
            var row = BuildFireControlComparisonTableRow();
            row.Add(BuildFireControlComparisonCell($"{record.speedThresholdKnot:0.#} kt", true, 74));
            foreach (var column in FireControlComparisonColumns)
            {
                var actual = record.GetValue(column.rangeBand, column.targetAspect);
                if (latentRecord == null)
                {
                    row.Add(BuildFireControlComparisonCell($"{actual:0.#} / {Localize("Missing")}", false));
                    continue;
                }

                var latent = latentRecord.GetValue(column.rangeBand, column.targetAspect);
                var rounded = RoundFireControlValue(latent);
                var diff = rounded - actual;
                row.Add(BuildFireControlComparisonCell($"{actual:0.#} / {latent:0.00} -> {rounded:0.#}\n{FormatFireControlDiff(diff, true)}", false));
            }
            table.Add(row);
        }

        var legend = new Label(Localize("Each cell is shown as current / latent variable -> rounded, then rounded-current delta."));
        legend.style.whiteSpace = WhiteSpace.Normal;
        legend.style.marginTop = 4;
        section.Add(legend);

        return section;
    }

    static VisualElement BuildFireControlComparisonTableRow()
    {
        var row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.flexShrink = 0;
        return row;
    }

    static Label BuildFireControlComparisonCell(string text, bool isHeader, int width = 96)
    {
        var cell = new Label(text);
        cell.style.width = width;
        cell.style.minHeight = isHeader ? 24 : 40;
        cell.style.paddingLeft = 4;
        cell.style.paddingRight = 4;
        cell.style.paddingTop = 3;
        cell.style.paddingBottom = 3;
        cell.style.marginRight = 1;
        cell.style.marginBottom = 1;
        cell.style.unityTextAlign = TextAnchor.MiddleCenter;
        cell.style.whiteSpace = WhiteSpace.Normal;
        cell.style.backgroundColor = isHeader ? new Color(0.16f, 0.16f, 0.16f, 0.18f) : new Color(0.16f, 0.16f, 0.16f, 0.08f);
        if (isHeader)
            cell.style.unityFontStyleAndWeight = FontStyle.Bold;
        return cell;
    }

    static FireControlErrorStats CalculateFireControlComparisonStats(List<FireControlTableRecord> records, IReadOnlyList<FireControlTableRecord> predictedRecords)
    {
        var stats = new FireControlErrorStats();
        var count = Mathf.Min(records.Count, predictedRecords?.Count ?? 0);
        for (var i = 0; i < count; i++)
        {
            var actualRecord = records[i];
            var predictedRecord = predictedRecords[i];
            foreach (var column in FireControlComparisonColumns)
            {
                stats.Add(
                    actualRecord.GetValue(column.rangeBand, column.targetAspect),
                    predictedRecord.GetValue(column.rangeBand, column.targetAspect));
            }
        }
        return stats;
    }

    static string FormatFireControlErrorStats(FireControlErrorStats stats)
    {
        if (stats.count == 0)
            return "n/a";

        return $"exact {stats.exact}/{stats.count} ({(100f * stats.exact / stats.count):0.#}%), MAE {stats.MAE:0.###}, RMSE {stats.RMSE:0.###}, max {stats.maxAbs:0.###}";
    }

    static string FormatFireControlDiff(float value, bool roundPredictions)
    {
        return roundPredictions ? $"{value:+0.#;-0.#;0}" : $"{value:+0.00;-0.00;0.00}";
    }

    static float RoundTenth(float value)
    {
        return Mathf.Floor(value * 10f + 0.5f) / 10f;
    }

    readonly struct FireControlComparisonColumn
    {
        public readonly string label;
        public readonly RangeBand rangeBand;
        public readonly TargetAspect targetAspect;

        public FireControlComparisonColumn(string label, RangeBand rangeBand, TargetAspect targetAspect)
        {
            this.label = label;
            this.rangeBand = rangeBand;
            this.targetAspect = targetAspect;
        }
    }

    class FireControlErrorStats
    {
        public int count;
        public int exact;
        public float sumAbs;
        public float sumSquared;
        public float maxAbs;

        public float MAE => count == 0 ? 0f : sumAbs / count;
        public float RMSE => count == 0 ? 0f : Mathf.Sqrt(sumSquared / count);

        public void Add(float actual, float predicted)
        {
            var abs = Mathf.Abs(predicted - actual);
            count++;
            if (abs <= 0.001f)
                exact++;
            sumAbs += abs;
            sumSquared += abs * abs;
            maxAbs = Mathf.Max(maxAbs, abs);
        }
    }

    class PenetrationComparisonStats
    {
        public readonly FireControlErrorStats rateOfFire = new();
        public readonly FireControlErrorStats verticalPenetration = new();
        public readonly FireControlErrorStats horizontalPenetration = new();
        public int rangeBandCount;
        public int rangeBandExact;
        public int missingRows;

        public void AddRateOfFire(float actual, float predicted) => rateOfFire.Add(actual, predicted);
        public void AddVertical(float actual, float predicted) => verticalPenetration.Add(actual, predicted);
        public void AddHorizontal(float actual, float predicted) => horizontalPenetration.Add(actual, predicted);

        public void AddRangeBand(RangeBand actual, RangeBand predicted)
        {
            rangeBandCount++;
            if (actual == predicted)
                rangeBandExact++;
        }
    }

    class PenetrationPrediction
    {
        public float distanceYards;
        public float rateOfFire;
        public RangeBand rangeBand;
        public float verticalPenetrationInches;
        public float horizontalPenetrationInches;
    }

    static string BuildFireControlSignature(IEnumerable<FireControlTableRecord> fireControlTableRecords)
    {
        return string.Join(",",
            (fireControlTableRecords ?? Enumerable.Empty<FireControlTableRecord>())
                .Select(record => $"{record.speedThresholdKnot:0.###}:{record.shortBroad:0.###}:{record.shortNarrow:0.###}:{record.mediumBroad:0.###}:{record.mediumNarrow:0.###}:{record.longBroad:0.###}:{record.longNarrow:0.###}:{record.extremeBroad:0.###}:{record.extremeNarrow:0.###}"));
    }

    VisualElement BuildBatteryFigureLegend()
    {
        var legendColumn = new VisualElement();
        legendColumn.style.width = 200;
        legendColumn.style.minWidth = 200;
        legendColumn.style.marginLeft = 10;
        legendColumn.style.paddingTop = 8;
        legendColumn.style.flexDirection = FlexDirection.Column;
        legendColumn.style.alignItems = Align.FlexStart;

        legendColumn.Add(BuildLegendItem(new Color(0.75f, 0.2f, 0.18f, 1f), Localize("Vertical Penetration (in)")));
        legendColumn.Add(BuildLegendItem(new Color(0.16f, 0.42f, 0.78f, 1f), Localize("Horizontal Penetration (in)")));
        legendColumn.Add(BuildLegendItem(new Color(0.12f, 0.6f, 0.24f, 1f), Localize("Fire Control (Lowest Speed Broad)")));

        return legendColumn;
    }

    VisualElement BuildLegendItem(Color color, string text)
    {
        var item = new VisualElement();
        item.style.flexDirection = FlexDirection.Row;
        item.style.alignItems = Align.Center;
        item.style.marginBottom = 6;

        var swatch = new VisualElement();
        swatch.style.width = 10;
        swatch.style.height = 10;
        swatch.style.backgroundColor = color;
        swatch.style.marginRight = 4;
        item.Add(swatch);

        var label = new Label(text);
        label.style.fontSize = 10;
        label.style.whiteSpace = WhiteSpace.Normal;
        label.style.flexGrow = 1;
        item.Add(label);

        return item;
    }

    static List<BatteryFigurePoint> BuildBatteryFigurePoints(BatteryRecord batteryRecord)
    {
        var lowestSpeedFireControlRow = (batteryRecord?.fireControlTableRecords ?? new List<FireControlTableRecord>())
            .OrderBy(record => record.speedThresholdKnot)
            .FirstOrDefault();

        return (batteryRecord?.penetrationTableRecords ?? new List<PenetrationTableRecord>())
            .OrderBy(record => record.distanceYards)
            .Select(record => new BatteryFigurePoint
            {
                distanceYards = record.distanceYards,
                verticalPenetrationInches = record.verticalPenetrationInchs,
                horizontalPenetrationInches = record.horizontalPenetrationInchs,
                fireControlValue = lowestSpeedFireControlRow?.GetValue(record.rangeBand, TargetAspect.Broad) ?? 0f
            })
            .ToList();
    }

    void ClearDefaultPlaceholderPreviewState()
    {
        DisposeDefaultPlaceholderPreviewTexture();
        lastDefaultPlaceholderSignature = null;
        lastDefaultPlaceholderShipObjectId = null;
    }

    void DisposeDefaultPlaceholderPreviewTexture()
    {
        if (defaultPlaceholderPreviewImage != null)
            defaultPlaceholderPreviewImage.image = null;

        if (defaultPlaceholderPreviewTexture != null)
        {
            Destroy(defaultPlaceholderPreviewTexture);
            defaultPlaceholderPreviewTexture = null;
        }
    }

    static bool IsElementActuallyVisible(VisualElement element)
    {
        return element != null
            && element.resolvedStyle.display != DisplayStyle.None
            && element.worldBound.width > 1f
            && element.worldBound.height > 1f;
    }

    static string Localize(string key, params object[] args) => ServiceLocator.Get<ILocalizeService>().Get(key, args);

    static void RefreshPictureField(VisualElement fieldRoot, PictureReference pictureReference)
    {
        if (fieldRoot == null || pictureReference == null)
            return;

        var textField = fieldRoot.Q<TextField>();
        if (textField != null)
            textField.SetValueWithoutNotify(pictureReference.path);

        var toggle = fieldRoot.Q<Toggle>();
        if (toggle != null)
            toggle.SetValueWithoutNotify(pictureReference.isBuiltin);
    }
}
