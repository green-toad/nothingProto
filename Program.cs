using System;
using System.Threading.Tasks;



namespace Nothing
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var a = new Server.Server();

            Console.ReadKey();

            await a.DisposeAsync();
        }
    }
}