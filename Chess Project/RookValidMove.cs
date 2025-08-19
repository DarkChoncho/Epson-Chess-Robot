using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Chess_Project
{
    /// <summary>
    /// This class handles logic for rook moves and ensures that the move is legal.
    /// The method used to verify this is by virtually stepping the proposed piece over each square prior to its destination.
    /// If any of those squares have a piece already on it, the move is rejected due to a collision.
    /// 
    /// In addition, a rook's move must meet the following criteria:
    /// - It must not change file if moving vertically
    /// - It must not change rank if moving horizontally 
    /// - A piece of the same color must not occupy the destination square
    /// </summary>

    internal class RookValidMove
    {
        public class RookValidation
        {
            private readonly Grid Chess_Board;
            private readonly MainWindow mainWindow;
            private readonly List<Tuple<int, int>> motionCoordinates = new();

            public RookValidation(Grid Chess_Board, MainWindow mainWindow)
            {
                this.Chess_Board = Chess_Board;
                this.mainWindow = mainWindow;
            }

            public bool ValidateMove(int oldRow, int oldColumn, int newRow, int newColumn)
            {
                int rowDirection = (newRow > oldRow) ? 1 : -1;   // Calculates which direction rook is moving in with respect to its row
                int columnDirection = (newColumn > oldColumn) ? 1 : -1;   // Calculates which direction rook is moving in with respect to its column
                int currentRow = oldRow + rowDirection;
                int currentColumn = oldColumn + columnDirection;

                mainWindow.PiecePositions();
                List<Tuple<int, int>> coordinates = mainWindow.ImageCoordinates;
                motionCoordinates.Clear();

                if ((oldRow == newRow) || (oldColumn == newColumn))   // If rook is moving horizontally or vertically
                {
                    if (oldRow == newRow)   // If rook is moving horizontally
                    {
                        while (currentColumn != newColumn)   // While current position being tested is not new intended position
                        {
                            motionCoordinates.Add(Tuple.Create(newRow, currentColumn));

                            currentColumn += columnDirection;
                        }

                        bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                        if (collision)   // If rook passes through any pieces
                        {
                            return false;
                        }

                        else
                        {
                            return true;
                        }
                    }

                    else   // If rook is moving vertically
                    {
                        while (currentRow != newRow)   // While current position being tested is not new intended position
                        {
                            motionCoordinates.Add(Tuple.Create(currentRow, newColumn));

                            currentRow += rowDirection;
                        }

                        bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                        if (collision)   // If rook passes through any pieces
                        {
                            return false;
                        }

                        else
                        {
                            return true;
                        }
                    }
                }

                else   // If rook is not moving horizontally or vertically
                {
                    return false;
                }
            }
        }
    }
}