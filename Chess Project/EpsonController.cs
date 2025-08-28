using System;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Chess_Project
{
    /// <summary>
    /// High-level TCP client for an Epson RC700A robot controller. Manages socket
    /// connection lifecycle, ASCII command/response exchange, motion sequencing driven
    /// by memory bits, and optional vision-based offset application via a connected
    /// <see cref="CognexController"/>.
    /// </summary>
    /// <param name="robotIp">IPv4 address of the RC700A controller.</param>
    /// <param name="robotPort">TCP port for the controller session.</param>
    /// <param name="baseDeltas">Base (ΔX, ΔY) offsets applied to motions in mm.</param>
    /// <param name="deltaScalar">Scale factor applied to vision deltas before they are added to <paramref name="baseDeltas"/>.</param>
    /// <param name="color">Which robot this controller instance represents (e.g., White or Black); used to update side-specific flags in <see cref="GlobalState"/>.</param>
    /// <param name="cognex">A connected <see cref="CognexController"/> used to fetch per-pick vision deltas when available.</param>
    /// <param name="cognexListenPort">Local TCP port on which this process listens for the camera's one-shot "Deltas: dx,dy" callback.</param>
    /// <remarks>
    /// <para><b>Responsibilities</b></para>
    /// <list type="bullet">
    ///     <item><description>Establish and tear down TCP sessions.</description></item>
    ///     <item><description>Serialize socket I/O with a per-connection <see cref="SemaphoreSlim"/> to avoid interleaved reads/writes.</description></item>
    ///     <item><description>Validate controller replies (prefix checks) and poll readiness.</description></item>
    ///     <item><description>Execute bit-driven motion sequences with optional Cognex deltas.</description></item>
    ///     <item><description>Expose convenience routines for speed modes.</description></item>
    ///     <item><description>Publish connection/state to <see cref="GlobalState"/> based on <paramref name="color"/>.</description></item>
    /// </list>
    /// 
    /// <para><b>Thread-safety</b></para>
    /// Instance methods may be called concurrently; all controller I/O is serialized internally.
    /// Global state updates are simply property assignments and are not otherwise synchronized.
    /// 
    /// <para><b>Error Handling</b></para>
    /// Expected validation/connect failures are logged and cause a <see langword="false"/> return from the
    /// calling method. Protocol rejections produce an internal <see cref="EpsonException"/> that is caught
    /// within public methods; exceptions are not propagated to callers unless explicitly documented.
    /// 
    /// <para>✅ Updated on 8/28/2025</para>
    /// </remarks>
    public class EpsonController(string robotIp, int robotPort, double[] baseDeltas, double deltaScalar, Color color, CognexController cognex, int cognexListenPort)
    {
        #region Fields and Protocol

        // Protocol tokens (immutable; keep private)
        private const string LoginCmd     = "$login";
        private const string ResetCmd     = "$reset";
        private const string GetStatusCmd = "$getstatus";
        private const string ReadyStatus  = "#getstatus,00100000001";
        private const string CRLF         = "\r\n";

        // Protocol helpers (string builders)
        private static string Start(int main)   => $"$start,{main}";
        private static string MemOn(string bit) => $"$execute,\"memon {bit}\"";
        private static string SetDouble(string variable, double value) => $"$setvariable,{variable},{value}";

        // Synchronization (per-connection)
        private readonly SemaphoreSlim _ioLock = new(1, 1);

        // Configuration (ctor-initialized)
        private readonly string _robotIp = robotIp;
        private readonly int _robotPort = robotPort;
        private readonly Color _color = color;
        private readonly int _listenPort = cognexListenPort;

        // Runtime state (mutable)
        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private StreamReader? _reader;
        private readonly CognexController _cognex = cognex;
        
        #endregion

        #region Connection and Disconnection

        /// <summary>
        /// Establishes a TCP connection to the Epson RC700A controller, performs login/reset/start,
        /// and verifies readiness. On failure, marks the controller disconnected, disposes the TCP
        /// connection, and logs the error.
        /// </summary>
        /// <remarks>
        /// This method does not throw on expected connection/validation failures; it logs and cleans up.
        /// <para>✅ Updated on 8/28/2025</para>
        /// </remarks>
        public async Task ConnectAsync()
        {
            try
            {
                if ((_color == Color.White && !GlobalState.WhiteEpsonConnected) || (_color == Color.Black && !GlobalState.BlackEpsonConnected))
                {
                    SetEpsonConnected(_color, true);

                    _tcpClient = new();
                    await _tcpClient.ConnectAsync(_robotIp, _robotPort);
                    _stream = _tcpClient.GetStream();
                    _reader = new(_stream, Encoding.UTF8);

                    string loginResponse = await SendCommandAsync(LoginCmd);
                    if (!IsValid(LoginCmd, loginResponse, "#", true))
                        throw new EpsonException(LoginCmd, loginResponse, _robotIp);

                    await Task.Delay(500);
                    string resetResponse = await SendCommandAsync(ResetCmd);
                    if (!IsValid(ResetCmd, resetResponse, "#", true))
                        throw new EpsonException(ResetCmd, resetResponse, _robotIp);

                    await Task.Delay(500);
                    string startResponse = await SendCommandAsync(Start(0));
                    if (!IsValid(Start(0), startResponse, "#", true))
                        throw new EpsonException(Start(0), startResponse, _robotIp);

                    await Task.Delay(500);
                    string? getstatusResponse = await WaitForReadyAsync(ReadyStatus);
                    if (!IsValid(GetStatusCmd, getstatusResponse, ReadyStatus, true))
                        throw new EpsonException(GetStatusCmd, getstatusResponse, _robotIp);
                }
            }
            catch (Exception ex)
            {
                ChessLog.LogError($"Failed to connect to Epson robot {_robotIp}:{_robotPort}.", ex);
                Disconnect();
            }
        }

        /// <summary>
        /// Closes and disposes the TCP connection and all I/O wrappers (reader, stream, client).
        /// Safe to call multiple times; any disposal errors are swallowed. In addition, marks
        /// the robot as disconnected.
        /// </summary>
        /// <remarks>✅ Updated on 8/26/2025</remarks>
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

            SetEpsonConnected(_color, false);
        }

        /// <summary>
        /// Sets the Epson connection status for the specified robot color by updating
        /// the corresponding flag in <see cref="GlobalState"/>.
        /// </summary>
        /// <param name="color">Which robot's status to set: <see cref="Color.White"/> or <see cref="Color.Black"/>.</param>
        /// <param name="value"><see langword="true"/> if connected; otherwise, <see langword="false"/>.</param>
        /// <exception>Thrown if <paramref name="color"/> is not a recognized value.</exception>
        /// <remarks>✅ Updated on 8/26/2025</remarks>
        private static void SetEpsonConnected(Color color, bool value)
        {
            switch (color)
            {
                case Color.White:
                    GlobalState.WhiteEpsonConnected = value;
                    break;

                case Color.Black:
                    GlobalState.BlackEpsonConnected = value;
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(color), color, "Unsupported robot color.");
            }
        }

        #endregion

        #region Command Sending and Response Handling

        /// <summary>
        /// Sends a command to the Epson RC700A over the established TCP connection and
        /// returns the controller's single-line reply.
        /// </summary>
        /// <param name="command">
        /// The command text to send (without line ending), e.g., <c>"$getstatus"</c>. A CRLF
        /// (<c>\r\n</c>) is appended automatically.
        /// </param>
        /// <param name="timeoutMs">
        /// Maximum time, in milliseconds, to wait for a complete line of response before timing out.
        /// Defaults to 5000 ms.
        /// </param>
        /// <param name="ct">
        /// A cancellation token that can abort the send/receive. If cancellation or a timeout occurs,
        /// the method logs the condition and returns <see cref="string.Empty"/>.
        /// </param>
        /// <returns>
        /// The response line (with any trailing <c>\r</c> trimmed); or <see cref="string.Empty"/> if an
        /// error, timeout, or cancellation occurs.
        /// </returns>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><description>
        ///     Uses a per-connection semaphore to serialize socket I/O, so concurrent calls do not interleave.
        ///     </description></item>
        ///     <item><description>
        ///     All exceptions are handled internally and logged; the method does not throw.
        ///     </description></item>
        ///     <item><description>
        ///     Uses UTF-8 encoding and reads until a newline is received.
        ///     </description></item>
        /// </list>
        /// ✅ Updated on 8/26/2025
        /// </remarks>
        private async Task<string> SendCommandAsync(string command, int timeoutMs = 5000, CancellationToken ct = default)
        {
            try
            {
                await _ioLock.WaitAsync(ct);
                try
                {
                    if (_stream is null || _reader is null)
                    {
                        ChessLog.LogError("Method called before connection.");
                        return string.Empty;
                    }

                    // Timeout tied to the caller's token
                    using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
                    timeoutCts.CancelAfter(timeoutMs);

                    // Write command + CRLF
                    var payload = Encoding.UTF8.GetBytes(command + "\r\n");
                    await _stream.WriteAsync(payload, timeoutCts.Token);

                    // Read a full line (cancellable)
                    string? line = await _reader.ReadLineAsync(timeoutCts.Token);

                    return line?.TrimEnd('\r') ?? string.Empty;
                }
                finally
                {
                    _ioLock.Release();
                }
            }
            catch (OperationCanceledException) when (!ct.IsCancellationRequested)
            {
                ChessLog.LogError($"Timeout waiting for response to '{command}'.");
                return string.Empty;
            }
            catch (Exception ex)
            {
                ChessLog.LogError($"Error sending command '{command}'.", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Validates a controller reply for a given command by checking that the
        /// response (after trimming CR/LF) begins with the expected prefix.
        /// </summary>
        /// <param name="command">The command that was sent (e.g., <c>"$login"</c>.</param>
        /// <param name="response">The raw response line from the controller (may be <c>null</c>).</param>
        /// <param name="expectedPrefix">The required prefix of a valid response (e.g., <c>"#"</c> or <c>"#getstatus,00100000001"</c>).</param>
        /// <param name="disconnectOnFailure">When <see langword="true"/>, the TCP connection is closed on failure.</param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="response"/> is non-empty and starts with <paramref name="expectedPrefix"/>; otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// On failure, logs an error.
        /// <para>✅ Updated on 8/26/2025</para>
        /// </remarks>
        private bool IsValid(string command, string? response, string expectedPrefix, bool disconnectOnFailure)
        {
            // Normalize for comparison
            var line = (response ?? string.Empty).TrimEnd('\r', '\n', ' ');
            bool ok = line.StartsWith(expectedPrefix, StringComparison.Ordinal);

            if (ok) return true;

            // Build a concise log message
            const int maxPreview = 256;
            var preview = line.Length <= maxPreview ? line : line.Substring(0, maxPreview) + "…";
            var message =
                $"Epson RC700A {_robotIp} rejected command '{command}': '{preview}' " +
                $"(expected prefix '{expectedPrefix}').";

            ChessLog.LogError(message);

            if (disconnectOnFailure)
                Disconnect();

            return false;
        }

        /// <summary>
        /// Polls the Epson controller for a ready state by issuing <c>$getstatus</c> and
        /// checking whether the reply begins with the expected prefix.
        /// </summary>
        /// <param name="expectedPrefix">The required prefix of a "ready" response (e.g., <c>#getstatus,00100000001</c>).</param>
        /// <param name="maxAttempts">Maximum number of attempts before giving up. Default is 80.</param>
        /// <param name="delayMs">Delay in milliseconds between attempts. Default is 250 ms.</param>
        /// <param name="ct">Optional cancellation token to abort the wait early.</param>
        /// <returns>
        /// The ready response line if observed; otherwise the last response received (which may not indicate ready).
        /// Returns <see langword="null"/> if no response was read at all.
        /// </returns>
        /// <remarks>
        /// Not-ready responses are expected during moves; they are logged at debug and do not disconnect.
        /// <para>✅ Updated on 8/26/2025</para>
        /// </remarks>
        private async Task<string?> WaitForReadyAsync(string expectedPrefix, int maxAttempts = 80, int delayMs = 250, CancellationToken ct = default)
        {
            string? last = null;

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                ct.ThrowIfCancellationRequested();

                last = await SendCommandAsync(GetStatusCmd, ct: ct);

                var line = (last ?? string.Empty).TrimEnd('\r', 'n', ' ');
                if (line.StartsWith(expectedPrefix, StringComparison.Ordinal))
                    return line;

                // Compact debug log
                const int maxPreview = 160;
                var preview = line.Length <= maxPreview ? line : line[..maxPreview] + "…";
                ChessLog.LogDebug($"Ready check {attempt}/{maxAttempts}: '{preview}'");

                await Task.Delay(delayMs, ct);
            }

            return last;
        }

        #endregion

        #region Motion Execution and Recovery

        /// <summary>
        /// Executes a batch "bit-run" on the Epson RC700A: for each requested output bit,
        /// asserts teh digital output, runs motion programs, optionally applies vision
        /// offsets, and verifies the controller returns to a ready state. Finishes by
        /// sending the robot home and re-validating readiness.
        /// </summary>
        /// <param name="rcBits">Comma-separated list of output bit values to activate with <c>memon</c>, e.g., <c>"6, 38"</c>. Each entry is parsed as an integer after trimming.
        /// Invalid entries are logged and cause the method to return <see langword="false"/>.</param>
        /// <param name="ct">Optional cancellation token. Propagated to command sends, ready waits, and camera reads so the operation can be aborted cooperatively.</param>
        /// <returns>
        /// <see langword="true"/> if every per-bit sequence succeeds and the final home/ready
        /// check completes; otherwise, <see langword="false"/> (errors are logged).
        /// </returns>
        /// <remarks>
        /// <para><b>Per-bit sequence:</b></para>
        /// <list type="bullet">
        ///     <item><description>Send <c>memon &lt;bit&gt;</c> to assert the output.</description></item>
        ///     <item><description>Start the stage program with <c>$start,1</c>.</description></item>
        ///     <item><description>Poll <c>$getstatus</c> until the ready banner (<c>#getstatus,00100000001</c>) appears.</description></item>
        ///     <item><description>Compute motion offsets from <see cref="baseDeltas"/>; when the matching Cognex camera is connected and the bit denotes a "pick" region, retrieve vision deltas and apply <see cref="deltaScalar"/>.</description></item>
        ///     <item><description>Write <c>deltaX</c> and <c>deltaY</c> to the controller.</description></item>
        ///     <item><description>Re-assert <c>memon &lt;bit&gt;</c>, run <c>$start,3</c>, and wait ready again.</description></item>
        /// </list>
        /// 
        /// <para><b>Completion</b></para>
        /// Sends <c>$start,2</c> (home) and validates ready one final time.
        /// 
        /// <para><b>Error Handling</b></para>
        /// Validation failures throw an internal <see cref="EpsonException"/> that is caught by this method;
        /// the failure is logged (including the bit list and endpoint), the robot state is set to
        /// <see cref="RobotState.Error"/>, and the method returns <see langword="false"/>. Bit-parsing errors
        /// are also logged and cause an immediate <see langword="false"/> return.
        /// This method does not rethrow to the caller.
        /// 
        /// <para>✅ Updated on 8/28/2025</para>
        /// </remarks>
        public async Task<bool> SendDataAsync(string rcBits, CancellationToken ct = default)
        {
            try
            {
                var activeBits = rcBits.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToArray();

                foreach (var bitStr in activeBits)
                {
                    if (!int.TryParse(bitStr, out int bitNumber))
                    {
                        ChessLog.LogError($"Invalid bit '{bitStr}' in '{rcBits}'.");
                        return false;
                    }

                    // memon <bit>
                    string memOnResponse = await SendCommandAsync(MemOn(bitStr), ct: ct);
                    if (!IsValid(MemOn(bitStr), memOnResponse, "#", disconnectOnFailure: false))
                        throw new EpsonException(MemOn(bitStr), memOnResponse, _robotIp);

                    // $start,1
                    string stageResponse = await SendCommandAsync(Start(1), ct: ct);
                    if (!IsValid(Start(1), stageResponse, "#", disconnectOnFailure: false))
                        throw new EpsonException(Start(1), stageResponse, _robotIp);

                    // getstatus
                    string? getstatusResponse = await WaitForReadyAsync(ReadyStatus, ct: ct);
                    if (!IsValid(GetStatusCmd, getstatusResponse, ReadyStatus, disconnectOnFailure:false))
                        throw new EpsonException(GetStatusCmd, getstatusResponse, _robotIp);

                    // Compute deltas
                    double deltaX = baseDeltas[0];
                    double deltaY = baseDeltas[1];

                    bool cameraConnected =
                        (_color == Color.White && GlobalState.WhiteCognexConnected) ||
                        (_color == Color.Black && GlobalState.BlackCognexConnected);

                    // Picking regions only
                    bool isPick = bitNumber < 64 || (bitNumber > 127 && bitNumber < 160);

                    if (cameraConnected && isPick && _cognex is not null)
                    {
                        var (ok, dx, dy) = await _cognex.GetDeltasAsync(_listenPort, ct);

                        if (ok)
                        {
                            deltaX += (dx * deltaScalar);
                            deltaY += (dy * deltaScalar);
                        }
                        else
                        {
                            ChessLog.LogWarning($"Camera deltas unavailable for bit {bitNumber}; using base deltas.");
                        }
                    }

                    // Write both deltas
                    if (!IsValid(SetDouble("deltaX", deltaX), await SendCommandAsync(SetDouble("deltaX", deltaX), ct: ct), "#", disconnectOnFailure: false)) return false;
                    if (!IsValid(SetDouble("deltaY", deltaY), await SendCommandAsync(SetDouble("deltaY", deltaY), ct: ct), "#", disconnectOnFailure: false)) return false;

                    // Re-assert bit, then $start,3
                    memOnResponse = await SendCommandAsync(MemOn(bitStr), ct: ct);
                    if (!IsValid(MemOn(bitStr), memOnResponse, "#", disconnectOnFailure: false))
                        throw new EpsonException(MemOn(bitStr), memOnResponse, _robotIp);

                    string startResponse = await SendCommandAsync(Start(3), ct: ct);
                    if (!IsValid(Start(3), startResponse, "#", disconnectOnFailure: false))
                        throw new EpsonException(Start(3), startResponse, _robotIp);

                    string? completionResponse = await WaitForReadyAsync(ReadyStatus, ct: ct);
                    if (!IsValid(GetStatusCmd, completionResponse, ReadyStatus, disconnectOnFailure:false))
                        throw new EpsonException(GetStatusCmd, completionResponse, _robotIp);
                }

                // Send robot home
                string homeResponse = await SendCommandAsync(Start(2), ct: ct);
                if (!IsValid(Start(2), homeResponse, "#", disconnectOnFailure:false))
                    throw new EpsonException(Start(2), homeResponse, _robotIp);

                string? ready = await WaitForReadyAsync(ReadyStatus, ct: ct);
                if (!IsValid(GetStatusCmd, ready, ReadyStatus, disconnectOnFailure:false))
                    throw new EpsonException(GetStatusCmd, ready, _robotIp);

                return true;

            }
            catch (Exception ex)
            {
                ChessLog.LogError($"Data send failed for {_robotIp}:{_robotPort} (bits: '{rcBits}').", ex);
                ChangeRobotState(RobotState.Error);
                return false;
            }
        }

        /// <summary>
        /// Starts the controller's low-speed routine (program slot <c>5</c>) by sending <c>$start,5</c>
        /// and validating the reply.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the controller acknowledges the command (reply begins with <c>#</c>);
        /// otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// This method sends <c>$start,5</c> and validates the response. This method itself does not throw.
        /// <para>✅ Updated on 8/28/2025</para>
        /// </remarks>
        public async Task<bool> LowSpeedAsync()
        {
            if (!IsValid(Start(5), await SendCommandAsync(Start(5)), "#", false)) return false;
            return true;
        }

        /// <summary>
        /// Starts the controller's high-speed routine (program slot <c>4</c>) by sending <c>$start,4</c>
        /// and validating the reply.
        /// </summary>
        /// <returns>
        /// <see langword="true"/> if the controller acknowledges the command (reply begins with <c>#</c>);
        /// otherwise, <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// This method sends <c>$start,4</c> and validates the response. This method itself does not throw.
        /// <para>✅ Updated on 8/28/2025</para>
        /// </remarks>
        public async Task<bool> HighSpeedAsync()
        {
            if (!IsValid(Start(4), await SendCommandAsync(Start(4)), "#", false)) return false;
            return true;
        }

        #endregion

        #region State Management

        /// <summary>
        /// Updates the global robot state for the associated robot (White or Black) based on this controller's color.
        /// </summary>
        /// <param name="robotState">The new <see cref="RobotState"/> to assign to the corresponding robot.</param>
        /// <remarks>
        /// This method sets either <see cref="GlobalState.WhiteState"/> or <see cref="GlobalState.BlackState"/> depending on the value of <see cref="_color"/>.
        /// <para>✅ Written on 6/10/2025</para>
        /// </remarks>
        public void ChangeRobotState(RobotState robotState)
        {
            if (_color == Color.White)
                GlobalState.WhiteState = robotState;
            else
                GlobalState.BlackState = robotState;
        }

        #endregion

        #region Exceptions

        /// <summary>
        /// Defines an exception that is thrown when the Epson RC700A controller responds to a command
        /// with an invalid, empty, or otherwise unexpected message.
        /// </summary>
        /// <param name="commandSent">The command that was issued to the controller (e.g., "$start,1").</param>
        /// <param name="response">The raw response returned by the controller.</param>
        /// <param name="ip">The IP address of the controller that generated the response.</param>
        /// <remarks>
        /// This exception provides detailed context by including the command, response, and controller IP
        /// in its message, making it easier to diagnose and log communication issues during robot operations.
        /// <para>✅ Written on 6/10/2025</para>
        /// </remarks>
        public class EpsonException(string commandSent, string response, string ip) : Exception(FormatMessage(commandSent, response, ip))
        {
            /// <summary>
            /// Formats a descriptive error message indicating that the Epson RC700A controller
            /// rejected a specific command, including the controller's IP address and the response received.
            /// </summary>
            /// <param name="command">The command that was sent to the controller.</param>
            /// <param name="response">The response received from the controller.</param>
            /// <param name="ip">The IP address of the controller that responded.</param>
            /// <returns>
            /// A formatted string describing the rejection, including the command, IP, and response details.
            /// If the response is null or empty, a default message is returned indicating that no response was received.
            /// </returns>
            /// <remarks>✅ Written on 6/10/2025</remarks>
            private static string FormatMessage(string command, string response, string ip)
            {
                if (string.IsNullOrWhiteSpace(response))
                    return $"Epson RC700A {ip} rejected command {command}: Null or empty response.";

                return $"Epson RC700A {ip} rejected command {command}: {response}.";
            }
        }

        #endregion
    }
}