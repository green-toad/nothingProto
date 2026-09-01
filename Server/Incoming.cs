using System;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

using NetDriver.AE;
using Nothing.Message;



namespace Nothing.Server
{
    internal class Listener : IAsyncDisposable
    {
        private readonly Networker _networker;
        private Socket _socket;
        private readonly CancellationTokenSource _cts = new();
        private readonly DisconnectEvent _disconnect;
        public readonly Channel<byte[]> OutputMessage = Channel.CreateUnbounded<byte[]>();

        private readonly Func<byte[], Task> _acceptTarget;
        private readonly Func<byte[], Task> _stepOne;
        private readonly Func<byte[], Task> _stepTwo;

        public Listener(
            Socket socket, 
            DisconnectEvent disconnect,
            Func<byte[], Task> acceptTarget,
            Func<byte[], Task> stepOne,
            Func<byte[], Task> stepTwo
        )
        {
            _socket = socket;
            _disconnect = disconnect;

            _acceptTarget = acceptTarget;
            _stepOne = stepOne;
            _stepTwo = stepTwo;

            _networker = new(_socket, AcceptingMessages, _disconnect);
        }

        private async Task AcceptingMessages(ResultContent result)
        {
            var message = Cat.Unpack(result.content);

            Console.Write("поймал что то (server)\n");
            Console.Write($"сообщение формата {message.type}");

            switch (message.type)
            {
                case Cat.Type.Meat:
                    await OutputMessage.Writer.WriteAsync(message.content);
                    break;
                case Cat.Type.Target:
                    try
                    {
                        await _acceptTarget(message.content);
                    }
                    catch
                    {
                        _disconnect(_socket);
                    }
                    break;
                case Cat.Type.FirstConfigurationKey:
                    try
                    {
                        await _stepOne(message.content);
                    }
                    catch
                    {
                        _disconnect(_socket);
                    }
                    break;
                case Cat.Type.SecondConfigurationKey:
                    try
                    {
                        await _stepTwo(message.content);
                    }
                    catch
                    {
                        _disconnect(_socket);
                    }
                    break;
                case Cat.Type.Disconnect:
                    _disconnect(_socket);
                    break;
            }
        }

        public async Task Answer(byte[] result)
        {
            await _networker.Send(false, result);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();

            await _socket.DisconnectAsync(false);
            _socket.Dispose();
            await _networker.Dispose();
            OutputMessage.Writer.Complete();

            _cts.Dispose();
        }
    }
}