using Medallion.Shell;
using Newtonsoft.Json;
using System;
using System.Diagnostics;
using System.IO;
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
        /// <param name="log">if true the output to standardout/error</param>
        /// <returns></returns>
        public async static Task<string> AsString(this Command cmd, bool log = false)
        {
            var cmdResult = await cmd.Task.ConfigureAwait(false);
            if (log)
            {
                Log(cmdResult);
            }

            if (!cmdResult.Success)
            {
                throw new CommandResultException(cmdResult);
            }

            var output = cmdResult.StandardOutput;
            return output;
        }

        /// <summary>
        /// Convert StandardOutput of command to dynamic object using Json deserialization (JObject)
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="log">if true the output to standardout/error</param>
        /// <returns>JObject</returns>
        public async static Task<dynamic> AsJson(this Command cmd, bool log = false)
        {
            var cmdResult = await cmd.Task.ConfigureAwait(false);
            if (log)
            {
                Log(cmdResult);
            }

            if (!cmdResult.Success)
            {
                throw new CommandResultException(cmdResult);
            }

            return JsonConvert.DeserializeObject(cmdResult.StandardOutput);
        }

        /// <summary>
        /// Convert StandardOutput of command to object using json deserialization
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmd"></param>
        /// <param name="log">if true the output to standardout/error</param>
        /// <returns></returns>
        public async static Task<T> AsJson<T>(this Command cmd, bool log = false)
        {
            var cmdResult = await cmd.Task.ConfigureAwait(false);
            if (log)
            {
                Log(cmdResult);
            }

            if (!cmdResult.Success)
            {
                throw new CommandResultException(cmdResult);
            }

            return JsonConvert.DeserializeObject<T>(cmdResult.StandardOutput);
        }

        /// <summary>
        /// Convert StandardOutput of command to object using xml deserialization
        /// </summary>
        /// <typeparam name="T"></typeparam>
        /// <param name="cmd"></param>
        /// <param name="log">if true the output to standardout/error</param>
        /// <returns></returns>
        public async static Task<T> AsXml<T>(this Command cmd, bool log = false)
        {
            var cmdResult = await cmd.Task.ConfigureAwait(false);
            if (log)
            {
                Log(cmdResult);
            }
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
        /// Return the CommandResult of the complted comman chain
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="log">if true the output to standardout/error</param>
        /// <returns>task which represents the completition of the command chain</returns>
        public static async Task<CommandResult> AsResult(this Command cmd, bool log = false)
        {
            var cmdResult = await cmd.Task.ConfigureAwait(false);
            if (log)
            {
                Log(cmdResult);
            }
            return cmdResult;
        }

        /// <summary>
        /// Execute the command returning the CommandResult (alias for AsResult()) 
        /// </summary>
        /// <param name="cmd"></param>
        /// <param name="log">if true the output to standardout/error</param>
        /// <returns>task which represents the completition of the command chain</returns>
        public static Task<CommandResult> Execute(this Command cmd, bool log = false)
        {
            return cmd.AsResult(log);
        }

        /// <summary>
        /// Take a StandardOutput/StandardError of previous command and write to file
        /// </summary>
        /// <param name="stdoutPath">path to write to</param>
        /// <returns>CommandResult of previous command</returns>
        public static async Task<CommandResult> AsFile(this Command cmd, string stdoutPath, string stderrPath = null)
        {
            var cmdResult = await cmd.Task;

            if (!Path.IsPathRooted(stdoutPath))
            {
                stdoutPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, stdoutPath));
            }
            File.WriteAllText(stdoutPath, cmdResult.StandardOutput);

            if (stderrPath != null)
            {
                if (!Path.IsPathRooted(stderrPath))
                {
                    stderrPath = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, stderrPath));
                }
                File.WriteAllText(stderrPath, cmdResult.StandardError);
            }
            return cmdResult;
        }

        /// <summary>
        /// Pipe to a new commmand
        /// </summary>
        /// <param name="sourceCommand"></param>
        /// <param name="exe"></param>
        /// <param name="arguments"></param>
        /// <returns></returns>
        public static Command PipeTo(this Command sourceCommand, string exe, params string[] arguments)
        {
            return sourceCommand.PipeTo(Command.Run(exe, arguments));
        }

        private static void Log(CommandResult cmdResult)
        {
            if (!cmdResult.Success)
            {
                Console.Error.Write(cmdResult.StandardError);
                Debug.Fail(cmdResult.StandardError);
            }
            Console.Write(cmdResult.StandardOutput);
            Debug.Write(cmdResult.StandardOutput);
        }
    }
}
