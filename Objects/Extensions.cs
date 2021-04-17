using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Text;
using System.Windows.Markup;
using System.Xml;

namespace DVDOrders.Objects
{
   
    public static class Extensions
    {
        public static string GetXmlString(this IDictionary dict)
        {
            DataContractSerializer serializer = new DataContractSerializer(dict.GetType());

            using StringWriter sw = new StringWriter();
            using XmlTextWriter writer = new XmlTextWriter(sw) {Formatting = Formatting.Indented};
            // add formatting so the XML is easy to read in the log

            serializer.WriteObject(writer, dict);

            writer.Flush();

            return sw.ToString();
        }

        public static string GetXmlString(this object obj)
        {
            DataContractSerializer serializer = new DataContractSerializer(obj.GetType());

            using StringWriter sw = new StringWriter();
            using XmlTextWriter writer = new XmlTextWriter(sw) { Formatting = Formatting.Indented };
            // add formatting so the XML is easy to read in the log

            serializer.WriteObject(writer, obj);

            writer.Flush();

            return sw.ToString();
        }

        public static object GetXmlObject(this Type type, string xml)
        {
            DataContractSerializer serializer = new DataContractSerializer(type);

            using StringReader sw = new StringReader(xml);
            using XmlTextReader writer = new XmlTextReader(sw);
            return serializer.ReadObject(writer);
        }
        public static string ReadXmlToString(string location)
        {
            try
            {
                return File.ReadAllText(location);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
        public static void SaveXmlToFile(this string xml, string location)
        {
            try
            {
                File.WriteAllText(location, xml);
            }
            catch (Exception e)
            {
                Console.WriteLine(e);
                throw;
            }
        }
    }
}
