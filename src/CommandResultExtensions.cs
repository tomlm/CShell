using Medallion.Shell;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.IO;
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
            if (!cmdResult.Success)
            {
                throw new CommandResultException(cmdResult);
            }

            return cmdResult.StandardOutput;
        }

        /// <summary>
        /// Parse the StandardOutput of a command as JSON.
        /// </summary>
        /// <remarks>
        /// Navigate it by indexer: `json["owner"]["login"]`. Member access -- `json.owner.login`
        /// -- worked against the Newtonsoft JObject this used to return and does not against a
        /// JsonNode; use AsJson&lt;T&gt;() where a shape is known.
        /// </remarks>
        /// <param name="cmdResult"></param>
        /// <returns>the parsed JSON</returns>
        public static JsonNode AsJson(this CommandResult cmdResult)
        {
            if (!cmdResult.Success)
            {
                throw new CommandResultException(cmdResult);
            }

            return JsonNode.Parse(Json.Clean(cmdResult.StandardOutput), null, Json.DocumentOptions);
        }

        /// <summary>
        /// Convert StandardOutput of command to object using json deserialization
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmdResult"></param>
        /// <returns></returns>
        public static T AsJson<T>(this CommandResult cmdResult)
        {
            if (!cmdResult.Success)
            {
                throw new CommandResultException(cmdResult);
            }

            return JsonSerializer.Deserialize<T>(Json.Clean(cmdResult.StandardOutput), Json.Options);
        }

        /// <summary>
        /// Convert StandardOutput of command to object using xml deserialization
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmdResult"></param>
        /// <returns></returns>
        public static T AsXml<T>(this CommandResult cmdResult)
        {
            if (!cmdResult.Success)
            {
                throw new CommandResultException(cmdResult);
            }

            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (TextReader reader = new StringReader(cmdResult.StandardOutput))
            {
                return (T)serializer.Deserialize(reader);
            }
        }

        /// <summary>
        /// Redirect from string content
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="content"></param>
        /// <returns></returns>
        public static Command RedirectFrom(this Command cmd, string content)
        {
            return cmd.RedirectFrom(new StringReader(content));
        }
    }
}
