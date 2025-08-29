using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Chess_Project
{
    /// <summary>
    /// High-level TCP client for a Cognex vision device. Manages socket setup/teardown,
    /// interactive login (user <c>admin</c>, blank password), and small command/response
    /// exchanges used by the chess project (e.g., triggering an acquisition and reading
    /// computed deltas).
    /// </summary>
    /// <param name="ip">IPv4 address of the Cognex camera.</param>
    /// <param name="port">TCP port for the camera session.</param>
    /// <param name="color">Which robot this camera instance represents (e.g., White or Black); used to update side-specific flags in <see cref="GlobalState"/>.</param>
    /// <remarks>
    /// <para><b>Responsibilities</b></para>
    /// <list type="bullet">
    ///     <item><description>Establishes and tears down a TCP session with the device.</description></item>
    ///     <item><description>Preforms the Cognex telnet-style login handshake and verifies the "User Logged In" banner.</description></item>
    ///     <item><description>Serializes all socket I/O with a per-connection <see cref="SemaphoreSlim"/> to prevent interleaved reads/writes.</description></item>
    ///     <item><description>Provides helpers to wait for specific prompts and to retrieve measurement deltas.</description></item>
    ///     <item><description>Logs failures and favors returning error results over throwing, except where documented.</description></item>
    /// </list>
    /// 
    /// <para><b>Thread-safety</b></para>
    /// Instance methods may be called from multiple threads; socket I/O is serialized internally,
    /// but the object itself is not otherwise thread-safe.
    /// 
    /// <para>✅ Updated on 8/28/2025</para>
    /// </remarks>
    public class CognexController(string ip, int port, ChessColor color)
    {
        #region Fields and Constants

        // Synchronization (per-connection)
        private readonly SemaphoreSlim _ioLock = new(1, 1);

        // Configuration (ctor-initialized)
        private readonly string _ip = ip;
        private readonly int _port = port;
        private readonly ChessColor _color = color;

        // Runtime state (mutable)
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private StreamReader? _reader;
        private StreamWriter? _writer;

        #endregion

        #region Connection and Disconnection

        /// <summary>
        /// Connects to the Cognex device over TCP, performs the interactive login
        /// (user: <c>admin</c>, blank password), and marks the connection state.
        /// </summary>
        /// <remarks>
        /// Any exceptions during connect or I/O are caught and logged; on failure the method
        /// updates connection flags and calls <see cref="Disconnect"/>.
        /// <para>✅ Updated on 8/28/2025</para>
        /// </remarks>
        public async Task ConnectAsync()
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_ip, _port);

                _stream = _tcpClient.GetStream();
                _reader = new StreamReader(_stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false);
                _writer = new StreamWriter(_stream, Encoding.ASCII) { AutoFlush = true, NewLine = "\r\n" };

                // Login handshake
                await ReadUntilAsync("User:");
                await _writer.WriteAsync("admin");

                await ReadUntilAsync("Password:");
                await _writer.WriteAsync(string.Empty);

                string banner = await ReadUntilAsync("User Logged In");
                bool connected = banner.Contains("User Logged In", StringComparison.OrdinalIgnoreCase);
                if (!connected) throw new InvalidOperationException($"Login banner not observed. Received: '{banner}'.");
                SetCognexConnected(_color, connected);
            }
            catch (Exception ex)
            {
                ChessLog.LogError($"Failed to connect to Cognex camera {_ip}:{_port}.", ex);
                Disconnect();
            }
        }

        /// <summary>
        /// Closes and disposes the TCP connection and all I/O wrappers (writer, reader, stream, client).
        /// Safe to call multiple times; any disposal errors are swallowed. In addition, marks
        /// the camera as disconnected.
        /// </summary>
        /// <remarks>✅ Updated on 8/28/2025</remarks>
        public void Disconnect()
        {
            // Best effort wrapper: ignore common disposal errors during teardown
            static void Try(Action a)
            {
                try { a(); }
                catch (ObjectDisposedException) { }
                catch (IOException) { }
                catch (SocketException) { }
            }
            
            // Flush/Dispose buffered writer first (it may hold outgoing bytes)
            if (_writer is not null)
            {
                Try(() => _writer.Flush());
                Try(() => _writer.Dispose());
                _writer = null;
            }

            // Dispose reader
            if (_reader is not null)
            {
                Try(() => _reader.Dispose());
                _reader = null;
            }

            // Close/Dispose the NetworkStream
            if (_stream is not null)
            {
                Try(() => _stream.Close());
                _stream = null;
            }

            // Shutdown and dispose the TcpClient/socket
            if (_tcpClient is not null)
            {
                Try(() =>
                {
                    if (_tcpClient.Connected)
                    {
                        // Graceful FIN; ignore failures if already closed
                        _tcpClient.Client.Shutdown(SocketShutdown.Both);
                    }
                });

                Try(() => _tcpClient.Close());
                Try(() => _tcpClient.Dispose());
                _tcpClient = null;
            }

            SetCognexConnected(_color, false);
        }

        /// <summary>
        /// Sets the Cognex connection status for the specified camera color by updating
        /// the corresponding flag in <see cref="GlobalState"/>.
        /// </summary>
        /// <param name="color">Which camera's status to set: <see cref="ChessColor.White"/> or <see cref="ChessColor.Black"/>.</param>
        /// <param name="value"><see langword="true"/> if connected; otherwise, <see langword="false"/>.</param>
        /// <exception>Thrown if <paramref name="color"/> is not a recognized value.</exception>
        /// <remarks>✅ Updated on 8/28/2025</remarks>
        private static void SetCognexConnected(ChessColor color, bool value)
        {
            switch (color)
            {
                case ChessColor.White:
                    GlobalState.WhiteCognexConnected = value;
                    break;

                case ChessColor.Black:
                    GlobalState.BlackCognexConnected = value;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(color), color, "Unsupported camera color.");
            }
        }

        #endregion

        #region Command Sending and Response Handling

        /// <summary>
        /// Reads from the Cognex's Tcp stream until the specified <paramref name="keyword"/> is observed
        /// in the incoming text, then returns the accumulated response (including the keyword).
        /// </summary>
        /// <param name="keyword">The required token to wait for in the incoming data. Comparison is case-sensitive and uses <see cref="StringComparison.Ordinal"/>.</param>
        /// <param name="timeoutMs">Maximum time, in milliseconds, to wait for the keyword before timing out. Defaults to 3000 ms.</param>
        /// <param name="ct">A cancellation token that can abort the read early. If the token is canceled by the caller, an <see cref="OperationCanceledException"/> is thrown.</param>
        /// <returns>
        /// The full text read to far once <paramref name="keyword"/> is found (the returned string includes the keyword).
        /// </returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="keyword"/> is <see langword="null"/> or empty.</exception>
        /// <exception cref="InvalidOperationException">Thrown if the keyword is not observed within <paramref name="timeoutMs"/>.</exception>
        /// <exception cref="TimeoutException">Thrown if <paramref name="ct"/> if canceled by the caller during the read.</exception>
        /// <remarks>
        /// Reads data in chunks and checks for the keyword after each chunk. On timeout (not caller
        /// cancellation), the method converts the internal cancellation token into a <see cref="TimeoutException"/>.
        /// A small in-memory cap is applied to prevent unbounded growth while waiting for the keyword.
        /// <para>✅ Updated on 8/28/2025</para>
        /// </remarks>
        private async Task<string> ReadUntilAsync(string keyword, int timeoutMs = 3000, CancellationToken ct = default)
        {
            if (string.IsNullOrEmpty(keyword))
                throw new ArgumentException("Keyword must be non-empty.", nameof(keyword));
            if (_reader is null)
                throw new InvalidOperationException("Reader not initialized.");

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeoutMs);

            var sb = new StringBuilder(1024);
            var buf = new char[512];

            try
            {
                while (true)
                {
                    int n = await _reader.ReadAsync(buf.AsMemory(0, buf.Length), cts.Token);
                    if (n <= 0) break;
                    sb.Append(buf, 0, n);

                    // Check: Ordinal for protocol tokens
                    if (sb.ToString().Contains(keyword, StringComparison.Ordinal))
                        return sb.ToString();

                    // Optional memory cap (keep last 4KB to avoid unbounded growth)
                    if (sb.Length > 4096)
                        sb.Remove(0, sb.Length - 2048);
                }

                var sw = Stopwatch.StartNew();
                while (sw.ElapsedMilliseconds < timeoutMs)
                {
                    int available = _reader.Peek();
                    if (available == -1) break;
                    int n = await _reader.ReadAsync(buf, 0, buf.Length);
                    if (n <= 0) break;
                    sb.Append(buf, 0, n);
                    if (sb.ToString().Contains(keyword, StringComparison.Ordinal))
                        return sb.ToString();
                }

                throw new TimeoutException($"Timeout waiting for '{keyword}'.");
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                // Timeout from CancelAfter
                throw new TimeoutException($"Timeout waiting for '{keyword}'.");
            }
        }

        /// <summary>
        /// Listens for a one-shot TCP connection from the Cognex device, parses a line of the form
        /// <c>"Deltas: &lt;dx&gt;,&lt;dy&gt;"</c>, and returns the parsed deltas.
        /// <para>
        /// The method first arms/triggers the camera (command <c>SW8</c>) and waits briefly for an ACK,
        /// then accepts a client connection and reads lines until a matching <c>Deltas:</c> payload arrives.
        /// </para>
        /// </summary>
        /// <param name="listenPort">The local TCP port to bind and listen on for the camera's outbound connection.</param>
        /// <param name="ct">Optional cancellation token to abort the operation (trigger, accept, and read). If canceled, the method logs the cancellation and returns a failed tuple.</param>
        /// <returns>
        /// A tuple <c>(ok, deltaX, deltaY)</c> where:
        /// <list type="bullet">
        ///     <item><description><c>ok</c> is <see langword="true"/> when a valid <c>Deltas:</c> line is received and parsed.</description></item>
        ///     <item><description><c>deltaX</c> and <c>deltaY</c> are the parsed values (invariant-culture floats) when <c>ok</c> is <see langword="true"/>.</description></item>
        ///     <item><description>On failure, <c>ok</c> is <see langword="false"/> and both <c>deltaX</c>/<c>deltaY</c> are <see cref="double.NaN"/>.</description></item>
        /// </list>
        /// </returns>
        /// <remarks>
        /// <para>Sequence: Start a <see cref="TcpListener"/> on <c>IPAddress.Any</c>, send <c>SW8</c> to arm/trigger (I/O serialized via a semaphore), wait briefly for an ACK, accept the client within 5 seconds, read lines until one begins with <c>Deltas:</c>, then parse two comma-separated numbers using <see cref="System.Globalization.CultureInfo.InvariantCulture"/>.</para>
        /// <para>All exceptions are caught and logged via <c>ChessLog.LogError</c>; the method does not throw and instead returns <c>(false, NaN, NaN)</c> on failure. The listener is stopped in <c>finally</c>.</para>
        /// <para>✅ Updated on 8/28/2025</para>
        /// <para>Expected payload example: <c>Deltas: 57.200,67.000</c>.</para>
        /// </remarks>
        public async Task<(bool ok, double deltaX, double deltaY)> GetDeltasAsync(int listenPort, CancellationToken ct = default)
        {
            TcpListener listener = new(IPAddress.Any, listenPort);
            listener.Start();

            try
            {
                // Arm/trigger the camera, serialized with any other I/O
                if (_writer is null) throw new InvalidOperationException("Cognex writer not initialized.");

                await _ioLock.WaitAsync(ct);
                try
                {
                    await _writer.WriteLineAsync("SW8");
                    _ = await ReadUntilAsync("1", timeoutMs: 1500, ct: ct);
                }
                finally
                {
                    _ioLock.Release();
                }

                // Accept connection with timeout
                using TcpClient client = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5), ct);
                client.NoDelay = true;

                using NetworkStream networkStream = client.GetStream();
                using StreamReader reader = new(networkStream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false, bufferSize: 1024, leaveOpen: false);

                // Read lines until we see "Deltas: ..."
                while (true)
                {
                    ct.ThrowIfCancellationRequested();

                    string? line = await reader.ReadLineAsync(ct);
                    if (line is null)
                        throw new InvalidOperationException("Client closed before sending deltas.");

                    if (!line.StartsWith("Deltas: ", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Slice after the colon, then trim spaces
                    int colon = line.IndexOf(':');
                    string payload = (colon >= 0 ? line[(colon + 1)..] : string.Empty).Trim();

                    // Split into two numbers
                    var parts = payload.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                        throw new FormatException($"Expected 2 deltas. Received: \"{line}\".");

                    if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double dx) ||
                        !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double dy))
                    {
                        throw new FormatException($"Could not parse deltas. Received: \"{line}\".");
                    }

                    return (true, dx, dy);
                }
            }
            catch (Exception ex)
            {
                ChessLog.LogError($"Failed to obtain deltas from {_ip};{listenPort}.", ex);
                return (false, double.NaN, double.NaN);
            }
            finally
            {
                listener.Stop();
            }
        }

        #endregion
    }
}