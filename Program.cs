using System;
using AVcontrol;
using NetDriver.AE;
using JabrAPI;
using Nothing.Server;

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