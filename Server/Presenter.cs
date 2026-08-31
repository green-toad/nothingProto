using System;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;

namespace Nothing.Server
{
    public class Server
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
            _socket.Bind(new IPEndPoint(IPAddress.Any, 23232));
            _socket.Listen();
            _accepter = Task.Run(AcceptingNewConnections);
            _killer = Task.Run(KillerTask);
        }

        private async Task AcceptingNewConnections()
        {
            while (!_cts.IsCancellationRequested)
            {
                var newUser = await _socket.AcceptAsync();

                _connections.TryAdd(newUser, new Bridge(newUser, disconnectSync));
            }
        }

        private async Task KillerTask()
        {
            await foreach(var sock in _deathQueue.Reader.ReadAllAsync())
            {
                if (_connections.TryGetValue(sock, out var res))
                {
                    await res.DisposeAsync();
                }
            }
        }

        private void disconnectSync(Socket socket)
        {
            _deathQueue.Writer.WriteAsync(socket);
        }
    }
}