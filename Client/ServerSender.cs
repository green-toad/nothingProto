using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using NetDriver.AE;
using Nothing.Message;

namespace Nothing.Client
{
    internal class ServerSender : IAsyncDisposable
    {
        private readonly Networker _networker;
        private readonly Socket _socket;
        private readonly CancellationTokenSource _cts = new();
        public readonly Channel<byte[]> OutFromServer = Channel.CreateUnbounded<byte[]>();

        public ServerSender(DisconnectEvent disconnect, Socket socket)
        {
            _socket = socket;
            _networker = new(_socket, IncomingAccepter, disconnect);
        }

        public async Task SendToServer(byte[] content)
        {
            await _networker.Send(false, content);
        }

        private async Task IncomingAccepter(ResultContent result)
        {
            var message = Cat.Unpack(result.content);

            switch (message.type)
            {
                case Cat.Type.Meat:
                    await OutFromServer.Writer.WriteAsync(message.content);
                    break;
                case Cat.Type.Target:
                    break; // здесь его быть не должно
                case Cat.Type.FirstConfigurationKey:
                    break;
                case Cat.Type.SecondConfigurationKey:
                    break;
            }
        }

        public ValueTask DisposeAsync()
        {
            throw new NotImplementedException();
        }
    }
}