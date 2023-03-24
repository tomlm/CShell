using Medallion.Shell;
using System;

namespace CShellNet
{
    public sealed class CommandResultException : Exception
    {
        public CommandResultException(CommandResult commandResult)
            : base(commandResult.StandardError)
        {
            this.CommandResult = commandResult;
        }

        /// <summary>
        /// The exit code of the process
        /// </summary>
        public CommandResult CommandResult { get; set; }
    }
}
