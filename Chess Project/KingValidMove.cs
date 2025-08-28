using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Chess_Project
{
    /// <summary>
    /// Move validation helpers for kings.
    /// Verifies that a proposed king move is one square unless castling.
    /// <para>
    /// This class does <c>not</c> check whether the destination square
    /// holds a same-color piece. This is instead accomplished by
    /// ensuring all same-color pieces are kept enabled, prohibiting
    /// the user from selecting a grid square under their own pieces.
    /// </para>
    /// </summary>
    /// <remarks>✅ Updated on 8/25/2025</remarks>
    internal sealed class KingValidMove
    {
        /// <summary>
        /// Validates king moves by ensuring the move is only one square away unless castling.
        /// </summary>
        /// <param name="chessBoard">The UI chess board grid.</param>
        /// <param name="mainWindow">The MainWindow instance.</param>
        /// <remarks>✅ Updated on 8/25/2025</remarks>
        public class KingValidation(Grid chessBoard, MainWindow mainWindow)
        {
            private readonly Grid _board = chessBoard;
            private readonly MainWindow _main = mainWindow;
            private readonly List<(int row, int col)> _path = [];

            /// <summary>
            /// Validates the proposed king move.
            /// </summary>
            /// <param name="startRow">The king's current row.</param>
            /// <param name="startCol">The king's current column.</param>
            /// <param name="endRow">The destination row.</param>
            /// <param name="endCol">The destination column.</param>
            /// <param name="move">The moving color. <c>1</c> for white, <c>0</c> for black.</param>
            /// <param name="wkCastle">White king castling flag.</param>
            /// <param name="bkCastle">Black king castling flag.</param>
            /// <param name="wr1Castle">White rook 1 castling flag.</param>
            /// <param name="wr2Castle">White rook 2 castling flag.</param>
            /// <param name="br1Castle">Black rook 1 castling flag.</param>
            /// <param name="br2Castle">Black rook 2 castling flag.</param>
            /// <returns><see langword="true"/> if the move is one square away;
            /// otherwise, <see langword="false"/>.</returns>
            /// <remarks>✅ Updated on 8/25/2025</remarks>
            public bool ValidateMove(int startRow, int startCol, int endRow, int endCol, int move, int wkCastle, int bkCastle, int wr1Castle, int wr2Castle, int br1Castle, int br2Castle)
            {
                // King must move no more than one square at a time unless castling and must actually move
                int dRow = Math.Abs(endRow - startRow);
                int dCol = Math.Abs(endCol - startCol);
                if ((dRow == 0 && dCol == 0) || (dRow > 1) || (dCol > 2))
                    return false;

                // Step direction for row/column
                int stepRow = dRow == 0 ? 0 : (endRow > startRow ? 1 : -1);
                int stepCol = dCol == 0 ? 0 : (endCol > startCol ? 1 : -1);

                // Refresh piece coordinates from the board
                _main.PiecePositions();
                var occupied = _main.ImageCoordinates;
                bool kingCanCastle = move == 1 ? wkCastle == 0 : bkCastle == 0;
                bool rook1CanCastle = move == 1 ? _board.Children.OfType<Image>().Any(image => image.Name == "WhiteRook1") && wr1Castle == 0 : _board.Children.OfType<Image>().Any(image => image.Name == "BlackRook1") && br1Castle == 0;
                bool rook2CanCastle = move == 1 ? _board.Children.OfType<Image>().Any(image => image.Name == "WhiteRook2") && wr2Castle == 0 : _board.Children.OfType<Image>().Any(image => image.Name == "BlackRook2") && br2Castle == 0;
                _path.Clear();

                // Walk squares between source and destination (exclusive)
                int r = startRow;
                int c = startCol;

                if (dCol > 1)  // King is castling
                {
                    if (dRow != 0 || (endCol != 2 && endCol != 6))
                        return false;

                    if (endCol == 6 && (!kingCanCastle || !rook2CanCastle))  // Kingside
                        return false;
                    else if (endCol == 2 && (!kingCanCastle || !rook1CanCastle))  // Queenside
                        return false;

                    while (r != endRow || c != endCol)
                    {
                        CheckVerification checkVerification = new(_board, _main);
                        bool outOfCheck = checkVerification.ValidatePosition(r, c, r, c, 9, 9, move);
                        if (!outOfCheck)
                            return false;

                        _path.Add((r, c));
                        r += stepRow;
                        c += stepCol;
                    }

                    // Add the b1 square for queenside castling
                    if (endCol == 2)
                        _path.Add((r, c - 1));

                    bool collision = _path.Any(p => occupied.Any(o => o.Item1 == p.row && o.Item2 == p.col));
                    return !collision;
                }
                else
                {
                    if (dCol > 1)
                        return false;

                    return true;
                }
            }
        }
    }
}