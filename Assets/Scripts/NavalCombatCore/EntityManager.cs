using System;
using System.Collections;
using System.Collections.Generic;

namespace NavalCombatCore
{

    public interface IObjectIdLabeled
    {
        string objectId { get; set; }
        IEnumerable<IObjectIdLabeled> GetSubObjects()
        {
            yield break;
        }
    }

    public class EntityManager
    {
        public Dictionary<string, IObjectIdLabeled> idToEntity = new();
        public Dictionary<IObjectIdLabeled, object> entityToParent = new();

        public event EventHandler<string> newGuidCreated;

        public void Reset()
        {
            idToEntity.Clear();
            entityToParent.Clear();
        }

        public void Register(IObjectIdLabeled obj, object parent)
        {
            if (obj.objectId == null || idToEntity.ContainsKey(obj.objectId))
            {
                do
                {
                    obj.objectId = System.Guid.NewGuid().ToString();
                } while (idToEntity.ContainsKey(obj.objectId));
                newGuidCreated?.Invoke(obj, obj.objectId);
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