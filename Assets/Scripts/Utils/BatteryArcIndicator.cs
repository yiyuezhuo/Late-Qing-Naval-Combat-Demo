
using System.Collections.Generic;
using System.Threading.Tasks;
using NavalCombatCore;
using UnityEngine;
using UnityEngine.UIElements;

[UxmlElement]
public partial class BatteryArcIndicator : VisualElement
{
    public static readonly string ussClassName = "battery-arc-indicator";

    // public ShipClass currentShipClass;
    public List<(float, float)> startEndTopZeroCWAngles = new();

    public BatteryArcIndicator()
    {
        AddToClassList(ussClassName);

        generateVisualContent += GenerateVisualContent;
    }

    void GenerateVisualContent(MeshGenerationContext context)
    {
        float width = contentRect.width;
        float height = contentRect.height;

        var painter = context.painter2D;
        painter.lineWidth = 1;
        painter.lineCap = LineCap.Butt;
        painter.strokeColor = Color.black;
        // painter.fillColor = Color.yellow;
        painter.fillColor = new Color(1, 1, 0.1f, 0.1f);

        // if (currentShipClass != null)
        // {
        //     foreach (var btyRec in currentShipClass.batteryRecords)
        //     {
        //         foreach (var mntRec in btyRec.mountLocationRecords)
        //         {
        //             foreach (var arc in mntRec.mountArcs)
        //             {
        //                 // painter.BeginPath();
        //                 // painter.Arc(new Vector2(width * 0.5f, height * 0.5f), width * 0.5f, arc.startDeg, arc.startDeg + arc.CoverageDeg);
        //                 // painter.Stroke();
        //                 DrawSectorTopZeroCW(painter, width, height, arc.startDeg, arc.startDeg + arc.CoverageDeg);
        //             }
        //         }
        //     }
        // }
        if (startEndTopZeroCWAngles != null)
        {
            foreach (var (startDeg, endDeg) in startEndTopZeroCWAngles)
            {
                DrawSectorTopZeroCW(painter, width, height, startDeg, endDeg);
            }
        }
        else
        {
            // Used to test in the builder
            // painter.BeginPath();
            // painter.Arc(new Vector2(width * 0.5f, height * 0.5f), width * 0.5f, 60, 300);
            // painter.Stroke();

            DrawSectorTopZeroCW(painter, width, height, 60, 300);
            // DrawSectorRightZeroCCW(painter, width, height, 60, 300);
        }
    }

    static float widthCoef = 1.5f;

    void DrawSectorRightZeroCW(Painter2D painter, float width, float height, float startAngle, float endAngle)
    {
        painter.BeginPath();
        var center = new Vector2(width * 0.5f, height * 0.5f);
        painter.MoveTo(center);
        painter.Arc(center, width * 0.5f * widthCoef, startAngle, endAngle);
        painter.ClosePath();
        painter.Fill();
        painter.Stroke();
    }

    void DrawSectorTopZeroCW(Painter2D painter, float width, float height, float startAngle, float endAngle)
    {
        var startAngle2 = startAngle - 90;
        var endAngle2 = endAngle - 90;
        DrawSectorRightZeroCW(painter, width, height, startAngle2, endAngle2);
        // DrawSectorRightZeroCW(painter, width, height, 10, -10);
        // DrawSectorRightZeroCCW(painter, width, height, -10, 10);
        // DrawSectorRightZeroCCW(painter, width, height, 30, -210);
        // DrawSectorRightZeroCCW(painter, width, height, 30, 150);
        // DrawSectorRightZeroCCW(painter, width, height, 30, 90);
    }
}