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
        private readonly DisconnectEvent _disconnect;
        private readonly Socket _socket;
        private readonly CancellationTokenSource _cts = new();
        public readonly Channel<byte[]> OutFromServer = Channel.CreateUnbounded<byte[]>();

        public ServerSender(DisconnectEvent disconnect, Socket socket)
        {
            Console.Write("начинаем создоваться\n");
            _disconnect = disconnect;
            _socket = socket;
            _networker = new(_socket, IncomingAccepter, disconnect);
            Console.Write("создались\n");
        }

        public async Task SendToServer(byte[] content)
        {
            await _networker.Send(false, content);
        }

        private async Task IncomingAccepter(ResultContent result)
        {
            var message = Cat.Unpack(result.content);

            Console.Write("поймал что то (client)\n");
            Console.Write($"сообщение формата {message.type}\n");

            switch (message.type)
            {
                case Cat.Type.Meat:
                    await OutFromServer.Writer.WriteAsync(message.content, _cts.Token);
                    break;
                case Cat.Type.Target:
                    break; // здесь его быть не должно
                case Cat.Type.FirstConfigurationKey:
                    break;
                case Cat.Type.SecondConfigurationKey:
                    break;
                case Cat.Type.Disconnect:
                    _disconnect(_socket);
                    break;
            }
        }

        public async ValueTask DisposeAsync()
        {
            Console.Write("уничтожаемся\n");
            _cts.Cancel();

            await _networker.Dispose();
            await _socket.DisconnectAsync(false);
            _socket.Dispose();
            OutFromServer.Writer.Complete();

            _cts.Dispose();
        }
    }
}