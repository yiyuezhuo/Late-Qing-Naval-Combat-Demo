
using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Serialization;
using System.Xml;

namespace CoreUtils
{
    public static class XmlUtils
    {
        static Dictionary<Type, XmlSerializer> serializerCache = new();

        static XmlWriterSettings defaultSetting = new()
        {
            Indent = true,
            IndentChars = " ",
        };

        static XmlSerializer GetOrCreate(Type t)
        {
            if (serializerCache.TryGetValue(t, out var serializer))
                return serializer;
            serializer = new XmlSerializer(t);
            serializerCache[t] = serializer;
            return serializer;
        }

        public static string ToXML<T>(T obj)
        {
            var serializer = GetOrCreate(typeof(T));

            using (var textWriter = new StringWriter())
            {
                using (XmlWriter xmlWriter = XmlWriter.Create(textWriter, defaultSetting))
                {
                    serializer.Serialize(xmlWriter, obj);
                    string serializedXml = textWriter.ToString();

                    return serializedXml;
                }
            }
        }

        public static T FromXML<T>(string xml)
        {
            var serializer = GetOrCreate(typeof(T));

            using (var reader = new StringReader(xml))
            {
                return (T)serializer.Deserialize(reader);
            }
        }
    }
}