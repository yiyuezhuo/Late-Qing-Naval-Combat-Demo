
using System.Collections.Generic;
using CoreUtils;
using NavalCombatCore;
using Unity.Properties;
using UnityEngine.UIElements;
using System.Linq;
using UnityEngine;

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
                if(namedShip != null)
                {
                    name = namedShip.name.GetShortName();
                }
                else
                {
                    name = shipClass?.name?.GetShortName() ?? "";
                }
                // var shipClass = GetShipClass();

                return $"{name} (x{quantity}, {shipClass.GetPoint()})>";
            }
        }
    }

    public class Force
    {
        public GlobalString topGroupName = new();
        public Country country;
        public float points = 100_000;
        public float battleShipWeight = 50;
        public float cruiserWeight = 50;
        public float torpedoBoatWeight = 10;
        public float transportWeight = 0;
        
        // Auto-generated to from the above parameter or manual specify this value.
        public List<ForceItem> forceItems = new();
        public List<NamedShip> validNamedShips = new();
        public List<ShipClass> validShipClasses = new();

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
    }
}