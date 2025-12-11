using System;
using System.Collections;
using System.Collections.Generic;

namespace CoreUtils
{

    public interface IObjectIdLabeled
    {
        string objectId { get; set; }
        IEnumerable<IObjectIdLabeled> GetSubObjects();
        // IEnumerable<IObjectIdLabeled> GetSubObjects()
        // {
        //     yield break;
        // }

        public void ResetObjectId() // Used to handle a copy, usually an external ResetAndRegisterAll call is required
        {
            objectId = null;
            foreach(var subObj in GetSubObjects())
            {
                subObj.objectId = null;
            }
        }
    }

    public partial class EntityManager
    {
        public Dictionary<string, IObjectIdLabeled> idToEntity = new();
        public Dictionary<IObjectIdLabeled, object> entityToParent = new();

        // public event EventHandler<string> newGuidCreated;

        public void Reset()
        {
            idToEntity.Clear();
            entityToParent.Clear();
        }

        public void Register(IObjectIdLabeled obj, object parent)
        {
            var createObjectIdForNull = obj.objectId == null;
            var deduplicateObjectId = !createObjectIdForNull && idToEntity.ContainsKey(obj.objectId);
            if (createObjectIdForNull || deduplicateObjectId)
            {
                do
                {
                    obj.objectId = System.Guid.NewGuid().ToString();
                } while (idToEntity.ContainsKey(obj.objectId));
                //  newGuidCreated?.Invoke(obj, obj.objectId);
                // ServiceLocator.Get<ILoggerService>().LogWarning($"New guid created: {obj.objectId} for {obj}");

                var createObjectIdForNullStr = createObjectIdForNull ? "(Create ObjectId For Null)" : "";
                var deduplicateObjectIdStr = deduplicateObjectId ? "(Deduplicate ObjectId)" : "";
                ServiceLocator.Get<ILoggerService>().LogWarning($"New guid created: {obj.objectId} for {obj} {createObjectIdForNullStr}{deduplicateObjectIdStr}");
            }
            idToEntity[obj.objectId] = obj;
            entityToParent[obj] = parent;

            foreach (var subObj in obj.GetSubObjects())
            {
                Register(subObj, obj);
            }
        }

        public void Unregister(IObjectIdLabeled obj)
        {
            foreach (var subObj in obj.GetSubObjects())
            {
                Unregister(subObj);
            }

            idToEntity.Remove(obj.objectId);
            entityToParent.Remove(obj);
        }

        public T Get<T>(string id) where T : class
        {
            if (id == null)
                return null;
            return idToEntity.GetValueOrDefault(id) as T;
        }

        public T GetParent<T>(IObjectIdLabeled obj) where T : class
        {
            if (obj == null)
                return null;
            return entityToParent.GetValueOrDefault(obj) as T;
        }

        // Move to partial method
        // public ShipLog GetOnMapShipLog(string id)
        // {
        //     var shipLog = Get<ShipLog>(id);
        //     if (shipLog == null || !shipLog.IsOnMap())
        //         return null;
        //     return shipLog;
        // }

        static EntityManager _instance;

        public static EntityManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new EntityManager();
                }
                return _instance;
            }
        }
    }
}