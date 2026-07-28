using System.Net;
using System.Net.Sockets;
using System.Collections.Concurrent;
using Shared;



namespace ClientSide
{
    public class Program
    {
        public static async Task Main()
        {
            var listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 8088);
            listener.Start();

            var workers = new ConcurrentDictionary<Connection, Task>();
            var cts = new CancellationTokenSource();

            var cleanTask = CleanupLoop(workers, cts.Token);
            var acceptTask = AcceptLoopAsync(listener, workers, cts.Token);

            Console.WriteLine("Прокси запущен. Нажмите любую клавишу для остановки...");
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
                        var connection = new Connection(client);

                        if (!await connection.Configurate())
                        {
                            await connection.DisposeAsync();
                            continue;
                        }

                        workers.TryAdd(connection, connection.working);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[Ошибка при приёме соединения] {ex}");
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
                                        Console.WriteLine($"fucked cleaner {ex.Message}");
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
                    Console.WriteLine($"CleanupLoop {ex.Message}");
                    await Task.Delay(1000, cancellationToken);
                }
            }
        }
    }
}