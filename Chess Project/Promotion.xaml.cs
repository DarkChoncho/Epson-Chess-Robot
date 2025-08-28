using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Chess_Project
{
    /// <summary>
    /// Handles pawn promotion by displaying a piece-selection window and exposing
    /// the user’s choice to the caller.
    /// </summary>
    /// <remarks>
    /// Expects an image-path list in this exact order:
    /// <list type="number">
    ///     <item><description>WhiteRook</description></item>
    ///     <item><description>BlackRook</description></item>
    ///     <item><description>WhiteKnight</description></item>
    ///     <item><description>BlackKnight</description></item>
    ///     <item><description>WhiteBishop</description></item>
    ///     <item><description>BlackBishop</description></item>
    ///     <item><description>WhiteQueen</description></item>
    ///     <item><description>BlackQueen</description></item>
    /// </list>
    /// ✅ Perfected on 8/19/2025. I love you all.
    /// </remarks>
    public partial class Promotion : Window
    {
        /// <summary>
        /// Gets the <see cref="Button.Name"/> of the option the user clicked
        /// (e.g., <c>WhiteQueenButton</c>). Empty until a selection is made.
        /// </summary>
        /// <value>
        /// The name of the clicked button, or <see cref="string.Empty"/> if no selection was made.
        /// </value>
        /// <remarks>✅ Perfected on 8/19/2025</remarks>
        public string ClickedButtonName { get; private set; } = string.Empty;

        /// <summary>
        /// Maps canonical piece keys (e.g., <c>"WhiteQueen"</c>) to image file paths.
        /// Keys are compared case-insensitively.
        /// </summary>
        /// <remarks>✅ Perfected on 8/19/2025</remarks>
        private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);

        public string whiteRookPath;
        public string whiteKnightPath;
        public string whiteBishopPath;
        public string whiteQueenPath;
        public string blackRookPath;
        public string blackKnightPath;
        public string blackBishopPath;
        public string blackQueenPath;

        /// <summary>
        /// Initializes a new promotion window and wires the supplied image paths to
        /// their corresponding piece keys (e.g., <c>"WhiteQueen"</c>).
        /// </summary>
        /// <param name="imagePaths">
        /// A list of 8 absolute image paths ordered as:
        /// WhiteRook, BlackRook, WhiteKnight, BlackKnight,
        /// WhiteBishop, BlackBishop, WhiteQueen, BlackQueen.
        /// </param>
        /// <remarks>
        /// This constructor assumes <paramref name="imagePaths"/> contains exactly 8 entries.
        /// <para>✅ Perfected on 8/19/2025</para>
        /// </remarks>
        public Promotion(List<string> imagePaths)
        {
            InitializeComponent();

            _paths["WhiteRook"] = imagePaths[0];
            _paths["BlackRook"] = imagePaths[1];
            _paths["WhiteKnight"] = imagePaths[2];
            _paths["BlackKnight"] = imagePaths[3];
            _paths["WhiteBishop"] = imagePaths[4];
            _paths["BlackBishop"] = imagePaths[5];
            _paths["WhiteQueen"] = imagePaths[6];
            _paths["BlackQueen"] = imagePaths[7];
        }

        /// <summary>
        /// Loads the appropriate piece image into the Image control that raised the event.
        /// Relies on the Image.Name containing "White" or "Black" and one of: Rook/Knight/Bishop/Queen.
        /// </summary>
        /// <remarks>✅ Perfected on 8/19/2025</remarks>>
        public void LoadImage(object sender, RoutedEventArgs e)
        {
            if (sender is not Image img) return;

            // Determine color and piece from the control's Name.
            // Example names: "WhiteRook", "BlackQueenImage", etc.
            string color = img.Name.StartsWith("White", StringComparison.OrdinalIgnoreCase) ? "White" : "Black";
            string piece = img.Name.Contains("Rook", StringComparison.OrdinalIgnoreCase) ? "Rook" :
                           img.Name.Contains("Knight", StringComparison.OrdinalIgnoreCase) ? "Knight" :
                           img.Name.Contains("Bishop", StringComparison.OrdinalIgnoreCase) ? "Bishop" :
                           img.Name.Contains("Queen", StringComparison.OrdinalIgnoreCase) ? "Queen" :
                           string.Empty;

            if (string.IsNullOrEmpty(piece))
                return; // Unknown control name—nothing to do.

            string key = $"{color}{piece}";
            if (_paths.TryGetValue(key, out var path) && !string.IsNullOrWhiteSpace(path))
            {
                // Use Absolute—your callers are building absolute paths.
                img.Source = new BitmapImage(new Uri(path, UriKind.Absolute));
            }
        }

        /// <summary>
        /// Handles a promotion option click: stores the clicked button’s name and closes the window.
        /// </summary>
        /// <param name="sender">The <see cref="Button"/> that represents the selected promotion piece.</param>
        /// <param name="e">The routed event arguments (unused).</param>
        /// <remarks>✅ Perfected on 8/19/2025</remarks>
        private void PromotionSelection(object sender, RoutedEventArgs e)
        {
            ClickedButtonName = ((Button)sender).Name;
            Close();
        }
    }
}