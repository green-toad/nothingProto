using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using AVcontrol;
using NetDriver.AE;

namespace ClientSide
{
    public class Connection : IAsyncDisposable
    {
        private readonly TcpClient _client;
        private readonly CancellationTokenSource _cts = new();
        private readonly NetworkStream _stream; 
        private readonly Networker _networker;
        private readonly Socket _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
        private readonly EncryptManager _eManager = new();
        public Task working;

        public Connection(TcpClient listener)
        {
            _socket.Connect(new IPEndPoint(IPAddress.Parse("144.31.71.55"), 22233));
            _networker = new(_socket, Reading);
            _client = listener;
            
            _stream = _client.GetStream();
        }
        public async Task<bool> Configurate()
        {
            try
            {
                byte[] header = new byte[2];
                await ReadFullAsync(_stream, header, 0, 2);
                int ver = header[0];
                int nMethods = header[1];
                if (ver != 5) throw new Exception($"Expected SOCKS5, got {ver}");

                
                byte[] methods = new byte[nMethods];
                await ReadFullAsync(_stream, methods, 0, nMethods);

                
                bool noAuthSupported = methods.Contains((byte)0);
                if (!noAuthSupported) throw new Exception("No acceptable authentication method");
                
                byte[] response = { 5, 0 };
                await _stream.WriteAsync(response, 0, response.Length);

                byte[] requestHeader = new byte[4];
                await ReadFullAsync(_stream, requestHeader, 0, 4);
                if (requestHeader[0] != 5) throw new Exception("fuck versions");
                byte cmd = requestHeader[1];
                if (cmd != 1) throw new Exception($"CONNECT (1) != {cmd}");

                byte atyp = requestHeader[3];

                string targetHost;
                int targetPort;


                switch (atyp)
                {
                    case 1:
                        byte[] ipv4 = new byte[4];
                        await ReadFullAsync(_stream, ipv4, 0, 4);
                        targetHost = new IPAddress(ipv4).ToString();
                        break;
                    case 3:
                        byte len = (byte)await ReadByteAsync(_stream);
                        byte[] domainBytes = new byte[len];
                        await ReadFullAsync(_stream, domainBytes, 0, len);
                        targetHost = Encoding.UTF8.GetString(domainBytes);
                        break;
                    case 4:
                        byte[] ipv6 = new byte[16];
                        await ReadFullAsync(_stream, ipv6, 0, 16);
                        targetHost = new IPAddress(ipv6).ToString();
                        break;
                    default:
                        throw new Exception($"wtf is this: {atyp}");
                }

                byte[] portBytes = new byte[2];
                await ReadFullAsync(_stream, portBytes, 0, 2);
                targetPort = (portBytes[0] << 8) | portBytes[1];
                
                // здесь надо навернуть первичный хендшейк
                // Console.WriteLine("попытка приконектится");
                // Console.WriteLine(_networker);
                // Console.WriteLine($"{targetHost}~:~{targetPort}");
                // var res = await _networker.Send(true, Encoding.ASCII.GetBytes($"{targetHost}~:~{targetPort}"), 10 * 1000);
                // Console.WriteLine(Encoding.ASCII.GetString(res.content));
                // if (Encoding.ASCII.GetString(res.content) != "OK") throw new Exception("server is fucked");
                // Console.WriteLine("заебок, получилось");

                //  так, регистрация идет в три шага, инициализируется от клиента

                Console.WriteLine("step 1");
                var res = await _networker.Send(true, Frame.Pack(new Frame() {type = Frame.Type.firstInitalizeStep, content = ToBinary.ASCII($"{targetHost}~:~{targetPort}")}), 10 * 1000);
                using (var RSAkey = RSA.Create())
                {
                    RSAkey.ImportRSAPublicKey(res.content, out _);

                    Console.WriteLine("step 2");
                    res = await _networker.Send(true, Frame.Pack(new Frame() {type = Frame.Type.secondInitializationStep, content = RSAkey.Encrypt(_eManager.GetMyKey(), RSAEncryptionPadding.Pkcs1)}));
                    Console.WriteLine("step 3");
                    _eManager.SetOtherKey(res.content);
                }

                

                Console.WriteLine("step 3");
                
                byte[] reply = new byte[]
                {
                    5, 0, 0, 1,
                    0, 0, 0, 0,
                    0, 0
                };
                await _stream.WriteAsync(reply, 0, reply.Length);

                working = Task.Run(Working);
                return true;
            }
            catch (Exception e)
            {
                Console.WriteLine("залупа на коннекте: " + e );
                return false;
            }
        }

        private async Task ReadFullAsync(NetworkStream stream, byte[] buffer, int offset, int count)
        {
            int totalRead = 0;
            while (totalRead < count)
            {
                int read = await stream.ReadAsync(buffer, offset + totalRead, count - totalRead);
                if (read == 0) throw new Exception("conection closed");
                totalRead += read;
            }
        }

        private async Task<int> ReadByteAsync(NetworkStream stream)
        {
            byte[] b = new byte[1];
            int read = await stream.ReadAsync(b, 0, 1);
            if (read == 0) throw new Exception("Соединение закрыто");
            return b[0];
        }

        private async Task Working()
        {
            byte[] readBuffer = new byte[8192];
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    int bytesRead = await _stream.ReadAsync(readBuffer, 0, readBuffer.Length);
                    if (bytesRead == 0) break;
                    Console.WriteLine("опа че то поймал");

                    byte[] chunk = new byte[bytesRead];
                    Array.Copy(readBuffer, 0, chunk, 0, bytesRead);

                    await _networker.Send(false, chunk);
                }
                catch(Exception e)
                {
                    Console.WriteLine($"fucked writing {e}");
                }
            }
        }

        private async Task Reading(ResultContent content)
        {
            if (!_stream.CanWrite) return;
            await _stream.WriteAsync(content.content);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            await working;
            _cts.Dispose();
            _client.Dispose();
        }
    }
}