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
        /// Walk it with a dot -- `json.owner.login` -- or with an indexer, or assign it to a
        /// JsonNode, JsonObject or JsonArray for the typed System.Text.Json API. The object behind
        /// it is a JsonDynamic either way. Use AsJson&lt;T&gt;() where the shape is known.
        /// </remarks>
        /// <param name="cmdResult"></param>
        /// <returns>the parsed JSON, as a JsonDynamic</returns>
        public static dynamic AsJson(this CommandResult cmdResult)
        {
            if (!cmdResult.Success)
            {
                throw new CommandResultException(cmdResult);
            }

            return new JsonDynamic(JsonNode.Parse(Json.Clean(cmdResult.StandardOutput), null, Json.DocumentOptions));
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
