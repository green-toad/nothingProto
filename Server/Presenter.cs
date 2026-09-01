using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;



namespace Nothing.Server
{
    public class Server : IAsyncDisposable
    {
        private readonly ConcurrentDictionary<Socket, Bridge> _connections = new();
        private readonly Channel<Socket> _deathQueue = Channel.CreateUnbounded<Socket>();
        private readonly CancellationTokenSource _cts = new();
        private readonly Socket _socket;
        private readonly Task _accepter;
        private readonly Task _killer;

        public Server()
        {
            _socket = new(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
            _socket.Bind(new IPEndPoint(IPAddress.Any, 22233));
            _socket.Listen();
            _accepter = Task.Run(AcceptingNewConnections);
            _killer = Task.Run(KillerTask);
        }

        private async Task AcceptingNewConnections()
        {
            while (!_cts.IsCancellationRequested)
            {
                var newUser = await _socket.AcceptAsync(_cts.Token);

                _connections.TryAdd(newUser, new Bridge(newUser, DisconnectSync));
            }
        }

        private async Task KillerTask()
        {
            await foreach(var sock in _deathQueue.Reader.ReadAllAsync(_cts.Token))
            {
                if (_connections.TryRemove(sock, out var res))
                {
                    Console.Write("kill connection\n");
                    await res.DisposeAsync();
                }
            }
        }

        private void DisconnectSync(Socket socket)
        {
            _deathQueue.Writer.WriteAsync(socket); // оно считай синхронно, ибо канал не ограниченый
        }


        #pragma warning disable CA1816 // Методы Dispose должны вызывать SuppressFinalize (не, неа, не должны)
        public async ValueTask DisposeAsync()
        #pragma warning restore CA1816
        {
            _cts.Cancel();
            foreach(var conn in _connections)
            {
                await conn.Value.DisposeAsync();
                _connections.Remove(conn.Key, out _);
            }
            await _accepter;
            await _killer;

            _deathQueue.Writer.Complete();

            _socket.Dispose();

            _cts.Dispose();
        }
    }
}