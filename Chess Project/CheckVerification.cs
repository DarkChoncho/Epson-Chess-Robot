using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;

namespace Chess_Project
{
    /// <summary>
    /// This class is for verifying that the position after the proposed move will not leave the active color's
    /// king in check. This is done by assuming the proposed move is legal, and then stepping the king one square
    /// at a time in each of the eight directions: N, NE, E, SE, S, SW, W, and NW.
    /// 
    /// When stepping through all eight directions, one of the following criteria must be met:
    /// - The king collides with a piece of its same color
    /// - The king collides with an opposing rook or king when moving diagonally
    /// - The king collides with an opposing pawn anytime after its first step when moving diagonally
    /// - The king collides with an opposing pawn that has already passed the king when moving diagonally
    /// - The king collides with an opposing bishop or pawn when moving orthogonally
    /// - The king reaches the edge of the board
    /// 
    /// In additon, some other things that must be satisfied are:
    /// - The king must not be adjacent to the opposing king in any direction
    /// - The king must not be in range of any opposing knights.
    /// </summary>

    internal class CheckVerification
    {
        public class Check
        {
            private readonly Grid Chess_Board;
            private readonly MainWindow mainWindow;
            private readonly List<Tuple<int, int>> motionCoordinates = new();
            private readonly List<Tuple<int, int>> knightCoordinates = new();
            private bool collision;

            public Check(Grid Chess_Board, MainWindow mainWindow)
            {
                this.Chess_Board = Chess_Board;
                this.mainWindow = mainWindow;
            }

