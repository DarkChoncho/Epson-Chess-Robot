using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Chess_Project
{
    /// <summary>
    /// Move validation helpers for pawns.
    /// Verifies that a proposed pawn move is either along a
    /// file or is diagonally forward one square when capturing.
    /// <para>
    /// This class does <c>not</c> check whether the destination square
    /// holds a same-color piece. This is instead accomplished by
    /// ensuring all same-color pieces are kept enabled, prohibiting
    /// the user from selecting a grid square under their own pieces.
    /// </para>
    /// </summary>
    /// <remarks>✅ Updated on 8/23/2025</remarks>
    internal sealed class PawnValidMove
    {
        /// <summary>
        /// Validates pawn moves by virtually stepping square-by-square toward
        /// the destination square and rejecting the move if the intermediate square is occupied.
        /// </summary>
        /// <param name="chessBoard">The UI chess board grid.</param>
        /// <param name="mainWindow">The MainWindow instance.</param>
        /// <remarks>✅ Updated on 8/23/2025</remarks>
        public class PawnValidation(Grid chessBoard, MainWindow mainWindow)
        {
            private readonly Grid _board = chessBoard;
            private readonly MainWindow _main = mainWindow;
            private readonly List<(int row, int col)> _path = [];

            /// <summary>
            /// Validates the proposed pawn move.
            /// </summary>
            /// <param name="startRow">The pawn's current row.</param>
            /// <param name="startCol">The pawn's current column.</param>
            /// <param name="endRow">The destination row.</param>
            /// <param name="endCol">The destination column.</param>
            /// <param name="move">The moving color. <c>1</c> for white, <c>0</c> for black.</param>
            /// <returns><see langword="true"/> if the move is along a file or diagonally forward
            /// one square when capturing and all intermediate squares are empty; otherwise <see langword="false"/>.</returns>
            /// <remarks>✅ Updated on 8/23/2025</remarks>
            public bool ValidateMove(int startRow, int startCol, int endRow, int endCol, int move)
            {
                // Pawn must move forward by no more than two squares or move diagonally forward one square and actually move
                int dRow = Math.Abs(endRow - startRow);
                int dCol = Math.Abs(endCol - startCol);
                if ((dRow == 0) || (dRow == 2 && dCol != 0) || (dRow > 2) || (dCol > 1))
                    return false;

                // Step direction for row/column
                int stepRow = (endRow > startRow ? 1 : -1);
                int stepCol = dCol == 0 ? 0 : (endCol > startCol ? 1 : -1);

                // Refresh piece coordinates from the board
                _main.PiecePositions();
                var occupied = _main.ImageCoordinates;
                var enPassant = _main.EnPassantSquare;
                _path.Clear();

                // Walk squares between source and destination (exclusive)
                int r = startRow + stepRow;
                int c = startCol + stepCol;

                if (dRow == 2)  // Pawn two-square advance
                {
                    bool firstMove = move == 1 ? startRow == 6 : startRow == 1;
                    if (!firstMove)
                        return false;

                    while (r != endRow || c != endCol)
                    {
                        _path.Add((r, c));
                        r += stepRow;
                        c += stepCol;
                    }

                    // Collision if the intermediate square is occupied
                    bool collision = _path.Any(p => occupied.Any(o => o.Item1 == p.row && o.Item2 == p.col));
                    return !collision;
                }
                else
                {
                    bool forward = move == 1 ? (startRow - endRow == 1) : (startRow - endRow == -1);
                    if (!forward)
                        return false;

                    _path.Add((r, c));

                    if (dCol == 0)  // Pawn one-square advance
                    {
                        // Collision if the destination square is occupied
                        bool collision = _path.Any(p => occupied.Any(o => o.Item1 == p.row && o.Item2 == p.col));
                        return !collision;
                    }
                    else  // Capture
                    {
                        // Collision if the destination square is occupied
                        bool collision = _path.Any(p => occupied.Any(o => o.Item1 == p.row && o.Item2 == p.col)) || _path.Any(p => enPassant.Any(o => o.Item1 == p.row && o.Item2 == p.col));
                        return collision;
                    }
                }
            }
        }
    }
}