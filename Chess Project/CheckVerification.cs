using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Controls;
using System.Windows;
using Image = System.Windows.Controls.Image;

namespace Chess_Project
{
    /// <summary>
    /// Verifies that, after a proposed move, the side-to-move’s king is not in check.
    /// <para>
    /// The check is performed by:
    /// <list type="bullet">
    ///    <item><description>Treating the destination square (<paramref name="newRow"/>, <paramref name="newCol"/>) as a blocker:
    ///     if an enemy piece stands there it is considered captured and removed from attack calculations, and
    ///     rays do not look “past” that square.</description></item>
    ///     <item><description>Ray-scanning from the king in the 8 directions (N, NE, E, SE, S, SW, W, NW) to detect rook/queen,
    ///     bishop/queen, adjacent king, and pawn attacks (only on the first diagonal step in the correct pawn
    ///     direction).</description></item>
    ///     <item><description>Checking the 8 knight jump squares for an enemy knight.</description></item>
    /// </list>
    /// </para>
    /// </summary>
    /// <param name="chessBoard">The WPF Grid containing the piece Images.</param>
    /// <param name="mainWindow">Host that exposes piece coordinates via <c>PiecePositions()</c>.</param>
    /// <remarks>✅ Updated on 8/25/2025</remarks>
    internal sealed class CheckVerification(Grid chessBoard, MainWindow mainWindow)
    {
        private readonly Grid _board = chessBoard;
        private readonly MainWindow _main = mainWindow;

        // N, NE, E, SE, S, SW, W, NW
        private static readonly (int dr, int dc)[] Rays =
        {
            (-1,0), (-1,1), (0,1), (1,1), (1,0), (1,-1), (0,-1), (-1,-1)
        };

        // Knight moves
        private static readonly (int dr, int dc)[] KnightJumps =
        {
            (-2,-1), (-2,1), (-1,-2), (-1,2),
            ( 1,-2), ( 1,2), ( 2,-1), ( 2,1)
        };

        /// <summary>
        /// Returns <c>true</c> if the active side’s king is safe (not in check) after assuming a move to
        /// (<paramref name="newRow"/>, <paramref name="newCol"/>). Treats an enemy on the destination as captured.
        /// </summary>
        /// <param name="wkRow">White king row.</param>
        /// <param name="wkCol">White king column.</param>
        /// <param name="bkRow">Black king row.</param>
        /// <param name="bkCol">Black king column.</param>
        /// <param name="newRow">Destination row of the proposed move.</param>
        /// <param name="newCol">Destination column of the proposed move.</param>
        /// <param name="move">1 = white to move; any other value = black to move.</param>
        /// <remarks>✅ Updated on 8/25/2025</remarks>
        public bool ValidatePosition(int wkRow, int wkCol, int bkRow, int bkCol, int newRow, int newCol, int move)
        {
            bool whiteMoving = (move == 1);
            int kingRow = whiteMoving ? wkRow : bkRow;
            int kingCol = whiteMoving ? wkCol : bkCol;

            // Build square -> Image map, skipping an enemy on the destination (treated as captured)
            var board = BuildBoardMapSkippingEnemyAtDest(newRow, newCol, whiteMoving);

            // --- Knight checks (jumpers) ---
            foreach (var (dr, dc) in KnightJumps)
            {
                int r = kingRow + dr, c = kingCol + dc;
                if (!InBounds(r, c)) continue;
                if (r == newRow && c == newCol) continue; // our moved piece will sit here

                if (board.TryGetValue((r, c), out var img) &&
                    (whiteMoving ? img.Name.StartsWith("Black", StringComparison.Ordinal)
                                 : img.Name.StartsWith("White", StringComparison.Ordinal)) &&
                    img.Name.Contains("Knight", StringComparison.Ordinal))
                {
                    return false;
                }
            }

            // Sliding / king / pawn threats along rays
            for (int i = 0; i < Rays.Length; i++)
            {
                var (dr, dc) = Rays[i];
                bool isOrth = (i % 2 == 0); // 0,2,4,6 are orthogonal
                int r = kingRow + dr, c = kingCol + dc, step = 1;

                while (InBounds(r, c))
                {
                    // Do not look past the destination square
                    if (r == newRow && c == newCol) break;

                    if (board.TryGetValue((r, c), out var img))
                    {
                        bool enemy = whiteMoving ? img.Name.StartsWith("Black", StringComparison.Ordinal)
                                                 : img.Name.StartsWith("White", StringComparison.Ordinal);
                        if (!enemy) break; // own piece blocks

                        // Sliding pieces
                        if (isOrth && (img.Name.Contains("Rook", StringComparison.Ordinal) ||
                                       img.Name.Contains("Queen", StringComparison.Ordinal)))
                            return false;

                        if (!isOrth && (img.Name.Contains("Bishop", StringComparison.Ordinal) ||
                                        img.Name.Contains("Queen", StringComparison.Ordinal)))
                            return false;

                        // Adjacent enemy king
                        if (step == 1 && img.Name.Contains("King", StringComparison.Ordinal))
                            return false;

                        // Pawn on first diagonal step (attacker is opponent)
                        if (step == 1 && img.Name.Contains("Pawn", StringComparison.Ordinal))
                        {
                            bool attackerIsWhite = !whiteMoving;
                            bool pawnThreat =
                                attackerIsWhite ? (dr == 1 && Math.Abs(dc) == 1)   // white pawn sits one down-left/right of king
                                                : (dr == -1 && Math.Abs(dc) == 1); // black pawn sits one up-left/right of king
                            if (pawnThreat) return false;
                        }

                        // Enemy but not a threat → ray blocked
                        break;
                    }

                    r += dr; c += dc; step++;
                }
            }

            return true;
        }

