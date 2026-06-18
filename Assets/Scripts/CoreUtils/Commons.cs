using System.Collections.Generic;

namespace CoreUtils
{
    public enum Country
    {
        General,
        China, // Qing
        Japan,
        Russia,
        Britain,
        France,
        Germany,
        UnitedStates,
        // Spain,
        Italy,
        AustriaHungary, // Austria-Hungary 
        Portugal,
        // Germany,
        // Turkey, // Ottoman
        // Holland,
        // Korea
    }

    public static class CoreCollectionUtils
    {
        public static void SyncListPairLength<T, T2>(List<T> list1, List<T2> list2, object parent) where T2 : IObjectIdLabeled, new()
        {
            SyncListToLength(list1.Count, list2, parent);
        }

        public static void SyncListToLength<T2>(int expectedLength, List<T2> list2, object parent) where T2 : IObjectIdLabeled, new()
        {
            var addElements = expectedLength - list2.Count;
            var removeElements = list2.Count - expectedLength;
            if (removeElements > 0)
            {
                for (int i = 0; i < removeElements; i++)
                {
                    var el = list2[list2.Count - 1];
                    EntityManager.Instance.Unregister(el);
                    list2.RemoveAt(list2.Count - 1);
                }
            }
            if (addElements > 0)
            {
                for (int i = 0; i < addElements; i++)
                {
                    var el = new T2();
                    list2.Add(el);
                    EntityManager.Instance.Register(el, parent);
                }
            }
        }
    }
}
