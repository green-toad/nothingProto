using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace Nothing.Client
{
    public class Client
    {
        private readonly TcpListener _listener;
        private readonly ConcurrentDictionary<Socket, Bridge> _connections = new();
        private readonly Channel<Socket> _deathQueue = Channel.CreateUnbounded<Socket>();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _accepter;
        private readonly Task _killer;
        public Client()
        {
            _listener = new TcpListener(IPAddress.Parse("127.0.0.1"), 8088);
            _listener.Start();

            _accepter = Task.Run(AcceptTask);
            _killer = Task.Run(KillerTask);
        }

        private async Task AcceptTask()
        {
            while (!_cts.IsCancellationRequested)
            {
                var client = await _listener.AcceptTcpClientAsync(_cts.Token);
                var sock = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                var bridge = new Bridge(sock, client, Disconnect);
                await bridge.Iitalize();
                _connections.TryAdd(sock, bridge);
            }
        }

        private async Task KillerTask()
        {
            await foreach (var deceased in _deathQueue.Reader.ReadAllAsync(_cts.Token))
            {
                if (_connections.TryRemove(deceased, out var res))
                {
                    Console.Write("kill connection\n");
                    await res.DisposeAsync();
                }
            }
        }

        private void Disconnect(Socket sock)
        {
            _deathQueue.Writer.WriteAsync(sock);
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

            _listener.Dispose();

            _cts.Dispose();
        }
    }
}