using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Chess_Project
{
    /// <summary>
    /// This class handles logic for pawn moves and ensures that the move is legal.
    /// 
    /// A pawn's move must meet the following criteria:
    /// - It cannot move backwards
    /// - It cannot move more than two ranks on its first turn, and then only one rank on subsequent turns
    /// - It cannot change files unless there is a capture
    /// - A piece of the same color must not occupy the destination square
    /// </summary>

    internal class PawnValidMove
    {
        public class PawnValidation
        {
            private readonly Grid Chess_Board;
            private readonly MainWindow mainWindow;
            private readonly List<Tuple<int, int>> motionCoordinates = new();

            public PawnValidation(Grid Chess_Board, MainWindow mainWindow)
            {
                this.Chess_Board = Chess_Board;
                this.mainWindow = mainWindow;
            }

            public bool ValidateMove(int oldRow, int oldColumn, int newRow, int newColumn, int Move)
            {
                int rowDiff = newRow - oldRow;   // Calculates row difference for proposed move
                int columnDiff = newColumn - oldColumn;   // Calculates column difference for proposed move
                int rowDirection = (newRow > oldRow) ? 1 : -1;   // Calculates which direction pawn is moving in with respect to its row
                int columnDirection = (newColumn > oldColumn) ? 1 : -1;   // Calculates which direction pawn is moving in with respect to its column
                int currentRow = oldRow + rowDirection;
                int currentColumn = oldColumn + columnDirection;

                mainWindow.PiecePositions();
                List<Tuple<int, int>> coordinates = mainWindow.ImageCoordinates;
                List<Tuple<int, int>> motionCoordinates = new();
                List<Tuple<int, int>> EnPassant = mainWindow.EnPassantSquare;
                motionCoordinates.Clear();

                if ((Move == 1 && rowDiff == -1) || (Move == 0 && rowDiff == 1) || (Move == 1 && rowDiff == -2 && oldRow == 6) || (Move == 0 && rowDiff == 2 && oldRow == 1))   // Legal pawn move combinations
                {
                    if (Move == 0)   // If black is moving
                    {
                        if (columnDiff == 0)   // If pawn is not moving horizontally
                        {
                            if (rowDiff == 1)   // If pawn is moving vertically one square
                            {
                                motionCoordinates.Add(Tuple.Create(newRow, oldColumn));

                                bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                                if (collision)
                                {
                                    return false;
                                }

                                else
                                {
                                    return true;
                                }
                            }

                            else if (rowDiff == 2)   // If pawn is moving vertically two squares on its first move
                            {
                                motionCoordinates.Add(Tuple.Create(currentRow, oldColumn));
                                motionCoordinates.Add(Tuple.Create(newRow, oldColumn));

                                bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                                if (collision)
                                {
                                    return false;
                                }

                                else
                                {
                                    return true;
                                }
                            }
                        }

                        if ((columnDiff == -1) || (columnDiff == 1))   // If pawn is capturing diagonally
                        {
                            if (rowDiff == 1)   // If pawn is moving vertically one square
                            {
                                motionCoordinates.Add(Tuple.Create(newRow, newColumn));

                                bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));
                                bool enPassant = motionCoordinates.Any(coord => EnPassant.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                                if (collision)
                                {
                                    return true;
                                }

                                if (enPassant)
                                {
                                    return true;
                                }

                                else
                                {
                                    return false;
                                }
                            }
                        }

                        else
                        {
                            return false;
                        }
                    }

                    else   // If white is moving
                    {
                        if (columnDiff == 0)   // If pawn is not moving horizontally
                        {
                            if (rowDiff == -1)   // If pawn is moving vertically one square
                            {
                                motionCoordinates.Add(Tuple.Create(newRow, oldColumn));

                                bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                                if (collision)
                                {
                                    return false;
                                }

                                else
                                {
                                    return true;
                                }
                            }

                            else if (rowDiff == -2)   // If pawn is moving vertically two squares on its first move
                            {
                                motionCoordinates.Add(Tuple.Create(currentRow, oldColumn));
                                motionCoordinates.Add(Tuple.Create(newRow, oldColumn));

                                bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                                if (collision)
                                {
                                    return false;
                                }

                                else
                                {
                                    return true;
                                }
                            }
                        }

                        if ((columnDiff == -1) || (columnDiff == 1))   // If pawn is capturing diagonally
                        {
                            if (rowDiff == -1)   // If pawn is moving vertically one square
                            {
                                motionCoordinates.Add(Tuple.Create(newRow, newColumn));

                                bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));
                                bool enPassant = motionCoordinates.Any(coord => EnPassant.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                                if (collision)
                                {
                                    return true;
                                }

                                if (enPassant)
                                {
                                    return true;
                                }

                                else
                                {
                                    return false;
                                }
                            }
                        }

                        else
                        {
                            return false;
                        }
                    }
                }

                return false;
            }
        }
    }
}