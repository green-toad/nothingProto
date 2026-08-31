using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;

using Shared;
using AVcontrol;
using NetDriver.AE;
using System.Text;



namespace ServerSide
{
     public class Connection : IAsyncDisposable
    {
        private readonly Networker _networker;
        private NetworkStream? _stream;
        private readonly TcpClient _client;
        private readonly EncryptionDevice _eManager = new(false, false);
        private readonly CancellationTokenSource _cts = new();
        private readonly IAsymetricEncryptor _asymEncrypter;
        public readonly Task working;

        public Connection(Socket con)
        {
            _networker = new(con, Reciver);
            _client = new TcpClient();
            working = Task.Run(Sending);

            _eManager.ApplyCustomSettings();
            _eManager.UpdateSendKey();

            _asymEncrypter = new EccEncryptor();
            // _asymEncrypter = new X25519Encryptor();
            // _ntruEncrypter = new RsaAsymetricEncryptor();
        }

        private async Task Reciver(ResultContent content)
        {
            Console.Write("че то поймал!\n");

            var pack = Frame.Unpack(content.content);
            switch(pack.type)
            {
                case Frame.Type.firstInitalizeStep:
                    Console.Write("это херь на подключение!\n");
                    IPAddress ip = new IPAddress(pack.content[..4]);
                    int port = BitConverter.ToInt32(pack.content[4..8]);
                    byte[] publicKey = pack.content[8..];
                    Console.WriteLine($"{ip.ToString()} : {port}");
                    Console.Write($"{pack.content.Length}\n");
                    
                    _asymEncrypter.ImportPublicKey(publicKey);
                    Console.Write($"{pack.content.Length}\n");

                    await _client.ConnectAsync(ip, port, _cts.Token);
                    _stream = _client.GetStream();

                    await _networker.Answer(_asymEncrypter.ExportPublicKey(), content.frameuid.Value);
                    break;
                case Frame.Type.secondInitializationStep:
                    Console.Write("это второй этап подключения!\n");
                    var decryptResult = _asymEncrypter.TryDecrypt(pack.content);
                    Console.Write("ну, мы расшифровали\n");
                    _eManager.ImportReceiveKeyWithoutDecrypt(decryptResult);
                    Console.Write("ну, мы импортировали\n");
                    var encryptedSendKey = _eManager.EncryptWithReciveKey(_eManager.ExportSendKey());
                    Console.Write("ну, мы зашифровали\n");

                    await _networker.Answer(encryptedSendKey, content.frameuid.Value);
                    Console.Write("ну, мы отправили\n");
                    break;
                case Frame.Type.content:
                    Console.Write("эта херь - пакет!\n");
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
            await _asymEncrypter.DisposeAsync();
        }
    }
}