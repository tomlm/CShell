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
        /// Take a file and write to standard out, suitable for piping into other programs
        /// </summary>
        /// <param name="stdoutPath"></param>
        /// <returns></returns>
        public static async Task<CommandResult> ToFile(this Command cmd, string stdoutPath, string stderrPath = null)
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

        // Currently the process appears to be not acccessible at this point. 
#if UPDATEENVIRONMENT
        /// <summary>
        /// Update the shell environment based on changes that the process has done
        /// </summary>
        /// <param name="cmd"></param>
        /// <returns>task which represents the completition of the command chain</returns>
        public static async Task<CommandResult> UpdateEnvironment(this Command cmd, CShell shell)
        {
            var result = await cmd.Task.ConfigureAwait(false);

            if (cmd.Process.StartInfo.WorkingDirectory != shell.CurrentFolder)
            {
                shell.CurrentFolder = cmd.Process.StartInfo.WorkingDirectory;
            }

            shell.Environment.Clear();
            foreach(var keyValue in cmd.Process.StartInfo.Environment)
            {
                shell.Environment[keyValue.Key] = keyValue.Value;
            }
            return result;
        }
#endif
    }

}
