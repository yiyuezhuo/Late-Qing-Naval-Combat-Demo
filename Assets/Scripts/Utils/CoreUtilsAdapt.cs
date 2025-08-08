using Unity.Properties;
using UnityEngine.UIElements;
using UnityEngine;

namespace CoreUtils
{
    public partial class PictureReference
    {
        public string ResolvePath()
        {
            if (isBuiltin)
            {
                return Application.streamingAssetsPath + "/" + path;
            }
            return path;
        }

        [CreateProperty]
        public bool setButtonAvailable
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                return false;
#else
                return true;
#endif
            }
        }

        [CreateProperty]
        public StyleBackground pictureStyleBackground
        {
            get
            {
                var path = ResolvePath();
                return UnityWebRequestImageReader.Instance.FetchStyleBackground(path);
            }
        }
    }
}