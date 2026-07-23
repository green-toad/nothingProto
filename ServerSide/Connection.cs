using NetDriver.AE;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Security.Cryptography;


namespace ServerSide
{
     public class Connection : IAsyncDisposable
    {
        private readonly Networker _networker;
        private bool isConfigurated = false;
        private NetworkStream _stream;
        private readonly TcpClient _client;
        private readonly CancellationTokenSource _cts = new();

        public readonly Task working;

        public Connection(Socket con)
        {
            _networker = new(con, Reciver);
            _client = new TcpClient();
            working = Task.Run(Sending);
        }

        private async Task Reciver(ResultContent content)
        {
            Console.WriteLine("че то поймал!");
            if (!isConfigurated && content.type == ResultContent.Type.from)
            {
                Console.WriteLine("это херь на подключение!");
                var res = Encoding.ASCII.GetString(content.content).Split("~:~");
                Console.WriteLine($"{res[0]} : {res[1]}");

                await _client.ConnectAsync(IPAddress.Parse(res[0]), int.Parse(res[1]), _cts.Token);
                // заменим пока что на перегон на хрей
                // await _client.ConnectAsync(IPAddress.Parse("127.0.0.1"), 1081, _cts.Token);
                
                isConfigurated = true;
                _stream = _client.GetStream();
                Console.WriteLine("ответил, что все норм!");
                await _networker.Answer(Encoding.ASCII.GetBytes("OK"), content.frameuid.Value);
                return;
            }

            Console.WriteLine("эта херь - пакет!");
            await _stream.WriteAsync(content.content, _cts.Token);
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

                    await _networker.Send(false, chunk);
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