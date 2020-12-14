using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FFMpegCore.Pipes;

namespace FFMpegCore.Arguments
{
    public abstract class PipeArgument
    {
        public string PipePath { get; private set; } = null!;

        private NamedPipeServerStream _pipe = null!;
        private TcpListener _tcpListener = null!;
        private Task<Stream> _connectStreamTask = null!;
        private readonly bool _isClient;
        private readonly List<IDisposable> _disposables;

        protected PipeArgument(bool isClient)
        {
            _isClient = isClient;
            _disposables = new List<IDisposable>();
        }

        public void Pre(CancellationToken cancellationToken = default)
        {
            if (_pipe != null || _tcpListener != null || _connectStreamTask != null)
                throw new InvalidOperationException("Pipe already has been opened");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var pipeName = PipeHelpers.GetUnqiuePipeName();
                _pipe = new NamedPipeServerStream(pipeName, _isClient ? PipeDirection.In : PipeDirection.Out, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);
                _disposables.Add(_pipe);
                PipePath = PipeHelpers.GetPipePath(pipeName);
            }
            else
            {
                _tcpListener = new TcpListener(IPAddress.Loopback, 0);
                _tcpListener.Start();

                var endPoint = ((IPEndPoint)_tcpListener.LocalEndpoint);
                PipePath = "tcp://" + endPoint.ToString();
            }

            this._connectStreamTask = Task.Run(ConnectPipeStream, cancellationToken);
        }

        public void Post()
        {
            Debug.WriteLine($"Disposing NamedPipeServerStream on {GetType().Name}");

            foreach (var x in _disposables)
            {
                x.Dispose();
            }

            _disposables.Clear();
            _pipe = null!;
            _tcpListener?.Stop();
            _tcpListener = null!;
        }

        public async Task During(CancellationToken cancellationToken = default)
        {
            try
            {
                await ProcessDataAsync(cancellationToken);
                Debug.WriteLine($"Disconnecting NamedPipeServerStream on {GetType().Name}");
                _pipe?.Disconnect();
                _tcpListener?.Stop();
            }
            catch (TaskCanceledException)
            {
                Debug.WriteLine($"ProcessDataAsync on {GetType().Name} cancelled");
            }
        }

        protected Task<Stream> GetStreamAsync()
        {
            return _connectStreamTask;
        }

        private Stream ConnectPipeStream()
        {
            if (_pipe != null)
            {
                _pipe.WaitForConnection();
                if (!_pipe.IsConnected)
                    throw new TaskCanceledException();

                return _pipe;
            }

            if (_tcpListener != null)
            {
                var tcpClient = _tcpListener.AcceptTcpClient();
                _disposables.Add(tcpClient);
                return tcpClient.GetStream();
            }

            throw new TaskCanceledException();
        }

        protected abstract Task ProcessDataAsync(CancellationToken token);
        public abstract string Text { get; }
    }
}
