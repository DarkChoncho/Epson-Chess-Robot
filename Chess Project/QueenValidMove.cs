using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Chess_Project
{
    /// <summary>
    /// Move validation helpers for queens.
    /// Verifies that a proposed queen move is along a diagonal or
    /// axis and that all intermediate squares are empty (i.e., no collisions).
    /// <para>
    /// This class does <c>not</c> check whether the destination square
    /// holds a same-color piece. This is instead accomplished by
    /// ensuring all same-color pieces are kept enabled, prohibiting
    /// the user from selecting a grid square under their own pieces.
    /// </para>
    /// </summary>
    /// <remarks>✅ Updated on 8/23/2025</remarks>
    internal sealed class QueenValidMove
    {
        /// <summary>
        /// Validates queen moves by virtually stepping square-by-square toward
        /// the destination and rejecting the move if any intermediate square is occupied.
        /// </summary>
        /// <param name="chessBoard">The UI chess board grid.</param>
        /// <param name="mainWindow">The MainWindow instance.</param>
        /// <remarks>✅ Updated on 8/23/2025</remarks>
        public class QueenValidation(Grid chessBoard, MainWindow mainWindow)
        {
            private readonly Grid _board = chessBoard;
            private readonly MainWindow _main = mainWindow;
            private readonly List<(int row, int col)> _path = [];

            /// <summary>
            /// Validates the proposed queen move.
            /// </summary>
            /// <param name="startRow">The queen's current row.</param>
            /// <param name="startCol">The queen's current column.</param>
            /// <param name="endRow">The destination row.</param>
            /// <param name="endCol">The destination column.</param>
            /// <returns><see langword="true"/> if the move is diagonal or axial and all intermediate
            /// squares are empty; otherwise, <see langword="false"/>.</returns>
            /// <remarks>✅ Updated on 8/23/2025</remarks>
            public bool ValidateMove(int startRow, int startCol, int endRow, int endCol)
            {
                // Queen must move diagonally (|Δrow| == |Δcol|) or move along one axis and must actually move
                int dRow = Math.Abs(endRow - startRow);
                int dCol = Math.Abs(endCol - startCol);
                if ((dRow == 0 && dCol == 0) || (dRow > 0 && dCol > 0 && dRow != dCol))
                    return false;

                // Step direction for row/column
                int stepRow = dRow == 0 ? 0 : (endRow > startRow ? 1 : -1);
                int stepCol = dCol == 0 ? 0 : (endCol > startCol ? 1 : -1);

                // Refresh piece coordinates from the board
                _main.PiecePositions();
                var occupied = _main.ImageCoordinates;
                _path.Clear();

                // Walk squares between source and destination (exclusive)
                int r = startRow + stepRow;
                int c = startCol + stepCol;
                while (r != endRow || c != endCol)
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