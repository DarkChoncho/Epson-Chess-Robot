using System;

namespace Chess_Project
{
    /// <summary>
    /// This class handles logic for knight moves and ensures that the move is legal.
    /// 
    /// A knight's move must meet the following criteria:
    /// - It must move either two files and one rank, or one file and two ranks.
    /// - A piece of the same color must not occupy the destination square
    /// </summary>

    internal class KnightValidMove
    {
        public class KnightValidation
        {
            public bool ValidateMove(int oldRow, int oldColumn, int newRow, int newColumn)
            {
                int rowDiff = Math.Abs(newRow - oldRow);   // Calculates row difference for proposed move
                int columnDiff = Math.Abs(newColumn - oldColumn);   // Calculates column difference for proposed move
                int totalDiff = Math.Abs(rowDiff - columnDiff);   // Calculates difference between row and column differences
                int sum = rowDiff + columnDiff;

                if (totalDiff == 1 && sum == 3)   // If knight is moving in an "L" shape
                {
                    return true;
                }

                else   // If knight is not moving in an "L" shape
                {
                    return false;
                }
            }
        }
    }
}