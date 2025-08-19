using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Project
{
    /// <summary>
    /// This class converts moves listed in UCI format to PGN format for PGN game saving.
    /// </summary>

    public class UCItoPGNConverter
    {
        public static string Convert(string fen, string uciMove, bool kingCastle, bool queenCastle, bool enPassant, bool promoted, string promotedTo, string checkModifier)
        {
            State gameState = new(fen);

            // Convert UCI move string to EngineMove object
            EngineMove move = new(uciMove);

            // Convert move to PGN
            string pgnMove = GetPgnMove(move, gameState, kingCastle, queenCastle, enPassant, promoted, promotedTo, checkModifier);

            // Apply the move (could be redundant?)
            gameState.ApplyMove(move);

            return pgnMove;
        }



        // Converts move from UCI format to PGN format
        private static string GetPgnMove(EngineMove move, State state, bool kingCastle, bool queenCastle, bool enPassant, bool promoted, string promotedTo, string checkModifier)
        {
            Piece piece = state.Pieces.First(p => p.Position.Equals(move.OldPosition));   // Locates piece that moved
            bool isCapture = state.Pieces.Any(p => p.Position.Equals(move.NewPosition));   // Locates captured piece (if any)
            string notation = "";

            if (piece.Type == PieceType.Knight)
            {
                var sameColorKnights = state.Pieces
                    .Where(p => p.Type == PieceType.Knight && p.IsBlack == piece.IsBlack && !p.Position.Equals(move.OldPosition)).ToList();

                bool requiresDisambiguation = sameColorKnights.Any(knight =>
                    (Math.Abs(move.NewPosition.X - knight.Position.X) == 2 && Math.Abs(move.NewPosition.Y - knight.Position.Y) == 1) ||
                    (Math.Abs(move.NewPosition.X - knight.Position.X) == 1 && Math.Abs(move.NewPosition.Y - knight.Position.Y) == 2));

                notation += piece.ToString();

                if (requiresDisambiguation)
                {
                    bool sameFile = sameColorKnights.Any(knight => knight.Position.X == move.OldPosition.X);

                    if (sameFile)
                    {
                        notation += (move.OldPosition.Y + 1);  // Use rank (row) for disambiguation
                    }
                    else
                    {
                        notation += (char)('a' + move.OldPosition.X);  // Use file (column) for disambiguation
                    }
                }

                if (isCapture)
                {
                    notation += "x";
                }
            }
            else if (piece.Type == PieceType.Bishop)
            {
                var sameColorBishops = state.Pieces
                    .Where(p => p.Type == PieceType.Bishop && p.IsBlack == piece.IsBlack && !p.Position.Equals(move.OldPosition)).ToList();

                bool requiresDisambiguation = sameColorBishops.Any(bishop =>
                    Math.Abs(move.NewPosition.X - bishop.Position.X) == Math.Abs(move.NewPosition.Y - bishop.Position.Y) &&
                    IsPathClear(bishop.Position, move.NewPosition, state));

                notation += piece.ToString();

                if (requiresDisambiguation)
                {
                    bool sameFile = sameColorBishops.Any(bishop => bishop.Position.X == move.OldPosition.X);

                    if (sameFile)
                    {
                        notation += (move.OldPosition.Y + 1);  // Use rank (row) for disambiguation
                    }
                    else
                    {
                        notation += (char)('a' + move.OldPosition.X);  // Use file (column) for disambiguation
                    }
                }

                if (isCapture)
                {
                    notation += "x";
                }
            }
            else if (piece.Type == PieceType.Rook)
            {
                var sameColorRooks = state.Pieces
                    .Where(p => p.Type == PieceType.Rook && p.IsBlack == piece.IsBlack && !p.Position.Equals(move.OldPosition)).ToList();

                bool requiresDisambiguation = sameColorRooks.Any(rook =>
                    (move.NewPosition.X == rook.Position.X || move.NewPosition.Y == rook.Position.Y) &&
                    IsPathClear(rook.Position, move.NewPosition, state));

                notation += piece.ToString();

                if (requiresDisambiguation)
                {
                    bool sameFile = sameColorRooks.Any(rook => rook.Position.X == move.OldPosition.X);

                    if (sameFile)
                    {
                        notation += (move.OldPosition.Y + 1);  // Use rank (row) for disambiguation
                    }
                    else
                    {
                        notation += (char)('a' + move.OldPosition.X);  // Use file (column) for disambiguation
                    }
                }

                if (isCapture)
                {
                    notation += "x";
                }
            }
            else if (piece.Type == PieceType.Queen)
            {
                var sameColorQueens = state.Pieces
                    .Where(p => p.Type == PieceType.Queen && p.IsBlack == piece.IsBlack && !p.Position.Equals(move.OldPosition)).ToList();

                bool requiresDisambiguation = sameColorQueens.Any(queen =>
                    (move.NewPosition.X == queen.Position.X || move.NewPosition.Y == queen.Position.Y ||  // Rook-like move
                    (Math.Abs(move.NewPosition.X - queen.Position.X) == Math.Abs(move.NewPosition.Y - queen.Position.Y))) && // Bishop-like move
                    IsPathClear(queen.Position, move.NewPosition, state));

                notation += piece.ToString();

                if (requiresDisambiguation)
                {
                    bool sameFile = sameColorQueens.Any(queen => queen.Position.X == move.OldPosition.X);

                    if (sameFile)
                    {
                        notation += (move.OldPosition.Y + 1);  // Use rank (row) for disambiguation
                    }
                    else
                    {
                        notation += (char)('a' + move.OldPosition.X);  // Use file (column) for disambiguation
                    }
                }

                if (isCapture)
                {
                    notation += "x";
                }
            }

            else if (piece.Type == PieceType.King)
            {
                notation += piece.ToString();

                if (isCapture)
                {
                    notation += "x";   // Add capture notation to move (Ex. Kxc3)
                }
            }

            else if (isCapture || enPassant)
            {
                notation += (char)('a' + move.OldPosition.X) + "x";   // Add capture notation to move (Ex. cxd5)
            }

            notation += (char)('a' + move.NewPosition.X);
            notation += (move.NewPosition.Y + 1);

            if (promoted)
            {          
                notation += "=" + promotedTo;   // Add promotion notation to move (Ex. e8=Q)
            }

            notation += checkModifier;   // Append check or checkmate symbol to move

            if (kingCastle)
            {
                notation = "O-O";
            }

            else if (queenCastle)
            {
                notation = "O-O-O";                
            }

            return notation;
        }



        /// <summary>
        /// Checks if there are any pieces blocking the path between two positions.
        /// Supports Bishops (diagonal), Rooks (horizontal/vertical), and Queens.
        /// </summary>
        private static bool IsPathClear(Position start, Position end, State state)
        {
            int dx = Math.Sign(end.X - start.X);
            int dy = Math.Sign(end.Y - start.Y);

            // Ensure the movement is valid for Bishop or Queen
            if ((dx != 0 && dy != 0) && Math.Abs(end.X - start.X) != Math.Abs(end.Y - start.Y))
            {
                return false; // Not a valid diagonal move for a Bishop or Queen
            }

            if ((dx == 0 && dy == 0) || (dx != 0 && dy != 0 && Math.Abs(end.X - start.X) != Math.Abs(end.Y - start.Y)))
            {
                return false; // Not a valid move for a sliding piece
            }

            int x = start.X + dx;
            int y = start.Y + dy;

            while (x != end.X || y != end.Y)
            {
                if (state.Pieces.Any(p => p.Position.X == x && p.Position.Y == y))
                    return false; // A piece is blocking the way

                x += dx;
                y += dy;
            }

            return true; // Path is clear
        }



        // Assigns chess piece positions
        private class Piece
        {
            public PieceType Type { get; }
            public Position Position { get; set; }
            public bool IsBlack { get; }

            public Piece(PieceType type, Position position, bool isBlack)
            {
                Type = type;
                Position = position;
                IsBlack = isBlack;
            }

            public override string ToString()
            {
                return Type switch
                {
                    PieceType.King => "K",
                    PieceType.Queen => "Q",
                    PieceType.Rook => "R",
                    PieceType.Bishop => "B",
                    PieceType.Knight => "N",
                    _ => "",
                };
            }
        }



        private enum PieceType { King, Queen, Rook, Bishop, Knight, Pawn }



        // Translates from UCI to PGN format
        private class EngineMove
        {
            public Position OldPosition { get; }
            public Position NewPosition { get; }
            public bool Capture { get; }

            public EngineMove(string uci)
            {
                if (uci.Length < 4)
                {
                    throw new ArgumentException($"Invalid UCI move: {uci} (must be at least 4 characters)");
                }

                System.Diagnostics.Debug.Write($"Received UCI Move: {uci}");

                // Extract characters
                char fromFile = uci[0];
                char fromRank = uci[1];
                char toFile = uci[2];
                char toRank = uci[3];

                System.Diagnostics.Debug.Write($"From: {fromFile}{fromRank}, To: {toFile}{toRank}");

                // Convert UCI to numeric coordinates
                int fromX = fromFile - 'a';
                int fromY = fromRank - '1';
                int toX = toFile - 'a';
                int toY = toRank - '1';

                System.Diagnostics.Debug.Write($"Parsed Numeric Positions: From=({fromX},{fromY}), To=({toX},{toY})");

                // Assign positions
                OldPosition = new Position(fromX, fromY);
                NewPosition = new Position(toX, toY);

                System.Diagnostics.Debug.Write($"Stored Positions: OldPosition={OldPosition}, NewPosition={NewPosition}");

                Capture = false;
            }

            public override string ToString()
            {
                return $"Move: {OldPosition} -> {NewPosition}";
            }
        }

        // Chessboard position
        private class Position
        {
            public int X { get; }
            public int Y { get; }

            public Position(int x, int y)
            {
                X = x;
                Y = y;
            }

            public override bool Equals(object obj)
            {
                if (obj is Position pos)
                {
                    return pos.X == X && pos.Y == Y;
                }
                return false;
            }

            public override int GetHashCode()
            {
                return HashCode.Combine(X, Y);
            }

            public override string ToString()
            {
                return $"({X},{Y})";
            }
        }

        // Board state representation, including FEN parsing and move application
        private class State
        {
            public List<Piece> Pieces { get; private set; }

            public State(string fen)
            {
                Pieces = ParseFEN(fen);
            }

            public void ApplyMove(EngineMove move)
            {

                System.Diagnostics.Debug.Write($"Applying Move: {move}");

                // Find the piece at OldPosition
                Piece piece = Pieces.FirstOrDefault(p => p.Position.Equals(move.OldPosition));

                if (piece == null)
                {
                    System.Diagnostics.Debug.Write($"ERROR: No piece found at {move.OldPosition}");

                    System.Diagnostics.Debug.Write("All Pieces on Board:");
                    foreach (var p in Pieces)
                    {
                        System.Diagnostics.Debug.Write($"{p.Type} at {p.Position}");
                    }
                    return;
                }

                System.Diagnostics.Debug.Write($"Moving {piece.Type} from {piece.Position} to {move.NewPosition}");
                piece.Position = move.NewPosition;
            }

            private List<Piece> ParseFEN(string fen)
            {
                List<Piece> pieces = new();
                string[] ranks = fen.Split(' ')[0].Split('/');

                for (int y = 0; y < 8; y++)
                {
                    int x = 0;
                    foreach (char c in ranks[7 - y])
                    {
                        if (char.IsDigit(c))
                        {
                            x += c - '0'; // Empty squares
                        }
                        else
                        {
                            bool isBlack = char.IsLower(c);
                            PieceType type = c.ToString().ToUpper() switch
                            {
                                "K" => PieceType.King,
                                "Q" => PieceType.Queen,
                                "R" => PieceType.Rook,
                                "B" => PieceType.Bishop,
                                "N" => PieceType.Knight,
                                _ => PieceType.Pawn
                            };
                            pieces.Add(new Piece(type, new Position(x, y), isBlack));
                            x++;
                        }
                    }
                }

                return pieces;
            }

            // ✅ Override ToString() for debugging
            public override string ToString()
            {
                string boardState = "Board State:\n";
                foreach (var piece in Pieces)
                {
                    boardState += $"{piece.Type} at {piece.Position}\n";
                }
                return boardState;
            }
        }
    }
}
