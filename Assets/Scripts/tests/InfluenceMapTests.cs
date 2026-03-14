using System.Collections.Generic;
using NUnit.Framework;
using NavalCombatCore;
using UnityEngine;

public class InfluenceMapTests
{
    [Test]
    public void GetDeployedShipsRecursive_TraversesNestedGroups()
    {
        var root = new ShipGroup { objectId = "root", childrenObjectIds = new List<string> { "shipA", "child" } };
        var childGroup = new ShipGroup { objectId = "child", parentObjectId = "root", childrenObjectIds = new List<string> { "shipB" } };
        var shipA = new ShipLog { objectId = "shipA", parentObjectId = "root", mapState = MapState.Deployed };
        var shipB = new ShipLog { objectId = "shipB", parentObjectId = "child", mapState = MapState.NotDeployed };

        var members = new Dictionary<string, IShipGroupMember>
        {
            [root.objectId] = root,
            [childGroup.objectId] = childGroup,
            [shipA.objectId] = shipA,
            [shipB.objectId] = shipB,
        };

        var ships = InfluenceMapUtility.GetDeployedShipsRecursive(root, objectId => members.TryGetValue(objectId, out var member) ? member : null);

        Assert.That(ships.Count, Is.EqualTo(1));
        Assert.That(ships[0], Is.SameAs(shipA));
    }

    [Test]
    public void TryBuildBattleBounds_UsesOnlyDeployedShipsAndAddsPadding()
    {
        var ship1 = new ShipLog { mapState = MapState.Deployed, position = new LatLon(20f, 120f) };
        var ship2 = new ShipLog { mapState = MapState.Deployed, position = new LatLon(21f, 122f) };

        var ok = InfluenceMapUtility.TryBuildBattleBounds(new[] { ship1, ship2 }, 0.1f, 0.05f, out var bounds);

        Assert.That(ok, Is.True);
        Assert.That(bounds.minLat, Is.EqualTo(19.9f).Within(0.001f));
        Assert.That(bounds.maxLat, Is.GreaterThanOrEqualTo(21.1f));
        Assert.That(bounds.minLon, Is.LessThanOrEqualTo(119.8f));
        Assert.That(bounds.maxLon, Is.GreaterThanOrEqualTo(122.2f));
    }

    [Test]
    public void TryBuildBattleBounds_UsesConfigurablePadding()
    {
        var ship1 = new ShipLog { mapState = MapState.Deployed, position = new LatLon(20f, 120f) };
        var ship2 = new ShipLog { mapState = MapState.Deployed, position = new LatLon(21f, 122f) };

        var ok = InfluenceMapUtility.TryBuildBattleBounds(new[] { ship1, ship2 }, 0.2f, 0.5f, out var bounds);

        Assert.That(ok, Is.True);
        Assert.That(bounds.minLat, Is.EqualTo(19.5f).Within(0.001f));
        Assert.That(bounds.maxLat, Is.EqualTo(21.5f).Within(0.001f));
        Assert.That(bounds.minLon, Is.EqualTo(119.5f).Within(0.001f));
        Assert.That(bounds.maxLon, Is.EqualTo(122.5f).Within(0.001f));
    }

    [Test]
    public void BuildContourLevels_ReturnsSymmetricLevels()
    {
        var levels = InfluenceMapUtility.BuildContourLevels(40f);

        Assert.That(levels, Is.EqualTo(new List<float> { -40f, -30f, -20f, -10f, 0f, 10f, 20f, 30f, 40f }));
    }

    [Test]
    public void EvaluatePowerContribution_UsesLinear36000YardFalloff()
    {
        Assert.That(InfluenceMapUtility.EvaluatePowerContribution(120f, 0f), Is.EqualTo(120f).Within(0.001f));
        Assert.That(InfluenceMapUtility.EvaluatePowerContribution(120f, 18000f), Is.EqualTo(60f).Within(0.001f));
        Assert.That(InfluenceMapUtility.EvaluatePowerContribution(120f, 36000f), Is.EqualTo(0f).Within(0.001f));
    }

    [Test]
    public void EvaluateSmoothedFirepower_InterpolatesAcrossQuadrants()
    {
        Assert.That(InfluenceMapUtility.EvaluateSmoothedFirepower(10f, 20f, 30f, 40f, 45f), Is.EqualTo(15f).Within(0.001f));
        Assert.That(InfluenceMapUtility.EvaluateSmoothedFirepower(10f, 20f, 30f, 40f, 135f), Is.EqualTo(25f).Within(0.001f));
        Assert.That(InfluenceMapUtility.EvaluateSmoothedFirepower(10f, 20f, 30f, 40f, 315f), Is.EqualTo(25f).Within(0.001f));
    }

    [Test]
    public void ComposeValue_ControlUsesGroup1MinusGroup2()
    {
        var value = InfluenceMapUtility.ComposeValue(InfluenceMapType.Control, 80f, 55f, 30f);

        Assert.That(value, Is.EqualTo(50f).Within(0.001f));
    }

    [Test]
    public void EvaluateDistanceAttenuation_SupportsAllConfiguredAlgorithms()
    {
        Assert.That(
            InfluenceMapUtility.EvaluateDistanceAttenuation(6000f, InfluenceMapFalloffAlgorithm.Linear, 12000f),
            Is.EqualTo(0.5f).Within(0.001f)
        );
        Assert.That(
            InfluenceMapUtility.EvaluateDistanceAttenuation(12000f, InfluenceMapFalloffAlgorithm.Exponential, 12000f),
            Is.EqualTo(Mathf.Exp(-1f)).Within(0.001f)
        );
        Assert.That(
            InfluenceMapUtility.EvaluateDistanceAttenuation(12000f, InfluenceMapFalloffAlgorithm.Inverse, 12000f),
            Is.EqualTo(0.5f).Within(0.001f)
        );
        Assert.That(
            InfluenceMapUtility.EvaluateDistanceAttenuation(12000f, InfluenceMapFalloffAlgorithm.Gaussian, 12000f),
            Is.EqualTo(Mathf.Exp(-0.5f)).Within(0.001f)
        );
    }

    [Test]
    public void GetTopLevelShipGroupsInOobOrder_ReturnsOnlyRoots()
    {
        var rootA = new ShipGroup { objectId = "rootA" };
        var child = new ShipGroup { objectId = "child", parentObjectId = "rootA" };
        var rootB = new ShipGroup { objectId = "rootB" };

        var groups = InfluenceMapUtility.GetTopLevelShipGroupsInOobOrder(new List<ShipGroup> { rootA, child, rootB });

        Assert.That(groups, Is.EqualTo(new List<ShipGroup> { rootA, rootB }));
    }

    [Test]
    public void GetFillBandIndex_MapsValuesIntoContourBands()
    {
        var levels = new List<float> { -10f, -5f, 0f, 5f, 10f };

        Assert.That(InfluenceMapUtility.GetFillBandIndex(levels, -20f), Is.EqualTo(0));
        Assert.That(InfluenceMapUtility.GetFillBandIndex(levels, -2f), Is.EqualTo(1));
        Assert.That(InfluenceMapUtility.GetFillBandIndex(levels, 3f), Is.EqualTo(2));
        Assert.That(InfluenceMapUtility.GetFillBandIndex(levels, 50f), Is.EqualTo(3));
    }
}
