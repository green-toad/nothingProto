using System;
using System.Threading.Tasks;



namespace Nothing
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            string? choise = Console.ReadLine();
            if (choise == "s")
            {
                Console.Write("Запущен сервер\n");
                var a = new Server.Server();

                Console.ReadKey();

                await a.DisposeAsync();
            }
            else
            {
                Console.Write("Запущен клиент\n");
                var a = new Client.Client();

                Console.ReadKey();

                await a.DisposeAsync();
            }
        }
    }
}