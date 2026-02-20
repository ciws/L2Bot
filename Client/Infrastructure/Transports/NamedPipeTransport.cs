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
    public class NamedPipeTransport : Domain.Transports.TransportInterface, IDisposable
    {
        public event DelegateMessage? Message;

        public async Task ConnectAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (!IsConnected())
            {
                Disconnect();

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
                string pipeName = Encoding.Unicode.GetString(buffer, 0, read).TrimEnd('\0');

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
        }

        public async Task StartReceiveAsync(CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (mainPipe == null)
            {
                return;
            }

            while (IsConnected() && !cancellationToken.IsCancellationRequested)
            {
                byte[] buffer = new byte[16384 * 2];
                int readBytes = await mainPipe.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);

                if (readBytes <= 0)
                {
                    Disconnect();
                    return;
                }

                if (readBytes != 0)
                {
                    string text = Encoding.Unicode.GetString(buffer, 0, readBytes).TrimEnd('\0');
                    if (!string.IsNullOrEmpty(text))
                    {
                        Message?.Invoke(text);
                    }
                }
            }
        }

        public async Task SendAsync(string data, CancellationToken cancellationToken = default)
        {
            ThrowIfDisposed();
            if (IsConnected())
            {
                var buffer = Encoding.Unicode.GetBytes(data);

                await mainPipe!.WriteAsync(buffer.AsMemory(0, buffer.Length), cancellationToken);
                await mainPipe.FlushAsync(cancellationToken);
            }
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