        /// <summary>
        /// Determines whether the given coordinates lie within an 8×8 chessboard (zero-based).
        /// </summary>
        /// <param name="r">Row index in the range 0–7 (0 = White’s back rank, 7 = Black’s back rank).</param>
        /// <param name="c">Column index in the range 0–7 (0 = file ‘a’, 7 = file ‘h’).</param>
        /// <returns>
        /// <c>true</c> if <paramref name="r"/> and <paramref name="c"/> are both within [0, 7]; otherwise <c>false</c>.
        /// </returns>
        /// <remarks>
        /// Checks bounds only; it does not test whether the square is occupied. Uses C# relational pattern matching.
        /// <para>✅ Written on 8/25/2025</para>
        /// </remarks>
        private static bool InBounds(int r, int c) => r is >= 0 and < 8 && c is >= 0 and < 8;

        /// <summary>
        /// Creates a map of visible chess pieces on the grid, skipping an enemy on the destination square
        /// so captures are reflected in threat calculation.
        /// </summary>
        /// <remarks>✅ Written on 8/25/2025</remarks>
        private Dictionary<(int r, int c), Image> BuildBoardMapSkippingEnemyAtDest(int newRow, int newCol, bool whiteToMove)
        {
            var map = new Dictionary<(int, int), Image>(64);
            foreach (var img in _board.Children.OfType<Image>())
            {
                if (img.Visibility != Visibility.Visible) continue; // ignore captured/hidden
                var name = img.Name;
                if (!(name.StartsWith("White", StringComparison.Ordinal) ||
                      name.StartsWith("Black", StringComparison.Ordinal)))
                    continue; // ignore overlays/markers

                int r = Grid.GetRow(img), c = Grid.GetColumn(img);

                // Skip enemy on destination (captured)
                if (r == newRow && c == newCol)
                {
                    bool enemyAtDest = whiteToMove
                        ? name.StartsWith("Black", StringComparison.Ordinal)
                        : name.StartsWith("White", StringComparison.Ordinal);
                    if (enemyAtDest) continue;
                }

                map[(r, c)] = img; // last one wins if duplicates
            }
            return map;
        }
    }
}