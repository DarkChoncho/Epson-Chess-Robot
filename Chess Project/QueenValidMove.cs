using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Chess_Project
{
    /// <summary>
    /// This class handles logic for queen moves and ensures that the move is legal.
    /// The method used to verify this is by virtually stepping the proposed piece over each square prior to its destination.
    /// If any of those squares have a piece already on it, the move is rejected due to a collision.
    /// 
    /// In addition, a queens's move must meet the following criteria:
    /// - It must not change file if moving vertically
    /// - It must not change rank if moving horizontally
    /// - The amount of files traveled must be equivalent to the amount of ranks traveled if moving diagonally
    /// - A piece of the same color must not occupy the destination square
    /// </summary>

    internal class QueenValidMove
    {
        public class QueenValidation
        {
            private readonly Grid Chess_Board;
            private readonly MainWindow mainWindow;
            private readonly List<Tuple<int, int>> motionCoordinates = new();

            public QueenValidation(Grid Chess_Board, MainWindow mainWindow)
            {
                this.Chess_Board = Chess_Board;
                this.mainWindow = mainWindow;
            }

            public bool ValidateMove(int oldRow, int oldColumn, int newRow, int newColumn)
            {
                int rowDiff = Math.Abs(newRow - oldRow);   // Calculates row difference for proposed move
                int columnDiff = Math.Abs(newColumn - oldColumn);   // Calculates column difference for proposed move
                int rowDirection = (newRow > oldRow) ? 1 : -1;   // Calculates which direction queen is moving in with respect to its row
                int columnDirection = (newColumn > oldColumn) ? 1 : -1;   // Calculates which direction queen is moving in with respect to its column
                int currentRow = oldRow + rowDirection;
                int currentColumn = oldColumn + columnDirection;

                mainWindow.PiecePositions();
                List<Tuple<int, int>> coordinates = mainWindow.ImageCoordinates;
                motionCoordinates.Clear();

                if ((oldRow == newRow || oldColumn == newColumn) || (rowDiff == columnDiff))   // If queen is moving like a bishop or a rook
                {
                    if (rowDiff == columnDiff)   // If queen is moving diagonally, like a bishop
                    {
                        while ((currentRow != newRow) && (currentColumn != newColumn))   // While current position being tested is not new intended position
                        {
                            motionCoordinates.Add(Tuple.Create(currentRow, currentColumn));

                            currentRow += rowDirection;
                            currentColumn += columnDirection;
                        }

                        bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                        if (collision)   // If queen passes through any pieces
                        {
                            return false;
                        }

                        else
                        {
                            return true;
                        }
                    }

                    else if (oldRow == newRow)   // If queen is moving horizontally, like a rook
                    {
                        while (currentColumn != newColumn)   // While current position being tested is not new intended position
                        {
                            motionCoordinates.Add(Tuple.Create(newRow, currentColumn));

                            currentColumn += columnDirection;
                        }

                        bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                        if (collision)   // If queen passes through any pieces
                        {
                            return false;
                        }

                        else
                        {
                            return true;
                        }
                    }

                    else   // If queen is moving vertically, like a rook
                    {
                        while (currentRow != newRow)   // While current position being tested is not new intended position
                        {
                            motionCoordinates.Add(Tuple.Create(currentRow, newColumn));

                            currentRow += rowDirection;
                        }

                        bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                        if (collision)   // If queen passes through any pieces
                        {
                            return false;
                        }

                        else
                        {
                            return true;
                        }
                    }
                }

                else   // If queen is not moving like a bishop or a rook
                {
                    return false;
                }
            }
        }
    }
}