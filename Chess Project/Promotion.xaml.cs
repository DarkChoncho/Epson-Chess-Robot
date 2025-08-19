using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Imaging;

namespace Chess_Project
{
    /// <summary>
    /// Handles pawn promotion by showing a selection menu and exposing the selected piece.
    /// Expects image paths in the following order:
    /// 0: WhiteRook, 1: BlackRook, 2: WhiteKnight, 3: BlackKnight,
    /// 4: WhiteBishop, 5: BlackBishop, 6: WhiteQueen, 7: BlackQueen.
    /// </summary>
    /// <remarks>✅ Updated on 8/19/2025</remarks>
    public partial class Promotion : Window
    {
        public string ClickedButtonName { get; private set; } = string.Empty;

        private readonly Dictionary<string, string> _paths = new(StringComparer.OrdinalIgnoreCase);

        public string whiteRookPath;
        public string whiteKnightPath;
        public string whiteBishopPath;
        public string whiteQueenPath;
        public string blackRookPath;
        public string blackKnightPath;
        public string blackBishopPath;
        public string blackQueenPath;

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
        /// <remarks>✅ Updated on 8/19/2025</remarks>>
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

        private void PromotionSelection(object sender, RoutedEventArgs e)
        {
            ClickedButtonName = ((Button)sender).Name;
            Close();
        }
    }
}