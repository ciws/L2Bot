using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using static Client.Domain.Transports.TransportInterface;

namespace Client.Infrastructure.Transports
{
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public class NamedPipeTransport : Domain.Transports.TransportInterface, IDisposable
    {
        public event DelegateMessage? Message;

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!IsConnected())
            {
                Disconnect();

                try
                {
                    connectionPipe = new NamedPipeClientStream(".", this.pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await connectionPipe.ConnectAsync(cancellationToken);
                    connectionPipe.ReadMode = PipeTransmissionMode.Message;
                    Debug.WriteLine("Connected to connection pipe");

                    byte[] buffer = new byte[16384 * 2];
                    int read = await connectionPipe.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                    if (read <= 0)
                    {
                        Disconnect();
                        return;
                    }
                    string pipeName = Encoding.Unicode.GetString(buffer, 0, read).TrimEnd('\0', '\r', '\n');

                    if (pipeName == "")
                    {
                        Disconnect();
                        return;
                    }

                    Debug.WriteLine("Received connection pipe name " + pipeName);

                    mainPipe = new NamedPipeClientStream(".", pipeName, PipeDirection.InOut, PipeOptions.Asynchronous);
                    await mainPipe.ConnectAsync(cancellationToken);
                    mainPipe.ReadMode = PipeTransmissionMode.Message;
                    Debug.WriteLine("Connected to main pipe\n");
                }
                catch { Disconnect(); throw; }
            }
        }

        public async Task StartReceiveAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (mainPipe == null)
            {
                return;
            }

            try
            {
                while (IsConnected() && !cancellationToken.IsCancellationRequested)
                {
                    using var message = new MemoryStream();
                    var pipe = mainPipe!;
                    var buffer = new byte[32768];
                    do
                    {
                        var count = await pipe.ReadAsync(buffer.AsMemory(), cancellationToken);
                        if (count == 0) return;
                        message.Write(buffer, 0, count);
                        if (message.Length > 8 * 1024 * 1024) throw new IOException("Pipe message is too large.");
                    } while (!pipe.IsMessageComplete);
                    var text = Encoding.Unicode.GetString(message.ToArray()).TrimEnd('\0');
                    if (!string.IsNullOrWhiteSpace(text)) Message?.Invoke(text);
                }
            }
            finally { Disconnect(); }
        }

        public async Task SendAsync(string data, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            await sendGate.WaitAsync(cancellationToken);
            try
            {
                if (!IsConnected()) return;
                var pipe = mainPipe!;
                var buffer = Encoding.Unicode.GetBytes(data);

                await pipe.WriteAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                await pipe.FlushAsync(cancellationToken);
            }
            finally { sendGate.Release(); }
        }

        public bool IsConnected()
        {
            return connectionPipe != null && connectionPipe.IsConnected && mainPipe != null && mainPipe.IsConnected;
        }

        public void Dispose()
        {
            disposed = true;
            Disconnect();
        }

        public NamedPipeTransport(string pipeName)
        {
            this.pipeName = pipeName;
        }

        private void Disconnect()
        {
            if (mainPipe != null)
            {
                Debug.WriteLine("Disconnected from main pipe");
                mainPipe.Close();
                mainPipe.Dispose();
                mainPipe = null;
            }
            if (connectionPipe != null)
            {
                Debug.WriteLine("Disconnected from connection pipe");
                connectionPipe.Close();
                connectionPipe.Dispose();
                connectionPipe = null;
            }
        }

        private readonly SemaphoreSlim sendGate = new(1, 1);
        private string pipeName;
        private NamedPipeClientStream? connectionPipe;
        private NamedPipeClientStream? mainPipe;
        private bool disposed;

        private void ThrowIfDisposed()
        {
            if (disposed)
            {
                throw new ObjectDisposedException(nameof(NamedPipeTransport));
            }
        }
    }
}
