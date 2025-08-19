using System;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Project
{
    /// <summary>
    /// Provides high-level communication and control logic for an Epson RC700A robot controller over TCP/IP.
    /// </summary>
    /// <param name="ip">The IP address of the Epson RC700A controller.</param>
    /// <param name="port">The TCP port number used to establish the connection.</param>
    /// <param name="robotColor">The identity of the robot (e.g., White or Black) used to distinguish controller state in global context.</param>
    /// <remarks>
    /// This class manages connection setup, command transmission, motion execution, error recovery,
    /// and state tracking for a specific Epson robot (White or Black). It includes the following key features:
    /// <list type="bullet">
    ///   <item>Connection lifecycle management via <c>ConnectAsync</c>, <c>Disconnect</c>, and internal retry logic.</item>
    ///   <item>Command execution and response validation for Epson ASCII-based control commands.</item>
    ///   <item>Motion sequencing through bit-driven output signals and homing routines.</item>
    ///   <item>Recovery routines using reset and home fallback strategies when errors occur.</item>
    ///   <item>Global state updates based on robot color and operational outcomes.</item>
    /// </list>
    /// <para>
    /// Exceptions thrown during robot operation are captured by the nested <see cref="EpsonException"/> class,
    /// which provides diagnostic information about failed commands and controller responses.
    /// </para>
    /// <para>✅ Class updated on 6/10/2025</para>
    /// </remarks>
    public class EpsonController(string robotIp, int robotPort, RobotColor robotColor, string cognexIp, int cognexPort)
    {
        #region Fields and Constants

        private TcpClient? _tcpClient;
        private NetworkStream? _networkStream;
        private StreamReader _reader;

        private readonly string _robotIp = robotIp;
        private readonly int _robotPort = robotPort;
        private readonly RobotColor _robotColor = robotColor;

        private CognexController _cognex;
        private readonly string _cognexIp = cognexIp;
        private readonly int _cognexPort = cognexPort;

        private const string ReadyStatus = "#getstatus,00100000001";
        private double _xAdjust;
        private double _yAdjust;

        #endregion

        #region Connection and Disconnection

        /// <summary>
        /// Establishes a TCP connection to the Epson RC700A controller, performs login and boot commands,
        /// and verifies that the controller is ready for operation.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the connection, login, boot, and readiness check are all successful; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// If any step fails, the controller is marked as disconnected, the TCP connection is disposed,
        /// and a failure result is returned. All validation and cleanup behavior is handled internally,
        /// and any exceptions encountered during the connection process are caught and logged.
        /// <para>✅ Updated on 6/10/2025</para>
        /// </remarks>
        public async Task<bool> ConnectAsync()
        {
            try
            {
                _tcpClient = new();
                await _tcpClient.ConnectAsync(_robotIp, _robotPort);
                _networkStream = _tcpClient.GetStream();
                if (_networkStream == null)
                    throw new InvalidOperationException($"Epson RC700A {_robotIp} network stream was not initialized.");
                _reader = new(_networkStream, Encoding.UTF8);

                if (!IsValid("$login", await SendCommandAsync("$login"), "#", true)) return false;
                if (!IsValid("$start,0", await SendCommandAsync("$start,0"), "#", true)) return false;
                if (!IsValid("$getstatus", await WaitForReadyAsync(), ReadyStatus, true)) return false;

                _cognex = new CognexController(_cognexIp, _cognexPort);
                if (!await _cognex.ConnectAsync()) return false;

                return true;
            }
            catch (Exception ex)
            {
                ChessLog.LogFatal($"Epson RC700A {_robotIp} encountered an unexpected error during connection", ex);
                ChangeRobotState(RobotState.Disconnected);
                Disconnect();
                return false;
            }
        }

        /// <summary>
        /// Gracefully disconnects from the Epson RC700A controller by closing the network stream,
        /// disposing of the TCP client, and marking the robot as disconnected in the global state.
        /// </summary>
        /// <remarks>
        /// The method updates <see cref="GlobalState.WhiteConnected"/> or <see cref="GlobalState.BlackConnected"/>
        /// depending on the controller's assigned color. Any exceptions encountered during cleanup
        /// caught and logged as warnings. If disposal is successful, an informational log entry is recorded.
        /// Internal references to the stream and client are nullified to prevent accidental reuse.
        /// <para>✅ Updated on 6/10/2025</para>
        /// </remarks>
        public void Disconnect()
        {
            try
            {
                if (_robotColor == RobotColor.White)
                    GlobalState.WhiteConnected = false;
                else
                    GlobalState.BlackConnected = false;

                _networkStream?.Close();
                _tcpClient?.Dispose();

                _networkStream = null;
                _tcpClient = null;

                ChessLog.LogInformation($"Disposed Epson RC700A {_robotIp} TCP client.");
            }
            catch (Exception ex)
            {
                ChessLog.LogWarning($"Failed to dispose Epson RC700A {_robotIp} TCP client cleanly.", ex);
            }
        }

        /// <summary>
        /// Attempts to gracefully reconnect to the Epson RC700A controller by first disconnecting
        /// the current TCP connection, waiting briefly, and then establishing a new connection.
        /// </summary>
        /// <returns>
        /// <c>true</c> if the reconnection attempt is successful; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// This method calls <see cref="Disconnect"/> to release the current connection,
        /// waits one second to allow the system or controller to reset, and then calls <see cref="ConnectAsync"/>.
        /// Any exceptions during disconnection are handled within the <c>Disconnect</c> method.
        /// <para>✅ Written on 6/10/2025</para>
        /// </remarks>
        private async Task<bool> AttemptReconnectAsync()
        {
            Disconnect();
            await Task.Delay(1000);
            return await ConnectAsync();
        }

        #endregion

        #region Command Sending and Response Handling

        /// <summary>
        /// Sends a command to the Epson RC700A controller over TCP and returns the response string.
        /// </summary>
        /// <param name="command">The ASCII command to send (e.g., "$getstatus").</param>
        /// <returns>
        /// The response string from the controller, or an empty string if an error occurs during transmission or reception.
        /// </returns>
        /// <remarks>
        /// This method handles all exceptions internally and logs errors using <see cref="ChessLog.LogError"/>.
        /// It never throws and always returns a string.
        /// <para>✅ Written on 6/10/2025</para>
        /// </remarks>
        private async Task<string> SendCommandAsync(string command)
        {
            try
            {
                byte[] bytes = Encoding.UTF8.GetBytes(command + "\r\n");
                await _networkStream.WriteAsync(bytes);

                char[] buffer = new char[1024];
                int bytesRead = await _reader.ReadAsync(buffer, 0, buffer.Length);
                return new string(buffer, 0, bytesRead);
            }
            catch (Exception ex)
            {
                ChessLog.LogError($"Failed while sending \"{command}\" to Epson RC700A {_robotIp}.", ex);
                return string.Empty;
            }
        }

        /// <summary>
        /// Validates the response received from the Epson RC700A controller for a given command,
        /// checking whether it begins with the expected search term.
        /// </summary>
        /// <param name="command">The command string that was sent to the controller (e.g., "$login").</param>
        /// <param name="response">The response received from the controller to validate.</param>
        /// <param name="searchTerm">
        /// The expected prefix of a valid response (e.g., "#" or "#getstatus,00100000001").
        /// </param>
        /// <param name="disconnectOnFailure">
        /// If <c>true</c>. the controller will be disconnected and marked as disconnected in global state upon failure.
        /// </param>
        /// <returns>
        /// <c>true</c> if the response is not null or empty and starts with the specified <paramref name="searchTerm"/>; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// If validation fails, an error is logged and the robot's connection state is updated.
        /// When <paramref name="disconnectOnFailure"/> is <c>true</c>, the TCP connection is also closed.
        /// <para>✅ Written on 6/10/2025</para>
        /// </remarks>
        private bool IsValid(string command, string? response, string searchTerm, bool disconnectOnFailure)
        {
            if (string.IsNullOrEmpty(response) || !response.StartsWith(searchTerm))
            {
                string message = $"Epson RC700A {_robotIp} rejected command {command}: {response}.";
                if (disconnectOnFailure)
                    message += " Disposing of TCP client.";

                ChessLog.LogError(message);
                ChangeRobotState(RobotState.Disconnected);

                if (disconnectOnFailure)
                    Disconnect();

                return false;
            }
            return true;
        }

        /// <summary>
        /// Polls the Epson controller for a ready state, retrying up to the specified number of attempts.
        /// </summary>
        /// <param name="maxAttempts">Maximum number of attempts before giving up.</param>
        /// <param name="delayMs">Delay in milliseconds between attempts.</param>
        /// <returns>
        /// The ready response string if successful; otherwise, the last received response (which may not indicate ready state).
        /// </returns>
        /// <remarks>✅ Written on 6/10/2025</remarks>
        private async Task<string?> WaitForReadyAsync(int maxAttempts = 10, int delayMs = 1000)
        {
            string? response = null;

            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                response = await SendCommandAsync("$getstatus");
                if (!string.IsNullOrEmpty(response) && response.StartsWith(ReadyStatus))
                    return response;

                ChessLog.LogDebug($"Attempt {attempt + 1} of {maxAttempts}: {response}.");
                await Task.Delay(delayMs);
            }

            return response;
        }

        #endregion

        #region Motion Execution and Recovery

        /// <summary>
        /// Sends a sequence of digital output commands to the Epson RC700A controller using the specified bit values,
        /// executes corresponding motion programs, and ensures the controller returns to a ready state after each operation.
        /// </summary>
        /// <param name="rcBits">
        /// A comma-separated string of bit values to activate via the <c>memon</c> command (e.g., "1000, 2000").
        /// </param>
        /// <returns>
        /// <c>true</c> if all bit activations, motion executions, and readiness checks complete successfully; otherwise, <c>false</c>.
        /// </returns>
        /// <remarks>
        /// For each bit value, this method:
        /// <list type="number">
        ///   <item>Sends a <c>memon</c> command to activate the output.</item>
        ///   <item>Starts the motion program with <c>$start,1</c>.</item>
        ///   <item>Waits for the controller to return a ready status.</item>
        /// </list>
        /// If any step fails, it attempts recovery by resetting and homing the controller, then retries the operation.  
        /// If the retry also fails, an <see cref="EpsonException"/> is thrown.
        /// After all bits are processed, the robot is commanded to return home using <c>$start,2</c>,
        /// and its readiness is revalidated.
        /// Any fatal error sets the robot to an error state and returns <c>false</c>.
        /// <para>✅ Updating...</para>
        /// </remarks>
        public async Task<bool> SendDataAsync(string rcBits)
        {
            try
            {
                string? readyResponse = null;
                string[] activeBits = rcBits.Split(", ");
                foreach (var bit in activeBits)
                {
                    string memOnCommand = $"$execute,\"memon {bit}\"";
                    string memOnResponse = await SendCommandAsync(memOnCommand);
                    if (!IsValid(memOnCommand, memOnResponse, "#", false))
                    {
                        await AttemptRecoveryAsync();

                        memOnResponse = await SendCommandAsync(memOnCommand);
                        if (!IsValid(memOnCommand, memOnResponse, "#", false))
                            throw new EpsonException(memOnCommand, memOnResponse, _robotIp);

                        ChessLog.LogInformation("Move successful after retry.");
                    }

                    string stageCommand = "$start,1";
                    string stageResponse = await SendCommandAsync(stageCommand);
                    if (!IsValid(stageCommand, stageResponse, "#", false))
                    {
                        await AttemptRecoveryAsync();

                        stageResponse = await SendCommandAsync(stageCommand);
                        if (!IsValid(stageCommand, stageResponse, "#", false))
                            throw new EpsonException(stageCommand, stageResponse, _robotIp);

                        ChessLog.LogInformation("Move successful after retry.");
                    }

                    readyResponse = await WaitForReadyAsync();
                    if (!IsValid("$getstatus", readyResponse, ReadyStatus, false))
                    {
                        await AttemptRecoveryAsync();

                        memOnResponse = await SendCommandAsync(memOnCommand);
                        if (!IsValid(memOnCommand, memOnResponse, "#", false))
                            throw new EpsonException(memOnCommand, memOnResponse, _robotIp);

                        stageResponse = await SendCommandAsync(stageCommand);
                        if (!IsValid(stageCommand, stageResponse, "#", false))
                            throw new EpsonException(stageCommand, stageResponse, _robotIp);

                        readyResponse = await WaitForReadyAsync();
                        if (!IsValid("$getstatus", readyResponse, ReadyStatus, false))
                            throw new EpsonException("$getstatus", readyResponse, _robotIp);

                        ChessLog.LogInformation("Stage successful after retry.");
                    }

                    int bitNumber = int.Parse(bit);
                    if (bitNumber < 64 || (bitNumber > 127 && bitNumber < 160))  // Picking a piece
                    {
                        (double pieceDeltaX, double pieceDeltaY) = await _cognex.GetDeltasAsync(DeltaType.Piece, 3000);

                        if (!IsValid($"$setvariable,pieceDeltaX,{pieceDeltaX}", await SendCommandAsync($"$setvariable,pieceDeltaX,{pieceDeltaX}"), "#", true)) return false;
                        if (!IsValid($"$setvariable,pieceDeltaY,{pieceDeltaY}", await SendCommandAsync($"$setvariable,pieceDeltaY,{pieceDeltaY}"), "#", true)) return false;
                    }
                    else  // Placing a piece
                    {
                        (double squareDeltaX, double squareDeltaY) = await _cognex.GetDeltasAsync(DeltaType.Square, 3000);

                        if (!IsValid($"$setvariable,squareDeltaX,{squareDeltaX}", await SendCommandAsync($"$setvariable,squareDeltaX,{squareDeltaX}"), "#", true)) return false;
                        if (!IsValid($"$setvariable,squareDeltaY,{squareDeltaY}", await SendCommandAsync($"$setvariable,squareDeltaY,{squareDeltaY}"), "#", true)) return false;
                    }

                    if (!IsValid($"$setvariable,deltasFound,0", await SendCommandAsync($"$setvariable,deltasFound,0"), "#", true)) return false;

                    string? completionResponse = await WaitForReadyAsync();
                    if (IsValid("$getstatus", completionResponse, ReadyStatus, false))
                    {
                        if (!IsValid($"$setvariable,deltasFound,-1", await SendCommandAsync($"$setvariable,deltasFound,-1"), "#", true)) return false;
                    }
                }

                string homeCommand = "$start,2";
                string homeResponse = await SendCommandAsync(homeCommand);
                if (!IsValid(homeCommand, homeResponse, "#", false))
                {
                    await AttemptRecoveryAsync();
                    ChessLog.LogInformation("Move to home successful after retry.");
                }

                readyResponse = await WaitForReadyAsync();
                if (!IsValid("$getstatus", readyResponse, ReadyStatus, false))
                {
                    await AttemptRecoveryAsync();
                    ChessLog.LogInformation("Move to home successful after retry.");
                }

                return true;

            }
            catch (EpsonException eEx)
            {
                ChessLog.LogFatal($"Epson RC700A {_robotIp} encountered a fatal error. Entering error state.", eEx);
                ChangeRobotState(RobotState.Error);
                return false;
            }
            catch (Exception ex)
            {
                ChessLog.LogFatal($"Epson RC700A {_robotIp} encountered a fatal error. Entering error state.", ex);
                ChangeRobotState(RobotState.Error);
                return false;
            }
        }

        /// <summary>
        /// Attempts to restore the Epson RC700A controller to a known good state by issuing a reset command,
        /// initiating the homing routine, and confirming that the controller reports a ready status.
        /// </summary>
        /// <returns>
        /// A task that represents the asynchronous recovery operation.
        /// </returns>
        /// <exception cref="EpsonException">
        /// Thrown if any of the recovery steps (reset, home, or ready check) fail to complete successfully.
        /// </exception>
        /// <remarks>
        /// This method is typically called after a failed motion command. It performs the following steps:
        /// <list type="number">
        ///   <item>Sends a <c>$reset</c> command to clear controller errors.</item>
        ///   <item>Issues a <c>$start,2</c> command to move the robot to its home position.</item>
        ///   <item>Polls the controller until it reports the expected ready status.</item>
        /// </list>
        /// Any failure during these steps results in an <see cref="EpsonException"/> being thrown.
        /// <para>✅ Written on 6/10/2025</para>
        /// </remarks>
        private async Task AttemptRecoveryAsync()
        {
            ChessLog.LogInformation($"Epson RC700A {_robotIp} attempting recovery to home.");

            string resetCommand = "$reset";
            string resetResponse = await SendCommandAsync(resetCommand);
            if (!resetResponse.StartsWith('#'))
                throw new EpsonException(resetCommand, resetResponse, _robotIp);

            string homeCommand = "$start,2";
            string homeResponse = await SendCommandAsync(homeCommand);
            if (!homeResponse.StartsWith('#'))
                throw new EpsonException(homeCommand, homeResponse, _robotIp);

            string? readyResponse = await WaitForReadyAsync();
            if (string.IsNullOrEmpty(readyResponse) || !readyResponse.StartsWith("#getstatus,00100000001"))
                throw new EpsonException("$getstatus", readyResponse, _robotIp);

            ChessLog.LogInformation($"Epson RC700A {_robotIp} successfully recovered. Retrying move.");
        }

        #endregion

        #region State Management

        /// <summary>
        /// Updates the global robot state for the associated robot (White or Black) based on this controller's color.
        /// </summary>
        /// <param name="robotState">The new <see cref="RobotState"/> to assign to the corresponding robot.</param>
        /// <remarks>
        /// This method sets either <see cref="GlobalState.WhiteState"/> or <see cref="GlobalState.BlackState"/> depending on the value of <see cref="_robotColor"/>.
        /// <para>✅ Written on 6/10/2025</para>
        /// </remarks>
        public void ChangeRobotState(RobotState robotState)
        {
            if (_robotColor == RobotColor.White)
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
        /// <para>✅ Class written on 6/10/2025</para>
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