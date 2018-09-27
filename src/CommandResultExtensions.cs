using Medallion.Shell;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace CShellNet
{
    public static class CommandResultExtensions
    {
        /// <summary>
        /// Get results of command as string
        /// </summary>
        /// <param name="cmdResult"></param>
        /// <returns></returns>
        public static string AsString(this CommandResult cmdResult)
        {
            return cmdResult.StandardOutput;
        }

        /// <summary>
        /// Convert StandardOutput of command to dynamic object using Json deserialization (JObject)
        /// </summary>
        /// <param name="cmdResult"></param>
        /// <returns></returns>
        public static dynamic AsJson(this CommandResult cmdResult)
        {
            return JsonConvert.DeserializeObject(cmdResult.StandardOutput);
        }

        /// <summary>
        /// Convert StandardOutput of command to object using json deserialization
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmdResult"></param>
        /// <returns></returns>
        public static T AsJson<T>(this CommandResult cmdResult)
        {
            return JsonConvert.DeserializeObject<T>(cmdResult.StandardOutput);
        }

        /// <summary>
        /// Convert StandardOutput of command to object using xml deserialization
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmdResult"></param>
        /// <returns></returns>
        public static T AsXml<T>(this CommandResult cmdResult)
        {
            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (TextReader reader = new StringReader(cmdResult.StandardOutput))
            {
                return (T)serializer.Deserialize(reader);
            }
        }
    }
}
