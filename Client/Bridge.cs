using System;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using NetDriver.AE;

namespace Nothing.Client
{
    internal class Bridge
    {
        private readonly ServerSender _sender;
        private readonly Socks5Parser _parser;

        public Bridge(TcpClient client, DisconnectEvent disconnect)
        {
            _parser = new(client, AcceptTarget);
            // _sender = new(disconnect);
        }

        private async Task AcceptTarget(IPEndPoint target)
        {
            // перекинуть его в пересыльщк на сервак, или, вообще его здесь создать можно
        }

        private async Task FromClientToServer()
        {
            await foreach(var content in _parser.OutputFromSocks.Reader.ReadAllAsync())
            {// аналогично, шифрование можно расположитьименно здесь
                
            }
        }

        private async Task FromServerToClient()
        {
            
        }
    }
}