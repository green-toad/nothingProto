using NetDriver.AE;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text;

namespace ServerSide
{
    public class Program
    {
        public static async Task Main(string[] arg)
        {
            var listener = new TcpListener(IPAddress.Any, 22233);
            listener.Start();

            var workers = new ConcurrentDictionary<Connection, Task>();
            var cts = new CancellationTokenSource();

            var cleanTask = CleanupLoop(workers, cts.Token);
            var acceptTask = AcceptLoopAsync(listener, workers, cts.Token);

            Console.WriteLine("Сервер запущен. Нажмите любую клавишу для остановки...");
            Console.ReadKey();

            cts.Cancel();
            await acceptTask;
            await cleanTask;
        }

        private static async Task AcceptLoopAsync(
            TcpListener listener,
            ConcurrentDictionary<Connection, Task> workers,
            CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    try
                    {
                        var client = await listener.AcceptTcpClientAsync(cancellationToken);
                        var connection = new Connection(client.Client);
                        workers.TryAdd(connection, connection.working);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Ошибка при приёме соединения] {ex.Message}");
                    }
                }
            }
            finally
            {
                listener.Stop();
            }
        }

        private static async Task CleanupLoop(
            ConcurrentDictionary<Connection, Task> workers,
            CancellationToken cancellationToken)
        {
            while (!cancellationToken.IsCancellationRequested)
            {
                try
                {
                    if (workers.IsEmpty)
                    {
                        await Task.Delay(1000, cancellationToken);
                        continue;
                    }

                    var taskToWait = Task.WhenAny(workers.Values);
                    var completedTask = await Task.WhenAny(taskToWait, Task.Delay(-1, cancellationToken));

                    if (completedTask == taskToWait)
                    {
                        foreach (var kv in workers)
                        {
                            if (kv.Value.IsCompleted)
                            {
                                if (workers.TryRemove(kv.Key, out _))
                                {
                                    try
                                    {
                                        await kv.Key.DisposeAsync();
                                    }
                                    catch (Exception ex)
                                    {
                                        Console.WriteLine($"Ошибка при очистке: {ex.Message}");
                                    }
                                }
                            }
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"CleanupLoop: {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
    }

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