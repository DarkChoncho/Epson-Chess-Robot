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
    public class RecoveryHandler
    {
        private readonly string _recoveryFilePath;

        public bool RecoveryNeeded { get; private set; }
        public Dictionary<string, MainWindow.PieceInit>? RecoveryPieces { get; private set; }

        public RecoveryHandler(string executableDirectory)
        {
            _recoveryFilePath = Path.Combine(executableDirectory, "recovery.json");
            LoadRecovery();
        }

        public void SaveRecovery(Dictionary<string, MainWindow.PieceInit> pieces, bool needed)
        {
            RecoveryPieces = pieces;
            RecoveryNeeded = needed;

            var dto = new RecoveryDto
            {
                RecoveryNeeded = needed,
                Pieces = pieces.ToDictionary(kv => kv.Key, kv => PieceDto.From(kv.Value))
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

            var dto = JsonSerializer.Deserialize<RecoveryDto>(File.ReadAllText(_recoveryFilePath));
            if (dto is null)
            {
                RecoveryNeeded = false;
                RecoveryPieces = null;
                return;
            }

            RecoveryNeeded = dto.RecoveryNeeded;
            RecoveryPieces = dto.Pieces.ToDictionary(
                kv => kv.Key,
                kv => kv.Value.ToPieceInit());
        }

        public void ClearRecovery()
        {
            RecoveryNeeded = false;
            RecoveryPieces = null;
            try { if (File.Exists(_recoveryFilePath)) File.Delete(_recoveryFilePath); } catch { }
        }

        private sealed class RecoveryDto
        {
            public bool RecoveryNeeded { get; set; }
            public Dictionary<string, PieceDto> Pieces { get; set; } = new();
        }

        private sealed class PieceDto
        {
            public string Name { get; set; } = "";
            public int Row { get; set; }
            public int Col { get; set; }
            public int Z { get; set; }
            public bool Enabled { get; set; }
            public string? Tag { get; set; }

            public static PieceDto From(MainWindow.PieceInit p) => new()
            {
                Name = p.Name,
                Row = p.Row,
                Col = p.Col,
                Z = p.Z,
                Enabled = p.Enabled,
                Tag = p.Tag
            };

            public MainWindow.PieceInit ToPieceInit() => new()
            {
                Img = null,
                Name = Name,
                Row = Row,
                Col = Col,
                Z = Z,
                Enabled = Enabled,
                Tag = Tag
            };
        }
    }
}
