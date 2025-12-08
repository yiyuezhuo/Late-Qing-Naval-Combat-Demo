
using System.Collections.Generic;
using CoreUtils;
using NavalCombatCore;
using Unity.Properties;
using UnityEngine.UIElements;
using System.Linq;
using UnityEngine;
using System;

public class ForceBuilder
{
    public class ForceItem
    {
        public string id; // id of NamedShip or ShipClass
        public int quantity; // NamedShip can only have 1, and ShipClass can have any (if cost restriction is respected.)

        [CreateProperty]
        public string forceBuilderText
        {
            get
            {
                string name;

                var namedShip = EntityManager.Instance.Get<NamedShip>(id);
                var shipClass = namedShip != null ? namedShip.shipClass : EntityManager.Instance.Get<ShipClass>(id);
                var isNamedShip = namedShip != null;
                if(isNamedShip)
                {
                    name = namedShip.name.GetShortName();
                }
                else
                {
                    name = shipClass?.name?.GetShortName() ?? "";
                }
                // var shipClass = GetShipClass();
                var prefix = isNamedShip ? "#" : "";

                return $"{prefix}{name} (x{quantity}, {shipClass.GetPoint()}) >";
            }
        }

        [CreateProperty]
        public Length portraitWidth => new Length(
            (GetShipClass()?.lengthFoot ?? 300) / 1000 * 100, // 300 foot => 30 (30%)
            LengthUnit.Percent
        );

        [CreateProperty]
        public StyleBackground portraitIconStyleBackground => UnityWebRequestImageReader.Instance.FetchStyleBackground(GetShipClass()?.portraitIconReference?.ResolvePath());

        public float GetPoints()
        {
            // var namedShip = EntityManager.Instance.Get<NamedShip>(id);
            // var shipClass = namedShip != null ? namedShip.shipClass : EntityManager.Instance.Get<ShipClass>(id);
            var shipClass = GetShipClass();
            if(shipClass == null)
                return 0;
            return shipClass.GetPoint() * quantity;
        }

        public ShipClass GetShipClass()
        {
            var namedShip = EntityManager.Instance.Get<NamedShip>(id);
            var shipClass = namedShip != null ? namedShip.shipClass : EntityManager.Instance.Get<ShipClass>(id);
            return shipClass;
        }
    }

    public class Force // TODO: Extract some logic to Core?
    {
        public GlobalString topGroupName = new();
        public Country country;
        public float points = 100_000;
        public float battleShipWeight = 50;
        public float cruiserWeight = 50;
        public float torpedoBoatWeight = 1; // mainly for collect remain points
        public float transportWeight = 0;

        public bool enableNamedShip = true;
        public bool enableShipClass = true;
        
        // Auto-generated to from the above parameter or manual specify this value.
        public List<ForceItem> forceItems = new();
        public List<NamedShip> validNamedShips = new();
        public List<ShipClass> validShipClasses = new();

        public float GetTotalUsedPoints()
        {
            return forceItems.Sum(x => x.GetPoints());
        }

        [CreateProperty]
        public float usedPoints => GetTotalUsedPoints();

        [CreateProperty]
        public string progressDesc => $"{GetTotalUsedPoints()}/{points}";

        // public List<ShipLog> selectedShipLogs = new();
        public void Refresh()
        {
            var committedIdSets = forceItems.Select(x => x.id).ToHashSet();
            var gameState = NavalGameState.Instance;
            
            validNamedShips = gameState.namedShips.Where(s => s.shipClass.country == country && !committedIdSets.Contains(s.objectId)).ToList();
            validShipClasses = gameState.shipClasses.Where(s => s.country == country).ToList();
        }

        public override string ToString()
        {
            return $"ForceBuilder.Force({topGroupName.GetMergedNamePure()})";
        }

        public void ForceItemClicked(ForceItem forceItem)
        {
            // var matched = forceItems.FirstOrDefault(item => item.id == forceI)
            forceItem.quantity -= 1;
            if(forceItem.quantity <= 0)
            {
                forceItems.Remove(forceItem);
            }

            Refresh();
        }

        public void ValidNamedShipClicked(NamedShip namedShip)
        {
            // var matchedItem = forceItems.FirstOrDefault(item => item.id == namedShip.objectId);
            // if(matchedI)
            forceItems.Add(new()
            {
                id = namedShip.objectId,
                quantity = 1
            });

            Refresh();
        }

