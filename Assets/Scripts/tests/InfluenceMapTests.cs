using System.Collections.Generic;
using NUnit.Framework;
using NavalCombatCore;

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
    public void TryBuildBattleBounds_AddsPaddingAndIncludesLabels()
    {
        var ship1 = new ShipLog { mapState = MapState.Deployed, position = new LatLon(20f, 120f) };
        var ship2 = new ShipLog { mapState = MapState.Deployed, position = new LatLon(21f, 122f) };
        var labels = new List<LocationLabel>
        {
            new LocationLabel { latitude = 19.5f, longitude = 121f },
        };

        var ok = InfluenceMapUtility.TryBuildBattleBounds(new[] { ship1, ship2 }, labels, out var bounds);

        Assert.That(ok, Is.True);
        Assert.That(bounds.minLat, Is.LessThanOrEqualTo(19.45f));
        Assert.That(bounds.maxLat, Is.GreaterThanOrEqualTo(21.1f));
        Assert.That(bounds.minLon, Is.LessThanOrEqualTo(119.8f));
        Assert.That(bounds.maxLon, Is.GreaterThanOrEqualTo(122.2f));
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
}
