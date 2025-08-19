using System;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using System.Windows.Controls;

namespace Chess_Project
{
    /// <summary>
    /// This class operates bits in the Epson RC+ script for the Oregon chess robot.
    /// It is responsible for telling the RC+ script what piece(s) to move and where to move them.
    /// </summary>

    public class UOCommunication
    {
        private NetworkStream? networkStream;
        private StreamReader reader;
        private readonly string hostname = "192.168.0.2";   // Oregon controller IP address
        private readonly int port = 5000;



        public bool InitializeConnection()   // Attempts to connect to Oregon
        {
            try
            {
                TcpClient client = new();
                client.Connect(hostname, port);
                networkStream = client.GetStream();   // Establishes connection with Oregon

                string login = "$login\r\n";
                byte[] loginBytes = Encoding.UTF8.GetBytes(login);
                networkStream.Write(loginBytes, 0, loginBytes.Length);   // Logs into Oregon
                reader = new StreamReader(networkStream, Encoding.UTF8);

                if (networkStream.CanRead)   // If network stream supports reading
                {
                    char[] buffer = new char[1024];
                    int bytesRead = reader.Read(buffer, 0, buffer.Length);
                    string response = new(buffer, 0, bytesRead);

                    System.Diagnostics.Debug.WriteLine("Initialization response: " + response);

                    if (response.StartsWith("!"))   // If Oregon rejected command
                    {
                        networkStream?.Close();
                        return false;
                    }
                }

                string startProgram = $"$start,0\r\n";
                byte[] startBytes = Encoding.UTF8.GetBytes(startProgram);
                networkStream.Write(startBytes, 0, startBytes.Length);   // Starts OregonBoot.prg in RC+
                reader = new StreamReader(networkStream, Encoding.UTF8);

                if (networkStream.CanRead)   // If network stream supports reading
                {
                    char[] buffer = new char[1024];
                    int bytesRead = reader.Read(buffer, 0, buffer.Length);
                    string response = new(buffer, 0, bytesRead);

                    System.Diagnostics.Debug.WriteLine("Start program response: " + response);

                    if (response.StartsWith("!"))   // If Oregon rejected command
                    {
                        networkStream?.Close();
                        return false;
                    }
                }

                bool checking = true;

                while (checking)   // While C# checks to see if Oregon completed current move
                {
                    string status = $"$getstatus\r\n";
                    byte[] statusBytes = Encoding.UTF8.GetBytes(status);
                    networkStream.Write(statusBytes, 0, statusBytes.Length);   // Gets status of Oregon
                    reader = new StreamReader(networkStream, Encoding.UTF8);

                    if (networkStream.CanRead)   // If network stream supports reading
                    {
                        char[] buffer = new char[1024];
                        int bytesRead = reader.Read(buffer, 0, buffer.Length);
                        string response = new(buffer, 0, bytesRead);

                        System.Diagnostics.Debug.WriteLine("Status: " + response);

                        if (response.StartsWith("#getstatus,00100000001"))   // If Oregon completed move
                        {
                            checking = false;
                        }

                        else   // Oregon is still moving
                        {
                            System.Threading.Thread.Sleep(1000);
                        }
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing connection: {ex.Message}");

                networkStream?.Close();
                return false;
            }
        }



        public async Task SendData(string rcBits)   // Sends move data to Oregon
        {
            if (networkStream == null)   // If there is not a network stream established
            {
                throw new InvalidOperationException("Connection is not initialized");
            }

            string[] activeBits = rcBits.Split(", ");   // Split moves up

            foreach (var bit in activeBits)   // For every move
            {
                try
                {
                    string rcCommand = $"$execute,\"memon {bit}\"\r\n";
                    byte[] bytes = Encoding.UTF8.GetBytes(rcCommand);
                    networkStream.Write(bytes, 0, bytes.Length);   // Turns on specified memory bit in Oregon
                    reader = new StreamReader(networkStream, Encoding.UTF8);

                    if (networkStream.CanRead)   // If network stream supports reading
                    {
                        char[] buffer = new char[1024];
                        int bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                        string response = new(buffer, 0, bytesRead);

                        System.Diagnostics.Debug.WriteLine(rcCommand);
                        System.Diagnostics.Debug.WriteLine("Feedback: " + response);

                        if (response.StartsWith("!"))   // If Oregon rejected command
                        {
                            networkStream?.Close();
                            return;
                        }
                    }

                    string findPiece = $"$start,1\r\n";
                    byte[] findBytes = Encoding.UTF8.GetBytes(findPiece);
                    networkStream.Write(findBytes, 0, findBytes.Length);   // Starts OregonMove.prg
                    reader = new StreamReader(networkStream, Encoding.UTF8);

                    if (networkStream.CanRead)   // If network stream supports reading
                    {
                        char[] buffer = new char[1024];
                        int bytesRead = await reader.ReadAsync(buffer, 0, buffer.Length);
                        string response = new(buffer, 0, bytesRead);

                        System.Diagnostics.Debug.WriteLine(findPiece);
                        System.Diagnostics.Debug.WriteLine("Feedback: " + response);

                        if (response.StartsWith("!"))   // If Oregon rejected move
                        {
                            networkStream?.Close();
                            return;
                        }
                    }

                    bool checking = true;

                    while (checking)   // While C# checks to see if Oregon completed current move
                    {
                        string status = $"$getstatus\r\n";
                        byte[] statusBytes = Encoding.UTF8.GetBytes(status);
                        networkStream.Write(statusBytes, 0, statusBytes.Length);   // Gets status of Oregon
                        reader = new StreamReader(networkStream, Encoding.UTF8);

                        if (networkStream.CanRead)   // If network stream supports reading
                        {
                            char[] buffer = new char[1024];
                            int bytesRead = reader.Read(buffer, 0, buffer.Length);
                            string response = new(buffer, 0, bytesRead);

                            System.Diagnostics.Debug.WriteLine("Status: " + response);

                            if (response.StartsWith("#getstatus,00100000001"))   // If Oregon completed move
                            {
                                checking = false;
                            }

                            else   // Oregon is still moving
                            {
                                await Task.Delay(1000);
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Error sending moves: {ex.Message}");

                    networkStream?.Close();
                    return;
                }
            }

            string home = $"$start,2\r\n";
            byte[] homeBytes = Encoding.UTF8.GetBytes(home);
            networkStream.Write(homeBytes, 0, homeBytes.Length);   // Starts OregonHome.prg
            reader = new StreamReader(networkStream, Encoding.UTF8);

            if (networkStream.CanRead)
            {
                char[] buffer = new char[1024];
                int bytesRead = reader.Read(buffer, 0, buffer.Length);
                string response = new(buffer, 0, bytesRead);

                System.Diagnostics.Debug.WriteLine("Feedback: " + response);

                if (response.StartsWith("!"))   // If Oregon rejected move
                {
                    networkStream?.Close();
                    return;
                }
            }

            bool waiting = true;

            while (waiting)   // While C# checks to see if Oregon has returned home
            {
                string status = $"$getstatus\r\n";
                byte[] statusBytes = Encoding.UTF8.GetBytes(status);
                networkStream.Write(statusBytes, 0, statusBytes.Length);   // Gets status of Oregon
                reader = new StreamReader(networkStream, Encoding.UTF8);

                if (networkStream.CanRead)   // If network stream supports reading
                {
                    char[] buffer = new char[1024];
                    int bytesRead = reader.Read(buffer, 0, buffer.Length);
                    string response = new(buffer, 0, bytesRead);

                    System.Diagnostics.Debug.WriteLine("Status: " + response);

                    if (response.StartsWith("#getstatus,00100000001"))   // If Oregon completed move
                    {
                        waiting = false;
                    }

                    else   // Oregon is still moving
                    {
                        await Task.Delay(1000);
                    }
                }
            }
        }



        public void CloseConnection()   // Ends connection with Oregon
        {
            string endProgram = $"$start,3\r\n";
            byte[] endProgramBytes = Encoding.UTF8.GetBytes(endProgram);
            networkStream.Write(endProgramBytes, 0, endProgramBytes.Length);   // Starts OregonShutdown.prg
            reader = new StreamReader(networkStream, Encoding.UTF8);

            if (networkStream.CanRead)   // If network stream supports reading
            {
                char[] buffer = new char[1024];
                int bytesRead = reader.Read(buffer, 0, buffer.Length);
                string response = new(buffer, 0, bytesRead);

                System.Diagnostics.Debug.WriteLine("Feedback: " + response);
            }

            networkStream?.Close();
        }
    }
}
