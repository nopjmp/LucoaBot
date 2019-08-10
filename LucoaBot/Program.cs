using System.Threading.Tasks;

namespace LucoaBot
{
    class Program
    {
        public static async Task Main(string[] _)
        {
            await new Bot().MainAsync();
        }
    }
}
