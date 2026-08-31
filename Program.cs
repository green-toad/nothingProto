using System;
using System.Threading.Tasks;



namespace Nothing
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            string choise = Console.ReadLine();
            if (choise == "s")
            {
                var a = new Server.Server();

                Console.ReadKey();

                await a.DisposeAsync();
            }
            else
            {
                var a = new Client.Client();

                Console.ReadKey();

                await a.DisposeAsync();
            }
        }
    }
}