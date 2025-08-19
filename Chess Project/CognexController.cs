using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
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
        private TcpListener? _listener;

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

        public async Task<(double deltaX, double deltaY)> GetDeltasAsync(DeltaType deltaType, int listenPort)
        {
            TcpListener listener = new(IPAddress.Any, listenPort);
            listener.Start();

            await _writer.WriteAsync("SW8\r\n");
            await ReadUntilAsync("1");

            using TcpClient resultClient = await listener.AcceptTcpClientAsync();
            using NetworkStream resultStream = resultClient.GetStream();
            using StreamReader resultReader = new(resultStream, Encoding.ASCII);

            string? line;
            while ((line = await resultReader.ReadLineAsync()) != null)
            {
                if (line.StartsWith("Deltas:"))
                {
                    string[] parts = line.Replace("Deltas:", "").Trim().Split(',');

                    switch (deltaType)
                    {
                        case DeltaType.Piece:
                            if (parts.Length <= 4 &&
                                double.TryParse(parts[0], out double pieceDeltaX) &&
                                double.TryParse(parts[1], out double pieceDeltaY))
                            {
                                return (pieceDeltaX, pieceDeltaY);
                            }

                            throw new FormatException($"Deltas format is invalid. Response: {line}.");

                        case DeltaType.Square:
                            if (parts.Length <= 4 &&
                                double.TryParse(parts[2], out double squareDeltaX) &&
                                double.TryParse(parts[3], out double squareDeltaY))
                            {
                                return (squareDeltaX, squareDeltaY);
                            }

                            throw new FormatException($"Deltas format is invalid. Response: {line}.");
                    }
                    
                }
            }

            throw new Exception("No 'Deltas:' string received.");
        }

        #endregion
    }
}
