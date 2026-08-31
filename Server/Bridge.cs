using System;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

using AVcontrol;
using NetDriver.AE;



namespace Nothing.Server
{
    internal class Bridge : IAsyncDisposable
    {
        private readonly Listener _listener;
        private EndpointSender _sender;
        private readonly Task CtT;
        private readonly Task TtC;
        private readonly CancellationTokenSource _cts = new();

        public Bridge(Socket socket, DisconnectEvent disconnectEventForListener)
        {
            _listener = new(
                socket, 
                disconnectEventForListener,
                AcceptTarget,
                FirstEncryptInitalizeStep,
                SecondEncryptInitalizeStep);

            CtT = Task.Run(FromClientToTarget);
            TtC = Task.Run(FromTargetToClient);
        }

        public async Task AcceptTarget(byte[] target)
        {
            if (target.Length != 6) 
                throw new Exception("не подходящий формат адреса");

            UInt16 port = FromBinary.LittleEndian<UInt16>(target.AsSpan(0, 2));
            IPAddress addr = new IPAddress(target.AsSpan(2, 4));
            _sender = new(new IPEndPoint(addr, port));
        }
        public async Task FirstEncryptInitalizeStep(byte[] firstData)
        { // по идеи, сначала зашифруемся, потом уже таргет прокинем.
            throw new Exception("пакачто пуста");
        }
        public async Task SecondEncryptInitalizeStep(byte[] secondData)
        {
            throw new Exception("пакачто пуста");
        }

        public async ValueTask DisposeAsync()
        {
            _cts.Cancel();

            await Task.WhenAll(CtT, TtC);
            await _listener.DisposeAsync();
            await _sender.DisposeAsync();
            _cts.Dispose();
        }

        private async Task FromClientToTarget()
        {
            await foreach (var message in _listener.OutputMessage.Reader.ReadAllAsync(_cts.Token))
            {
                try{
                // скорее всего именно суды мы вставим расшифровку, если конечно не будем (а точнее пока не) сувать ее в подкопотню нетдрайвера
                    await _sender.Request(message);
                }
                catch
                {
                    // неа, просто что бы не падало
                }
            }
        }

        private async Task FromTargetToClient()
        {
            await foreach (var message in _sender.OutputStream.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    // аналогично с шифрованием и здесь
                    await _listener.Answer(message);
                }
                catch
                {
                    // неа, просто что бы не падало
                }
            }
        }
    }
}