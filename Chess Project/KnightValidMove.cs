using System;

namespace Chess_Project
{
    /// <summary>
    /// Move validation helpers for knights.
    /// Verifies that a proposed knight move is L-shaped.
    /// <para>
    /// This class does <c>not</c> check whether the destination square
    /// holds a same-color piece. This is instead accomplished by
    /// ensuring all same-color pieces are kept enabled, prohibiting
    /// the user from selecting a grid square under their own pieces.
    /// </para>
    /// </summary>
    /// <remarks>✅ Updated on 8/23/2025</remarks>
    internal sealed class KnightValidMove
    {
        /// <summary>
        /// Validates knight moves by calculating the proposed move's geometry
        /// and ensuring that it follows an L-shaped footprint (hoof lol).
        /// </summary>
        /// <remarks>✅ Updated on 8/23/2025</remarks>
        public class KnightValidation
        {
            /// <summary>
            /// Validates the proposed knight move.
            /// </summary>
            /// <param name="startRow">The knight's current row.</param>
            /// <param name="startCol">The knight's current column.</param>
            /// <param name="endRow">The destination row.</param>
            /// <param name="endCol">The destination column.</param>
            /// <returns><see langword="true"/> if the move is L-shaped and all intermediate squares
            /// are empty; otherwise, <see langword="false"/>.</returns>
            /// <remarks>✅ Updated on 8/23/2025</remarks>
            public bool ValidateMove(int startRow, int startCol, int endRow, int endCol)
            {
                // Knight must move L-shaped and must actually move
                int dRow = Math.Abs(endRow - startRow);
                int dCol = Math.Abs(endCol - startCol);
                if ((dRow == 1 && dCol == 2) || (dRow == 2 && dCol == 1))
                    return true;

                return false;
            }
        }
    }
}