        public void ValidShipClassClicked(ShipClass shipClass)
        {
            var matchedItem = forceItems.FirstOrDefault(item => item.id == shipClass.objectId);
            if(matchedItem == null)
            {
                matchedItem = new()
                {
                    id = shipClass.objectId,
                    quantity = 0
                };
                forceItems.Add(matchedItem);
            }
            matchedItem.quantity += 1;

            // Refresh();
        }

        static HashSet<ShipType> battleshipTypes = new(){
            ShipType.Battleship
        };

        static HashSet<ShipType> cruiserTypes = new(){
            ShipType.Cruiser,
            ShipType.ArmoredCruiser,
            ShipType.LightCruiser,
            ShipType.PatrolGunboat // TODO: Well, is it a proper location, anyway the current PG is classified to cruiser in some reference though.
        };

        static HashSet<ShipType> torpedoBoatTypes = new(){
            ShipType.TorpedoBoat
        };

        static HashSet<ShipType> transportTypes = new(){
            ShipType.Transport,
            ShipType.Repair
        };

        void SampleGroup(HashSet<ShipType> filterTypeSet, ref float subPoints)
        {
            while(true)
            {
                float p = subPoints;
                var possibleNamedShips = !enableNamedShip ? new() : validNamedShips.Where(x => filterTypeSet.Contains(x.shipClass.type) && x.shipClass.GetPoint() <= p && x.shipClass.GetPoint() > 0).ToList();
                var possibleShipClasses = !enableShipClass ? new() : validShipClasses.Where(x => filterTypeSet.Contains(x.type) && x.GetPoint() <= p && x.GetPoint() > 0).ToList();
                if(possibleNamedShips.Count == 0 && possibleShipClasses.Count == 0)
                    break;
                
                var namedShipPercentage = (float)possibleNamedShips.Count / (possibleNamedShips.Count + possibleShipClasses.Count);
                if(RandomUtils.NextFloat() < namedShipPercentage)
                {
                    var sampledNamedShip = RandomUtils.Sample(possibleNamedShips);
                    subPoints -= sampledNamedShip.shipClass.GetPoint();
                    ValidNamedShipClicked(sampledNamedShip);
                }
                else
                {
                    var sampledShipClass = RandomUtils.Sample(possibleShipClasses);
                    subPoints -= sampledShipClass.GetPoint();
                    ValidShipClassClicked(sampledShipClass);
                }
            }
        }

        public void Regenerate()
        {
            forceItems.Clear();
            Refresh();

            var totalWeight = battleShipWeight + cruiserWeight + torpedoBoatWeight + transportWeight;

            var battleShipPoints = points * battleShipWeight / totalWeight;
            var cruiserPoints = points * cruiserWeight / totalWeight;
            var torpedoPoints = points * torpedoBoatWeight / totalWeight;
            var transportPoints = points * transportWeight / totalWeight;

            SampleGroup(battleshipTypes, ref battleShipPoints);

            if(cruiserPoints > 0)
            {
                cruiserPoints += battleShipPoints;
                battleShipPoints = 0;
            }
            else if(torpedoPoints > 0)
            {
                torpedoPoints += battleShipPoints;
                battleShipPoints = 0;
            }
            else if(transportPoints > 0)
            {
                transportPoints += battleShipPoints;
                battleShipPoints = 0;
            }

            SampleGroup(cruiserTypes, ref cruiserPoints);

            if(torpedoPoints > 0)
            {
                torpedoPoints += cruiserPoints;
                cruiserPoints = 0;
            }
            else if(transportPoints > 0)
            {
                transportPoints += cruiserPoints;
                cruiserPoints = 0;
            }

            SampleGroup(torpedoBoatTypes, ref torpedoPoints);

            if(transportPoints > 0)
            {
                transportPoints += torpedoPoints;
                torpedoPoints = 0;
            }

            SampleGroup(transportTypes, ref transportPoints);
        }
    }

    public List<Force> forces = new();

    ListView forceListView;

