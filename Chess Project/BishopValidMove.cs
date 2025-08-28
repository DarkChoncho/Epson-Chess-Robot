using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Chess_Project
{
    /// <summary>
    /// Move validation helpers for bishops.
    /// Verifies that a proposed bishop move is along a diagonal and
    /// that all intermediate squares are empty (i.e., no collisions).
    /// <para>
    /// This class does <c>not</c> check whether the destination square
    /// holds a same-color piece. This is instead accomplished by
    /// ensuring all same-color pieces are kept enabled, prohibiting
    /// the user from selecting a grid square under their own pieces.
    /// </para>
    /// </summary>
    /// <remarks>✅ Updated on 8/23/2025</remarks>
    internal sealed class BishopValidMove
    {
        /// <summary>
        /// Validates bishop moves by virtually stepping square-by-square toward
        /// the destination and rejecting the move if any intermediate square is occupied.
        /// </summary>
        /// <param name="chessBoard">The UI chess board grid.</param>
        /// <param name="mainWindow">The MainWindow instance.</param>
        /// <remarks>✅ Updated on 8/23/2025</remarks>
        public class BishopValidation(Grid chessBoard, MainWindow mainWindow)
        {
            private readonly Grid _board = chessBoard;
            private readonly MainWindow _main = mainWindow;
            private readonly List<(int row, int col)> _path = [];

            /// <summary>
            /// Validates the proposed bishop move.
            /// </summary>
            /// <param name="startRow">The bishop's current row.</param>
            /// <param name="startCol">The bishop's current column.</param>
            /// <param name="endRow">The destination row.</param>
            /// <param name="endCol">The destination column.</param>
            /// <returns><see langword="true"/> if the move is diagonal and all intermediate squares are empty;
            /// otherwise, <see langword="false"/>.</returns>
            /// <remarks>✅ Updated on 8/23/2025</remarks>
            public bool ValidateMove(int startRow, int startCol, int endRow, int endCol)
            {
                // Bishop must move diagonally (|Δrow| == |Δcol|) and must actually move
                int dRow = Math.Abs(endRow - startRow);
                int dCol = Math.Abs(endCol - startCol);
                if (dRow == 0 || dRow != dCol)
                    return false;

                // Step direction for row/column
                int stepRow = (endRow > startRow) ? 1 : -1;
                int stepCol = (endCol > startCol) ? 1 : -1;

                // Refresh piece coordinates from the board
                _main.PiecePositions();
                var occupied = _main.ImageCoordinates;
                _path.Clear();

                // Walk squares between source and destination (exclusive)
                int r = startRow + stepRow;
                int c = startCol + stepCol;
                while (r != endRow && c != endCol)
                {
                    _path.Add((r, c));
                    r += stepRow;
                    c += stepCol;
                }

                // Collision if any intermediate square is occupied
                bool collision = _path.Any(p => occupied.Any(o => o.Item1 == p.row && o.Item2 == p.col));
                return !collision;
            }
        }
    }
}