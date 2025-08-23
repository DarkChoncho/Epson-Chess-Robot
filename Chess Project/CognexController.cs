using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Chess_Project
{
    public class CognexController(string ip, int port)
    {
        #region Fields and Constants

        private readonly string _ip = ip;
        private readonly int _port = port;

        private TcpClient? _tcpClient;
        private NetworkStream? _stream;
        private StreamReader? _reader;
        private StreamWriter? _writer;

        #endregion

        #region Connection and Disconnection

        public async Task<bool> ConnectAsync()
        {
            try
            {
                _tcpClient = new TcpClient();
                await _tcpClient.ConnectAsync(_ip, _port);
                _stream = _tcpClient.GetStream();

                _reader = new StreamReader(_stream, Encoding.ASCII);
                _writer = new StreamWriter(_stream, Encoding.ASCII) { AutoFlush = true };

                await ReadUntilAsync("User:");
                await _writer.WriteAsync("admin\r\n");

                await ReadUntilAsync("Password:");
                await _writer.WriteAsync("\r\n");

                string loginMessage = await ReadUntilAsync("User Logged In");
                return loginMessage.Contains("User Logged In");
            }
            catch (Exception ex)
            {
                ChessLog.LogError($"Failed to login to camera {_ip}:{_port}.", ex);
                return false;
            }
        }

        public void Disconnect()
        {
            _stream?.Close();
            _tcpClient?.Close();
            _stream = null;
            _tcpClient = null;
        }


        #endregion

        #region Command Sending and Response Handling

        private async Task<string> ReadUntilAsync(string keyword, int timeoutMs = 3000)
        {
            StringBuilder buffer = new();
            char[] readBuffer = new char[1];
            var start = DateTime.Now;

            while ((DateTime.Now - start).TotalMilliseconds < timeoutMs)
            {
                if (_reader.Peek() > -1)
                {
                    await _reader.ReadAsync(readBuffer, 0, 1);
                    buffer.Append(readBuffer[0]);

                    string currentText = buffer.ToString();
                    if (currentText.Contains(keyword))
                        return currentText;
                }
                else
                {
                    await Task.Delay(10);
                }
            }

            throw new TimeoutException($"Timeout waiting for '{keyword}'.");
        }

        public async Task<(double deltaX, double deltaY)> GetDeltasAsync(int listenPort)
        {
            TcpListener listener = new(IPAddress.Any, listenPort);
            listener.Start();

            try
            {
                // Arm/trigger the camera
                await _writer.WriteAsync("SW8\r\n");
                await ReadUntilAsync("1");

                using TcpClient client = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
                client.NoDelay = true;

                using NetworkStream networkStream = client.GetStream();
                using StreamReader reader = new(networkStream, Encoding.ASCII);

                string? line;
                while ((line = await reader.ReadLineAsync()) != null)
                {
                    // Expect: "Deltas: 57.200,67.000"
                    if (!line.StartsWith("Deltas:", StringComparison.OrdinalIgnoreCase))
                        continue;

                    // Pull the payload after "Deltas:"
                    string payload = line["Deltas: ".Length..].Trim();

                    // Split into two numbers
                    var parts = payload.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length != 2)
                        throw new FormatException($"Expected 2 deltas. Received: \"{line}\".");

                    if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out double dx) ||
                        !double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out double dy))
                    {
                        throw new FormatException($"Could not parse deltas. Received: \"{line}\".");
                    }

                    return (dx, dy);
                }

                throw new InvalidOperationException("No 'Deltas:' line received.");
            }
            catch
            {
                return (0, 0);
            }
            finally
            {
                listener.Stop();
            }
        }

        #endregion
    }
}
