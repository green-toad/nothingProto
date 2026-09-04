using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using AVcontrol;
using NetDriver.AE;
using Nothing.Cryptography;
using Nothing.Message;



namespace Nothing.Server
{
    internal class Bridge : IAsyncDisposable
    {
        private readonly Listener _listener;
        private EndpointSender _sender;
        private readonly Task CtT;
        private Task TtC;
        private readonly CancellationTokenSource _cts = new();
        private readonly DisconnectEvent _disconnectEvent;
        private readonly Socket _socket;

        private readonly X25519_Device _cryptoDevice;

        #pragma warning disable CS8618
        public Bridge(Socket socket, DisconnectEvent disconnectEventForListener)
        {
            _listener = new(
                socket, 
                disconnectEventForListener,
                AcceptTarget,
                FirstEncryptInitalizeStep,
                SecondEncryptInitalizeStep);
            
            _cryptoDevice = new();

            _disconnectEvent = disconnectEventForListener;
            _socket = socket;

            CtT = Task.Run(FromClientToTarget);
        }
        #pragma warning restore CS8618

        public async Task AcceptTarget(byte[] target)
        {
            if (target.Length != 6) 
                throw new Exception("не подходящий формат адреса");

            UInt16 port = FromBinary.LittleEndian<UInt16>(target.AsSpan(0, 2));
            IPAddress addr = new IPAddress(target.AsSpan(2, 4));
            Console.Write($"настройка таргета -- {addr.ToString()} : {port}\n");
            _sender = new(new IPEndPoint(addr, port));
            TtC = Task.Run(FromTargetToClient);
        }
        public async Task FirstEncryptInitalizeStep(Guid uid, byte[] message)
        { // по идеи, сначала зашифруемся, потом уже таргет прокинем.
            Console.Write("получен ключь шифрования\n");
            _cryptoDevice.ComputeSharedSecret(message);
            await _listener.Answer(uid, message);
        }
        public async Task SecondEncryptInitalizeStep(byte[] secondData)
        {
            throw new Exception("пакачто пуста");
        }

        public async ValueTask DisposeAsync()
        {
            await _listener.SendResultData(Cat.Pack(new Cat([], Cat.Type.Disconnect)));
            _cts.Cancel();

            await Task.WhenAll(CtT, TtC);
            await _listener.DisposeAsync();
            await _sender.DisposeAsync();
            _cts.Dispose();
        }

        private async Task FromClientToTarget()
        {
            await foreach (var message in _listener.OutputMessage.Reader.ReadAllAsync(_cts.Token))
            {
                try{
                // скорее всего именно суды мы вставим расшифровку, если конечно не будем (а точнее пока не) сувать ее в подкопотню нетдрайвера
                    await _sender.Request(message);
                }
                catch
                {
                    _disconnectEvent(_socket);
                }
            }
        }

        private async Task FromTargetToClient()
        {
            Console.Write(_sender + "\n");
            await foreach (var message in _sender.OutputStream.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    // аналогично с шифрованием и здесь
                    Console.Write("вынимаем контент из бриджа\n");
                    await _listener.SendResultData(Cat.Pack(new Cat(message, Cat.Type.Meat)));
                }
                catch (Exception e)
                {
                    Console.Write(e + "\n");
                }
            }
        }
    }
}