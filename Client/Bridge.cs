using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AVcontrol;
using NetDriver.AE;
using Nothing.Message;

namespace Nothing.Client
{
    internal class Bridge : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly ServerSender _sender;
        private readonly Socks5Parser _parser;
        private readonly Socket _socket;

        private readonly Task CtT;
        private readonly Task TtC;

        public Bridge(Socket socket, TcpClient client, DisconnectEvent disconnect)
        {
            _parser = new(client, AcceptTarget);
            _socket = socket;
            _socket.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 23232)); // тесты
            // _socket.Connect(new IPEndPoint(IPAddress.Parse("144.31.71.55"), 23232)); // прод
            _sender = new(disconnect, _socket);

            CtT = Task.Run(FromClientToServer);
            TtC = Task.Run(FromServerToClient);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();

            await Task.WhenAny([CtT, TtC]);

            await _sender.DisposeAsync();
            await _parser.DisposeAsync();
            await _socket.DisconnectAsync(false);
            _socket.Dispose();

            _cts.Dispose();
        }

        private async Task AcceptTarget(IPEndPoint target)
        {
            byte[] cat = new byte[6]; // достаточно для порта (2) + IP (4)
            byte[] portBytes = ToBinary.LittleEndian<UInt16>((UInt16)target.Port);
            Buffer.BlockCopy(portBytes, 0, cat, 0, 2);

            byte[] ipBytes = target.Address.GetAddressBytes();
            Buffer.BlockCopy(ipBytes, 0, cat, 2, 4);

            await _sender.SendToServer(Cat.Pack(new Cat(cat, Cat.Type.Target)));
        }

        private async Task FromClientToServer()
        {
            await foreach(var content in _parser.OutputFromSocks.Reader.ReadAllAsync(_cts.Token))
            {// аналогично, шифрование можно расположить именно здесь
                await _sender.SendToServer(Cat.Pack(new Cat(content, Cat.Type.Meat)));
            }
        }

        private async Task FromServerToClient()
        {
            await foreach(var content in _sender.OutFromServer.Reader.ReadAllAsync(_cts.Token))
            {// аналогично, шифрование можно расположить именно здесь
                await _parser.Reading(content);
            }
        }
    }
}