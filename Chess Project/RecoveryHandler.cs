using Chess_Project;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Transactions;

namespace Chess_Project
{
    public class RecoveryHandler(string executableDirectory)
    {
        private readonly string _recoveryFilePath = Path.Combine(executableDirectory, "recovery.json");
        
        public bool RecoveryNeeded { get; private set; }
        public Dictionary<string, MainWindow.PieceInit>? RecoveryPieces { get; private set; }

        public void SaveRecovery(Dictionary<string, MainWindow.PieceInit> pieces, bool needed)
        {
            RecoveryPieces = pieces;
            RecoveryNeeded = needed;

            var dto = new RecoveryDto()
            {
                RecoveryNeeded = needed,
                Pieces = pieces
            };

            var options = new JsonSerializerOptions { WriteIndented = true };
            File.WriteAllText(_recoveryFilePath, JsonSerializer.Serialize(dto, options));
        }

        public void LoadRecovery()
        {
            if (!File.Exists(_recoveryFilePath))
            {
                RecoveryNeeded = false;
                RecoveryPieces = null;
                return;
            }

            var json = File.ReadAllText(_recoveryFilePath);
            var dto = JsonSerializer.Deserialize<RecoveryDto>(json);

            if (dto != null)
            {
                RecoveryNeeded = dto.RecoveryNeeded;
                RecoveryPieces = dto.Pieces ?? new Dictionary<string, MainWindow.PieceInit>();
            }
            else
            {
                RecoveryNeeded = false;
                RecoveryPieces = null;
            }
        }

        public void ClearRecovery()
        {
            RecoveryNeeded = false;
            RecoveryPieces = null;
            if (File.Exists(_recoveryFilePath))
                File.Delete(_recoveryFilePath);
        }

        private class RecoveryDto
        {
            public bool RecoveryNeeded { get; set; }
            public Dictionary<string, MainWindow.PieceInit> Pieces { get; set; }
        }
    }
}
