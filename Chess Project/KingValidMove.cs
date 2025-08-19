using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Chess_Project
{
    /// <summary>
    /// This class handles logic for king moves and ensures that the move is legal.
    /// 
    /// A king's move must meet the following criteria:
    /// - The destination cannot be adjacent to the opposing king in any direction
    /// - It cannot move more than one square in any direction unless castling
    /// - A piece of the same color must not occupy the destination square
    /// </summary>

    internal class KingValidMove
    {
        public class KingValidation
        {
            private readonly Grid Chess_Board;
            private readonly MainWindow mainWindow;
            private readonly List<Tuple<int, int>> motionCoordinates = new();

            public KingValidation(Grid Chess_Board, MainWindow mainWindow)
            {
                this.Chess_Board = Chess_Board;
                this.mainWindow = mainWindow;
            }

            public bool ValidateMove(int Move, int oldRow, int oldColumn, int newRow, int newColumn, int WK, int BK, int WR1, int WR2, int BR1, int BR2)
            {
                int rowDiff = newRow - oldRow;   // Calculates row difference for proposed move
                int columnDiff = newColumn - oldColumn;   // Calculates column difference for proposed move
                int currentColumn;
                currentColumn = oldColumn;

                mainWindow.PiecePositions();
                List<Tuple<int, int>> coordinates = mainWindow.ImageCoordinates;
                motionCoordinates.Clear();

                if (Math.Abs(rowDiff) < 2 && Math.Abs(columnDiff) < 2)
                {
                    return true;
                }

                if (Move == 1 && Math.Abs(columnDiff) == 2 && rowDiff == 0)   // If white is trying to castle
                {
                    bool whiteRook1Exists = Chess_Board.Children.OfType<Image>().Any(image => image.Name == "WhiteRook1");
                    bool whiteRook2Exists = Chess_Board.Children.OfType<Image>().Any(image => image.Name == "WhiteRook2");

                    if (columnDiff == -2 && whiteRook1Exists)   // If white is trying to castle queenside
                    {
                        currentColumn -= 1;

                        while (currentColumn != 0)
                        {
                            motionCoordinates.Add(Tuple.Create(oldRow, currentColumn));

                            currentColumn -= 1;
                        }

                        bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                        if (!collision)
                        {
                            if (WK == 0 && WR1 == 0)
                            {
                                CheckVerification.Check checkValidator1 = new(Chess_Board, mainWindow);
                                bool outOfCheck1 = checkValidator1.ValidatePosition(Move, oldRow, oldColumn, oldRow, oldColumn, 9, 9);

                                if (outOfCheck1)
                                {
                                    CheckVerification.Check checkValidator2 = new(Chess_Board, mainWindow);
                                    bool outOfCheck2 = checkValidator2.ValidatePosition(Move, oldRow, oldColumn - 1, oldRow, oldColumn - 1, 9, 9);

                                    if (outOfCheck2)
                                    {
                                        CheckVerification.Check checkValidator3 = new(Chess_Board, mainWindow);
                                        bool outOfCheck3 = checkValidator3.ValidatePosition(Move, oldRow, newColumn, oldRow, newColumn, 9, 9);

                                        if (outOfCheck3)
                                        {
                                            return true;
                                        }

                                        else
                                        {
                                            return false;
                                        }
                                    }

                                    else
                                    {
                                        return false;
                                    }
                                }

                                else
                                {
                                    return false;
                                }
                            }

                            else
                            {
                                return false;
                            }
                        }

                        else
                        {
                            return false;
                        }
                    }

                    else if (columnDiff == 2 && whiteRook2Exists)   // If white is trying to castle kingside
                    {
                        currentColumn += 1;

                        while (currentColumn != 7)
                        {
                            motionCoordinates.Add(Tuple.Create(oldRow, currentColumn));

                            currentColumn += 1;
                        }

                        bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                        if (!collision)
                        {
                            if (WK == 0 && WR2 == 0)
                            {
                                CheckVerification.Check checkValidator1 = new(Chess_Board, mainWindow);
                                bool outOfCheck1 = checkValidator1.ValidatePosition(Move, oldRow, oldColumn, oldRow, oldColumn, 9, 9);

                                if (outOfCheck1)
                                {
                                    CheckVerification.Check checkValidator2 = new(Chess_Board, mainWindow);
                                    bool outOfCheck2 = checkValidator2.ValidatePosition(Move, oldRow, oldColumn + 1, oldRow, oldColumn + 1, 9, 9);

                                    if (outOfCheck2)
                                    {
                                        CheckVerification.Check checkValidator3 = new(Chess_Board, mainWindow);
                                        bool outOfCheck3 = checkValidator3.ValidatePosition(Move, oldRow, newColumn, oldRow, newColumn, 9, 9);

                                        if (outOfCheck3)
                                        {
                                            return true;
                                        }

                                        else
                                        {
                                            return false;
                                        }
                                    }

                                    else
                                    {
                                        return false;
                                    }
                                }

                                else
                                {
                                    return false;
                                }
                            }

                            else
                            {
                                return false;
                            }
                        }

                        else
                        {
                            return false;
                        }
                    }

                    else
                    {
                        return false;
                    }

                }

                if (Move == 0 && Math.Abs(columnDiff) == 2 && rowDiff == 0)   // If black is trying to castle
                {
                    bool blackRook1Exists = Chess_Board.Children.OfType<Image>().Any(image => image.Name == "BlackRook1");
                    bool blackRook2Exists = Chess_Board.Children.OfType<Image>().Any(image => image.Name == "BlackRook2");

                    if (columnDiff == -2 && blackRook1Exists)   // If black is trying to castle queenside
                    {
                        currentColumn -= 1;

                        while (currentColumn != 0)
                        {
                            motionCoordinates.Add(Tuple.Create(oldRow, currentColumn));

                            currentColumn -= 1;
                        }

                        bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                        if (!collision)
                        {
                            if (BK == 0 && BR1 == 0)
                            {
                                CheckVerification.Check checkValidator1 = new(Chess_Board, mainWindow);
                                bool outOfCheck1 = checkValidator1.ValidatePosition(Move, oldRow, oldColumn, oldRow, oldColumn, 9, 9);

                                if (outOfCheck1)
                                {
                                    CheckVerification.Check checkValidator2 = new(Chess_Board, mainWindow);
                                    bool outOfCheck2 = checkValidator2.ValidatePosition(Move, oldRow, oldColumn - 1, oldRow, oldColumn - 1, 9, 9);

                                    if (outOfCheck2)
                                    {
                                        CheckVerification.Check checkValidator3 = new(Chess_Board, mainWindow);
                                        bool outOfCheck3 = checkValidator3.ValidatePosition(Move, oldRow, newColumn, oldRow, newColumn, 9, 9);

                                        if (outOfCheck3)
                                        {
                                            return true;
                                        }

                                        else
                                        {
                                            return false;
                                        }
                                    }

                                    else
                                    {
                                        return false;
                                    }
                                }

                                else
                                {
                                    return false;
                                }
                            }

                            else
                            {
                                return false;
                            }
                        }

                        else
                        {
                            return false;
                        }

                    }

                    else if (columnDiff == 2 && blackRook2Exists)   // If black is trying to castle kingside
                    {
                        currentColumn += 1;

                        while (currentColumn != 7)
                        {
                            motionCoordinates.Add(Tuple.Create(oldRow, currentColumn));

                            currentColumn += 1;
                        }

                        bool collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                        if (!collision)
                        {
                            if (BK == 0 && BR2 == 0)
                            {
                                CheckVerification.Check checkValidator1 = new(Chess_Board, mainWindow);
                                bool outOfCheck1 = checkValidator1.ValidatePosition(Move, oldRow, oldColumn, oldRow, oldColumn, 9, 9);

                                if (outOfCheck1)
                                {
                                    CheckVerification.Check checkValidator2 = new(Chess_Board, mainWindow);
                                    bool outOfCheck2 = checkValidator2.ValidatePosition(Move, oldRow, oldColumn + 1, oldRow, oldColumn + 1, 9, 9);

                                    if (outOfCheck2)
                                    {
                                        CheckVerification.Check checkValidator3 = new(Chess_Board, mainWindow);
                                        bool outOfCheck3 = checkValidator3.ValidatePosition(Move, oldRow, newColumn, oldRow, newColumn, 9, 9);

                                        if (outOfCheck3)
                                        {
                                            return true;
                                        }

                                        else
                                        {
                                            return false;
                                        }
                                    }

                                    else
                                    {
                                        return false;
                                    }
                                }

                                else
                                {
                                    return false;
                                }
                            }

                            else
                            {
                                return false;
                            }
                        }

                        else
                        {
                            return false;
                        }
                    }

                    else
                    {
                        return false;
                    }

                }

                else
                {
                    return false;
                }
            }
        }
    }
}