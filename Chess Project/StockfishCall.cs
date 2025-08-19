using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace Chess_Project
{
    /// <summary>
    /// This class is for prompting Stockfish to evaluate the current position for the CheckmateVerifier.
    /// It calculates if there are any legal moves available, and if so, the centipawn value of the best possible move.
    /// </summary>

    public class StockfishCall : IDisposable
    {
        private readonly Process stockfishProcess;

        public StockfishCall(string stockfishPath)
        {
            stockfishProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = stockfishPath,
                    UseShellExecute = false,
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                },
                EnableRaisingEvents = true
            };

            stockfishProcess.ErrorDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    Debug.WriteLine($"Stockfish error: {e.Data}");
                }
            };

            stockfishProcess.Start();
        }

        public string GetStockfishResponse(string fenCode)
        {
            stockfishProcess.StandardInput.WriteLine($"position fen {fenCode}");
            stockfishProcess.StandardInput.WriteLine($"go depth {24}");

            System.Threading.Thread.Sleep(1000);
            stockfishProcess.StandardInput.WriteLine("quit");

            string output = stockfishProcess.StandardOutput.ReadToEnd();

            return output;
        }

        public void Dispose()
        {
            stockfishProcess.Close();
            stockfishProcess.Dispose();
        }
    }
}