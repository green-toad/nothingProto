using NetDriver.AE;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;
using AVcontrol;


namespace ServerSide
{
     public class Connection : IAsyncDisposable
    {
        private readonly Networker _networker;
        private NetworkStream _stream;
        private readonly TcpClient _client;
        private readonly EncryptionDevice _eManager = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly RSA _rsaKey;
        private readonly byte[] _rsaExport;
        public readonly Task working;

        public Connection(Socket con, RSA rsa, byte[] export)
        {
            _networker = new(con, Reciver);
            _client = new TcpClient();
            working = Task.Run(Sending);
            _rsaExport = export;
            _rsaKey = rsa;
        }

        private async Task Reciver(ResultContent content)
        {
            Console.WriteLine("че то поймал!");
            // if (!isConfigurated && content.type == ResultContent.Type.from)
            // {
            //     Console.WriteLine("это херь на подключение!");
            //     var res = Encoding.ASCII.GetString(content.content).Split("~:~");
            //     Console.WriteLine($"{res[0]} : {res[1]}");

            //     await _client.ConnectAsync(IPAddress.Parse(res[0]), int.Parse(res[1]), _cts.Token);
            //     // заменим пока что на перегон на хрей
            //     // await _client.ConnectAsync(IPAddress.Parse("127.0.0.1"), 1081, _cts.Token);

            //     isConfigurated = true;
            //     _stream = _client.GetStream();
            //     Console.WriteLine("ответил, что все норм!");
            //     await _networker.Answer(Encoding.ASCII.GetBytes("OK"), content.frameuid.Value);
            //     return;
            // }

            // Console.WriteLine("эта херь - пакет!");
            // await _stream.WriteAsync(content.content, _cts.Token);


            _eManager.ApplyCustomSettings();

            var pack = Frame.Unpack(content.content);

            switch(pack.type)
            {
                case Frame.Type.firstInitalizeStep:
                    Console.WriteLine("это херь на подключение!");
                    var addr = FromBinary.ASCII(pack.content).Split("~:~");
                    Console.WriteLine($"{addr[0]} : {addr[1]}");

                    await _client.ConnectAsync(IPAddress.Parse(addr[0]), int.Parse(addr[1]), _cts.Token);
                    _stream = _client.GetStream();
                    Console.WriteLine("ответил, что все норм!");

                    await _networker.Answer(_rsaExport, content.frameuid.Value);
                    break;
                case Frame.Type.secondInitializationStep:
                    _eManager.ImportEncryptedReceiveKey(_rsaKey.Decrypt(pack.content, RSAEncryptionPadding.Pkcs1));
                    await _networker.Answer(_eManager.Encrypt(_eManager.ExportSendKey()), content.frameuid.Value);
                    break;
                case Frame.Type.content:
                    Console.WriteLine("эта херь - пакет!");
                    await _stream.WriteAsync(_eManager.Decrypt(pack.content), _cts.Token);
                    break;
            }


            var gotThisBS = _eManager.ExportReceiveKey();
            StringBuilder sb = new();
            Console.Write("\n\tReceived reKey after handshake:\n ");
            foreach (Byte aboba in gotThisBS) sb.Append(aboba);
            Console.Write(sb.ToString() + "\n\n    ");
        }

        private async Task Sending()
        {
            byte[] readBuffer = new byte[8192];
            while (!_cts.IsCancellationRequested)
            {
                try
                {
                    if (_stream == null)
                    {
                        await Task.Delay(100, _cts.Token);
                        continue;
                    }

                    int bytesRead = await _stream.ReadAsync(readBuffer, 0, readBuffer.Length, _cts.Token);
                    if (bytesRead == 0) break;

                    byte[] chunk = new byte[bytesRead];
                    Array.Copy(readBuffer, 0, chunk, 0, bytesRead);

                    await _networker.Send(false, _eManager.Encrypt(chunk));
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Ошибка при пересылке: {ex.Message}");
                    break;
                }
            }
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            try
            {
                await working;
            }
            catch (OperationCanceledException) { /* ожидаемо */ }
            catch (Exception ex)
            {
                Console.WriteLine($"Ошибка при освобождении: {ex.Message}");
            }
            _cts.Dispose();
            _client.Dispose();
        }
    }
}