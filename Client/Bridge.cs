using System;
using System.Net;
using System.Net.Sockets;

namespace Nothing.Client
{
    internal class Bridge
    {
        // здесь будет пересыльщик на сервак
        private readonly Socks5Parser _parser;

        public Bridge(TcpClient client)
        {
            _parser = new(client, AcceptTarget);
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