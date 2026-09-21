using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using AVcontrol;
using JabrAPI;
using NetDriver.AE;
using Nothing.Cryptography;
using Nothing.Message;

namespace Nothing.Client
{
    internal class Bridge : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cts = new();

        private readonly ServerSender _sender;
        private readonly Socks5Parser _parser;

        private readonly Socket _socket;

        private readonly DisconnectEvent _disconnect;

        private readonly X25519_Device _cryptoDevice;

        private Task CtT;           // увы, надо запускать только после инициализации ключей
        private readonly Task TtC;

        private readonly SymCryptoDevice _myCrypto = new();

        public Bridge(Socket socket, TcpClient client, DisconnectEvent disconnect)
        {
            Console.Write("создан мост\n");
            _socket = socket;
            // _socket.Connect(new IPEndPoint(IPAddress.Parse("127.0.0.1"), 22233)); // тесты
            // _socket.Connect(new IPEndPoint(IPAddress.Parse("144.31.71.55"), 22233)); // прод
            _socket.Connect(new IPEndPoint(IPAddress.Parse("81.29.149.18"), 22233)); // прод

            _disconnect = disconnect;
            _cryptoDevice = new();
            _sender = new(disconnect, _socket);

            

            _parser = new(client, AcceptTarget);
            if (! _parser.Initalize().Result) disconnect(socket);
            _parser.working.ContinueWith((Task tsk) => {disconnect(socket);});

            
            TtC = Task.Run(FromServerToClient);
        }

        public async Task Iitalize() // необходимо вызвать при создании
        {
            var otherKey = await _sender.SendWithAnswer(
                Cat.Pack(
                    new Cat(
                        _cryptoDevice.GetPublicKey(),
                        Cat.Type.FirstConfigurationKey
                    )
                )
            );
            if (otherKey == null) _disconnect(_socket);
            #pragma warning disable CS8604 // очевидно, я проверил
            _cryptoDevice.ComputeSharedSecret(otherKey);
            #pragma warning restore CS8604

            
            if ((await _sender.SendWithAnswer(
                Cat.Pack(
                    new Cat(
                        _cryptoDevice.EncryptData(_myCrypto.exportKey()),
                        Cat.Type.SecondConfigurationKey
                    )
                )
            )) == null)
            {
                _disconnect(_socket);
            }


            CtT = Task.Run(FromClientToServer);
        }

        public async ValueTask DisposeAsync()
        {
            await _sender.SendToServer(Cat.Pack(new Cat([], Cat.Type.Disconnect)));
            _cts.Cancel();

            await Task.WhenAll([CtT, TtC]);

            try {await _sender.DisposeAsync();} catch{}
            try {await _parser.DisposeAsync();} catch{}

            _cts.Dispose();
        }

        private async Task AcceptTarget(IPEndPoint target)
        {
            byte[] cat = new byte[6]; // достаточно для порта (2) + IP (4)
            byte[] portBytes = ToBinary.LittleEndian<UInt16>((UInt16)target.Port);
            Buffer.BlockCopy(portBytes, 0, cat, 0, 2);

            byte[] ipBytes = target.Address.GetAddressBytes();
            Buffer.BlockCopy(ipBytes, 0, cat, 2, 4);
            Console.Write("отправлен таргет\n");
            Console.Write($"{_sender}\n");
            await _sender.SendToServer(Cat.Pack(new Cat(cat, Cat.Type.Target)));
        }

        private async Task FromClientToServer()
        {
            try{
                await foreach(var content in _parser.OutputFromSocks.Reader.ReadAllAsync(_cts.Token))
                {// аналогично, шифрование можно расположить именно здесь
                    await _sender.SendToServer(Cat.Pack(new Cat(_myCrypto.Encrypt(content), Cat.Type.Meat)));
                }
            }catch(TaskCanceledException){}
            catch(OperationCanceledException){}
        }

        private async Task FromServerToClient()
        {
            try{
                await foreach(var content in _sender.OutFromServer.Reader.ReadAllAsync(_cts.Token))
                {// аналогично, шифрование можно расположить именно здесь
                    await _parser.Reading(_myCrypto.Decript(content));
                }
            }catch(TaskCanceledException){}
            catch(OperationCanceledException){}
        }
    }
}