    public void OnCreated(object sender, VisualElement root)
    {
        forceListView = root.Q<ListView>("ForceListView");
        // Refresh();

        forceListView.makeItem = () =>
        {
            var el = forceListView.itemTemplate.CloneTree(); // Force element
            Utils.BindItemsSourceRecursive(el);

            var forceItemListView = el.Q<ListView>("ForceItemListView");
            forceItemListView.makeItem = () =>
            {
                var el2 = forceItemListView.itemTemplate.CloneTree();
                el2.Q<Button>().clicked += () =>
                {
                    if(Utils.TryResolveCurrentValueForBinding<Force>(el, out var force))
                    {
                        if(Utils.TryResolveCurrentValueForBinding<ForceItem>(el2, out var forceItem))
                        {
                            Debug.Log($"forceItemListView {force} {forceItem}");
                            force.ForceItemClicked(forceItem);
                        }
                    }
                };
                return el2;
            };

            var validNamedShipListView = el.Q<ListView>("ValidNamedShipListView");
            validNamedShipListView.makeItem = () =>
            {
                var el2 = validNamedShipListView.itemTemplate.CloneTree();
                el2.Q<Button>().clicked += () =>
                {
                    if(Utils.TryResolveCurrentValueForBinding<Force>(el, out var force))
                    {
                        if(Utils.TryResolveCurrentValueForBinding<NamedShip>(el2, out var namedShip))
                        {
                            Debug.Log($"validNamedShipListView {force} {namedShip}");
                            force.ValidNamedShipClicked(namedShip);
                        }
                    }
                };

                return el2;
            };

            var validShipClassListView = el.Q<ListView>("ValidShipClassListView");
            validShipClassListView.makeItem = () =>
            {
                var el2 = validShipClassListView.itemTemplate.CloneTree();
                el2.Q<Button>().clicked += () =>
                {
                    if(Utils.TryResolveCurrentValueForBinding<Force>(el, out var force))
                    {
                        if(Utils.TryResolveCurrentValueForBinding<ShipClass>(el2, out var shipClass))
                        {
                            Debug.Log($"validShipClassListView {force} {shipClass}");
                            force.ValidShipClassClicked(shipClass);
                        }
                    }
                };

                return el2;
            };

            el.Q<Button>("GenerateButton").clicked += () =>
            {
                if(Utils.TryResolveCurrentValueForBinding<Force>(el, out var force))
                {
                    force.Regenerate();
                }
            };

            return el;
        };

        // default value filling
        forces = new()
        {
            new()
            {
                topGroupName = GlobalString.redStr.Clone(),
                country = Country.China,
            },
            new()
            {
                topGroupName = GlobalString.blueStr.Clone(),
                country = Country.Japan,
            }
        };

        foreach(var force in forces)
        {
            force.Refresh();
        }
    }

    public void OnConfirm(object sender, VisualElement root)
    {
        Debug.Log("ForceBuilder OnConfirm");

        CreateShipLogsAndDynamicNamedShips();
        DialogRoot.Instance.PopupAutoDeploymentDialog();
    }

    void CreateShipLogsAndDynamicNamedShips()
    {
        var gameState = NavalGameState.Instance;
        var entityManager = EntityManager.Instance;

        foreach(var force in forces)
        {
            var shipGroup = new ShipGroup()
            {
                name = force.topGroupName.Clone()
            };
            gameState.shipGroups.Add(shipGroup);
            entityManager.Register(shipGroup, null);

            foreach(var forceItem in force.forceItems)
            {
                for(var i=0; i<forceItem.quantity; i++)
                {
                    var namedShip = entityManager.Get<NamedShip>(forceItem.id);
                    if(namedShip != null)
                    {
                        CreateShipLogByNamedShip(namedShip, shipGroup);
                    }
                    else
                    {
                        var shipClass = entityManager.Get<ShipClass>(forceItem.id);
                        if(shipClass != null)
                        {
                            namedShip = new NamedShip()
                            {
                                shipClassObjectId=shipClass.objectId,
                                name=gameState.GetNameForNewShipClass(shipClass)
                            };
                            gameState.namedShips.Add(namedShip);
                            entityManager.Register(namedShip, null);

                            CreateShipLogByNamedShip(namedShip, shipGroup);
                        }
                    }
                }
            }
        }
    }

    void CreateShipLogByNamedShip(NamedShip namedShip, ShipGroup shipGroup)
    {
        var gameState = NavalGameState.Instance;
        var entityManager = EntityManager.Instance;

        var shipLog = new ShipLog()
        {
            namedShipObjectId=namedShip.objectId
        };
        gameState.shipLogs.Add(shipLog);
        entityManager.Register(shipLog, null);

        shipGroup.childrenObjectIds.Add(shipLog.objectId);
        shipLog.parentObjectId = shipGroup.objectId;

    }
}