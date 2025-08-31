using System.Xml.Serialization;
using System.Xml;
using System.Collections.Generic;

namespace CoreUtils
{
    public partial class PathReference
    {
        public string path;
        public bool isBuiltin;
    }

    // PictureReference is used to picture, which is generally "optional" and can be loaded in the arbitrary time.
    public partial class PictureReference : PathReference
    {
    }

    // TextReference is used to text or serialized data, which is generally should be loaded before any processing.
    public partial class TextReference : PathReference
    {
        static Dictionary<string, string> pathToText = new();
        public static void ClearAllCache() => pathToText.Clear();
        
    }
}