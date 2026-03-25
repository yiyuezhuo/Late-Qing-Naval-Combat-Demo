using System.Collections.Generic;
using UnityEngine;
using StrategicCombatCore;
using System.Linq;

public class WaypointController : MonoBehaviour
{
    public LineRenderer lineRenderer;
    float defaultWidthMultiplier = -1f;

    public void Sync(List<XY> waypoints)
    {
        Sync(waypoints, null, true, null, 0f, false);
    }

    public void Sync(List<XY> waypoints, Color? lineColor, bool smooth, float? widthMultiplier, float planarOffset, bool preserveEndpoints)
    {
        var positions = Utils.XYListToVector3Array(waypoints);
        positions = ApplyPlanarOffset(positions, planarOffset, preserveEndpoints);

        StrategicLineRenderUtils.ConfigureLineRenderer(lineRenderer);
        EnsureDefaultWidthMultiplierCaptured();
        if (lineColor.HasValue)
        {
            ApplyColor(lineColor.Value);
        }
        lineRenderer.widthMultiplier = widthMultiplier ?? defaultWidthMultiplier;

        var renderedPositions = smooth
            ? StrategicLineRenderUtils.BuildSmoothPolyline(positions)
            : positions;

        lineRenderer.positionCount = renderedPositions.Length;
        lineRenderer.SetPositions(renderedPositions);
    }

    void EnsureDefaultWidthMultiplierCaptured()
    {
        if (defaultWidthMultiplier < 0f)
        {
            defaultWidthMultiplier = lineRenderer.widthMultiplier;
        }
    }

    void ApplyColor(Color color)
    {
        var gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(color, 0f),
                new GradientColorKey(color, 1f)
            },
            new[]
            {
                new GradientAlphaKey(color.a, 0f),
                new GradientAlphaKey(color.a, 1f)
            }
        );
        lineRenderer.colorGradient = gradient;
        lineRenderer.startColor = color;
        lineRenderer.endColor = color;
    }

    static Vector3[] ApplyPlanarOffset(Vector3[] positions, float planarOffset, bool preserveEndpoints)
    {
        if (positions == null || positions.Length < 2 || Mathf.Approximately(planarOffset, 0f))
            return positions;

        var offsetPositions = new Vector3[positions.Length];
        for (int i = 0; i < positions.Length; i++)
        {
            var direction = GetDirectionAt(positions, i);
            if (direction.sqrMagnitude < 1e-6f)
            {
                offsetPositions[i] = positions[i];
                continue;
            }

            var normal = new Vector3(-direction.y, direction.x, 0f).normalized;
            offsetPositions[i] = positions[i] + normal * planarOffset;
        }

        if (preserveEndpoints)
        {
            offsetPositions[0] = positions[0];
            offsetPositions[^1] = positions[^1];
        }
        return offsetPositions;
    }

    static Vector3 GetDirectionAt(Vector3[] positions, int index)
    {
        if (positions.Length < 2)
            return Vector3.zero;

        if (index == 0)
            return (positions[1] - positions[0]).normalized;

        if (index == positions.Length - 1)
            return (positions[^1] - positions[^2]).normalized;

        var incoming = (positions[index] - positions[index - 1]).normalized;
        var outgoing = (positions[index + 1] - positions[index]).normalized;
        var blended = incoming + outgoing;
        if (blended.sqrMagnitude < 1e-6f)
            return outgoing.sqrMagnitude >= 1e-6f ? outgoing : incoming;

        return blended.normalized;
    }
}