            public bool ValidatePosition(int Move, int wKingRow, int wKingColumn, int bKingRow, int bKingColumn, int newRow, int newColumn)
            {
                mainWindow.PiecePositions();
                List<Tuple<int, int>> coordinates = mainWindow.ImageCoordinates;
                motionCoordinates.Clear();
                int Step = 0;

                if (Move == 1)  // If white is moving
                {
                    int TestRow = wKingRow;
                    int TestColumn = wKingColumn;

                    if (wKingColumn != 0)  // If white king is not on the a-file, step toward it
                    {
                        while (TestColumn != 0)
                        {
                            TestColumn -= 1;
                            Step += 1;

                            if (wKingRow == newRow && TestColumn == newColumn)
                            {
                                TestColumn = 0; break;
                            }

                            motionCoordinates.Add(Tuple.Create(wKingRow, TestColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == wKingRow && checkCol == TestColumn)
                                    {
                                        if (image.Name.StartsWith("BlackRook") || image.Name.StartsWith("BlackQueen"))   // If white king collides with black rook or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("BlackKing") && Step == 1)   // If white king collides with black king on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestColumn = 0; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestColumn = wKingColumn;
                    Step = 0;

                    if (wKingColumn != 7)  // If white king is not on the h-file, step toward it
                    {
                        while (TestColumn != 7)
                        {
                            TestColumn += 1;
                            Step += 1;

                            if (wKingRow == newRow && TestColumn == newColumn)
                            {
                                TestColumn = 7; break;
                            }

                            motionCoordinates.Add(Tuple.Create(wKingRow, TestColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == wKingRow && checkCol == TestColumn)
                                    {
                                        if (image.Name.StartsWith("BlackRook") || image.Name.StartsWith("BlackQueen"))   // If white king collides with black rook or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("BlackKing") && Step == 1)   // If white king collides with black king on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestColumn = 7; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestColumn = wKingColumn;
                    Step = 0;

                    if (wKingRow != 0)  // If white king is not on the 8th rank, step toward it
                    {
                        while (TestRow != 0)
                        {
                            TestRow -= 1;
                            Step += 1;

                            if (TestRow == newRow && wKingColumn == newColumn)
                            {
                                TestRow = 0; break;
                            }

                            motionCoordinates.Add(Tuple.Create(TestRow, wKingColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == TestRow && checkCol == wKingColumn)
                                    {
                                        if (image.Name.StartsWith("BlackRook") || image.Name.StartsWith("BlackQueen"))   // If white king collides with black rook or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("BlackKing") && Step == 1)   // If white king collides with black king on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestRow = 0; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestRow = wKingRow;
                    Step = 0;

                    if (wKingRow != 7)  // If white king is not on the 1st rank, step toward it
                    {
                        while (TestRow != 7)
                        {
                            TestRow += 1;
                            Step += 1;

                            if (TestRow == newRow && wKingColumn == newColumn)
                            {
                                TestRow = 7; break;
                            }

                            motionCoordinates.Add(Tuple.Create(TestRow, wKingColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == TestRow && checkCol == wKingColumn)
                                    {
                                        if (image.Name.StartsWith("BlackRook") || image.Name.StartsWith("BlackQueen"))   // If white king collides with black rook or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("BlackKing") && Step == 1)   // If white king collides with black king on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestRow = 7; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestRow = wKingRow;
                    Step = 0;

                    if (wKingRow != 0 && wKingColumn != 0)  // If white king is not in either the 8th rank or the a-file, step toward it  
                    {
                        while (TestRow != 0 && TestColumn != 0)
                        {
                            TestRow -= 1;
                            TestColumn -= 1;
                            Step += 1;

                            if (TestRow == newRow && TestColumn == newColumn)
                            {
                                TestRow = 0; break;
                            }

                            motionCoordinates.Add(Tuple.Create(TestRow, TestColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == TestRow && checkCol == TestColumn)
                                    {
                                        if (image.Name.StartsWith("BlackBishop") || image.Name.StartsWith("BlackQueen"))   // If white king collides with black bishop or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("BlackKing") && Step == 1)   // If white king collides with black king on its first step
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("BlackPawn") && Step == 1)   // If white king collides with black pawn on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestRow = 0; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestRow = wKingRow;
                    TestColumn = wKingColumn;
                    Step = 0;

                    if (wKingRow != 0 && wKingColumn != 7)  // If white king is not in either the 8th rank or the h-file, step toward it  
                    {
                        while (TestRow != 0 && TestColumn != 7)
                        {
                            TestRow -= 1;
                            TestColumn += 1;
                            Step += 1;

                            if (TestRow == newRow && TestColumn == newColumn)
                            {
                                TestRow = 0; break;
                            }

                            motionCoordinates.Add(Tuple.Create(TestRow, TestColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == TestRow && checkCol == TestColumn)
                                    {
                                        if (image.Name.StartsWith("BlackBishop") || image.Name.StartsWith("BlackQueen"))   // If white king collides with black bishop or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("BlackKing") && Step == 1)   // If white king collides with black king on its first step
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("BlackPawn") && Step == 1)   // If white king collides with black pawn on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestRow = 0; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestRow = wKingRow;
                    TestColumn = wKingColumn;
                    Step = 0;

                    if (wKingRow != 7 && wKingColumn != 0)  // If white king is not in either the 1st rank or the a-file, step toward it  
                    {
                        while (TestRow != 7 && TestColumn != 0)
                        {
                            TestRow += 1;
                            TestColumn -= 1;
                            Step += 1;

                            if (TestRow == newRow && TestColumn == newColumn)
                            {
                                TestRow = 7; break;
                            }

                            motionCoordinates.Add(Tuple.Create(TestRow, TestColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == TestRow && checkCol == TestColumn)
                                    {
                                        if (image.Name.StartsWith("BlackBishop") || image.Name.StartsWith("BlackQueen"))   // If white king collides with black bishop or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("BlackKing") && Step == 1)   // If white king collides with black king on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestRow = 7; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestRow = wKingRow;
                    TestColumn = wKingColumn;
                    Step = 0;

                    if (wKingRow != 7 && wKingColumn != 7)  // If white king is not in either the 1st rank or the h-file, step toward it  
                    {
                        while (TestRow != 7 && TestColumn != 7)
                        {
                            TestRow += 1;
                            TestColumn += 1;
                            Step += 1;

                            if (TestRow == newRow && TestColumn == newColumn)
                            {
                                TestRow = 7; break;
                            }

                            motionCoordinates.Add(Tuple.Create(TestRow, TestColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == TestRow && checkCol == TestColumn)
                                    {
                                        if (image.Name.StartsWith("BlackBishop") || image.Name.StartsWith("BlackQueen"))   // If white king collides with black bishop or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("BlackKing") && Step == 1)   // If white king collides with black king on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestRow = 7; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    foreach (Image image in Chess_Board.Children.OfType<Image>())
                    {
                        if (image.Name.StartsWith("BlackKnight"))   // Obtain locations of all black knights
                        {
                            int knightRow = Grid.GetRow(image);
                            int knightCol = Grid.GetColumn(image);
                            knightCoordinates.Add(Tuple.Create(knightRow, knightCol));
                        }
                    }

                    foreach (Image image in Chess_Board.Children.OfType<Image>())
                    {
                        if (image.Name.StartsWith("White"))
                        {
                            int testTakeRow = Grid.GetRow(image);
                            int testTakeCol = Grid.GetColumn(image);

                            if (knightCoordinates.Any(coord => coord.Item1 == testTakeRow && coord.Item2 == testTakeCol))
                            {
                                knightCoordinates.RemoveAll(coord => coord.Item1 == testTakeRow && coord.Item2 == testTakeCol);
                            }
                        }
                    }

                    foreach (var knightCoordinate in knightCoordinates)
                    {
                        int knightRowDiff = Math.Abs(wKingRow - knightCoordinate.Item1);
                        int knightColDiff = Math.Abs(wKingColumn - knightCoordinate.Item2);

                        if ((knightRowDiff == 1 && knightColDiff == 2) || (knightRowDiff == 2 && knightColDiff == 1))   // If a black knight can attack the white king
                        {
                            return false;
                        }
                    }

                    return true;
                }

                else  // If black is moving
                {
                    int TestRow = bKingRow;
                    int TestColumn = bKingColumn;
                    Step = 0;

                    if (bKingColumn != 0)  // If black king is not on the a-file, step toward it
                    {
                        while (TestColumn != 0)
                        {
                            TestColumn -= 1;
                            Step += 1;

                            if (bKingRow == newRow && TestColumn == newColumn)
                            {
                                TestColumn = 0; break;
                            }

                            motionCoordinates.Add(Tuple.Create(bKingRow, TestColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == bKingRow && checkCol == TestColumn)
                                    {
                                        if (image.Name.StartsWith("WhiteRook") || image.Name.StartsWith("WhiteQueen"))   // If black king collides with white rook or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("WhiteKing") && Step == 1)   // If black king collides with white king on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestColumn = 0; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestColumn = bKingColumn;
                    Step = 0;

                    if (bKingColumn != 7)  // If black king is not on the h-file, step toward it
                    {
                        while (TestColumn != 7)
                        {
                            TestColumn += 1;
                            Step += 1;

                            if (bKingRow == newRow && TestColumn == newColumn)
                            {
                                TestColumn = 7; break;
                            }

                            motionCoordinates.Add(Tuple.Create(bKingRow, TestColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == bKingRow && checkCol == TestColumn)
                                    {
                                        if (image.Name.StartsWith("WhiteRook") || image.Name.StartsWith("WhiteQueen"))   // If balck king collides with white rook or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("WhiteKing") && Step == 1)   // If black king collides with white king on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestColumn = 7; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestColumn = bKingColumn;
                    Step = 0;

                    if (bKingRow != 0)  // If black king is not on the 8th rank, step toward it
                    {
                        while (TestRow != 0)
                        {
                            TestRow -= 1;
                            Step += 1;

                            if (TestRow == newRow && bKingColumn == newColumn)
                            {
                                TestRow = 0; break;
                            }

                            motionCoordinates.Add(Tuple.Create(TestRow, bKingColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == TestRow && checkCol == bKingColumn)
                                    {
                                        if (image.Name.StartsWith("WhiteRook") || image.Name.StartsWith("WhiteQueen"))   // If black king collides with white rook or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("WhiteKing") && Step == 1)   // If black king collides with white king on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestRow = 0; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestRow = bKingRow;
                    Step = 0;

                    if (bKingRow != 7)  // If black king is not on the 1st rank, step toward it
                    {
                        while (TestRow != 7)
                        {
                            TestRow += 1;
                            Step += 1;

                            if (TestRow == newRow && bKingColumn == newColumn)
                            {
                                TestRow = 7; break;
                            }

                            motionCoordinates.Add(Tuple.Create(TestRow, bKingColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == TestRow && checkCol == bKingColumn)
                                    {
                                        if (image.Name.StartsWith("WhiteRook") || image.Name.StartsWith("WhiteQueen"))   // If black king collides with white rook or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("WhiteKing") && Step == 1)   // If black king collides with white king on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestRow = 7; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestRow = bKingRow;
                    Step = 0;

                    if (bKingRow != 0 && bKingColumn != 0)  // If black king is not in either the 8th rank or the a-file, step toward it  
                    {
                        while (TestRow != 0 && TestColumn != 0)
                        {
                            TestRow -= 1;
                            TestColumn -= 1;
                            Step += 1;

                            if (TestRow == newRow && TestColumn == newColumn)
                            {
                                TestRow = 0; break;
                            }

                            motionCoordinates.Add(Tuple.Create(TestRow, TestColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == TestRow && checkCol == TestColumn)
                                    {
                                        if (image.Name.StartsWith("WhiteBishop") || image.Name.StartsWith("WhiteQueen"))   // If black king collides with white bishop or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("WhiteKing") && Step == 1)   // If black king collides with white king on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestRow = 0; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestRow = bKingRow;
                    TestColumn = bKingColumn;
                    Step = 0;

                    if (bKingRow != 0 && bKingColumn != 7)  // If black king is not in either the 8th rank or the h-file, step toward it  
                    {
                        while (TestRow != 0 && TestColumn != 7)
                        {
                            TestRow -= 1;
                            TestColumn += 1;
                            Step += 1;

                            if (TestRow == newRow && TestColumn == newColumn)
                            {
                                TestRow = 0; break;
                            }

                            motionCoordinates.Add(Tuple.Create(TestRow, TestColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == TestRow && checkCol == TestColumn)
                                    {
                                        if (image.Name.StartsWith("WhiteBishop") || image.Name.StartsWith("WhiteQueen"))   // If black king collides with white bishop or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("WhiteKing") && Step == 1)   // If black king collides with white king on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestRow = 0; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestRow = bKingRow;
                    TestColumn = bKingColumn;
                    Step = 0;

                    if (bKingRow != 7 && bKingColumn != 0)  // If black king is not in either the 1st rank or the a-file, step toward it  
                    {
                        while (TestRow != 7 && TestColumn != 0)
                        {
                            TestRow += 1;
                            TestColumn -= 1;
                            Step += 1;

                            if (TestRow == newRow && TestColumn == newColumn)
                            {
                                TestRow = 7; break;
                            }

                            motionCoordinates.Add(Tuple.Create(TestRow, TestColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == TestRow && checkCol == TestColumn)
                                    {
                                        if (image.Name.StartsWith("WhiteBishop") || image.Name.StartsWith("WhiteQueen"))   // If black king collides with white bishop or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("WhiteKing") && Step == 1)   // If black king collides with white king on its first step
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("WhitePawn") && Step == 1)   // If black king collides with white pawn on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestRow = 7; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    TestRow = bKingRow;
                    TestColumn = bKingColumn;
                    Step = 0;

                    if (bKingRow != 7 && bKingColumn != 7)  // If black king is not in either the 1st rank or the h-file, step toward it  
                    {
                        while (TestRow != 7 && TestColumn != 7)
                        {
                            TestRow += 1;
                            TestColumn += 1;
                            Step += 1;

                            if (TestRow == newRow && TestColumn == newColumn)
                            {
                                TestRow = 7; break;
                            }

                            motionCoordinates.Add(Tuple.Create(TestRow, TestColumn));
                            collision = motionCoordinates.Any(coord => coordinates.Any(c => c.Item1 == coord.Item1 && c.Item2 == coord.Item2));

                            if (collision)
                            {
                                foreach (Image image in Chess_Board.Children.OfType<Image>())
                                {
                                    int checkRow = Grid.GetRow(image);
                                    int checkCol = Grid.GetColumn(image);

                                    if (checkRow == TestRow && checkCol == TestColumn)
                                    {
                                        if (image.Name.StartsWith("WhiteBishop") || image.Name.StartsWith("WhiteQueen"))   // If black king collides with white bishop or queen
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("WhiteKing") && Step == 1)   // If black king collides with white king on its first step
                                        {
                                            return false;
                                        }

                                        else if (image.Name.StartsWith("WhitePawn") && Step == 1)   // If black king collides with white pawn on its first step
                                        {
                                            return false;
                                        }

                                        else
                                        {
                                            TestRow = 7; break;
                                        }
                                    }
                                }
                            }

                            motionCoordinates.Clear();
                        }
                    }

                    foreach (Image image in Chess_Board.Children.OfType<Image>())
                    {
                        if (image.Name.StartsWith("WhiteKnight"))   // Obtain locations of all white knights
                        {
                            int knightRow = Grid.GetRow(image);
                            int knightCol = Grid.GetColumn(image);
                            knightCoordinates.Add(Tuple.Create(knightRow, knightCol));
                        }
                    }

                    foreach (Image image in Chess_Board.Children.OfType<Image>())
                    {
                        if (image.Name.StartsWith("Black"))
                        {
                            int testTakeRow = Grid.GetRow(image);
                            int testTakeCol = Grid.GetColumn(image);

                            if (knightCoordinates.Any(coord => coord.Item1 == testTakeRow && coord.Item2 == testTakeCol))
                            {
                                knightCoordinates.RemoveAll(coord => coord.Item1 == testTakeRow && coord.Item2 == testTakeCol);
                            }
                        }
                    }

                    foreach (var knightCoordinate in knightCoordinates)
                    {
                        int knightRowDiff = Math.Abs(bKingRow - knightCoordinate.Item1);
                        int knightColDiff = Math.Abs(bKingColumn - knightCoordinate.Item2);

                        if ((knightRowDiff == 1 && knightColDiff == 2) || (knightRowDiff == 2 && knightColDiff == 1))   // If a white knight can attack the black king
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }
        }
    }
}