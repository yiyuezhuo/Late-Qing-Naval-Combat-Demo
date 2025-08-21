using System.Collections.Generic;

namespace StrategicCombatCore
{
    // Sparse Representation of Hex
    public class HexInfo
    {
        public int x;
        public int y;
        public List<StrategicGroupReference> strategicGroupReference = new();

        public bool IsEmpty() // Don't need a dedicated representation
        {
            return strategicGroupReference.Count == 0;
        }
    }

    public class SerializedHexInfo
    {
        public List<HexInfo> records;
    }
}