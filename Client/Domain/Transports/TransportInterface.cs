using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Client.Domain.Transports
{
    public interface TransportInterface
    {
        public event DelegateMessage Message;

        bool IsConnected();
        Task ConnectAsync(CancellationToken cancellationToken = default);
        Task SendAsync(string data, CancellationToken cancellationToken = default);
        Task StartReceiveAsync(CancellationToken cancellationToken = default);

        public delegate void DelegateMessage(string args);
    }
}
