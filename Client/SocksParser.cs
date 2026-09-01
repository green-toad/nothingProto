using System;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;



namespace Nothing.Client
{
    internal class Socks5Parser : IAsyncDisposable
    {
        private readonly TcpClient _client;
        private readonly NetworkStream _stream; 
        public readonly Channel<byte[]> OutputFromSocks = Channel.CreateUnbounded<byte[]>();
        private readonly Func<IPEndPoint, Task> _initalizeTarget;
        private readonly CancellationTokenSource _cts = new();
        public Task working { get; private set; } // тоже очень хочу рид онли, но, если сделать, то будут гонки =(

        public Socks5Parser(TcpClient client, Func<IPEndPoint, Task> initalizeTarget)
        {
            _client = client;

            _stream = _client.GetStream();
            _initalizeTarget = initalizeTarget;

            working = Task.CompletedTask;
        }
        public async Task Reading(byte[] content)
        {
            if (!_stream.CanWrite) return;
            await _stream.WriteAsync(content);
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

                    byte[] chunk = new byte[bytesRead];
                    Array.Copy(readBuffer, 0, chunk, 0, bytesRead);

                    await OutputFromSocks.Writer.WriteAsync(chunk);
                }
                catch(Exception e)
                {
                    Console.Write(e + "\n");
                }
            }
        }
        public async Task<bool> Initalize() // надо обязательно вызвать при создании. . . по хорошему, лучше сделать фабрику из статической функции, но, лень
        {
            try
            {
                // вот это нам надо для сокс 5 -- конкретно здесь хз, как оно работает, но, работает, и ладно
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

                IPAddress targetHost = IPAddress.Any;
                int targetPort;


                switch (atyp)
                {
                    case 1:
                        byte[] ipv4 = new byte[4];
                        await ReadFullAsync(_stream, ipv4, 0, 4);
                        targetHost = new IPAddress(ipv4);
                        break;
                    case 3:
                    // этого никогда не сущестовало
                        // byte len = (byte)await ReadByteAsync(_stream);
                        // byte[] domainBytes = new byte[len];
                        // await ReadFullAsync(_stream, domainBytes, 0, len);
                        // targetHost = Encoding.UTF8.GetString(domainBytes);
                        break;
                    case 4:
                        byte[] ipv6 = new byte[16];
                        await ReadFullAsync(_stream, ipv6, 0, 16);
                        targetHost = new IPAddress(ipv6);
                        break;
                    default:
                        throw new Exception($"wtf is this: {atyp}");
                }

                byte[] portBytes = new byte[2];
                await ReadFullAsync(_stream, portBytes, 0, 2);
                targetPort = (portBytes[0] << 8) | portBytes[1];

                if (targetHost == IPAddress.Any) throw new Exception("wrong target");
                await _initalizeTarget(new IPEndPoint(targetHost, targetPort));

                byte[] reply =
                [
                    5, 0, 0, 1,
                    0, 0, 0, 0,
                    0, 0
                ];
                await _stream.WriteAsync(reply, 0, reply.Length);


                working = Task.Run(Working);
                return true;
            }
            catch (Exception e)
            {
                Console.Write("залупа на коннекте: " + e  + "\n");
                return false;
            }
        }
        private async Task<int> ReadByteAsync(NetworkStream stream)
        {
            byte[] b = new byte[1];
            int read = await stream.ReadAsync(b, 0, 1);
            if (read == 0) throw new Exception("Соединение закрыто");
            return b[0];
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
        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();

            try
            {
                _stream?.Close();
            }
            catch { }

            try
            {
                _client?.Close();
            }
            catch { }
            if (working != null)
            {
                try
                {
                    await working.ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                }
            }

            OutputFromSocks.Writer.TryComplete();

            _cts.Dispose();
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}