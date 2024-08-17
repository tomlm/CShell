global using static CShellNet.Globals;
using CShellNet;

namespace Sample
{
    public class Sample
    {
        public static async Task Main(string[] args)
        {
            var job = echo("test") | Run("wc");
            var result = await job.AsString();
            Console.WriteLine(result);
        }
    }
}

