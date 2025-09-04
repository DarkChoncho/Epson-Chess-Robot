using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Chess_Project
{
    public sealed class Watchdog : IDisposable
    {
        private readonly CancellationTokenSource _cts = new();
        private readonly TimeSpan _interval = TimeSpan.FromSeconds(20);
        private Task? _loop;

        public void Start()
        {
            _loop = Task.Run(async () =>
            {
                while (!_cts.IsCancellationRequested)
                {
                    try
                    {
                        // Epson and Cognex checks here
                        Debug.WriteLine("Watchdog tick at" + DateTime.Now);
                    }
                    catch (Exception ex)
                    {
                        ChessLog.LogError($"Watchdog error: {ex.Message}");
                    }

                    await Task.Delay(_interval, _cts.Token);
                }
            });
        }

        public void Dispose()
        {
            _cts.Cancel();
            _loop?.Dispose();
            _cts.Dispose();
        }
    }

    public interface IEpsonEndpoint
    {
        string Name { get; }
        Task<bool> PingAsync(CancellationToken ct);
        Task<bool> EnsureConnectedAsync(CancellationToken ct);
    }

    public interface ICognexEndpoint
    {
        string Name { get; }
        Task<bool> PingAsync(CancellationToken ct);
        Task<bool> EnsureConnectedAsync(CancellationToken ct);
    }
}
