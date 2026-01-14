using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using YYZ;

namespace CoreUtils
{
    public partial class LazyLocalizedString
    {
        [XmlType("LazyLocalizedStringType")]
        public enum Type
        {
            Raw,
            LocalizedRequired,
            Template,
            GlobalStringShort,
            GlobalStringLong
        }

        [XmlAttribute]
        public Type type;

        [XmlAttribute]
        public string content;

        public GlobalString globalString;
        
        public List<LazyLocalizedString> args;

        public string Resolve()
        {
            switch (type)
            {
                case Type.Raw:
                    return content;
                case Type.LocalizedRequired:
                    return ServiceLocator.Get<ILocalizeService>().Get(content);
                case Type.Template:
                    return ServiceLocator.Get<ILocalizeService>().Get(content, args.Select(a => a.Resolve()).ToArray());
                case Type.GlobalStringShort:
                    return globalString?.GetShortName();
                case Type.GlobalStringLong:
                    return globalString?.GetMergedName();
                default:
                    throw new System.Exception("Invalid type");
            }
        }

        public static LazyLocalizedString MakeRaw(object content)
        {
            return new()
            {
                type = Type.Raw,
                content = content.ToString()
            };
        }

        public static LazyLocalizedString MakeLocalizedRequired(string content)
        {
            return new()
            {
                type = Type.LocalizedRequired,
                content = content
            };
        }

        public static LazyLocalizedString MakeTemplate(string template, params LazyLocalizedString[] args)
        {
            return new()
            {
                type = Type.Template,
                content = template,
                args = args.ToList()
            };
        }

        public static LazyLocalizedString MakeGlobalStringShort(GlobalString globalString)
        {
            return new()
            {
                type = Type.GlobalStringShort,
                globalString = globalString
            };
        }

        public static LazyLocalizedString MakeGlobalStringLong(GlobalString globalString)
        {
            return new()
            {
                type = Type.GlobalStringLong,
                globalString = globalString
            };
        }

        public static LazyLocalizedString MakeEnum<T>(T enumValue)
        {
            return new()
            {
                type = Type.LocalizedRequired,
                content = $"{typeof(T).FullName}.{enumValue}"
            };
        }
    }
}