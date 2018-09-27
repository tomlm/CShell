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

    public static class CommandExtensions
    {
        /// <summary>
        /// Get results of command as string
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public async static Task<string> AsString(this Command cmd)
        {
            var cmdResult = await cmd.Task.ConfigureAwait(false);
            return cmdResult.StandardOutput;
        }

        /// <summary>
        /// Convert StandardOutput of command to dynamic object using Json deserialization (JObject)
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public async static Task<dynamic> AsJson(this Command cmd)
        {
            var cmdResult = await cmd.Task.ConfigureAwait(false);
            return JsonConvert.DeserializeObject(cmdResult.StandardOutput);
        }

        /// <summary>
        /// Convert StandardOutput of command to object using json deserialization
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public async static Task<T> AsJson<T>(this Command cmd)
        {
            var cmdResult = await cmd.Task.ConfigureAwait(false);
            return JsonConvert.DeserializeObject<T>(cmdResult.StandardOutput);
        }

        /// <summary>
        /// Convert StandardOutput of command to object using xml deserialization
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmd"></param>
        /// <returns></returns>
        public async static Task<T> AsXml<T>(this Command cmd)
        {
            var cmdResult = await cmd.Task.ConfigureAwait(false);

            XmlSerializer serializer = new XmlSerializer(typeof(T));
            using (TextReader reader = new StringReader(cmdResult.StandardOutput))
            {
                return (T)serializer.Deserialize(reader);
            }
        }

        /// <summary>
        /// Return the CommandResult of the complted comman chain
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns>task which represents the completition of the command chain</returns>
        public static Task<CommandResult> AsResult(this Command cmd)
        {
            return cmd.Task;
        }

        /// <summary>
        /// Take a StandardOutput/StandardError of previous command and write to file
        /// </summary>
        /// <param name="stdoutPath">path to write to</param>
        /// <returns>CommandResult of previous command</returns>
        public static async Task<CommandResult> AsFile(this Command cmd, string stdoutPath, string stderrPath = null)
        {
            var result = await cmd.Task;

            if (!Path.IsPathRooted(stdoutPath))
            {
                stdoutPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, stdoutPath));
            }
            File.WriteAllText(stdoutPath, result.StandardOutput);

            if (stderrPath != null)
            {
                if (!Path.IsPathRooted(stderrPath))
                {
                    stderrPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, stderrPath));
                }
                File.WriteAllText(stderrPath, result.StandardError);
            }
            return result;
        }

    }

}
