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

                string? serverIp = null;
                while (string.IsNullOrEmpty(serverIp))
                {
                    Console.Write("\nВведи IPv4 сервера: ");
                    serverIp = Console.ReadLine();
                }
                var a = new Client.Client(serverIp);

                Console.ReadKey();

                await a.DisposeAsync();
            }
        }
    }
}
