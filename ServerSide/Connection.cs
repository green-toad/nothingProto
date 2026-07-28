using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

using Shared;
using AVcontrol;
using NetDriver.AE;



namespace ServerSide
{
     public class Connection : IAsyncDisposable
    {
        private readonly Networker _networker;
        private NetworkStream? _stream;
        private readonly TcpClient _client;
        private readonly EncryptionDevice _eManager = new(false, false);
        private readonly CancellationTokenSource _cts = new();
        private readonly IAsymetricEncryptor _ntruEncrypter;
        public readonly Task working;

        public Connection(Socket con)
        {
            _networker = new(con, Reciver);
            _client = new TcpClient();
            working = Task.Run(Sending);

            _eManager.ApplyCustomSettings();
            _eManager.UpdateSendKey();

            //_ntruEncrypter = new NtruEncryptor();
            _ntruEncrypter = new RsaAsymetricEncryptor();
        }

        private async Task Reciver(ResultContent content)
        {
            Console.Write("че то поймал!\n");

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

                    await _networker.Answer(_ntruEncrypter.ExportPublicKey(), content.frameuid.Value);
                    break;
                case Frame.Type.secondInitializationStep:
                    if (_eManager.AddPartOfKey(_ntruEncrypter.TryDecrypt(pack.content)) == 5)
                    {
                        _eManager.ApplyReceiveKeyWithParts();
                        await _networker.Answer(_eManager.EncryptWithReciveKey(_eManager.ExportSendKey()), content.frameuid.Value);
                    }
                    else
                    {
                        await _networker.Answer([], content.frameuid.Value);
                    }
                    break;
                case Frame.Type.content:
                    Console.WriteLine("эта херь - пакет!");
                    if (_stream != null) await _stream.WriteAsync(
                        _eManager.Decrypt(pack.content), _cts.Token);
                    break;
            }
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

                    int bytesRead = await _stream.ReadAsync(readBuffer, _cts.Token);
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
            await _ntruEncrypter.DisposeAsync();
        }
    }
}