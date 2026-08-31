using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;



namespace Nothing.Server
{
    internal class EndpointSender : IAsyncDisposable
    {
        private readonly IPEndPoint _target;
        private readonly NetworkStream _stream;
        private readonly TcpClient _client;
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _recivingTask;

        public Channel<byte[]> OutputStream = Channel.CreateUnbounded<byte[]>();

        public EndpointSender(IPEndPoint target)
        {
            _target = target;
            _client = new();
            _client.Connect(_target);
            _stream = _client.GetStream();
            _recivingTask = Task.Run(Reciving);
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();
            await _recivingTask;
            await _stream.DisposeAsync();
            OutputStream.Writer.Complete();
            _client.Close();
            _client.Dispose();
            _cts.Dispose();
        }

        public async Task Request(byte[] content)
        {
            await _stream.WriteAsync(content, _cts.Token);
        }

        private async Task Reciving()
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

                    await OutputStream.Writer.WriteAsync(chunk, _cts.Token);
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
    }
}