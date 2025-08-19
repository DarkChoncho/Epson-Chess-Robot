using Chess_Project.Configuration;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Net.Sockets;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Automation.Peers;
using System.Windows.Automation.Provider;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Windows.Threading;

namespace Chess_Project
{
    /// <summary>
    /// This script serves as the chess engine for SA0181868, the Epson Robot Chess Stand. The script utilizes the chess engine, Stockfish, for computer moves.
    /// The script features three modes: User Vs. User, User Vs. Computer, and Computer Vs. Computer. It also features multiple appearances for pieces, boards, and backgrounds.
    /// Written in approximately 400 hours from October 2023 to July 2024.
    /// </summary>

    public partial class MainWindow : Window
    {
        private DispatcherTimer _inactivityTimer;
        private EpsonController _whiteRobot;
        private EpsonController _blackRobot;
        private readonly string _whiteRobotIp = "192.168.0.2";
        private readonly string _blackRobotIp = "192.168.0.3";
        private readonly int _whiteRobotPort = 5000;
        private readonly int _blackRobotPort = 5000;

        private readonly string _whiteCognexIp = "192.168.0.12";
        private readonly string _blackCognexIp = "192.168.0.13";
        private readonly int _whiteCognexPort = 23;
        private readonly int _blackCognexPort = 23;

        private readonly Dictionary<string, SoundPlayer> _soundPlayer = [];
        public List<Tuple<int, int>> ImageCoordinates = [];
        public List<Tuple<int, int>> EnPassantSquare = [];
        public List<string> _gameFens = [];

        private ComboBoxItem? _selectedPlayType;  // Combo boxes
        private ComboBoxItem? _selectedElo;
        private ComboBoxItem? _selectedColor;
        private ComboBoxItem? _selectedWhiteElo;
        private ComboBoxItem? _selectedBlackElo;
        private ComboBoxItem? _backgroundTheme;
        private ComboBoxItem? _pieceTheme;
        private ComboBoxItem? _boardTheme;

        private Image? _clickedPawn;  // Piece entities
        private Image? _clickedRook;
        private Image? _clickedKnight;
        private Image? _clickedBishop;
        private Image? _clickedQueen;
        private Image? _clickedKing;
        private Image? _capturedPiece;

        public int imageRows;  // Piece coordinates
        public int imageColumns;
        private int _pickBit1;
        private int _pickBit2;
        private int _pickBit3;
        private int _placeBit1;
        private int _placeBit2;
        private int _placeBit3;
        private string _promotedPawn;
        private string _promotedTo;
        private string _activePiece;
        private string _takenPiece;
        private string _rcPastWhiteBits;
        private string _rcPastBlackBits;
        private string _rcWhiteBits;
        private string _rcBlackBits;
        private string _fen;
        private string _previousFen;

        private readonly int[] _pawnPick = [136, 137, 138, 139, 140, 141, 142, 143];  // Off-board pick locations
        private readonly int[] _rookPick = [128, 135, 148, 149, 151, 151];
        private readonly int[] _knightPick = [129, 134, 156, 157, 158, 159];
        private readonly int[] _bishopPick = [130, 133, 152, 153, 154, 155];
        private readonly int[] _queenPick = [131, 144, 145, 146, 147];
        private readonly int _kingPick = 132;

        private readonly int[] _pawnPlace = [168, 169, 170, 171, 172, 173, 174, 175];  // Off-board place locations
        private readonly int[] _rookPlace = [160, 167, 180, 181, 182, 183];
        private readonly int[] _knightPlace = [161, 166, 188, 189, 190, 191];
        private readonly int[] _bishopPlace = [162, 165, 184, 185, 186, 187];
        private readonly int[] _queenPlace = [163, 176, 177, 178, 179];
        private readonly int _kingPlace = 164;

        private readonly int[] _whitePawnOrigin = [72, 73, 74, 75, 76, 77, 78, 79];  // On-board place locations
        private readonly int[] _blackPawnOrigin = [112, 113, 114, 115, 116, 117, 118, 119];
        private readonly int[] _whiteRookOrigin = [64, 71];
        private readonly int[] _blackRookOrigin = [120, 127];
        private readonly int[] _whiteKnightOrigin = [65, 70];
        private readonly int[] _blackKnightOrigin = [121, 126];
        private readonly int[] _whiteBishopOrigin = [66, 69];
        private readonly int[] _blackBishopOrigin = [122, 125];
        private readonly int _whiteQueenOrigin = 67;
        private readonly int _blackQueenOrigin = 123;
        private readonly int _whiteKingOrigin = 68;
        private readonly int _blackKingOrigin = 124;

        private int _oldRow;  // Active piece coordinates
        private int _oldColumn;
        private int _newRow;
        private int _newColumn;
        private string _clickedButtonName;
        private string _pawnName;
        private char? _startFile;
        private string? _startRank;
        private char? _endFile;
        private string _endRank;
        private string? _startPosition;
        private string? _endPosition;
        private string? _executedMove;
        private string? _pgnMove;

        private int _whiteKingRow;  // King coordinates
        private int _whiteKingColumn;
        private int _blackKingRow;
        private int _blackKingColumn;

        #region Castling Flags

        private int _cWK = 0;
        private int _cWR1 = 0;
        private int _cWR2 = 0;
        private int _cBK = 0;
        private int _cBR1 = 0;
        private int _cBR2 = 0;
        private bool _kingCastle;
        private bool _queenCastle;

        #endregion

        #region Capture & Promotion Flags

        private bool _capture;
        private bool _enPassantCreated;
        private bool _enPassant;
        private bool _promoted;
        private char _promotionPiece;
        private int _numWQ;
        private int _numWR;
        private int _numWB;
        private int _numWN;
        private int _numBQ;
        private int _numBR;
        private int _numBB;
        private int _numBN;

        #endregion

        #region CPU Values & Flags

        private int _depth;  // CPU flags and values
        private int _whiteCpuDepth;
        private int _blackCpuDepth;
        private int _blunderPercent;
        private int _whiteCpuBlunderPercent;
        private int _blackCpuBlunderPercent;
        private int _scale = 1;
        private bool _blunderMove;
        private bool _topEngineMove;
        private bool _whiteCpuTopEngineMove;
        private bool _blackCpuTopEngineMove;

        #endregion

        #region Turn Tracking & Movement

        private int _move;
        private int _mode;
        private int _halfmove;
        private int _fullmove;
        private bool _userTurn = false;
        private bool _pieceSounds = true;
        private bool _moveConfirm = true;
        private bool _robotComm = true;
        private bool _moving = false;
        private bool _holdResume = false;
        private bool _wasPlayable = false;
        private bool _wasResumable = false;
        private bool _isPaused = false;
        private bool _boardSet = false;
        private int _timeoutDuration = 120;

        #endregion

        #region Board Flipping Values

        private int _flip;
        private int _theta;

        #endregion

        #region Stockfish Evaluation

        private string _stockfishEvaluation = "0";
        private double _quantifiedEvaluation = 10;
        private string _displayedAdvantage = "0.0";
        private int _whiteMaterial;
        private int _blackMaterial;

        #endregion

        #region Game End Flags

        private bool _endGame;
        private bool _threefoldRepetition;

        #endregion

        #region File Paths

        private readonly string _executableDirectory;
        private string? _stockfishPath;
        private readonly string _fenFilePath = "FEN_Codes.txt";
        private readonly string _pgnFilePath = "GamePGN.pgn";
        private string _preferencesFilePath;
        private string? _storedBoardTheme;
        private string _whitePawnImagePath;
        private string _whiteRookImagePath;
        private string _whiteKnightImagePath;
        private string _whiteBishopImagePath;
        private string _whiteQueenImagePath;
        private string _whiteKingImagePath;
        private string _blackPawnImagePath;
        private string _blackRookImagePath;
        private string _blackKnightImagePath;
        private string _blackBishopImagePath;
        private string _blackQueenImagePath;
        private string _blackKingImagePath;
        private string _backgroundImagePath;
        private string _boardImagePath;
        private Preferences _preferences;

        #endregion

        #region Program Initialization

        /// <summary>
        /// Initializes the main application window, sets up user preferences, audio, robot configurations,
        /// inactivity tracking, and applies theme formatting. Also, attaches a global mouse click event handler
        /// to reset inactivity tracking.
        /// </summary>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        public MainWindow()
        {
            InitializeComponent();
            _executableDirectory = AppDomain.CurrentDomain.BaseDirectory;

            InitializeUserPreferences();
            InitializeSounds();
            SetupInactivityTimer();
            InitializeRobots();
            ApplyThemeFormatting();

            this.PreviewMouseDown += MainWindow_PreviewMouseDown;
        }

        /// <summary>
        /// Load and pre-caches all chess-related sound effects into memory for fast playback during gameplay.
        /// </summary>
        /// <remarks>
        /// This method scans the "Assets/Sounds" directory for predefined sound effect names (e.g., "PieceMove", "GameStart"),
        /// constructs their full file path, and loads each sound into a <see cref="SoundPlayer"/> instance. The preloaded sounds
        /// are stored in the <see cref="_soundPlayer"/> dictionary using their name as the key for efficient lookup.
        /// <para>
        /// Missing sound files are logged as warnings and skipped. This ensures that gameplay can continue
        /// even if one or more sound files are unavailable.
        /// </para>
        /// ✅ Updated on 6/11/2025
        /// </remarks>
        private void InitializeSounds()
        {
            string soundsDirectory = System.IO.Path.Combine(_executableDirectory, "Assets", "Sounds");
            string[] soundNames = [
                "GameEnd", "GameStart", "PieceCapture", "PieceCastle", "PieceCheck",
                "PieceIllegal", "PieceMove", "PieceOpponent", "PiecePromote"
            ];

            foreach (var sound in soundNames)
            {
                string soundFilePath = System.IO.Path.Combine(soundsDirectory, $"{sound}.wav");

                if (!File.Exists(soundFilePath))
                {
                    ChessLog.LogWarning($"Sound file nto found: {soundFilePath}");
                    continue;
                }

                SoundPlayer player = new(soundFilePath);
                player.Load();
                _soundPlayer[sound] = player;

                ChessLog.LogDebug($"Preloaded sound: {sound} -> {soundFilePath}");
            }
        }

        /// <summary>
        /// Initializes and starts the inactivity timer that triggers after a specified timeout period.
        /// </summary>
        /// <remarks>
        /// The timer uses the UI thread via <see cref="DispatcherTimer"/> and is configured to tick every
        /// <see cref="_timeoutDuration"/> seconds. When the timer elapses, it invokes the <see cref="InactivityTimer_Tick(object?, EventArgs)"/>
        /// handler to perform any inactivity-related actions.
        /// <para>✅ Updated on 6/11/2025</para>
        /// </remarks>
        private void SetupInactivityTimer()
        {
            _inactivityTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(_timeoutDuration)
            };

            _inactivityTimer.Tick += InactivityTimer_Tick;
            _inactivityTimer.Start();
        }

        /// <summary>
        /// Instantiates Epson robot controller objects for both the white and black robots
        /// using their respective IP addresses and ports, then attempts to establish initial
        /// connections to both controllers.
        /// </summary>
        /// <remarks>✅ Updated on 8/1/2025</remarks>
        private void InitializeRobots()
        {
            _whiteRobot = new EpsonController(_whiteRobotIp, _whiteRobotPort, RobotColor.White, _whiteCognexIp, _whiteCognexPort);
            _blackRobot = new EpsonController(_blackRobotIp, _blackRobotPort, RobotColor.Black, _blackCognexIp, _blackCognexPort);
            HandleEpsonConnectionAsync();
        }

        /// <summary>
        /// Handles Epson RC+ robot connection toggling. Attempts to connect ot disconnect both robots
        /// and updates the application state and preferences accordingly.
        /// </summary>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        private async void HandleEpsonConnectionAsync()
        {
            if (EpsonRCConnection.IsChecked == true)
            {
                // Attempt to connect to both robots
                GlobalState.WhiteConnected = await _whiteRobot.ConnectAsync();
                GlobalState.BlackConnected = await _blackRobot.ConnectAsync();
                _robotComm = GlobalState.WhiteConnected && GlobalState.BlackConnected;

                // Update UI
                EpsonRCConnection.IsChecked = _robotComm;
                SetStatusLights(
                    GlobalState.WhiteConnected ? Brushes.Green : Brushes.Red,
                    GlobalState.BlackConnected ? Brushes.Green : Brushes.Red
                );

                // Update preference and save
                _preferences.EpsonRC = _robotComm;
                PreferencesManager.Save(_preferences);
            }
            else
            {
                // Disconnect all robots and update UI
                if (GlobalState.WhiteConnected) _whiteRobot.Disconnect();
                if (GlobalState.BlackConnected) _blackRobot.Disconnect();

                GlobalState.WhiteConnected = false;
                GlobalState.BlackConnected = false;
                _robotComm = false;
                SetStatusLights(Brushes.Red, Brushes.Red);

                // Update preference and save
                _preferences.EpsonRC = false;
                PreferencesManager.Save(_preferences);
            }
        }

        #endregion

        #region Theme and Preference Methods

        /// <summary>
        /// Initializes user preferences by loading values from persistent storage (JSON),
        /// updating UI toggle states, and preloading all required asset paths for themes and pieces.
        /// </summary>
        /// <remarks>
        /// Falls back to default preferences if loading fails. Also logs missing or failed preference loads.
        /// Paths to all piece and board assets are resolved and cached for later use.
        /// <para>✅ Updated on 6/11/2025</para>
        /// </remarks>
        private void InitializeUserPreferences()
        {
            _stockfishPath = System.IO.Path.Combine(_executableDirectory, "Stockfish.exe");

            try
            {
                _preferences = PreferencesManager.Load();
            }
            catch (Exception ex)
            {
                ChessLog.LogWarning("Failed to load preferences. Using defaults.", ex);
                _preferences = new Preferences();
            }

            // Update checkboxes and internal flags based on loaded preferences
            Sounds.IsChecked = _pieceSounds = _preferences.PieceSounds;
            ConfirmMove.IsChecked = _moveConfirm = _preferences.ConfirmMove;
            EpsonRCConnection.IsChecked = _robotComm = _preferences.EpsonRC;

            // Define base asset paths
            string assetPath = System.IO.Path.Combine(_executableDirectory, "Assets");
            string piecePath = System.IO.Path.Combine(assetPath, "Pieces", _preferences.Pieces);

            // Set chess piece image paths using reflection
            SetPieceImagePaths(piecePath);

            // Set image paths for board and background
            _backgroundImagePath = System.IO.Path.Combine(_executableDirectory, "Assets", "Backgrounds", $"{_preferences.Background}.png");
            _boardImagePath = System.IO.Path.Combine(_executableDirectory, "Assets", "Boards", $"{_preferences.Board}.png");
        }

        /// <summary>
        /// Sets the image paths for each chess piece (white and black) based on the given theme directory.
        /// Uses reflection to assign the resolved file paths to backing fields in the <see cref="MainWindow"/>
        /// class. Logs a warning if any image asset is missing.
        /// </summary>
        /// <param name="piecePath">The absolute path to the themed piece image directory.</param>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        private void SetPieceImagePaths(string piecePath)
        {
            string[] pieces = ["Pawn", "Knight", "Bishop", "Rook", "Queen", "King"];

            foreach (string piece in pieces)
            {
                string whitePath = System.IO.Path.Combine(piecePath, $"White{piece}.png");
                string blackPath = System.IO.Path.Combine(piecePath, $"Black{piece}.png");

                if (!File.Exists(whitePath))
                    ChessLog.LogWarning($"Missing asset: {whitePath}");

                if (!File.Exists(blackPath))
                    ChessLog.LogWarning($"Missing asset: {blackPath}");

                typeof(MainWindow).GetField($"white{piece}ImagePath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(this, whitePath);

                typeof(MainWindow).GetField($"black{piece}ImagePath", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(this, blackPath);
            }
        }

        /// <summary>
        /// Applies visual formatting to the background, piece, and board selection ComboBoxes
        /// based on the currently loaded user preferences.
        /// </summary>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        private void ApplyThemeFormatting()
        {
            SetComboBoxSelection(BackgroundSelection, FormatThemeName(_preferences.Background));
            SetComboBoxSelection(PieceSelection, FormatThemeName(_preferences.Pieces));
            SetComboBoxSelection(BoardSelection, FormatThemeName(_preferences.Board));
        }

        /// <summary>
        /// Formats a theme name by inserting a space between lowercase and uppercase letters.
        /// </summary>
        /// <param name="input">The theme name to format.</param>
        /// <returns>The formatted theme name with spaces inserted between camel case transitions.</returns>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        private static string FormatThemeName(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return input ?? string.Empty;  // Ensures null safety

            StringBuilder sb = new();

            for (int i = 0; i < input.Length - 1; i++)  // Skip last char to compare with next safely
            {
                sb.Append(input[i]);

                // Add space between lowercase followed by uppercase (e.g., "NeoWood" → "Neo Wood")
                if (char.IsLower(input[i]) && char.IsUpper(input[i + 1]))
                {
                    sb.Append(' ');
                }
            }

            sb.Append(input[^1]); // Append the final character
            return sb.ToString();
        }

        /// <summary>
        /// Loads and applies the user's selected background image to the given <see cref="Grid"/>.
        /// Logs a warning and exits if the image path is invalid or the file is missing.
        /// </summary>
        /// <param name="sender">The <see cref="Grid"/> control receiving the background image.</param>
        /// <param name="e">Optional routed event arguments.</param>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        private void LoadBackground(object sender, RoutedEventArgs? e)
        {
            if (sender is not Grid background)
                return;

            if (string.IsNullOrWhiteSpace(_backgroundImagePath))
            {
                ChessLog.LogWarning("Background image path is null or empty. Background not applied.");
                return;
            }

            if (!File.Exists(_backgroundImagePath))
            {
                ChessLog.LogWarning($"Background image file does not exist: {_backgroundImagePath}");
                return;
            }

            background.Background = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(_backgroundImagePath, UriKind.Absolute))
            };
        }

        /// <summary>
        /// Loads and applies the appropriate image to a chess piece <see cref="Image"/> control based on its name
        /// and the selected piece theme.
        /// </summary>
        /// <param name="sender">The <see cref="Image"/> control to update.</param>
        /// <param name="e">Optional routed event arguments.</param>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        private void LoadImage(object sender, RoutedEventArgs? e)
        {
            if (sender is not Image img)
                return;

            if (string.IsNullOrWhiteSpace(_preferences.Pieces))
            {
                ChessLog.LogWarning("Piece theme not set. Cannot load image.");
                return;
            }

            // Remove numbers from the piece name (e.g., "WhitePawn1" → "WhitePawn")
            string pieceName = new([.. img.Name.Where(c => !char.IsDigit(c))]);

            // Determine color and type
            bool isWhite = pieceName.StartsWith("White");
            string pieceType = pieceName.Replace("White", "").Replace("Black", "");

            // Construct the image path
            string imagePath = System.IO.Path.Combine(
                _executableDirectory, "Assets", "Pieces", _preferences.Pieces,
                $"{(isWhite ? "White" : "Black")}{pieceType}.png"
            );

            if (!File.Exists(imagePath))
            {
                ChessLog.LogWarning($"Missing piece image: {imagePath}");
                return;
            }

            // Apply the image to the piece
            img.Source = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
        }

        /// <summary>
        /// Applies a visual theme to the chessboard grid, including the background image and the rank/file label colors,
        /// based on the currently selected board theme.
        /// </summary>
        /// <param name="sender">The <see cref="Grid"/> representing the board UI element.</param>
        /// <param name="e">Optional event arguments for routed event handlers (unused).</param>
        /// <remarks>
        /// This method first ensures the sender is a valid <see cref="Grid"/> and that a board image path is provided.
        /// It then applies the selected background image and updates rank/file label colors to match the theme.
        /// If the theme is not found or the color conversion fails, a warning is logged and only the background is applied.
        /// <para>✅ Updated on 6/10/2025</para>
        /// </remarks>
        private void LoadBoard(object sender, RoutedEventArgs? e)
        {
            if (sender is not Grid board || string.IsNullOrWhiteSpace(_boardImagePath))
            {
                ChessLog.LogError("Board image path is missing or invalid. Blank board will be applied.");
                return;
            }

            board.Background = new ImageBrush
            {
                ImageSource = new BitmapImage(new Uri(_boardImagePath, UriKind.Absolute))
            };

            // Board theme color mapping
            var boardThemes = new Dictionary<string, (string Light, string Dark)>
            {
                { "Baseball",       ("#F0D9B5", "#B58863") },
                { "Basketball",     ("#F0D9B5", "#B58863") },
                { "Blue",           ("#ECECD7", "#4D6D92") },
                { "Brown",          ("#F0D9B5", "#B58863") },
                { "Bubblegum",      ("#fff3f3", "#f9cdd3") },
                { "Dash",           ("#bd9257", "#6b3a27") },
                { "Glass",          ("#667188", "#282f3f") },
                { "Green",          ("#edeed1", "#779952") },
                { "IcySea",         ("#c5d5dc", "#7a9db2") },
                { "Light",          ("#dcdcdc", "#aaaaaa") },
                { "Purple",         ("#EFEFEF", "#8877B7") },
                { "Red",            ("#F0D8BF", "#BA5546") },
                { "Sky",            ("#efefef", "#c2d7e2") },
                { "Tournament",     ("#ebece8", "#316549") },
                { "Valentine'sDay", ("#f1fbff", "#f1a3a7") },
                { "Walnut",         ("#c0a684", "#835f42") },
                { "8-Bit",          ("#f3f3f4", "#6a9b41") }
            };

            if (boardThemes.TryGetValue(_preferences.Board, out var colors))
            {
                var converter = new BrushConverter();
                if (converter.ConvertFrom(colors.Light) is SolidColorBrush lightBrush &&
                    converter.ConvertFrom(colors.Dark) is SolidColorBrush darkBrush)
                {
                    ApplyTextBlockColors(lightBrush, darkBrush);
                }
                else
                {
                    ChessLog.LogWarning("Failed to convert board theme colors.");
                }
            }
            else
            {
                ChessLog.LogWarning($"Unknown board theme: {_preferences.Board}. Skipping label coloring.");
            }
        }

        /// <summary>
        /// Applies the specified brushes to the board's coordinate labels based on square color.
        /// </summary>
        /// <param name="lightBrush">Brush to apply to text blocks over light-colored squares.</param>
        /// <param name="darkBrush">Brush to apply to text blocks over dark-colored squares.</param>
        /// <remarks>
        /// This method groups coordinate labels by light and dark square alignment, then applies the
        /// appropriate brush to each group. If either brush is null, the method exits early.
        /// <para>✅ Updated on 6/11/2025</para>
        /// </remarks>
        private void ApplyTextBlockColors(SolidColorBrush? lightBrush, SolidColorBrush? darkBrush)
        {
            if (lightBrush is null || darkBrush is null)
                return;

            TextBlock[] lightTextBlocks =
            [
                aFile, cFile, eFile, gFile,
                firstRank, thirdRank, fifthRank, seventhRank
            ];

            TextBlock[] darkTextBlocks =
            [
                bFile, dFile, fFile, hFile,
                secondRank, fourthRank, sixthRank, eighthRank
            ];

            foreach (TextBlock textBlock in lightTextBlocks)
                textBlock.Foreground = lightBrush;

            foreach (TextBlock textBlock in darkTextBlocks)
                textBlock.Foreground = darkBrush;
        }

        /// <summary>
        /// Handles theme selection change, updates the corresponding preference property,
        /// writes the updated preferences to JSON, and reapplies the visual changes.
        /// </summary>
        /// <param name="sender">The ComboBox that triggered the event.</param>
        /// <param name="e">The event arguments.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void ThemeChange(object sender, EventArgs e)
        {
            if (sender is not ComboBox comboBox) return;

            // Extract selected theme and normalize spacing
            string selectedTheme = (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Replace(" ", "") ?? "";

            if (string.IsNullOrEmpty(selectedTheme)) return;

            // Update the appropriate preference field
            if (comboBox == BackgroundSelection)
                _preferences.Background = selectedTheme;
            else if (comboBox == PieceSelection)
                _preferences.Pieces = selectedTheme;
            else if (comboBox == BoardSelection)
                _preferences.Board = selectedTheme;

            // Persist changes and reapply theme
            PreferencesManager.Save(_preferences);
            InitializeUserPreferences();
            ApplyThemeChanges(comboBox);
        }

        /// <summary>
        /// Applies visual updates for the theme category whose selection just changed.
        /// Dispatches to background, piece, or board update logic depending on which ComboBox supplied the change.
        /// </summary>
        /// <param name="comboBox">The ComboBox containing the newly selected theme.</param>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><c>BackgroundSelection</c> → Reloads the main screen background image.</item>
        ///     <item><c>PieceSelection</c> → Reloads all visible chess piece images on the board.</item>
        ///     <item><c>BoardSelection</c> → Reapplies the board surface and coordinate label colors.</item>
        /// </list>
        /// <para>Event args are not required; this method calls the underlying load functions directly.</para>
        /// ✅ Updated on 7/18/2025
        /// </remarks>
        private void ApplyThemeChanges(ComboBox comboBox)
        {
            if (comboBox is null) return;

            if (comboBox == BackgroundSelection)
            {
                LoadBackground(Screen, null);
            }
            else if (comboBox == PieceSelection)
            {
                // Update all chess pieces to reflect the new theme
                foreach (Image piece in Chess_Board.Children.OfType<Image>())
                    LoadImage(piece, null);
            }
            else if (comboBox == BoardSelection)
            {
                LoadBoard(Chess_Board, null);
            }
            // else ignore: ComboBox from some unrelated source triggered this handler
        }

        #endregion

        #region Main Game Logic

        /// <summary>
        /// Begins the game based on <see cref="_selectedPlayType"/> and configurations.
        /// </summary>
        /// <param name="sender">The button that triggered the event.</param>
        /// <param name="e">The event arguments.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        public async void PlayAsync(object sender, EventArgs e)
        {
            _inactivityTimer.Stop();
            PlaySound("GameStart");

            // Store selections
            _selectedPlayType = (ComboBoxItem)Play_Type.SelectedItem;
            _selectedColor = (ComboBoxItem)Color.SelectedItem;
            string? playType = _selectedPlayType?.Content?.ToString();
            string? playerColor = _selectedColor?.Content?.ToString();

            // UI Setup: Hide and disable setup elements
            Game_Start.Visibility = Visibility.Collapsed;
            Game_Start.IsEnabled = false;
            UvCorUvU.Visibility = Visibility.Collapsed;
            UvCorUvU.IsEnabled = false;
            CvC.Visibility = Visibility.Collapsed;
            CvC.IsEnabled = false;
            PlayButton.Visibility = Visibility.Collapsed;
            PlayButton.IsEnabled = false;
            ResumeButton.Visibility = Visibility.Visible;
            EpsonRCConnection.IsEnabled = false;

            // Game state initializations
            _move = 1;
            _fullmove = 1;
            ResetCapturedPieceCounts();
            EnableAllPieces();
            EraseAnnotations();

            // Handle robot-controlled game setup
            if (_robotComm)
            {
                ShowSetupPopup(true);

                if (!_boardSet)
                    await SetupBoard();

                ShowSetupPopup(false);
            }

            // Initialize FEN and PGN
            FENCode();
            File.WriteAllText(_fenFilePath, string.Empty);

            // Determine game mode
            switch (playType)
            {
                case "Com Vs. Com":
                    _mode = 1;
                    _moving = true;
                    _userTurn = false;
                    Chess_Board.IsHitTestVisible = false;

                    ComputerMove();
                    break;

                case "User Vs. Com":
                    _mode = 2;

                    // Flip board if necessary
                    if ((_flip == 0 && playerColor == "Black") || (_flip == 1 && playerColor == "White"))
                    {
                        FlipBoard();
                        UpdateEvalBar();
                    }

                    if (playerColor == "Black")
                    {
                        _moving = true;
                        _userTurn = false;
                        ComputerMove();
                    }
                    else
                    {
                        _userTurn = true;
                    }
                    break;

                case "User Vs. User":
                default:
                    _mode = 3;
                    _userTurn = true;
                    break;
            }

            await WriteFENCode();
            WritePGNFile();
        }

        /// <summary>
        /// Manages a pawn move end-to-end: applies captures (including en passant),
        /// updates castling rights, handles promotion, optionally animates and confirms
        /// the move with the user, and finalizes game state (FEN, checkmate check, selection).
        /// </summary>
        /// <param name="activePawn">The pawn being moved. Must be a valid piece Image on the board.</param>
        /// <remarks>
        /// Steps:
        /// <list type="number">
        ///     <item>Snapshot state (positions, castling rights), resolve captures.</item>
        ///     <item>Apply en passant capture and eligibility when applicable.</item>
        ///     <item>Handle promotion if the pawn reaches its last rank.</item>
        ///     <item>If confirm moves is enabled, animate → confirm → finalize or undo.</item>
        ///     <item>Otherwise finalize immediately (update FEN, verify checkmate, clear selection).</item>
        /// </list>
        /// <para>✅ Updated on 8/18/2025</para></remarks>
        public async Task PawnMoveManagerAsync(Image activePawn)
        {
            // Snapshot board state & castling rights for potential undo.
            PiecePositions();
            int[] castlingRightsSnapshot = [_cWR1, _cWK, _cWR2, _cBR1, _cBK, _cBR2];

            _pawnName = activePawn.Name;

            // Captures & castling rights
            HandlePieceCapture(activePawn);  // sets _capturedPiece if any
            HandleEnPassantCapture();  // resolves en passant capture if applicable
            DisableCastlingRights(activePawn, _capturedPiece);

            // En passant eligibility & promotion
            _enPassantCreated = false;

            if (_move == 1)  // White just moved
            {
                if (_oldRow - _newRow == 2)
                {
                    EnPassantSquare.Clear();
                    EnPassantSquare.Add(Tuple.Create(_newRow + 1, _newColumn));
                    _enPassantCreated = true;
                }
                else if (_newRow == 0)
                {
                    PawnPromote(activePawn, _move);
                }
            }
            else  // Black just moved
            {
                if (_newRow - _oldRow == 2)
                {
                    EnPassantSquare.Clear();
                    EnPassantSquare.Add(Tuple.Create(_newRow - 1, _newColumn));
                    _enPassantCreated = true;
                }
                else if (_newRow == 7)
                {
                    PawnPromote(activePawn, _move);
                }
            }

            // Optional user confirmation path
            if (_userTurn && _moveConfirm)
            {
                // Callout proposed move
                SelectedPiece(_oldRow, _oldColumn);
                SelectedPiece(_newRow, _newColumn);

                // Animate from old to new (board already updated by caller)
                Grid.SetRow(activePawn, _oldRow);
                Grid.SetColumn(activePawn, _oldColumn);
                await MovePieceAsync(activePawn, _newRow, _newColumn, _oldRow, _oldColumn);

                bool confirmed = await WaitForConfirmationAsync();
                EraseAnnotations();

                if (confirmed)
                {
                    MoveCallout(_oldRow, _oldColumn, _newRow, _newColumn);
                    await FinalizeMoveAsync(activePawn);
                    await WriteFENCode();
                    await CheckmateVerifierAsync();
                    DeselectPieces();
                }
                else
                {
                    UndoMove(activePawn, castlingRightsSnapshot);
                }
                return;
            }

            // Immediate finalize path
            await FinalizeMoveAsync(activePawn);
            await WriteFENCode();
            await CheckmateVerifierAsync();
            DeselectPieces();
        }

        /// <summary>
        /// Orchestrates a non-pawn move: updates positions, applies captures/castling constraints,
        /// optionally shows/animates a proposed move for user confirmation, and then finalizes or reverts.
        /// </summary>
        /// <param name="activePiece">The piece being moved. Must be a valid piece Image on the board.</param>
        /// <remarks>✅ Updated on 8/18/2025</remarks>>
        public async Task MoveManagerAsync(Image activePiece)
        {
            // Snapshot board state & castling rights for potential undo.
            PiecePositions();
            int[] castlingRightsSnapshot = [_cWR1, _cWK, _cWR2, _cBR1, _cBK, _cBR2];

            // King-specific pre-processing (e.g., castling)
            if (activePiece.Name.Contains("King"))
                KingMoveManager();

            // Captures and castling rights
            HandlePieceCapture(activePiece);
            DisableCastlingRights(activePiece, _capturedPiece);

            // Optional user confirmation path
            if (_userTurn && _moveConfirm)
            {
                // Callout proposed move
                if (_kingCastle)
                {
                    await HandleCastlingMoveAsync("King");
                }
                else if (_queenCastle)
                {
                    await HandleCastlingMoveAsync("Queen");
                }
                else
                {
                    SelectedPiece(_oldRow, _oldColumn);
                    SelectedPiece(_newRow, _newColumn);
                }

                // Animate from old → new (board already updated by caller)
                Grid.SetRow(activePiece, _oldRow);
                Grid.SetColumn(activePiece, _oldColumn);
                await MovePieceAsync(activePiece, _newRow, _newColumn, _oldRow, _oldColumn);

                bool confirmed = await WaitForConfirmationAsync();
                EraseAnnotations();

                if (confirmed)
                {
                    MoveCallout(_oldRow, _oldColumn, _kingCastle ? _oldRow : _queenCastle ? _oldRow : _newRow, _kingCastle ? 7 : _queenCastle ? 0 : _newColumn);
                    await FinalizeMoveAsync(activePiece);
                    await WriteFENCode();
                    await CheckmateVerifierAsync();
                    DeselectPieces();
                }
                else
                {
                    UndoMove(activePiece, castlingRightsSnapshot);
                }
                return;
            }

            // Immediate finalize path
            await FinalizeMoveAsync(activePiece);
            await WriteFENCode();
            await CheckmateVerifierAsync();
            DeselectPieces();
        }

        /// <summary>
        /// If the current king move is a castle (2-column shift), updates the corresponding rook's grid
        /// position to its castled square and sets the appropriate castling flag.
        /// </summary>
        /// <remarks>✅ Updated on 8/18/2025</remarks>
        public void KingMoveManager()
        {
            // Not a castling move unless the king shifts exactly two files.
            if (Math.Abs(_oldColumn - _newColumn) != 2)
                return;

            bool isKingside = _oldColumn < _newColumn;

            // Determine which side (White/Black) from the active piece name.
            // Assumes _activePiece is set (e.g., in Square_ClickAsync) to the moving piece's name.
            bool isWhite = _activePiece?.StartsWith("White") == true;

            // Pick rook name based on side and castle direction.
            // By convention: Rook1 = queenside rook (col 0), Rook2 = kingside rook (col 7).
            string rookName =
                isWhite
                    ? (isKingside ? "WhiteRook2" : "WhiteRook1")
                    : (isKingside ? "BlackRook2" : "BlackRook1");

            // Rook ends adjacent to the king's destination square:
            // kingside → to the left of the king; queenside → to the right of the king.
            int rookEndColumn = isKingside ? _newColumn - 1 : _newColumn + 1;

            var rook = Chess_Board.Children
                .OfType<Image>()
                .FirstOrDefault(img => img.Name == rookName);

            if (rook != null)
            {
                Grid.SetRow(rook, _newRow);
                Grid.SetColumn(rook, rookEndColumn);
            }

            // Mark which castle type occurred for downstream logic/visuals.
            if (isKingside)
                _kingCastle = true;
            else
                _queenCastle = true;
        }

        /// <summary>
        /// Handles a castling move by animating the appropriate rook to its castled square
        /// (adjacent to the king's destination) and highlighting the king's start and rook target.
        /// </summary>
        /// <param name="side">"King" for kingside, "Queen" for queenside.</param>
        /// <remarks>✅ Updated on 8/18/2025</remarks>
        private async Task HandleCastlingMoveAsync(string side)
        {
            if (string.IsNullOrWhiteSpace(side))
                return;

            bool isKingside = side.Equals("King", StringComparison.OrdinalIgnoreCase);
            bool isQueenside = side.Equals("Queen", StringComparison.OrdinalIgnoreCase);
            if (!isKingside && !isQueenside)
                return;

            // Determine color from the active moving piece (e.g., "WhiteKing", "BlackKing")
            bool isWhite = _activePiece?.StartsWith("White", StringComparison.Ordinal) == true;

            // By naming convention: Rook1 = queenside (col 0), Rook2 = kingside (col 7)
            string rookName =
                isWhite
                    ? (isKingside ? "WhiteRook2" : "WhiteRook1")
                    : (isKingside ? "BlackRook2" : "BlackRook1");

            int rookColumn = (side == "King") ? 7 : 0;
            int rookTargetColumn = (side == "King") ? _newColumn - 1 : _newColumn + 1;

            // Try to get rook by name
            var rook = Chess_Board.Children.OfType<Image>()
                .FirstOrDefault(img => img.Name == rookName);

            // Animate from old → new (board already updated by caller)
            if (rook != null)
            {
                Grid.SetRow(rook, _oldRow);
                Grid.SetColumn(rook, rookColumn);
                await MovePieceAsync(rook, _newRow, rookTargetColumn, _oldRow, rookColumn);
            }

            // Highlight king's start and rook's target squares for visual confirmation.
            SelectedPiece(_oldRow, _oldColumn);
            SelectedPiece(_newRow, rookColumn);
        }

        /// <summary>
        /// Resets the rook to its original square if a castling move is undone,
        /// then clears the castling flag.
        /// </summary>
        /// <remarks>✅ Updated on 8/18/2025</remarks>
        private void HandleRookReset()
        {
            // Only applicable if a castle was in effect
            if ((!_kingCastle && !_queenCastle))
                return;

            string rookName = (_move == 1) ?
                (_kingCastle ? "WhiteRook2" : "WhiteRook1") :
                (_kingCastle ? "BlackRook2" : "BlackRook1");

            var rook = Chess_Board.Children.OfType<Image>().FirstOrDefault(img => img.Name == rookName);
            if (rook != null)
            {
                Grid.SetRow(rook, _oldRow);
                Grid.SetColumn(rook, _kingCastle ? 7 : 0);
            }
        }

        /// <summary>
        /// Checks if an opposing piece exists at the destination square (if any).
        /// </summary>
        /// <param name="activePiece">The piece being moved.</param>
        /// <remarks>
        /// If an enemy piece is on (<see cref="_newRow"/>, <see cref="_newColumn"/>), it's recorded,
        /// removed from the board, and <see cref="_capture"/> is set. The moving piece is untouched.
        /// <para>✅ Updated on 8/19/2025</para>
        /// </remarks>
        private void HandlePieceCapture(Image activePiece)
        {
            // Fast check: does any image currently sit on the destination square?
            bool occupied = ImageCoordinates.Any(coord => coord.Item1 == _newRow && coord.Item2 == _newColumn);
            if (!occupied) return;

            // Locate the captured piece
            var captured = Chess_Board.Children
                .OfType<Image>()
                .FirstOrDefault(img => img != activePiece && Grid.GetRow(img) == _newRow && Grid.GetColumn(img) == _newColumn);

            if (captured is null) return;
 
            _takenPiece = captured.Name;
            _capturedPiece = captured;

            // Remove visually & logically
            _capturedPiece.Visibility = Visibility.Collapsed;
            _capturedPiece.IsHitTestVisible = false;
            _capturedPiece.IsEnabled = false;
            Chess_Board.Children.Remove(_capturedPiece);

            _capture = true;
        }

        /// <summary>
        /// Executes en en passant capture when a pawn moves onto the designated en passant square.
        /// </summary>
        /// <remarks>
        /// If the move qualifies as en passant, the opposing pawn directly adjacent to the destination
        /// is identified, recorded as captured, and removed from the board, and the en passant state is cleared.
        /// <para>✅ Updated on 8/19/2025</para>
        /// </remarks>
        private void HandleEnPassantCapture()
        {
            // Is the destination square currently flagged as en passant?
            bool isEnPassant = EnPassantSquare.Any(coord => coord.Item1 == _newRow && coord.Item2 == _newColumn);
            if (!isEnPassant) return;

            // Determine the row of the pawn to capture (the pawn that advanced two squares last move)
            int capturedRow = (_move == 1) ? _newRow + 1 : _newRow - 1;

            // Locate the captured pawn
            var capturedPawn = Chess_Board.Children
                .OfType<Image>()
                .FirstOrDefault(img => Grid.GetRow(img) == capturedRow && Grid.GetColumn(img) == _newColumn);

            if (capturedPawn != null)
            {
                _takenPiece = capturedPawn.Name;
                _capturedPiece = capturedPawn;

                // Remove visually & logically
                _capturedPiece.Visibility = Visibility.Collapsed;
                _capturedPiece.IsHitTestVisible = false;
                _capturedPiece.IsEnabled = false;
                Chess_Board.Children.Remove(_capturedPiece);

                _enPassant = true;
            }
        }

        /// <summary>
        /// Finalizes a move: flips the side-to-move, updates clocks/FEN, animates the move,
        /// and (if castling) animates the rook as well. Also clears en passant unless created this move
        /// and highlights the move with a callout.
        /// </summary>
        /// <param name="activePiece">The piece that completed the move.</param>
        /// <remarks>✅ Updated on 8/19/2025</remarks>
        public async Task FinalizeMoveAsync(Image activePiece)
        {
            // Switch side to move
            _move = 1 - _move;

            // Clear en passant square unless one was created by a double pawn push
            if (!_enPassantCreated) EnPassantSquare.Clear();

            // Halfmove clock: reset on capture or pawn move; otherwise increment
            _halfmove = (_capture || _clickedPawn != null) ? 0 : _halfmove + 1;

            // Fullmove number increases after Black completes a move
            if (_move == 1) _fullmove++;

            // Record SAN/PGN start square; update FEN snapshot.
            _startFile = (char)('a' + _oldColumn);
            _startRank = (8 - _oldRow).ToString();
            _startPosition = $"{_startFile}{_startRank}";
            FENCode();

            // If the user isn't moving or the user isn't confirming moves, animate & callout now
            if (!_userTurn || !_moveConfirm)
            {
                // Animate the piece to its destination
                Grid.SetRow(activePiece, _oldRow);
                Grid.SetColumn(activePiece, _oldColumn);
                await MovePieceAsync(activePiece, _newRow, _newColumn, _oldRow, _oldColumn);

                if (_kingCastle || _queenCastle)
                {
                    bool kingside = _kingCastle;
                    bool blackJustMoved = (_move == 1);

                    // Callout uses rook file for castle visualization
                    MoveCallout(_oldRow, _oldColumn, _oldRow, kingside ? 7 : 0);

                    await MoveCastleRookAsync(blackJustMoved, kingside);
                }
                else
                {
                    MoveCallout(_oldRow, _oldColumn, _newRow, _newColumn);
                }
            }
        }

        /// <summary>
        /// Reverts a tentative move (after user rejection).
        /// Restores piece locations, castling rights, captured pieces, and promotion state.
        /// </summary>
        /// <param name="activePiece">The piece that was moved.</param>
        /// <param name="castlingRights">Previous castling rights in order: WR1, WK, WR2, BR1, BK, BR2.</param>
        /// <remarks>✅ Updated on 8/19/2025</remarks>
        private void UndoMove(Image activePiece, int[] castlingRights)
        {
            if (activePiece is null) return;

            // Re-enable board interaction
            Chess_Board.IsHitTestVisible = true;

            // Put the moved piece back
            Grid.SetRow(activePiece, _oldRow);
            Grid.SetColumn(activePiece, _oldColumn);

            // Restore castling rights (defensive: ensure array shape)
            if (castlingRights is { Length: 6 })
            {
                _cWR1 = castlingRights[0];
                _cWK = castlingRights[1];
                _cWR2 = castlingRights[2];
                _cBR1 = castlingRights[3];
                _cBK = castlingRights[4];
                _cBR2 = castlingRights[5];
            }

            // If the move was a castle attempt, put the rook back
            if (_kingCastle || _queenCastle)
                HandleRookReset();

            // If a capture or en passant was undone, restore the captured piece
            if (_capture || _enPassant)
            {
                _capturedPiece.Visibility = Visibility.Visible;
                _capturedPiece.IsHitTestVisible = true;
                _capturedPiece.IsEnabled = true;
                Chess_Board.Children.Add(_capturedPiece);
            }

            // If a promotion was undone, restore the pawn's sprite, name, events, and counts
            if (_promoted)
            {
                bool whiteToMove = (_move == 1);

                string pawnImagePath = System.IO.Path.Combine(
                    _executableDirectory, "Assets", "Pieces",
                    _preferences.Pieces, $"{(whiteToMove ? "White" : "Black")}Pawn.png");
                _clickedPawn.Source = new BitmapImage(new Uri(pawnImagePath));
                _clickedPawn.Name = _pawnName;

                // Restore handlers: pawn regains Pawn handler, remove promoted-piece handler
                _clickedPawn.MouseUp += ChessPawn_Click;

                if (_clickedButtonName.StartsWith("Queen", StringComparison.Ordinal))
                {
                    _clickedPawn.MouseUp -= ChessQueen_Click;
                    if (whiteToMove) _numWQ--; else _numBQ--;
                }
                else if (_clickedButtonName.StartsWith("Knight", StringComparison.Ordinal))
                {
                    _clickedPawn.MouseUp -= ChessKnight_Click;
                    if (whiteToMove) _numWN--; else _numBN--;
                }
                else if (_clickedButtonName.StartsWith("Rook", StringComparison.Ordinal))
                {
                    _clickedPawn.MouseUp -= ChessRook_Click;
                    if (whiteToMove) _numWR--; else _numBR--;
                }
                else
                {
                    _clickedPawn.MouseUp -= ChessBishop_Click;
                    if (whiteToMove) _numWB--; else _numBB--;
                }
            }

            // Clear selection and transient flags
            DeselectPieces();
            _capturedPiece = null;
            _capture = false;
            _enPassant = false;
            _promoted = false;
            _kingCastle = false;
            _queenCastle = false;
        }

        #endregion

        #region Piece Movement and Animation

        /// <summary>
        /// Animates a chess piece's movement from its current position to a target position.
        /// Uses asynchronous animations with transform groups to smoothly translate and rotate the piece.
        /// </summary>
        /// <param name="piece">The chess piece image to animate.</param>
        /// <param name="newRow">The destination row.</param>
        /// <param name="newColumn">The destination column.</param>
        /// <param name="oldRow">The origin row.</param>
        /// <param name="oldColumn">The origin column.</param>
        /// <remarks>✅ Updated on 7/22/2025</remarks>
        private async Task MovePieceAsync(Image piece, int newRow, int newColumn, int oldRow, int oldColumn)
        {
            _theta = _flip == 1 ? 180 : 0;

            // Calculate movement distances
            var offsetX = (newColumn - oldColumn) * Chess_Board.ColumnDefinitions[0].ActualWidth;
            var offsetY = (newRow - oldRow) * Chess_Board.RowDefinitions[0].ActualHeight;

            // Create transformations
            TranslateTransform translateTransform = new();
            RotateTransform rotateTransform = new();

            TransformGroup transformGroup = new();
            transformGroup.Children.Add(rotateTransform);
            transformGroup.Children.Add(translateTransform);
            piece.RenderTransform = transformGroup;

            // Define animations
            var horizontalAnimation1 = new DoubleAnimation(0, offsetX, TimeSpan.FromSeconds(0.15));
            var verticalAnimation1 = new DoubleAnimation(0, offsetY, TimeSpan.FromSeconds(0.15));
            var horizontalAnimation2 = new DoubleAnimation(0, 0, TimeSpan.FromSeconds(0));
            var verticalAnimation2 = new DoubleAnimation(0, 0, TimeSpan.FromSeconds(0));

            await Application.Current.Dispatcher.InvokeAsync(async () =>
            {
                var tcsH1 = new TaskCompletionSource<object>();
                var tcsV1 = new TaskCompletionSource<object>();

                horizontalAnimation1.Completed += (_, _) => tcsH1.SetResult(null!);
                verticalAnimation1.Completed += (_, _) => tcsV1.SetResult(null!);

                translateTransform.BeginAnimation(TranslateTransform.XProperty, horizontalAnimation1);
                translateTransform.BeginAnimation(TranslateTransform.YProperty, verticalAnimation1);

                rotateTransform.Angle = _theta;

                await Task.WhenAll(tcsH1.Task, tcsV1.Task);

                var tcsH2 = new TaskCompletionSource<object>();
                var tcsV2 = new TaskCompletionSource<object>();

                horizontalAnimation2.Completed += (_, _) => tcsH2.SetResult(null!);
                verticalAnimation2.Completed += (_, _) => tcsV2.SetResult(null!);

                translateTransform.BeginAnimation(TranslateTransform.XProperty, horizontalAnimation2);
                translateTransform.BeginAnimation(TranslateTransform.YProperty, verticalAnimation2);

                Grid.SetRow(piece, newRow);
                Grid.SetColumn(piece, newColumn);

                await Task.WhenAll(tcsH2.Task, tcsV2.Task);
            });
        }

        /// <summary>
        /// Animates the rook during castling based on which side (king/queen) and who just moved.
        /// </summary>
        /// <param name="blackJustMoved">True if Black made the king move; otherwise White did.</param>
        /// <param name="kingside">True for kingside castling, false for queenside.</param>
        /// <remarks>✅ Written on 8/19/2025</remarks>
        private async Task MoveCastleRookAsync(bool blackJustMoved, bool kingside)
        {
            string rookName = blackJustMoved
                ? (kingside ? "BlackRook2" : "BlackRook1")
                : (kingside ? "WhiteRook2" : "WhiteRook1");

            int rookStartCol = kingside ? 7 : 0;
            int rookTargetCol = kingside ? _newColumn - 1 : _newColumn + 1;

            var rook = Chess_Board.Children.OfType<Image>().FirstOrDefault(img => img.Name == rookName);
            if (rook is null) return;

            Grid.SetRow(rook, _oldRow);
            Grid.SetColumn(rook, rookStartCol);
            await MovePieceAsync(rook, _newRow, rookTargetCol, _oldRow, rookStartCol);
        }

        #endregion

        #region Event Handlers

        /// <summary>
        /// Enables fullscreen mode and refreshes the layout to reflect the new window style.
        /// </summary>
        /// <param name="sender">The UI element that triggered the event.</param>
        /// <param name="e">Teh routed event arguments.</param>
        /// <remarks>✅ Verified on 6/11/2025</remarks>
        private void Fullscreen(object sender, RoutedEventArgs e)
        {
            mainWindow.WindowStyle = WindowStyle.None;

            // Refresh the settings panel to ensure proper layout scaling
            SettingsInterface.IsOpen = false;
            SettingsInterface.IsOpen = true;

            // Redraw evaluation bar to fit the new layout
            UpdateEvalBar();
        }

        /// <summary>
        /// Exits fullscreen mode when the Escape key is pressed.
        /// </summary>
        /// <param name="sender">The UI element that triggered the event.</param>
        /// <param name="e">The routed event arguments.</param>
        /// <remarks>✅ Verified on 6/11/2025</remarks>
        private void Window_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Escape)
            {
                // Update UI state
                FullscreenEnable.IsChecked = false;

                // Restore default window border
                mainWindow.WindowStyle = WindowStyle.SingleBorderWindow;
            }
        }

        /// <summary>
        /// Restores windowed mode and reformats the screen layout accordingly.
        /// </summary>
        /// <param name="sender">The UI element that triggered the event.</param>
        /// <param name="e">The routed event arguments.</param>
        /// <remarks>✅ Verified on 6/11/2025</remarks>
        private void Windowed(object sender, RoutedEventArgs e)
        {
            // Restore window border
            mainWindow.WindowStyle = WindowStyle.SingleBorderWindow;

            // Refresh the settings panel if currently open
            if (SettingsInterface.IsOpen)
            {
                SettingsInterface.IsOpen = false;
                SettingsInterface.IsOpen = true;
            }

            // Reapply evaluation bar alignment or styling
            UpdateEvalBar();
        }

        /// <summary>
        /// Closes the settings panel when the user clicks outside of it.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The mouse event data.</param>
        /// <remarks>✅ Updated on 7/22/2025</remarks>
        private void MainWindow_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (SettingsInterface.IsOpen && !SettingsInterface.IsMouseOver)
            {
                SettingsInterface.IsOpen = false;
                Settings.IsEnabled = true;
            }
        }

        /// <summary>
        /// Event handler that delegates to <see cref="FlipBoard()"/> so the baord
        /// can also be flipped by UI events (e.g. menu clicks, button presses).
        /// </summary>
        /// <param name="sender">The control or object that raised the event.</param>
        /// <param name="e">The event arguments associated with the event.</param>
        /// <remarks>✅ Updated on 8/18/2025</remarks>
        public void FlipBoard(object sender, EventArgs e) => FlipBoard();

        /// <summary>
        /// Toggles the Stockfish evaluation window visibility.
        /// Also calls <see cref="UpdateEvalBar"/> to refresh the evaluation bar display.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>✅ Updated on 7/22/2025</remarks>
        private void EvaluationInterface(object sender, EventArgs e)
        {
            EngineEvaluation.IsOpen = !EngineEvaluation.IsOpen;
            UpdateEvalBar();
        }

        /// <summary>
        /// Updates UI combo boxes and controls based on the selected play type.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void PlayTypeChanged(object sender, EventArgs e)
        {
            _inactivityTimer.Stop();

            // Cache current selections
            _selectedPlayType = (ComboBoxItem)Play_Type.SelectedItem;
            _selectedElo = (ComboBoxItem)Elo.SelectedItem;
            _selectedColor = (ComboBoxItem)Color.SelectedItem;
            _selectedWhiteElo = (ComboBoxItem)WhiteCPUElo.SelectedItem;
            _selectedBlackElo = (ComboBoxItem)BlackCPUElo.SelectedItem;

            string? playType = _selectedPlayType?.Content?.ToString();

            if (playType is "Com Vs. Com" or "User Vs. Com")
            {
                bool isUserVsCom = playType == "User Vs. Com";

                // Update visibility and interactivity
                UvCorUvU.Visibility = isUserVsCom ? Visibility.Visible : Visibility.Collapsed;
                UvCorUvU.IsEnabled = isUserVsCom;
                CvC.Visibility = isUserVsCom ? Visibility.Collapsed : Visibility.Visible;
                CvC.IsEnabled = !isUserVsCom;
                Elo.IsEnabled = isUserVsCom;
                Color.IsEnabled = isUserVsCom;
                WhiteCPUElo.IsEnabled = !isUserVsCom;
                BlackCPUElo.IsEnabled = !isUserVsCom;
                PlayButton.IsEnabled = false;
                ResumeButton.IsEnabled = false;

                // Clear irrelevant selections
                if (isUserVsCom)
                {
                    WhiteCPUElo.SelectedItem = null;
                    BlackCPUElo.SelectedItem = null;
                }
                else
                {
                    Elo.SelectedItem = null;
                    Color.SelectedItem = null;
                }
            }
            else if (playType == "User Vs. User")
            {
                // Enable User Vs. User settings
                UvCorUvU.Visibility = Visibility.Visible;
                UvCorUvU.IsEnabled = true;
                CvC.Visibility = Visibility.Collapsed;
                CvC.IsEnabled = false;
                Elo.IsEnabled = false;
                Color.IsEnabled = false;
                WhiteCPUElo.IsEnabled = false;
                BlackCPUElo.IsEnabled = false;

                // Resume or play based on pause state
                if (!_isPaused)
                    PlayButton.IsEnabled = true;
                else
                    ResumeButton.IsEnabled = true;

                // Clear irrelevant selections
                Elo.SelectedItem = null;
                Color.SelectedItem = null;
            }

            _inactivityTimer.Start();
        }

        /// <summary>
        /// Enables the play button only if all required dropdowns are selected based on the play type.
        /// </summary>
        /// <param name="sender">The source of the event.</param>
        /// <param name="e">The event data.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void CheckDropdownSelections(object sender, EventArgs e)
        {
            _inactivityTimer.Stop();
            _inactivityTimer.Start();

            _selectedElo = Elo.SelectedItem as ComboBoxItem;
            _selectedColor = Color.SelectedItem as ComboBoxItem;
            _selectedWhiteElo = WhiteCPUElo.SelectedItem as ComboBoxItem;
            _selectedBlackElo = BlackCPUElo.SelectedItem as ComboBoxItem;

            string? playType = _selectedPlayType?.Content?.ToString();

            switch (playType)
            {
                case "Com Vs. Com":
                    TogglePlayButtons(_selectedWhiteElo != null && _selectedBlackElo != null);
                    break;

                case "User Vs. Com":
                    TogglePlayButtons(_selectedElo != null && _selectedColor != null);
                    break;

                default:
                    TogglePlayButtons(false);
                    break;
            }
        }

        /// <summary>
        /// Handles inactivity timeout by stopping the timer, randomizing Elo,
        /// setting the game mode to Computer vs. Computer, resetting relevant UI elements,
        /// and auto-starting or resuming the game based on pause state.
        /// </summary>
        /// <param name="sender">The source of the event (typically the timer).</param>
        /// <param name="e">Event data associated with the timer tick.</param>
        /// <remarks>
        /// This method is triggered when the inactivity timer elapses.
        /// It prepares the game for autonomous play and ensures the UI reflects the new state.
        /// <para>✅ Verified on 6/11/2025</para>
        /// </remarks>
        private void InactivityTimer_Tick(object? sender, EventArgs e)
        {
            _inactivityTimer.Stop();

            // Randomize difficulty and set mode
            AssignRandomElo();
            Play_Type.SelectedIndex = (int)GameMode.ComVsCom;

            // Reset UI state
            ToggleUIState(false);
            PlayButton.IsEnabled = true;
            ResumeButton.IsEnabled = false;
            Elo.SelectedItem = null;
            Color.SelectedItem = null;

            // Start or resume the game
            if (!_isPaused)
            {
                ChessLog.LogInformation("Inactivity timeout reached. Starting new game.");
                SimulateStartClick(PlayButton);
            }
            else
            {
                ChessLog.LogInformation("Inactivity timout reached. Resuming game.");
                ResumeButton.IsEnabled = true;
                SimulateStartClick(ResumeButton);
            }
        }

        /// <summary>
        /// Handles the user selecting a pawn. Clears other selections, 
        /// checks if it's the correct turn, and highlights valid move squares.
        /// </summary>
        /// <remarks>✅ Updated on 7/23/2025</remarks>
        private void ChessPawn_Click(Object sender, MouseButtonEventArgs e)
        {
            EraseAnnotations();
            DeselectPieces();

            if (sender is not Image clickedPawn)
                return;

            _clickedPawn = clickedPawn;

            int row = Grid.GetRow(_clickedPawn);
            int column = Grid.GetColumn(_clickedPawn);
            SelectedPiece(row, column);

            bool isWhite = _clickedPawn.Name.StartsWith("White");

            // If it's not the player's turn, deselect the pawn and return
            if ((isWhite && _move == 0) || (!isWhite && _move == 1))
            {
                _clickedPawn = null;
                return;
            }

            // Disable opponent pieces to allow square selection
            EnableImagesWithTag(isWhite ? "BlackPiece" : "WhitePiece", false);
        }

        /// <summary>
        /// Handles the user selecting a knight. Clears other selections, 
        /// checks if it's the correct turn, and highlights valid move squares.
        /// </summary>
        /// <remarks>✅ Updated on 7/23/2025</remarks>
        private void ChessKnight_Click(Object sender, MouseButtonEventArgs e)
        {
            EraseAnnotations();
            DeselectPieces();

            if (sender is not Image clickedKnight)
                return;

            _clickedKnight = clickedKnight;

            int row = Grid.GetRow(_clickedKnight);
            int column = Grid.GetColumn(_clickedKnight);
            SelectedPiece(row, column);

            bool isWhite = _clickedKnight.Name.StartsWith("White");

            // If it's not the player's turn, deselect the knight and return
            if ((isWhite && _move == 0) || (!isWhite && _move == 1))
            {
                _clickedKnight = null;
                return;
            }

            // Disable opponent pieces to allow square selection
            EnableImagesWithTag(isWhite ? "BlackPiece" : "WhitePiece", false);
        }

        /// <summary>
        /// Handles the user selecting a bishop. Clears other selections, 
        /// checks if it's the correct turn, and highlights valid move squares.
        /// </summary>
        /// <remarks>✅ Updated on 7/23/2025</remarks>
        private void ChessBishop_Click(Object sender, MouseButtonEventArgs e)
        {
            EraseAnnotations();
            DeselectPieces();

            if (sender is not Image clickedBishop)
                return;

            _clickedBishop = clickedBishop;

            int row = Grid.GetRow(_clickedBishop);
            int column = Grid.GetColumn(_clickedBishop);
            SelectedPiece(row, column);

            bool isWhite = _clickedBishop.Name.StartsWith("White");

            // If it's not the player's turn, deselect the bishop and return
            if ((isWhite && _move == 0) || (!isWhite && _move == 1))
            {
                _clickedBishop = null;
                return;
            }

            // Disable opponent pieces to allow square selection
            EnableImagesWithTag(isWhite ? "BlackPiece" : "WhitePiece", false);
        }

        /// <summary>
        /// Handles the user selecting a rook. Clears other selections, 
        /// checks if it's the correct turn, and highlights valid move squares.
        /// </summary>
        /// <remarks>✅ Updated on 7/23/2025</remarks>
        private void ChessRook_Click(Object sender, MouseButtonEventArgs e)
        {
            EraseAnnotations();
            DeselectPieces();

            if (sender is not Image clickedRook)
                return;

            _clickedRook = clickedRook;

            int row = Grid.GetRow(_clickedRook);
            int column = Grid.GetColumn(_clickedRook);
            SelectedPiece(row, column);

            bool isWhite = _clickedRook.Name.StartsWith("White");

            // If it's not the player's turn, deselect the rook and return
            if ((isWhite && _move == 0) || (!isWhite && _move == 1))
            {
                _clickedRook = null;
                return;
            }

            // Disable opponent pieces to allow square selection
            EnableImagesWithTag(isWhite ? "BlackPiece" : "WhitePiece", false);
        }

        /// <summary>
        /// Handles the user selecting a queen. Clears other selections, 
        /// checks if it's the correct turn, and highlights valid move squares.
        /// </summary>
        /// <remarks>✅ Updated on 7/23/2025</remarks>
        private void ChessQueen_Click(Object sender, MouseButtonEventArgs e)
        {
            EraseAnnotations();
            DeselectPieces();

            if (sender is not Image clickedQueen)
                return;

            _clickedQueen = clickedQueen;

            int row = Grid.GetRow(_clickedQueen);
            int column = Grid.GetColumn(_clickedQueen);
            SelectedPiece(row, column);

            bool isWhite = _clickedQueen.Name.StartsWith("White");

            // If it's not the player's turn, deselect the queen and return
            if ((isWhite && _move == 0) || (!isWhite && _move == 1))
            {
                _clickedQueen = null;
                return;
            }

            // Disable opponent pieces to allow square selection
            EnableImagesWithTag(isWhite ? "BlackPiece" : "WhitePiece", false);
        }

        /// <summary>
        /// Handles the user selecting a king. Clears other selections, 
        /// checks if it's the correct turn, and highlights valid move squares.
        /// </summary>
        /// <remarks>✅ Updated on 7/23/2025</remarks>
        private void ChessKing_Click(Object sender, MouseButtonEventArgs e)
        {
            EraseAnnotations();
            DeselectPieces();

            if (sender is not Image clickedKing)
                return;

            _clickedKing = clickedKing;

            int row = Grid.GetRow(_clickedKing);
            int column = Grid.GetColumn(_clickedKing);
            SelectedPiece(row, column);

            bool isWhite = _clickedKing.Name.StartsWith("White");

            // If it's not the player's turn, deselect the king and return
            if ((isWhite && _move == 0) || (!isWhite && _move == 1))
            {
                _clickedKing = null;
                return;
            }

            // Disable opponent pieces to allow square selection
            EnableImagesWithTag(isWhite ? "BlackPiece" : "WhitePiece", false);
        }

        /// <summary>
        /// Handles a board-square click to complete a move.
        /// Validates the move for the currently selected piece, ensures the king is not left in check,
        /// applies the move (including special rules handled downstream), and then dispatches to the
        /// correct move handler (<see cref="PawnMoveManagerAsync"/> for pawns, <see cref="MoveManagerAsync"/> for others).
        /// </summary>
        /// <param name="sender">The clicked square (a <see cref="Button"/>).</param>
        /// <param name="e">Routed event args.</param>
        /// <remarks>✅ Updated on 8/18/2025</remarks>
        private async void Square_ClickAsync(object sender, RoutedEventArgs e)
        {
            EraseAnnotations();

            // Require a selected piece and a square button
            Image? selectedPiece =
                _clickedPawn ?? _clickedKnight ?? _clickedBishop ??
                _clickedRook ?? _clickedQueen ?? _clickedKing;

            if (selectedPiece == null) return;
            if (sender is not Button clickedSquare) return;

            // From & To grid coords
            _oldRow = Grid.GetRow(selectedPiece);
            _oldColumn = Grid.GetColumn(selectedPiece);
            _newRow = Grid.GetRow(clickedSquare);
            _newColumn = Grid.GetColumn(clickedSquare);

            // Human-readable (e.g., "e4")
            _endFile = (char)(_newColumn + 'a');
            _endRank = (8 - _newRow).ToString();
            _endPosition = $"{_endFile}{_endRank}";

            // Validate move based on piece type (avoid per-click dictionary allocation)
            bool IsValidMove()
            {
                if (ReferenceEquals(selectedPiece, _clickedPawn))
                    return new PawnValidMove.PawnValidation(Chess_Board, this)
                        .ValidateMove(_oldRow, _oldColumn, _newRow, _newColumn, _move);

                if (ReferenceEquals(selectedPiece, _clickedKnight))
                    return new KnightValidMove.KnightValidation()
                        .ValidateMove(_oldRow, _oldColumn, _newRow, _newColumn);

                if (ReferenceEquals(selectedPiece, _clickedBishop))
                    return new BishopValidMove.BishopValidation(Chess_Board, this)
                        .ValidateMove(_oldRow, _oldColumn, _newRow, _newColumn);

                if (ReferenceEquals(selectedPiece, _clickedRook))
                    return new RookValidMove.RookValidation(Chess_Board, this)
                        .ValidateMove(_oldRow, _oldColumn, _newRow, _newColumn);

                if (ReferenceEquals(selectedPiece, _clickedQueen))
                    return new QueenValidMove.QueenValidation(Chess_Board, this)
                        .ValidateMove(_oldRow, _oldColumn, _newRow, _newColumn);

                if (ReferenceEquals(selectedPiece, _clickedKing))
                    return new KingValidMove.KingValidation(Chess_Board, this)
                        .ValidateMove(_move, _oldRow, _oldColumn, _newRow, _newColumn,
                                      _cWK, _cBK, _cWR1, _cWR2, _cBR1, _cBR2);

                return false;
            }

            if (!IsValidMove())
                return;

            // Apply tentative board change for check verification
            Grid.SetRow(selectedPiece, _newRow);
            Grid.SetColumn(selectedPiece, _newColumn);

            // Track king squares
            if (selectedPiece.Name.StartsWith("WhiteKing", StringComparison.Ordinal))
            {
                _whiteKingRow = _newRow;
                _whiteKingColumn = _newColumn;
            }
            else if (selectedPiece.Name.StartsWith("BlackKing", StringComparison.Ordinal))
            {
                _blackKingRow = _newRow;
                _blackKingColumn = _newColumn;
            }

            // Ensure the move does not leave your king in check
            var checkValidator = new CheckVerification.Check(Chess_Board, this);
            bool positionOk = checkValidator.ValidatePosition(
                _move, _whiteKingRow, _whiteKingColumn, _blackKingRow, _blackKingColumn, _newRow, _newColumn);

            if (!positionOk)
            {
                // Revert tentative move
                Grid.SetRow(selectedPiece, _oldRow);
                Grid.SetColumn(selectedPiece, _oldColumn);

                // Revert king tracking if we updated it
                if (selectedPiece.Name.StartsWith("WhiteKing", StringComparison.Ordinal))
                {
                    _whiteKingRow = _oldRow;
                    _whiteKingColumn = _oldColumn;
                }
                else if (selectedPiece.Name.StartsWith("BlackKing", StringComparison.Ordinal))
                {
                    _blackKingRow = _oldRow;
                    _blackKingColumn = _oldColumn;
                }

                if (_pieceSounds) PlaySound("PieceIllegal");
                return;
            }

            Chess_Board.IsHitTestVisible = false;
            _activePiece = selectedPiece.Name;

            // Dispatch to the correct move manager
            if (selectedPiece.Name.Contains("Pawn", StringComparison.Ordinal))
                await PawnMoveManagerAsync(selectedPiece);
            else
                await MoveManagerAsync(selectedPiece);
        }

        #endregion

        #region Settings Panel Handlers

        /// <summary>
        /// Opens the settings panel and disables the settings button to prevent re-entry.
        /// </summary>
        /// <param name="sender">The UI element that triggered the event.</param>
        /// <param name="e">Event data associated with the action.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        /// 
        private void SettingsMenu(object sender, EventArgs e)
        {
            SettingsInterface.IsOpen = true;
            Settings.IsEnabled = false;
        }

        /// <summary>
        /// Updates the piece sounds setting based on the checkbox state and saves the updated preferences.
        /// </summary>
        /// <param name="sender">The checkbox triggering the event.</param>
        /// <param name="e">The event arguments (unused).</param>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        private void PieceSoundsSetting(object sender, EventArgs e)
        {
            if (sender is not CheckBox checkBox)
                return;

            // Update in-memory preference
            _pieceSounds = checkBox.IsChecked == true;
            _preferences.PieceSounds = _pieceSounds;

            // Save updated preferences to file
            PreferencesManager.Save(_preferences);
        }

        /// <summary>
        /// Updates the confirm move setting based on the checkbox state and saves the updated preferences.
        /// </summary>
        /// <param name="sender">The checkbox triggering the event.</param>
        /// <param name="e">The event arguments (unused).</param>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        private void ConfirmMoveSetting(object sender, EventArgs e)
        {
            if (sender is not CheckBox checkBox)
                return;

            // Update in-memory preference
            _moveConfirm = checkBox.IsChecked == true;
            _preferences.ConfirmMove = _moveConfirm;

            // Save updated preferences to file
            PreferencesManager.Save(_preferences);
        }

        /// <summary>
        /// Handles Epson RC connection status and UI updates.
        /// Displays "In Progress" status light while attempting connection.
        /// </summary>
        /// <param name="sender">The UI element that triggered the event.</param>
        /// <param name="e">Event data associated with the action.</param>
        /// <remarks>✅ Updated on 8/19/2025</remarks>
        private async void EpsonRcAsync(object sender, EventArgs e)
        {
            if (sender is not CheckBox checkBox)
                return;

            // Stop inactivity timer and indicate attempt
            _inactivityTimer.Stop();
            SetStatusLights(Brushes.Yellow, Brushes.Yellow);
            DisableUI();

            // Preserve resume/play state
            StorePlayState();

            // Begin animated feedback
            EpsonRCRect.Height = 45;
            AttemptingConnection.Visibility = Visibility.Visible;
            UpdateRectangleClip();

            // Toggle RobotComm state and attempt communication
            _robotComm = !_robotComm;
            await AttemptCommunication(checkBox);

            // Restore UI
            EnableUI();
        }

        #endregion

        #region UI Helpers

        /// <summary>
        /// Flips the board view 180° so the opposite side is "at the bottom".
        /// Rotates the board surface, rank/file labels, and all piece images;
        /// updates the evaluation bar to match the new perspective.
        /// </summary>
        /// <remarks>✅ Updated on 8/18/2025</remarks>
        public void FlipBoard()
        {
            // Choose rotation based on current flip state
            int angle = (_flip == 0) ? 180 : 0;
            var rotation = new RotateTransform(angle);

            // Board surface
            Board.LayoutTransform = rotation;

            // Rank/file labels (rotate around center)
            var labels = new[]
            {
                aFile, bFile, cFile, dFile, eFile, fFile, gFile, hFile,
                firstRank, secondRank, thirdRank, fourthRank,
                fifthRank, sixthRank, seventhRank, eighthRank
            };

            foreach (var tb in labels)
            {
                tb.RenderTransform = rotation;
            }

            // Piece images
            RotateImagesWithTag("WhitePiece", true);
            RotateImagesWithTag("BlackPiece", true);

            // Toggle state and refresh evaluation UI
            _flip = 1 - _flip;
            UpdateEvalBar();
        }

        /// <summary>
        /// Sets the fill color of both robot status indicators to reflect connection state.
        /// </summary>
        /// <param name="whiteStatus">The brush color to apply to the white robot's status light.</param>
        /// <param name="blackStatus">The brush color ro apply to the black robot's status light.</param>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><c>Green</c>: Connected</item>
        ///     <item><c>Red</c>: Disconnected</item>
        ///     <item><c>Yellow</c>: Attempting connection</item>
        /// </list>
        /// <para>✅ Updated on 7/18/2025</para>
        /// </remarks>
        private void SetStatusLights(Brush whiteStatus, Brush blackStatus)
        {
            uoStatus.Fill = whiteStatus;
            osuStatus.Fill = blackStatus;
        }

        /// <summary>
        /// Disables key UI elements during an Epson RC+ connection attempt.
        /// </summary>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void DisableUI()
        {
            EpsonRCConnection.IsChecked = false;
            EpsonRCConnection.IsEnabled = false;
            Play_Type.IsEnabled = false;

            TogglePlayTypeUI(false);
        }

        /// <summary>
        /// Re-enables key UI elements after completing an Epson RC+ connection attempt.
        /// </summary>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void EnableUI()
        {
            Play_Type.IsEnabled = true;

            RestorePlayState();
            TogglePlayTypeUI(true);
        }

        /// <summary>
        /// Updates the clipping geometry of the <see cref="EpsonRCRect"/> rectangle to support smooth animation effects.
        /// </summary>
        /// <remarks>
        /// Modifies the clip region's dimensions and corner radius for consistent visual behavior
        /// during connection state transitions.
        /// <para>✅ Updated on 7/18/2025</para>
        /// </remarks>
        private void UpdateRectangleClip()
        {
            if (FindName("EpsonRCRect") is Rectangle epsonRCRect && epsonRCRect.Clip is RectangleGeometry clipGeometry)
            {
                clipGeometry.Rect = new Rect(0, -10, 180, 55);
                clipGeometry.RadiusX = 5;
                clipGeometry.RadiusY = 5;
            }
        }

        /// <summary>
        /// Toggles visibility of the "Setup in Progress" popup and its associated UI elements.
        /// </summary>
        /// <param name="isVisible">if <c>true</c>, the popup is shown; otherwise, it is hidden.</param>
        /// <remarks>✅ Written on 7/18/2025</remarks>
        private void ShowSetupPopup(bool isVisible)
        {
            InfoSymbol.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            SetupText.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            MoveInProgRect.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Toggles the visibility of the "Move in Progress" popup and its associated UI elements.
        /// </summary>
        /// <param name="isVisible">If <c>true</c>, the popup is shown; otherwise, it is hidden.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void ShowMoveInProgressPopup(bool isVisible)
        {
            InfoSymbol.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            InProgressText.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            MoveInProgRect.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Toggles visibility of the "Cleanup in Progress" popup elements.
        /// </summary>
        /// <param name="isVisible">True to show the popup; false to hide it.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void ShowCleanupPopup(bool isVisible)
        {
            InfoSymbol.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            CleanupText.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
            MoveInProgRect.Visibility = isVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        /// <summary>
        /// Enables or disables the appropriate UI elements based on the selected play type.
        /// </summary>
        /// <param name="isEnabled">True to enable, false to disable.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void TogglePlayTypeUI(bool isEnabled)
        {
            if (Play_Type.SelectedIndex == -1)
            {
                UvCorUvU.IsEnabled = isEnabled;
                return;
            }

            string? playType = _selectedPlayType?.Content?.ToString();

            if (playType == "Com vs. Com")
            {
                CvC.IsEnabled = isEnabled;
            }
            else
            {
                UvCorUvU.IsEnabled = isEnabled;
            }
        }

        /// <summary>
        /// Toggles the visibility and interactivity of UI elements based on whether a human user is playing.
        /// </summary>
        /// <param name="userPlaying">
        /// <c>true</c> if a user is playing.
        /// <c>false</c> if the game is running in CPU vs CPU mode.
        /// </param>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        private void ToggleUIState(bool userPlaying)
        {
            // Show or hide the appropriate game mode controls
            UvCorUvU.Visibility = userPlaying ? Visibility.Visible : Visibility.Collapsed;
            CvC.Visibility = userPlaying ? Visibility.Collapsed : Visibility.Visible;

            // Enable or disable the controls accordingly
            UvCorUvU.IsEnabled = userPlaying;
            CvC.IsEnabled = !userPlaying;
            Elo.IsEnabled = userPlaying;
            Color.IsEnabled = userPlaying;
            WhiteCPUElo.IsEnabled = !userPlaying;
            BlackCPUElo.IsEnabled = !userPlaying;
        }

        /// <summary>
        /// Rotates all images based on their tag.
        /// </summary>
        /// <param name="tag">The tag identifying the pieces to rotate.</param>
        /// <param name="enable">Determines if the pieces should remain enabled.</param>
        /// <remarks>✅ Updated on 7/23/2025</remarks>
        private void RotateImagesWithTag(string tag, bool enable)
        {
            foreach (Image image in Chess_Board.Children.OfType<Image>())
            {
                if (image.Tag?.ToString() == tag)
                {
                    image.RenderTransform = Board.LayoutTransform;
                    image.IsEnabled = enable;
                }
            }
        }

        /// <summary>
        /// Suspends execution while the user accepts or rejects a proposed move.
        /// </summary>
        /// <returns>True if the user confirms the move, false if it is rejected.</returns>
        /// <remarks>✅ Updated on 7/23/2025</remarks>
        private async Task<bool> WaitForConfirmationAsync()
        {
            // Show and enable confirmation buttons
            Confirm.Visibility = Visibility.Visible;
            Confirm.IsEnabled = true;
            Reject.Visibility = Visibility.Visible;
            Reject.IsEnabled = true;
            PauseButton.IsHitTestVisible = false;

            var confirmationTask = new TaskCompletionSource<bool>();

            // Define event handlers
            void ConfirmHandler(object sender, RoutedEventArgs e)
            {
                CleanupHandlers();
                confirmationTask.SetResult(true);
            }

            void RejectHandler(object sender, RoutedEventArgs e)
            {
                CleanupHandlers();
                confirmationTask.SetResult(false);
            }

            void CleanupHandlers()
            {
                Confirm.Click -= ConfirmHandler;
                Reject.Click -= RejectHandler;
            }

            // Attach event handlers
            Confirm.Click += ConfirmHandler;
            Reject.Click += RejectHandler;

            try
            {
                return await confirmationTask.Task;
            }
            finally
            {
                // Hide and disable confirmation buttons after selection
                Confirm.Visibility = Visibility.Collapsed;
                Confirm.IsEnabled = false;
                Reject.Visibility = Visibility.Collapsed;
                Reject.IsEnabled = false;
                PauseButton.IsHitTestVisible = true;
            }
        }

        #endregion

        #region Annotations

        /// <summary>
        /// Handles right-click annotation input on a chess piece image.
        /// Determines which annotation to apply based on modifier keys.
        /// </summary>
        /// <param name="sender">The image control that was right-clicked.</param>
        /// <param name="e">Mouse button event argument.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void PieceAnnotate(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Image image) return;

            int annotationRow = Grid.GetRow(image);
            int annotationCol = Grid.GetColumn(image);
            ModifierKeys modifiers = Keyboard.Modifiers;

            switch (modifiers)
            {
                case ModifierKeys.Control:
                    CTRLAnnotate(annotationRow, annotationCol);
                    break;

                case ModifierKeys.Alt:
                    ALTAnnotate(annotationRow, annotationCol);
                    break;

                case ModifierKeys.Shift:
                    ShiftAnnotate(annotationRow, annotationCol);
                    break;

                default:
                    RedAnnotate(annotationRow, annotationCol);
                    break;
            }
        }

        /// <summary>
        /// Handles right-click annotation input on a board square.
        /// Determines which annotation to apply based on modifier keys.
        /// </summary>
        /// <param name="sender">The button control that was right-clicked.</param>
        /// <param name="e">Mouse button event argument.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void SquareAnnotate(object sender, MouseButtonEventArgs e)
        {
            if (sender is not Button button) return;

            int annotationRow = Grid.GetRow(button);
            int annotationCol = Grid.GetColumn(button);
            ModifierKeys modifiers = Keyboard.Modifiers;

            switch (modifiers)
            {
                case ModifierKeys.Control:
                    CTRLAnnotate(annotationRow, annotationCol);
                    break;

                case ModifierKeys.Alt:
                    ALTAnnotate(annotationRow, annotationCol);
                    break;

                case ModifierKeys.Shift:
                    ShiftAnnotate(annotationRow, annotationCol);
                    break;

                default:
                    RedAnnotate(annotationRow, annotationCol);
                    break;
            }
        }

        /// <summary>
        /// Toggles an orange annotation on the specified square when the user CTRL-clicks.
        /// Hides other overlay types (ALT, Shift, Red) on the same square.
        /// </summary>
        /// <param name="row">The row index of the square (0-7).</param>
        /// <param name="col">The column index of the square (0-7).</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void CTRLAnnotate(int row, int col)
        {
            char annotateCol = (char)(col + 1 + 96);  // Convert to 'a'-'h'
            string annotateRow = (8 - row).ToString();  // Convert to '1'-'8'
            string annotateSquare = $"{annotateCol}{annotateRow}";

            // Find the CTRL overlay rectangle for the square
            var overlay = Chess_Board.Children.OfType<Rectangle>()
                .FirstOrDefault(r => r.Name.Contains($"{annotateSquare}_CTRLOverlay"));

            if (overlay != null)
            {
                overlay.Visibility = overlay.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            // Hide all other overlays (ALT, Shift, Red) on the same square
            foreach (var square in Chess_Board.Children.OfType<Rectangle>())
            {
                if (square.Name.Contains(annotateSquare) &&
                    square.Name.Contains("Overlay") &&
                    !square.Name.Contains("CTRL"))
                {
                    square.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Toggles a blue annotation on the specified square when the user ALT-clicks.
        /// Hides other overlay types (CRTL, Shift, Red) on the same square.
        /// </summary>
        /// <param name="row">The row index of the square (0-7).</param>
        /// <param name="col">The column index of the square (0-7).</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void ALTAnnotate(int row, int col)
        {
            char annotateCol = (char)(col + 1 + 96);  // Convert to 'a'-'h'
            string annotateRow = (8 - row).ToString();  // Convert to '1'-'8'
            string annotateSquare = $"{annotateCol}{annotateRow}";

            // Find the ALT overlay rectangle for the square
            var overlay = Chess_Board.Children.OfType<Rectangle>()
                .FirstOrDefault(r => r.Name.Contains($"{annotateSquare}_ALTOverlay"));

            if (overlay != null)
            {
                overlay.Visibility = overlay.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            // Hide all other overlays (CTRL, Shift, Red) on the same square
            foreach (var square in Chess_Board.Children.OfType<Rectangle>())
            {
                if (square.Name.Contains(annotateSquare) &&
                    square.Name.Contains("Overlay") &&
                    !square.Name.Contains("ALT"))
                {
                    square.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Toggles a green annotation on the specified square when the user Shift-clicks.
        /// Hides other overlay types (CTRL, ALT, Red) on the same square.
        /// </summary>
        /// <param name="row">The row index of the square (0-7).</param>
        /// <param name="col">The column index of the square (0-7).</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void ShiftAnnotate(int row, int col)
        {
            char annotateCol = (char)(col + 1 + 96);  // Convert to 'a'-'h'
            string annotateRow = (8 - row).ToString();  // Convert to '1'-'8'
            string annotateSquare = $"{annotateCol}{annotateRow}";

            // Find the Shift overlay rectangle for the square
            var overlay = Chess_Board.Children.OfType<Rectangle>()
                .FirstOrDefault(r => r.Name.Contains($"{annotateSquare}_ShiftOverlay"));

            if (overlay != null)
            {
                overlay.Visibility = overlay.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            // Hide all other overlays (CTRL, ALT, Red) on the same square
            foreach (var square in Chess_Board.Children.OfType<Rectangle>())
            {
                if (square.Name.Contains(annotateSquare) &&
                    square.Name.Contains("Overlay") &&
                    !square.Name.Contains("Shift"))
                {
                    square.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Toggles a red annotation on the specified square when the user Shift-clicks.
        /// Hides other overlay types (CTRL, ALT, Shift) on the same square.
        /// </summary>
        /// <param name="row">The row index of the square (0-7).</param>
        /// <param name="col">The column index of the square (0-7).</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void RedAnnotate(int row, int col)
        {
            char annotateCol = (char)(col + 1 + 96);  // Convert to 'a'-'h'
            string annotateRow = (8 - row).ToString();  // Convert to '1'-'8'
            string annotateSquare = $"{annotateCol}{annotateRow}";

            // Find the Red overlay rectangle for the square
            var overlay = Chess_Board.Children.OfType<Rectangle>()
                .FirstOrDefault(r => r.Name.Contains($"{annotateSquare}_RedOverlay"));

            if (overlay != null)
            {
                overlay.Visibility = overlay.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            // Hide all other overlays (CTLR, ALT, Shift) on the same square
            foreach (var square in Chess_Board.Children.OfType<Rectangle>())
            {
                if (square.Name.Contains(annotateSquare) &&
                    square.Name.Contains("Overlay") &&
                    !square.Name.Contains("Red"))
                {
                    square.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Toggles a light blue annotation on the specified square when the user selects a piece.
        /// Hides other overlay types (CTRL, ALT, Shift, Red) on the same square.
        /// </summary>
        /// <param name="row">The row index of the square (0-7).</param>
        /// <param name="col">The column index of the square (0-7).</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void SelectedPiece(int row, int col)
        {
            char annotateCol = (char)(col + 1 + 96);  // Convert column index to letter (a-h)
            string annotateRow = (8 - row).ToString();  // Convert row index to chess notation
            string annotateSquare = $"{annotateCol}{annotateRow}";  // Construct coordinate (e.g., "e4")

            // Find the Selected overlay rectangle for the square
            var overlay = Chess_Board.Children.OfType<Rectangle>()
                .FirstOrDefault(r => r.Name.Contains($"{annotateSquare}_SelectedOverlay"));

            if (overlay != null)
            {
                overlay.Visibility = overlay.Visibility == Visibility.Visible
                    ? Visibility.Collapsed
                    : Visibility.Visible;
            }

            // Hide all other overlays (CTRL, ALT, Shift, Red) on the same square
            foreach (var square in Chess_Board.Children.OfType<Rectangle>())
            {
                if (square.Name.Contains(annotateSquare) &&
                    square.Name.Contains("Overlay") &&
                    !square.Name.Contains("Selected"))
                {
                    square.Visibility = Visibility.Collapsed;
                }
            }
        }

        /// <summary>
        /// Highlights two squares yellow to indicate the previous move made on the board.
        /// </summary>
        /// <param name="oldRow">The original row index of the moved piece.</param>
        /// <param name="oldColumn">The original column index of the moved piece.</param>
        /// <param name="newRow">The destination row index of the moved piece.</param>
        /// <param name="newColumn">The destination column index of the moved piece.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void MoveCallout(int oldRow, int oldColumn, int newRow, int newColumn)
        {
            foreach (var square in Chess_Board.Children.OfType<Rectangle>())
            {
                if (!square.Name.Contains("MoveCallout"))
                    continue;

                bool isMoveCallout =
                    (Grid.GetRow(square) == newRow && Grid.GetColumn(square) == newColumn) ||
                    (Grid.GetRow(square) == oldRow && Grid.GetColumn(square) == oldColumn);

                square.Visibility = isMoveCallout ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        /// <summary>
        /// Hides all annotation overlays on the chessboard (e.g., CTRL, ALT, Shift, Red).
        /// </summary>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void EraseAnnotations()
        {
            foreach (var overlay in Chess_Board.Children.OfType<Rectangle>())
            {
                if (overlay.Name.Contains("Overlay"))
                {
                    overlay.Visibility = Visibility.Collapsed;
                }
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Updates the coordinates of all pieces on the chessboard.
        /// Also tracks the positions of the white and black kings.
        /// </summary>
        /// <remarks>✅ Updated on 7/23/2025</remarks>
        public void PiecePositions()
        {
            ImageCoordinates.Clear();

            foreach (var image in Chess_Board.Children.OfType<Image>())
            {
                int row = Grid.GetRow(image);
                int column = Grid.GetColumn(image);

                ImageCoordinates.Add(Tuple.Create(row, column));

                // Directly update king positions
                if (image.Name.StartsWith("WhiteKing"))
                {
                    _whiteKingRow = row;
                    _whiteKingColumn = column;
                }
                else if (image.Name.StartsWith("BlackKing"))
                {
                    _blackKingRow = row;
                    _blackKingColumn = column;
                }
            }
        }

        /// <summary>
        /// Selects the specified theme in a ComboBox if it exists.
        /// </summary>
        /// <param name="comboBox">The ComboBox to update.</param>
        /// <param name="selectedTheme">The theme to select.</param>
        private static void SetComboBoxSelection(ComboBox comboBox, string? selectedTheme)
        {
            if (string.IsNullOrEmpty(selectedTheme)) return;  // Avoid unnecessary iterations

            foreach (ComboBoxItem item in comboBox.Items)
            {
                if (item.Content.ToString() == selectedTheme)
                {
                    comboBox.SelectedItem = item;
                    return;
                }
            }
        }

        /// <summary>
        /// Programmatically simulates a user click on a button to start or resume the game.
        /// </summary>
        /// <param name="button">The <see cref="Button"/> to simulate a click on.</param>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        private static void SimulateStartClick(Button button)
        {
            var peer = new ButtonAutomationPeer(button);
            if (peer.GetPattern(PatternInterface.Invoke) is IInvokeProvider invokeProvider)
            {
                invokeProvider.Invoke();
            }
        }

        /// <summary>
        /// Saves the current enabled state of the Resume or Play button before disabling them,
        /// allowing them to be restored later if appropriate.
        /// </summary>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void StorePlayState()
        {
            if (_isPaused && ResumeButton.IsEnabled)
            {
                _wasResumable = true;
                ResumeButton.IsEnabled = false;
            }
            else if (PlayButton.IsEnabled)
            {
                _wasPlayable = true;
                PlayButton.IsEnabled = false;
            }
        }

        /// <summary>
        /// Restores the Resume or Play button to its enabled state
        /// if it was previously marked as usable before being disabled.
        /// </summary>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void RestorePlayState()
        {
            if (_wasResumable)
            {
                _wasResumable = false;
                ResumeButton.IsEnabled = true;
            }
            else if (_wasPlayable)
            {
                _wasPlayable = false;
                PlayButton.IsEnabled = true;
            }
        }

        /// <summary>
        /// Toggles the Play or Resume button based on pause state and readiness to start.
        /// </summary>
        /// <param name="canPlay">True to enable the appropriate button; false to disable.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void TogglePlayButtons(bool canPlay)
        {
            if (_isPaused)
                ResumeButton.IsEnabled = canPlay;
            else
                PlayButton.IsEnabled = canPlay;
        }

        /// <summary>
        /// Attempts to play a sound by name if it exists in the sound dictionary.
        /// </summary>
        /// <param name="soundName">The name of the sound to play.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void PlaySound(string soundName)
        {
            if (_soundPlayer.TryGetValue(soundName, out var player))
            {
                try
                {
                    player.Stop();  // Ensure the sound is reset
                    player.Load();  // Preload before playing to avoid glitches
                    player.Play();
                }
                catch (Exception ex)
                {
                    ChessLog.LogError($"Error playing sound '{soundName}'.", ex);
                }
            }
            else
            {
                ChessLog.LogError($"Sound '{soundName}' not found in dictionary.");
            }
        }

        /// <summary>
        /// Resets captured piece counters to initial values.
        /// </summary>
        /// <remarks>✅ Written on 7/18/2025</remarks>
        private void ResetCapturedPieceCounts()
        {
            _numWQ = 2; _numBQ = 2;
            _numWN = 3; _numBN = 3;
            _numWR = 3; _numBR = 3;
            _numWB = 3; _numBB = 3;
        }

        /// <summary>
        /// Enables all visible chess pieces for gameplay.
        /// </summary>
        /// <remarks>✅ Written on 7/18/2025</remarks>
        private void EnableAllPieces()
        {
            foreach (Image image in Chess_Board.Children.OfType<Image>())
            {
                if (image.Tag != null && (image.Tag.ToString() == "WhitePiece" || image.Tag.ToString() == "BlackPiece"))
                {
                    image.Visibility = Visibility.Visible;
                    image.IsEnabled = true;
                }
            }
        }

        /// <summary>
        /// Randomly selects Elo ratings for both White and Black CPU players,
        /// temporarily unsubscribing event handlers to prevent unnecessary triggers.
        /// </summary>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        private void AssignRandomElo()
        {
            if (WhiteCPUElo.Items.Count == 0 || BlackCPUElo.Items.Count == 0)
                return;

            // Temporarily unsubscribe to prevent triggering logic during changes
            WhiteCPUElo.SelectionChanged -= CheckDropdownSelections;
            BlackCPUElo.SelectionChanged -= CheckDropdownSelections;

            Random rng = new();
            WhiteCPUElo.SelectedIndex = rng.Next(WhiteCPUElo.Items.Count);
            BlackCPUElo.SelectedIndex = rng.Next(BlackCPUElo.Items.Count);

            // Re-subscribe after assignments
            WhiteCPUElo.SelectionChanged += CheckDropdownSelections;
            BlackCPUElo.SelectionChanged += CheckDropdownSelections;
        }

        /// <summary>
        /// Enables or disables interaction with images based on their tag.
        /// </summary>
        /// <param name="tag">The tag used to filter images.</param>
        /// <param name="enable">True to enable interaction, false to disable.</param>
        /// <remarks>✅ Updated on 7/23/2025</remarks>
        private void EnableImagesWithTag(string tag, bool enable)
        {
            foreach (var image in Chess_Board.Children.OfType<Image>())
            {
                if (image.Tag?.ToString() == tag)
                {
                    image.IsHitTestVisible = enable;
                    image.IsEnabled = enable;
                }
            }
        }

        /// <summary>
        /// Deselects all currently clicked chess pieces by resetting their references.
        /// </summary>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void DeselectPieces()
        {
            _clickedPawn = null;
            _clickedKnight = null;
            _clickedBishop = null;
            _clickedRook = null;
            _clickedQueen = null;
            _clickedKing = null;
        }

        #endregion

        #region Writers

        /// <summary>
        /// Writes the PGN file header with metadata based on the selected play mode and players.
        /// </summary>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void WritePGNFile()
        {
            File.WriteAllText(_pgnFilePath, "[Event \"Chess Match\"]\n");
            File.AppendAllText(_pgnFilePath, "[Site \"Tyler's Chess Program\"]\n");
            File.AppendAllText(_pgnFilePath, $"[Date \"{DateTime.Now:yyyy.MM.dd}\"]\n");
            File.AppendAllText(_pgnFilePath, "[Round \"1\"]\n");

            switch (_mode)
            {
                case 1:  // Com Vs. Com
                    File.AppendAllText(_pgnFilePath, $"[White \"Bot\"]\n[Black \"Bot\"]\n[Result \"*\"]\n[WhiteElo \"{_selectedWhiteElo?.Content}\"]\n[BlackElo \"{_selectedBlackElo?.Content}\"]\n\n");
                    break;

                case 2:  // User Vs. Com
                    if (_selectedColor?.Content.ToString() == "White")
                    {
                        File.AppendAllText(_pgnFilePath, $"[White \"User\"]\n[Black \"Bot\"]\n[Result \"*\"]\n[BlackElo \"{_selectedElo?.Content}\"]\n\n");
                    }
                    else
                    {
                        File.AppendAllText(_pgnFilePath, $"[White \"Bot\"]\n[Black \"User\"]\n[Result \"*\"]\n[WhiteElo \"{_selectedElo?.Content}\"]\n\n");
                    }
                    break;

                case 3:  // User Vs. User
                default:
                    File.AppendAllText(_pgnFilePath, $"[White \"User\"]\n[Black \"User\"]\n[Result \"*\"]\n\n");
                    break;
            }
        }

        #endregion


        /// <summary>
        /// Attempts communication and updates Epson RC+ connection setting in the "Preferences" file.
        /// </summary>
        /// <param name="sender">The sender object triggering the connection attempt.</param>
        /// <returns>An asynchronous task representing the connection attempt.</returns>
        private async Task AttemptCommunication(object sender)  // ✅
        {
            // Ensure sender is a CheckBox
            if (sender is not CheckBox checkBox)
            {
                SetStatusLights(Brushes.Red, Brushes.Red);
                return;
            }

            if (checkBox.IsChecked.HasValue && _robotComm)  // If user is trying to connect to Epson robots
            {
                // Attempt to connect
                GlobalState.WhiteConnected = !GlobalState.WhiteConnected && await Task.Run(() => _whiteRobot.ConnectAsync());
                GlobalState.BlackConnected = !GlobalState.BlackConnected && await Task.Run(() => _blackRobot.ConnectAsync());

                if (GlobalState.WhiteConnected && GlobalState.BlackConnected)  // Successfully connected to both Epson robots
                {
                    _preferences.EpsonRC = true;
                    PreferencesManager.Save(_preferences);

                    _robotComm = true;
                    checkBox.IsChecked = true;
                    SetStatusLights(Brushes.Green, Brushes.Green);

                    EpsonRCRect.Height = 30;
                    AttemptingConnection.Visibility = Visibility.Collapsed;
                    InfoSymbol.Visibility = Visibility.Visible;
                    SetupText.Visibility = Visibility.Visible;
                    MoveInProgRect.Visibility = Visibility.Visible;

                    if (!_boardSet)
                        await SetupBoard();

                    InfoSymbol.Visibility = Visibility.Collapsed;
                    SetupText.Visibility = Visibility.Collapsed;
                    MoveInProgRect.Visibility = Visibility.Collapsed;

                    // Process any prior moves before proceeding
                    if (!string.IsNullOrEmpty(_rcPastWhiteBits))
                    {
                        ShowMoveInProgressPopup(true);

                        string[] whiteBitLines = _rcPastWhiteBits.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in whiteBitLines)
                            await _whiteRobot.SendDataAsync(line);

                        if (!string.IsNullOrEmpty(_rcPastBlackBits))
                        {
                            string[] blackBitLines = _rcPastWhiteBits.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in blackBitLines)
                                await _blackRobot.SendDataAsync(line);
                        }

                        ShowMoveInProgressPopup(false);
                    }
                }
                else
                {
                    _robotComm = false;
                    _preferences.EpsonRC = false;
                    PreferencesManager.Save(_preferences);

                    checkBox.IsChecked = false;
                    SetStatusLights(
                        GlobalState.WhiteConnected ? Brushes.Green : Brushes.Red,
                        GlobalState.BlackConnected ? Brushes.Green : Brushes.Red
                    );
                }

                EpsonRCRect.Height = 30;
                AttemptingConnection.Visibility = Visibility.Collapsed;
            }
            else
            {
                EpsonRCRect.Height = 30;
                AttemptingConnection.Visibility = Visibility.Collapsed;

                _robotComm = false;
                _preferences.EpsonRC = false;
                PreferencesManager.Save(_preferences);

                GlobalState.WhiteConnected = false;
                GlobalState.BlackConnected = false;
                checkBox.IsChecked = false;

                SetStatusLights(Brushes.Red, Brushes.Red);

                if (_mode != 0)  // Cleanup process if necessary
                {
                    ShowCleanupPopup(true);
                    await ClearBoard();
                    ShowCleanupPopup(false);
                }

                _whiteRobot.Disconnect();
                _blackRobot.Disconnect();
            }

            // Adjust UI element clip
            if (FindName("EpsonRCRect") is Rectangle epsonRCRect)
            {
                if (epsonRCRect.Clip is RectangleGeometry clipGeometry)
                {
                    clipGeometry.Rect = new Rect(0, -10, 180, 40);
                    clipGeometry.RadiusX = 5;
                    clipGeometry.RadiusY = 5;
                }
            }

            // Restart inactivity timer
            EpsonRCConnection.IsEnabled = true;
            _inactivityTimer.Start();
        }

        /// <summary>
        /// Pauses the game, displaying the game start panel, disabling piece interactions, 
        /// and enabling/disabling UI elements based on the current game state.
        /// </summary>
        /// <param name="sender">The event sender.</param>
        /// <param name="e">The event arguments.</param>
        private void Pause(object sender, EventArgs e)  // ✅
        {
            // Show game start panel and disable interactions with the chessboard
            Game_Start.Visibility = Visibility.Visible;
            Game_Start.IsEnabled = true;
            Chess_Board.IsHitTestVisible = false;

            bool isUserVsUserOrCom = _selectedPlayType.Content.ToString() == "User Vs. User" || _selectedPlayType.Content.ToString() == "User Vs. Com";

            if (isUserVsUserOrCom)
            {
                UvCorUvU.Visibility = Visibility.Visible;
                if (!_moving)
                {
                    UvCorUvU.IsEnabled = true;
                    Play_Type.IsEnabled = true;
                }
            }
            else
            {
                CvC.Visibility = Visibility.Visible;
                if (!_moving)
                {
                    CvC.IsEnabled = true;
                    Play_Type.IsEnabled = true;
                }
            }

            _isPaused = true;
            PauseButton.IsEnabled = false;

            if (!_moving)   // If CPU is not currently moving
            {
                _inactivityTimer.Start();
                ResumeButton.IsEnabled = true;
                EpsonRCConnection.IsEnabled = true;   // Enables user to attempt communication with Epson
            }
            else   // CPU is currently moving
            {
                ShowMoveInProgressPopup(true);
                Play_Type.IsEnabled = false;
                ResumeButton.IsEnabled = false;
                _holdResume = true;
            }

            // Enable relevant settings based on game mode
            if (_mode == 1)
            {
                WhiteCPUElo.IsEnabled = true;
                BlackCPUElo.IsEnabled = true;
            }
            else if (_mode == 2)
            {
                Elo.IsEnabled = true;
                Color.IsEnabled = true;
            }

            // Disable piece interactions
            EnableImagesWithTag("WhitePiece", false);
            EnableImagesWithTag("BlackPiece", false);
            EraseAnnotations();

            DeselectPieces();
        }

        /// <summary>
        /// Resumes the paused game and restores the game state.
        /// </summary>
        private void Resume(object sender, EventArgs e)  // ✅
        {
            _inactivityTimer.Stop();

            _selectedPlayType = (ComboBoxItem)Play_Type.SelectedItem;
            _selectedColor = (ComboBoxItem)Color.SelectedItem;

            // Hide and disable UI elements for game start
            Game_Start.Visibility = Visibility.Collapsed;
            Game_Start.IsEnabled = false;
            UvCorUvU.Visibility = Visibility.Collapsed;
            UvCorUvU.IsEnabled = false;
            CvC.Visibility = Visibility.Collapsed;
            CvC.IsEnabled = false;

            _isPaused = false;
            PauseButton.IsEnabled = true;
            ResumeButton.IsEnabled = false;
            EpsonRCConnection.IsEnabled = false;

            EnableImagesWithTag("WhitePiece", true);
            EnableImagesWithTag("BlackPiece", true);

            if (_selectedPlayType.Content.ToString() == "Com Vs. Com")
            {
                _mode = 1;
                _moving = true;
                _userTurn = false;
                Chess_Board.IsHitTestVisible = false;

                //SetEloValues(selectedWhiteElo.Content.ToString(), true);
                //SetEloValues(selectedBlackElo.Content.ToString(), false);

                ComputerMove();
            }
            else if (_selectedPlayType.Content.ToString() == "User Vs. Com")
            {
                _mode = 2;
                //SetEloValues(selectedElo.Content.ToString(), true);

                // Check if board needs to be flipped
                if ((_flip == 0 && _selectedColor.Content.ToString() == "Black") ||
                    (_flip == 1 && _selectedColor.Content.ToString() == "White"))
                {
                    FlipBoard();
                    FENCode();
                    UpdateEvalBar();
                }

                // If it's the computer's turn, make a move
                if ((_move == 1 && _selectedColor.Content.ToString() == "Black") ||
                    (_move == 0 && _selectedColor.Content.ToString() == "White"))
                {
                    _moving = true;
                    _userTurn = false;
                    Chess_Board.IsHitTestVisible = false;
                    ComputerMove();
                }
                else
                {
                    _userTurn = true;
                    Chess_Board.IsHitTestVisible = true;
                }
            }
            else
            {
                _mode = 3;
                _userTurn = true;
                Chess_Board.IsHitTestVisible = true;
            }
        }

        /// <summary>
        /// Updates the Stockfish evaluation bar and adjusts its UI components.
        /// Dynamically scales and animates the evaluation bar based on board size, screen width,
        /// and the current evaluation score.
        /// </summary>
        /// <remarks>✅ Updating...</remarks>
        private void UpdateEvalBar()
        {
            // Layout constants
            const double ExternalHorizontalMargin = 10;  // Left and right margins outside the evaluation interface
            const double ExternalVerticalMargin = 77;  // Top and bottom margins outside the evaluation interface
            const double MinimumWidth = 280;  // Minimum pixels required between left and right external margins
            const double InternalHorizontalMargin = 15;  // Left and right margins inside the evaluation interface
            const double InternalVerticalMargin = 15;  // Top and bottom margins inside the evaluation interface
            const double EvalBarCornerRadius = 2;  // Corner radius of evaluation bar
            const double AnimationDuration = 1.5;

            WhiteAdvantage.Text = _displayedAdvantage;
            BlackAdvantage.Text = _displayedAdvantage;

            // Find the pixel width between the left edge of the screen and the right edge of the board with the buffer applied.
            double availableWidth = (Screen.ActualWidth / 2) - (Board.ActualWidth / 2) - (2 * ExternalHorizontalMargin);

            if (availableWidth < MinimumWidth)
            {
                EngineEvaluation.IsOpen = false;
                return;
            }

            EngineEvaluation.Width = availableWidth;
            EngineEvaluation.HorizontalOffset = EngineEvaluation.Width + ExternalHorizontalMargin;  // Sets the anchor point offset (top right corner) from the left edge of the screen
            EngineEvaluation.VerticalOffset = ExternalVerticalMargin;  // Sets the anchor point offset (top right corner) from the top of the screen
            EvalBar.Height = Board.ActualHeight - (2 * InternalVerticalMargin);
            EvalBar.Width = EngineEvaluation.Width / 15;
            EvalBar.Margin = new Thickness(0, -(StockfishEvaluationText.Height - InternalVerticalMargin), InternalHorizontalMargin, 0);  // Shifts the bar upwards since the stockfish evaluation text forces it lower than desired.

            RectangleGeometry clipGeometry = new(new(0, 0, EvalBar.Width, EvalBar.Height), EvalBarCornerRadius, EvalBarCornerRadius);
            EvalBar.Clip = clipGeometry;

            WhiteAdvantage.Width = BlackAdvantage.Width = EvalBar.Width;
            WhiteAdvantage.Height = BlackAdvantage.Height = EvalBar.Width;

            PlayedMoves.Width = (EngineEvaluation.Width - ((3 * InternalHorizontalMargin) + EvalBar.Width));
            PlayedMoves.Height = EvalBar.Height - 30;
            PlayedMoves.Margin = new Thickness(InternalVerticalMargin, -EvalBar.Height + 30, 0, 0);

            // Flip the evaluation perspective if the board is flipped
            bool whiteIsWinning = (_flip == 0) ? (_quantifiedEvaluation <= 10) : (_quantifiedEvaluation > 10);

            if (whiteIsWinning)
            {
                WhiteAdvantage.Visibility = Visibility.Visible;
                BlackAdvantage.Visibility = Visibility.Collapsed;
                WhiteAdvantage.Foreground = new SolidColorBrush(_flip == 0 ? Colors.Black : (Color)ColorConverter.ConvertFromString("#FFD0D0D0"));
            }
            else
            {
                WhiteAdvantage.Visibility = Visibility.Collapsed;
                BlackAdvantage.Visibility = Visibility.Visible;
                BlackAdvantage.Foreground = new SolidColorBrush(_flip == 0 ? (Color)ColorConverter.ConvertFromString("#FFD0D0D0") : Colors.Black);
            }

            // Set bar colors based on the flipped evaluation
            EvalBar.Fill = new SolidColorBrush(_flip == 0 ? (Color)ColorConverter.ConvertFromString("#FFD0D0D0") : Colors.Black);
            AdvantageGauge.Fill = new SolidColorBrush(_flip == 0 ? Colors.Black : (Color)ColorConverter.ConvertFromString("#FFD0D0D0"));
            AdvantageGauge.Clip = clipGeometry;

            // Compute animation height, flipping evaluation for the bar height
            double oldHeight = double.IsNaN(AdvantageGauge.Height) ? EvalBar.Height / 2 : AdvantageGauge.Height;
            double newHeight = (EvalBar.Height / 20) * (_flip == 0 ? _quantifiedEvaluation : (20 - _quantifiedEvaluation));

            DoubleAnimation animation = new()
            {
                From = oldHeight,
                To = newHeight,
                Duration = new Duration(TimeSpan.FromSeconds(AnimationDuration)),
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseInOut }
            };

            AdvantageGauge.BeginAnimation(Rectangle.HeightProperty, animation);
            AdvantageGauge.Width = EvalBar.Width;
            AdvantageGauge.Margin = new Thickness(0, -EvalBar.Height, InternalVerticalMargin, 0);

            WhiteAdvantage.Margin = new Thickness(0, -EvalBar.Width, InternalVerticalMargin, 0);
            BlackAdvantage.Margin = new Thickness(0, -EvalBar.Height, InternalVerticalMargin, 0);
        }

        /// <summary>
        /// Disables castling rights when a king or rook moves or is captured.
        /// Ensures that future castling is not allowed once a relevant piece moves.
        /// </summary>
        /// <param name="activePiece">The piece that is actively moving.</param>
        /// <param name="capturedPiece">The piece that is being captured (if applicable).</param>
        public void DisableCastlingRights(Image? activePiece, Image? capturedPiece)
        {
            if (activePiece == null) return;

            if (capturedPiece != null)
            {
                if (capturedPiece.Name.StartsWith("WhiteRook1")) _cWR1 = 1;
                else if (capturedPiece.Name.StartsWith("WhiteRook2")) _cWR2 = 1;
                else if (capturedPiece.Name.StartsWith("BlackRook1")) _cBR1 = 1;
                else if (capturedPiece.Name.StartsWith("BlackRook2")) _cBR2 = 1;
            }

            if (activePiece.Name.StartsWith("WhiteRook1")) _cWR1 = 1;
            else if (activePiece.Name.StartsWith("WhiteRook2")) _cWR2 = 1;
            else if (activePiece.Name.StartsWith("BlackRook1")) _cBR1 = 1;
            else if (activePiece.Name.StartsWith("BlackRook2")) _cBR2 = 1;

            if (activePiece.Name.StartsWith("WhiteKing")) _cWK = 1;
            else if (activePiece.Name.StartsWith("BlackKing")) _cBK = 1;
        }

        /// <summary>
        /// Handles pawn promotion for user or CPU, updating the piece image, name, handlers, and counters.
        /// Shows the promotion dialog when it's the user's turn.
        /// </summary>
        /// <param name="activePawn">The pawn being moved.</param>
        /// <param name="move">The moving color. 1 for White and 0 for Black</param>
        /// <remarks>✅ Updating...</remarks>
        public void PawnPromote(Image activePawn, int move)
        {
            string[] promotionPieces = ["Rook", "Knight", "Bishop", "Queen"];
            List<string> imagePaths = [];

            foreach (string piece in promotionPieces)
            {
                // White version
                imagePaths.Add(System.IO.Path.Combine(
                    _executableDirectory, "Assets", "Pieces",
                    _preferences.Pieces, $"White{piece}.png"));

                // Black version
                imagePaths.Add(System.IO.Path.Combine(
                    _executableDirectory, "Assets", "Pieces",
                    _preferences.Pieces, $"Black{piece}.png"));
            }

            if (move == 1)
            {
                _promoted = true;
                _promotedPawn = activePawn.Name;

                if (_userTurn)
                {
                    Promotion promotion = new(imagePaths);
                    promotion.WhiteQueen.Visibility = Visibility.Visible;
                    promotion.WhiteKnight.Visibility = Visibility.Visible;
                    promotion.WhiteRook.Visibility = Visibility.Visible;
                    promotion.WhiteBishop.Visibility = Visibility.Visible;
                    promotion.BlackQueen.Visibility = Visibility.Collapsed;
                    promotion.BlackKnight.Visibility = Visibility.Collapsed;
                    promotion.BlackRook.Visibility = Visibility.Collapsed;
                    promotion.BlackBishop.Visibility = Visibility.Collapsed;
                    promotion.Owner = this;
                    promotion.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    promotion.ShowDialog();

                    _clickedButtonName = promotion.ClickedButtonName;

                    if (_clickedButtonName.StartsWith("Rook"))  // If user promoted to a rook
                    {
                        _clickedPawn.Source = new BitmapImage(new Uri(imagePaths[0]));
                        _clickedPawn.Name = $"WhiteRook{_numWR}";
                        _clickedPawn.MouseUp -= ChessPawn_Click;
                        _clickedPawn.MouseUp += ChessRook_Click;

                        _promotionPiece = 'r';
                        _numWR++;
                    }
                    else if (_clickedButtonName.StartsWith("Knight"))  // If user promoted to a knight
                    {
                        _clickedPawn.Source = new BitmapImage(new Uri(imagePaths[2]));
                        _clickedPawn.Name = $"WhiteKnight{_numWN}";
                        _clickedPawn.MouseUp -= ChessPawn_Click;
                        _clickedPawn.MouseUp += ChessKnight_Click;

                        _promotionPiece = 'n';
                        _numWN++;
                    }
                    else if (_clickedButtonName.StartsWith("Bishop"))  // If user promoted to a bishop
                    {
                        _clickedPawn.Source = new BitmapImage(new Uri(imagePaths[4]));
                        _clickedPawn.Name = $"WhiteBishop{_numWB}";
                        _clickedPawn.MouseUp -= ChessPawn_Click;
                        _clickedPawn.MouseUp += ChessBishop_Click;

                        _promotionPiece = 'b';
                        _numWB++;
                    }
                    else if (_clickedButtonName.StartsWith("Queen"))  // If user promoted to a queen
                    {
                        _clickedPawn.Source = new BitmapImage(new Uri(imagePaths[6]));
                        _clickedPawn.Name = $"WhiteQueen{_numWQ}";
                        _clickedPawn.MouseUp -= ChessPawn_Click;
                        _clickedPawn.MouseUp += ChessQueen_Click;

                        _promotionPiece = 'q';
                        _numWQ++;
                    }
                }
                else
                {
                    if (_promotionPiece == 'r')  // If CPU promoted to a rook
                    {
                        _clickedPawn.Source = new BitmapImage(new Uri(imagePaths[0]));
                        _clickedPawn.Name = $"WhiteRook{_numWR}";
                        _clickedPawn.MouseUp -= ChessPawn_Click;
                        _clickedPawn.MouseUp += ChessRook_Click;

                        _promoted = true;
                        _numWR++;
                    }
                    else if (_promotionPiece == 'n')  // If CPU promoted to a knight
                    {
                        _clickedPawn.Source = new BitmapImage(new Uri(imagePaths[2]));
                        _clickedPawn.Name = $"WhiteKnight{_numWN}";
                        _clickedPawn.MouseUp -= ChessPawn_Click;
                        _clickedPawn.MouseUp += ChessKnight_Click;

                        _promoted = true;
                        _numWN++;
                    }
                    else if (_promotionPiece == 'b')  // If CPU promoted to a bishop
                    {
                        _clickedPawn.Source = new BitmapImage(new Uri(imagePaths[4]));
                        _clickedPawn.Name = $"WhiteBishop{_numWB}";
                        _clickedPawn.MouseUp -= ChessPawn_Click;
                        _clickedPawn.MouseUp += ChessBishop_Click;

                        _promoted = true;
                        _numWB++;
                    }
                    else if (_promotionPiece == 'q')  // If CPU promoted to a queen
                    {
                        _clickedPawn.Source = new BitmapImage(new Uri(imagePaths[6]));
                        _clickedPawn.Name = $"WhiteQueen{_numWQ}";
                        _clickedPawn.MouseUp -= ChessPawn_Click;
                        _clickedPawn.MouseUp += ChessQueen_Click;

                        _promoted = true;
                        _numWQ++;
                    }
                }

                _activePiece = activePawn.Name;
            }
            else if (move == 0)
            {
                _promoted = true;
                _promotedPawn = activePawn.Name;

                if (_userTurn)
                {
                    Promotion promotion = new(imagePaths);
                    promotion.WhiteQueen.Visibility = Visibility.Collapsed;
                    promotion.WhiteKnight.Visibility = Visibility.Collapsed;
                    promotion.WhiteRook.Visibility = Visibility.Collapsed;
                    promotion.WhiteBishop.Visibility = Visibility.Collapsed;
                    promotion.BlackQueen.Visibility = Visibility.Visible;
                    promotion.BlackKnight.Visibility = Visibility.Visible;
                    promotion.BlackRook.Visibility = Visibility.Visible;
                    promotion.BlackBishop.Visibility = Visibility.Visible;
                    promotion.Owner = this;
                    promotion.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    promotion.ShowDialog();

                    _clickedButtonName = promotion.ClickedButtonName;

                    if (_clickedButtonName.StartsWith("Rook"))  // If user promoted to a rook
                    {
                        _clickedPawn.Source = new BitmapImage(new Uri(imagePaths[1]));
                        _clickedPawn.Name = $"BlackRook{_numBR}";
                        _clickedPawn.MouseUp -= ChessPawn_Click;
                        _clickedPawn.MouseUp += ChessRook_Click;

                        _promotionPiece = 'r';
                        _numBR++;
                    }
                    else if (_clickedButtonName.StartsWith("Knight"))  // If user promoted to a knight
                    {
                        _clickedPawn.Source = new BitmapImage(new Uri(imagePaths[3]));
                        _clickedPawn.Name = $"BlackKnight{_numBN}";
                        _clickedPawn.MouseUp -= ChessPawn_Click;
                        _clickedPawn.MouseUp += ChessKnight_Click;

                        _promotionPiece = 'n';
                        _numBN++;
                    }
                    else if (_clickedButtonName.StartsWith("Bishop"))  // If user promoted to a bishop
                    {
                        _clickedPawn.Source = new BitmapImage(new Uri(imagePaths[5]));
                        _clickedPawn.Name = $"BlackBishop{_numBB}";
                        _clickedPawn.MouseUp -= ChessPawn_Click;
                        _clickedPawn.MouseUp += ChessBishop_Click;

                        _promotionPiece = 'b';
                        _numBB++;
                    }
                    else if (_clickedButtonName.StartsWith("Queen"))  // If user promoted to a queen
                    {
                        _clickedPawn.Source = new BitmapImage(new Uri(imagePaths[7]));
                        _clickedPawn.Name = $"BlackQueen{_numBQ}";
                        _clickedPawn.MouseUp -= ChessPawn_Click;
                        _clickedPawn.MouseUp += ChessQueen_Click;

                        _promotionPiece = 'q';
                        _numBQ++;
                    } 
                }
                else
                {
                    if (_promotionPiece == 'r')  // If CPU promoted to a rook
                    {
                        activePawn.Source = new BitmapImage(new Uri(imagePaths[1]));
                        activePawn.Name = $"BlackRook{_numBR}";
                        activePawn.MouseUp -= ChessPawn_Click;
                        activePawn.MouseUp += ChessRook_Click;

                        _promoted = true;
                        _numBR++;
                    }
                    else if (_promotionPiece == 'n')  // If CPU promoted to a knight
                    {
                        activePawn.Source = new BitmapImage(new Uri(imagePaths[3]));
                        activePawn.Name = $"BlackKnight{_numBN}";
                        activePawn.MouseUp -= ChessPawn_Click;
                        activePawn.MouseUp += ChessKnight_Click;

                        _promoted = true;
                        _numBN++;
                    }
                    else if (_promotionPiece == 'b')  // If CPU promoted to a bishop
                    {
                        activePawn.Source = new BitmapImage(new Uri(imagePaths[5]));
                        activePawn.Name = $"BlackBishop{_numBB}";
                        activePawn.MouseUp -= ChessPawn_Click;
                        activePawn.MouseUp += ChessBishop_Click;

                        _promoted = true;
                        _numBB++;
                    }
                    else if (_promotionPiece == 'q')  // If CPU promoted to a queen
                    {
                        activePawn.Source = new BitmapImage(new Uri(imagePaths[7]));
                        activePawn.Name = $"BlackQueen{_numBQ}";
                        activePawn.MouseUp -= ChessPawn_Click;
                        activePawn.MouseUp += ChessQueen_Click;

                        _promoted = true;
                        _numBQ++;
                    }
                }

                _activePiece = activePawn.Name;
            }
        }



        /// <summary>
        /// 
        /// </summary>
        public async Task CheckmateVerifierAsync()
        {
            if (_robotComm)
            {
                EnableImagesWithTag("WhitePiece", false);
                EnableImagesWithTag("BlackPiece", false);
                PauseButton.IsEnabled = false;
                EpsonRCConnection.IsEnabled = false;

                InfoSymbol.Visibility = Visibility.Visible;   // Show "Move in progress" popup
                InProgressText.Visibility = Visibility.Visible;
                MoveInProgRect.Visibility = Visibility.Visible;
            }

            using StockfishCall stockfishResponse = new(_stockfishPath!);
            string stockfishFEN = await Task.Run(() => stockfishResponse.GetStockfishResponse(_fen));
            string[] lines = stockfishFEN.Split('\n').Skip(2).ToArray();
            string[] infoLines = lines.Where(line => line.TrimStart().StartsWith("info")).ToArray();
            string accurateEvaluationLine = infoLines.LastOrDefault() ?? "Most accurate evaluation line not found";
            string[] accurateEvaluation = accurateEvaluationLine.Split(' ');

            if (accurateEvaluation[5].StartsWith("0"))   // If game has been won
            {
                _displayedAdvantage = "1-0";

                if (_move == 0)
                {
                    _quantifiedEvaluation = 0;
                }

                else
                {
                    _quantifiedEvaluation = 20;
                }
            }

            else if (accurateEvaluation[8].StartsWith("mate") && accurateEvaluation.Length > 9)   // If there is a mating sequence present
            {
                if (accurateEvaluation[9].StartsWith("-"))
                {
                    _displayedAdvantage = $"M{accurateEvaluation[9][1..]}";
                }

                else
                {
                    _displayedAdvantage = $"M{accurateEvaluation[9]}";
                }

                if (_move == 0)   // White just moved
                {
                    if (accurateEvaluation[9].StartsWith("-"))
                    {
                        _quantifiedEvaluation = 0;
                    }

                    else
                    {
                        _quantifiedEvaluation = 20;
                    }
                }

                else   // Black just moved
                {
                    if (accurateEvaluation[9].StartsWith("-"))
                    {
                        _quantifiedEvaluation = 20;
                    }

                    else
                    {
                        _quantifiedEvaluation = 0;
                    }
                }
            }

            else
            {
                _quantifiedEvaluation = double.Parse(accurateEvaluation[9].ToString()) / 100;
                _displayedAdvantage = Math.Abs(_quantifiedEvaluation).ToString("0.0");

                if (_move == 0)   // White just moved
                {
                    _quantifiedEvaluation = 10 + _quantifiedEvaluation;

                    if (_quantifiedEvaluation < 1)
                    {
                        _quantifiedEvaluation = 1;
                    }

                    else if (_quantifiedEvaluation > 19)
                    {
                        _quantifiedEvaluation = 19;
                    }
                }

                else   // Black just moved
                {
                    _quantifiedEvaluation = 10 - _quantifiedEvaluation;

                    if (_quantifiedEvaluation < 1)
                    {
                        _quantifiedEvaluation = 1;
                    }

                    else if (_quantifiedEvaluation > 19)
                    {
                        _quantifiedEvaluation = 19;
                    }
                }
            }

            UpdateEvalBar();

            string bestMoveLine = lines.FirstOrDefault(line => line.TrimStart().StartsWith("bestmove")) ?? "Bestmove line not found";
            string[] parts = bestMoveLine.Split(' ');

            if (parts[1].StartsWith("(none)"))   // If there are no best moves then game is over
            {
                string outcomeLine = lines[^2];
                string[] partsOutcome = outcomeLine.Split(' ');

                if (infoLines.Any(line => line.Contains("mate")))  // If Stockfish calculates a checkmate
                {
                    if (_move == 0)   // Checkmate for white
                    {
                        GameOver gameOver = new();
                        gameOver.WinnerText.Text = $"White wins by checkmate in {_fullmove} moves!";
                        gameOver.Owner = this;
                        gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        gameOver.Show();
                        System.Diagnostics.Debug.WriteLine("White wins by checkmate");
                    }

                    else   // Checkmate for black
                    {
                        GameOver gameOver = new();
                        gameOver.WinnerText.Text = $"Black wins by checkmate in {_fullmove - 1} moves!";
                        gameOver.Owner = this;
                        gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        gameOver.Show();
                        System.Diagnostics.Debug.WriteLine("Black wins by checkmate");
                    }
                }

                else if (infoLines.Any(line => line.Contains("cp")))   // If Stockfish calculates a stalemate
                {
                    _displayedAdvantage = ".5 - .5";
                    _quantifiedEvaluation = 10;

                    if (_move == 0)
                    {
                        GameOver gameOver = new();
                        gameOver.WinnerText.Text = $"Game ends in a stalemate after {_fullmove} moves";
                        gameOver.Owner = this;
                        gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        gameOver.Show();
                        System.Diagnostics.Debug.WriteLine("Stalemate");
                    }

                    else
                    {
                        GameOver gameOver = new();
                        gameOver.WinnerText.Text = $"Game ends in a stalemate after {_fullmove - 1} moves";
                        gameOver.Owner = this;
                        gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        gameOver.Show();
                        System.Diagnostics.Debug.WriteLine("Stalemate");
                    }
                }

                _endGame = true;
                EnableImagesWithTag("WhitePiece", false);
                EnableImagesWithTag("BlackPiece", false);
                UpdateEvalBar();
            }

            else if (_halfmove == 100)   // If fifty-move rule occurs
            {
                _displayedAdvantage = ".5 - .5";
                _quantifiedEvaluation = 10;

                GameOver gameOver = new();
                gameOver.WinnerText.Text = "The game is a draw due to the fifty-move rule,\n" +
                                           "as there have been no pawn movements\n" +
                                           "or captures in the last fifty full turns.";
                gameOver.Owner = this;
                gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                gameOver.Show();
                System.Diagnostics.Debug.WriteLine("Draw due to fifty-move rule");

                _endGame = true;
                EnableImagesWithTag("WhitePiece", false);
                EnableImagesWithTag("BlackPiece", false);
                UpdateEvalBar();
            }

            else if (_threefoldRepetition)   // If threefold repetition occurs
            {
                _displayedAdvantage = ".5 - .5";
                _quantifiedEvaluation = 10;

                GameOver gameOver = new();
                gameOver.WinnerText.Text = "The game is a draw due to threemove repetition,\n" +
                                           "as the same position was reached three\n" +
                                           "times with the same color to move each time.";
                gameOver.Owner = this;
                gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                gameOver.Show();
                System.Diagnostics.Debug.WriteLine("Draw due to threefold repetition");

                _endGame = true;
                EnableImagesWithTag("WhitePiece", false);
                EnableImagesWithTag("BlackPiece", false);
                UpdateEvalBar();
            }

            else
            {
                bool insufficient = true;

                int whiteBishopCount = 0;
                int blackBishopCount = 0;
                int whiteKnightCount = 0;
                int blackKnightCount = 0;

                int whiteLightSquareBishopCount = 0;
                int whiteDarkSquareBishopCount = 0;
                int blackLightSquareBishopCount = 0;
                int blackDarkSquareBishopCount = 0;

                foreach (Image image in Chess_Board.Children.OfType<Image>())
                {
                    if (image.Name.Contains("Pawn") || image.Name.Contains("Rook") || image.Name.Contains("Queen"))
                    {
                        insufficient = false;

                        break;   // Sufficient mating material is present on the board
                    }

                    if (image.Name.StartsWith("WhiteBishop"))
                    {
                        whiteBishopCount++;

                        if (((Grid.GetRow(image) + 1) + (Grid.GetColumn(image) + 1)) % 2 == 1)
                        {
                            whiteLightSquareBishopCount++;
                        }

                        else
                        {
                            whiteDarkSquareBishopCount++;
                        }
                    }

                    if (image.Name.StartsWith("BlackBishop"))
                    {
                        blackBishopCount++;

                        if (((Grid.GetRow(image) + 1) + (Grid.GetColumn(image) + 1)) % 2 == 1)
                        {
                            blackLightSquareBishopCount++;
                        }

                        else
                        {
                            blackDarkSquareBishopCount++;
                        }
                    }

                    if (image.Name.StartsWith("WhiteKnight"))
                    {
                        whiteKnightCount++;
                    }

                    if (image.Name.StartsWith("BlackKnight"))
                    {
                        blackKnightCount++;
                    }
                }

                if (insufficient)   // If no pawns, rooks, or queens are on the board
                {
                    if (whiteKnightCount >= 2 || blackKnightCount >= 2 || whiteBishopCount >= 2 || blackBishopCount >= 2)
                    {
                        insufficient = false;

                        return;   // Sufficient mating material is present on the board
                    }

                    else if (whiteKnightCount == 1 && blackKnightCount == 1)
                    {
                        insufficient = false;

                        return;   // Sufficient mating material is present on the board
                    }

                    else if ((whiteLightSquareBishopCount >= 1 && blackDarkSquareBishopCount >= 1) || (whiteDarkSquareBishopCount >= 1 && blackLightSquareBishopCount >= 1))
                    {
                        insufficient = false;

                        return;   // Sufficient mating material is present on the board
                    }
                }

                if (insufficient)   // If there is insufficient checkmating material
                {
                    _displayedAdvantage = ".5 - .5";
                    _quantifiedEvaluation = 10;

                    GameOver gameOver = new();
                    gameOver.WinnerText.Text = "The game is a draw due to insufficient material,\n" +
                                                "as neither side has enough remaining pieces\n" +
                                                "on the board to force a checkmate.";
                    gameOver.Owner = this;
                    gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    gameOver.Show();
                    System.Diagnostics.Debug.WriteLine("Draw due to insufficient material");

                    _endGame = true;
                    EnableImagesWithTag("WhitePiece", false);
                    EnableImagesWithTag("BlackPiece", false);
                    UpdateEvalBar();
                }
            }

            await CentralMoveHub();
        }



        // White CPU engine
        public async void ComputerMove()
        {
            EnableImagesWithTag("WhitePiece", false);   // Disables user from interacting with pieces
            EnableImagesWithTag("BlackPiece", false);

            List<(string cp, string cpValue, string possibleMove)> moveCatalog = new();
            List<(string cp, string cpValue, string possibleMove)> moveCatalogReserve = new();
            string[] lines;
            int cpuElo;
            Image? selectedPiece = null;

            // Randomize wait times for CPU moves
            Random delayMultiplier = new();
            int delay = delayMultiplier.Next(1000, 4501);
            await Task.Delay(delay);

            if (!_endGame)
            {
                _moving = true;

                if (_mode == 2)
                {
                    cpuElo = int.Parse(_selectedElo.Content.ToString()); // Parse CPU's elo as an integer
                }
                else
                {
                    if (_move == 1)
                    {
                        cpuElo = int.Parse(_selectedWhiteElo.Content.ToString()); // Parse CPU's elo as an integer
                    }
                    else
                    {
                        cpuElo = int.Parse(_selectedBlackElo.Content.ToString()); // Parse CPU's elo as an integer
                    }
                    
                }

                var settings = EloSettings.GetSettings(cpuElo);

                // Apply settings
                int searchDepth = settings.Depth;
                int cpLossThreshold = settings.CpLossThreshold;
                double bellCurvePercentile = settings.BellCurvePercentile;
                int criticalMoveConversion = settings.CriticalMoveConversion;

                // Call Stockfish and get moves
                (moveCatalog, moveCatalogReserve, lines) = await ParseStockfishOutput(_fen, searchDepth, _stockfishPath!);

                if (moveCatalog.Count == 0)
                {
                    moveCatalog.AddRange(moveCatalogReserve);
                    moveCatalogReserve.Clear();
                }

                var sortedMoveCatalog = moveCatalog.OrderByDescending(move =>   // Moves are listed in best to worst centipawn value
                {
                    if (move.cp.StartsWith("mate"))
                    {
                        if (!move.cpValue.StartsWith("-"))   // If mate is for moving color
                        {
                            return int.MaxValue;
                        }
                        else   // If mate is against moving color
                        {
                            return int.MinValue;
                        }
                    }

                    else
                    {
                        return int.Parse(move.cpValue);
                    }
                });

                var topMove = sortedMoveCatalog.FirstOrDefault();   // Best available move in the position
                var moves = new List<(string cp, string cpValue, string possibleMove)>();   // List of moves
                int maxCpValue = sortedMoveCatalog
                    .Where(move => !move.cp.StartsWith("mate"))
                    .Select(move => int.Parse(move.cpValue))
                    .FirstOrDefault();

                //System.Diagnostics.Debug.WriteLine("\n\nPossible Moves:");

                foreach (var entry in sortedMoveCatalog)
                {
                    if (entry.cp.StartsWith("mate"))
                    {
                        if (!entry.cpValue.StartsWith("-"))
                        {
                            moves.Add(entry);
                        }
                        else
                        {
                            if (sortedMoveCatalog.Count() < 3)
                            {
                                moves.Add(entry);
                            }
                        }
                    }
                    else
                    {
                        if (!int.TryParse(entry.cpValue, out int entryCpValue))
                            continue; // Skip if invalid

                        int difference = Math.Abs(entryCpValue - maxCpValue);

                        if (_fullmove < 6)
                        {
                            if (difference > (cpLossThreshold / 8))
                                continue; // Skip moves that exceed the allowed mistake threshold
                        }
                        else
                        {
                            if (difference > cpLossThreshold)
                                continue; // Skip moves that exceed the allowed mistake threshold
                        }

                        moves.Add(entry);            
                    }

                    //System.Diagnostics.Debug.WriteLine($"{entry.cp} {entry.cpValue} {entry.possibleMove}");
                }

                string bestMoveLine = lines.FirstOrDefault(line => line.TrimStart().StartsWith("bestmove"))!;   // Line stating the best move
                string[] bestParts = bestMoveLine.Split(' ');   // Splits best move line into parts

                if (moves.Count > 0 && moves[0].cp == "mate" && !_topEngineMove)   // Finding mating sequences
                {
                    int mateIn = Math.Abs(int.Parse(moves[0].cpValue));

                    if (ShouldPlayMatingMove(cpuElo, mateIn))
                    {
                        _topEngineMove = true;
                    }
                }

                if (moves.Count == 1 || _topEngineMove)   // Top engine move
                {
                    if (topMove.possibleMove.TrimEnd('\r').Length == 4)
                    {
                        string selMove = topMove.possibleMove.TrimEnd('\r');
                        _startPosition = selMove[..2];
                        _endPosition = selMove[2..];
                    }

                    else if (topMove.possibleMove.TrimEnd('\r').Length == 5)
                    {
                        string selMove = topMove.possibleMove.TrimEnd('\r');
                        _promotionPiece = selMove[^1];
                        _startPosition = selMove[..2];
                        _endPosition = selMove[2..4];
                    }

                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Best Move Line was not found");
                    }
                }
                else
                {
                    // Check for a critical moment by evaluating cp differences between top moves
                    Random random = new();
                    const int criticalCpThreshold = 300; // Define the threshold for a critical moment
                    var topMoves = moves.Take(3).ToList();

                    if (topMoves.Count >= 2)
                    {
                        // Parse centipawn values for the top moves
                        bool isTopMoveCpParsed = int.TryParse(topMoves[0].cpValue, out int topMoveCp);
                        bool isSecondMoveCpParsed = int.TryParse(topMoves[1].cpValue, out int secondMoveCp);

                        if (isTopMoveCpParsed && isSecondMoveCpParsed)
                        {
                            int differenceBetween1and2 = Math.Abs(topMoveCp - secondMoveCp);

                            if (differenceBetween1and2 >= criticalCpThreshold)
                            {
                                // Generate a random integer between 0 and 100
                                int randomValue = random.Next(101);

                                if (randomValue > criticalMoveConversion)
                                {
                                    // Random value is greater than criticalMoveConversion
                                    // Remove the second move (index 1) from sortedMoveCatalog
                                    if (moves.Count() > 1)
                                    {
                                        moves.RemoveAt(0);
                                    }

                                    System.Diagnostics.Debug.WriteLine("\n\nFailed critical moment between moves 1 and 2!\n");
                                }
                                else
                                {
                                    moves.RemoveRange(1, moves.Count - 1);

                                    System.Diagnostics.Debug.WriteLine("\n\nConverted critical moment between moves 1 and 2!\n");
                                }
                            }
                            else if (topMoves.Count >= 3)
                            {
                                bool isThirdMoveCpParsed = int.TryParse(topMoves[2].cpValue, out int thirdMoveCp);

                                if (isThirdMoveCpParsed)
                                {
                                    int differenceBetween2and3 = Math.Abs(secondMoveCp - thirdMoveCp);

                                    if (differenceBetween2and3 >= criticalCpThreshold)
                                    {
                                        // Generate a random integer between 0 and 100
                                        int randomValue = random.Next(101);

                                        if (randomValue > criticalMoveConversion)
                                        {
                                            // Random value is greater than criticalMoveConversion
                                            if (moves.Count() > 2)
                                            {
                                                moves.RemoveRange(0, 2);
                                            }

                                            System.Diagnostics.Debug.WriteLine("\n\nFailed critical moment between moves 2 and 3!\n");
                                        }
                                        else
                                        {
                                            moves.RemoveRange(2, moves.Count - 2);

                                            System.Diagnostics.Debug.WriteLine("\n\nConverted critical moment between moves 1 and 2!\n");
                                        }
                                    }
                                }
                            }
                        }
                    }

                    System.Diagnostics.Debug.WriteLine("\n\nPossible moves:");
                    foreach (var m in moves)
                    {
                        System.Diagnostics.Debug.WriteLine(m);
                    }

                    // Calculate the mean index based on the bellCurvePercentile
                    double percentile = bellCurvePercentile / 100.0;
                    int meanIndex = (int)Math.Round((1 - percentile) * (moves.Count - 1));

                    // Standard deviation: Adjust as needed
                    double standardDeviation = moves.Count / 4.0; // Example: covers ~95% within the list

                    // Initialize the random number generator
                    Random outcome = new();

                    // Function to generate a normally distributed random number
                    double GenerateNormalRandom(double mean, double stddev)
                    {
                        // Use Box-Muller transform
                        double u1 = 1.0 - outcome.NextDouble(); // Uniform(0,1] random doubles
                        double u2 = 1.0 - outcome.NextDouble();
                        double randStdNormal = Math.Sqrt(-2.0 * Math.Log(u1)) *
                                               Math.Sin(2.0 * Math.PI * u2); // Random normal(0,1)
                        double randNormal = mean + stddev * randStdNormal; // Random normal(mean,stdDev)
                        return randNormal;
                    }

                    // Generate a valid index based on the normal distribution
                    int selectedIndex;
                    do
                    {
                        double randIndex = GenerateNormalRandom(meanIndex, standardDeviation);
                        selectedIndex = (int)Math.Round(randIndex);
                    } while (selectedIndex < 0 || selectedIndex >= moves.Count);

                    // Select the move based on the generated index
                    string selMove = moves[selectedIndex].possibleMove.TrimEnd('\r');

                    if (selMove.Length == 4)
                    {
                        _startPosition = selMove[..2];
                        _endPosition = selMove[2..];
                    }

                    else if (selMove.Length == 5)
                    {
                        _promotionPiece = selMove[^1];
                        _startPosition = selMove[..2];
                        _endPosition = selMove[2..4];
                    }

                    else
                    {
                        System.Diagnostics.Debug.WriteLine("Move Line was not found");
                    }
                }

                _oldRow = 8 - Convert.ToInt32(_startPosition[1].ToString());
                _oldColumn = _startPosition[0] - 'a';
                _newRow = 8 - Convert.ToInt32(_endPosition[1].ToString());
                _newColumn = _endPosition[0] - 'a';

                foreach (Image image in Chess_Board.Children.OfType<Image>())
                {
                    if ((Grid.GetRow(image) == _oldRow) && Grid.GetColumn(image) == _oldColumn)
                    {
                        selectedPiece = image;

                        Grid.SetRow(selectedPiece, _newRow);
                        Grid.SetColumn(selectedPiece, _newColumn);
                        continue;
                    }
                }

                // Determine which move handler to use
                if (selectedPiece.Name.Contains("Pawn"))
                {
                    _clickedPawn = selectedPiece;
                    await PawnMoveManagerAsync(selectedPiece);
                }
                else
                {
                    if (selectedPiece.Name.Contains("Knight"))
                    {
                        _clickedKnight = selectedPiece;
                    }
                    else if (selectedPiece.Name.Contains("Bishop"))
                    {
                        _clickedBishop = selectedPiece;
                    }
                    else if (selectedPiece.Name.Contains("Rook"))
                    {
                        _clickedRook = selectedPiece;
                    }
                    else if (selectedPiece.Name.Contains("Queen"))
                    {
                        _clickedQueen = selectedPiece;
                    }
                    else if (selectedPiece.Name.Contains("King"))
                    {
                        _clickedKing = selectedPiece;
                    }
                        
                    await MoveManagerAsync(selectedPiece);
                }
            }
        }



        /// <summary>
        /// Calls Stockfish, retrieves output, and extracts valid move data.
        /// </summary>
        /// <param name="fen">Current board FEN string.</param>
        /// <param name="depth">Stockfish depth setting.</param>
        /// <param name="stockfishPath">Path to Stockfish engine.</param>
        /// <returns>Two lists of valid moves - main and reserve (fallback) moves.</returns>
        private async Task<(List<(string cp, string cpValue, string possibleMove)>, List<(string cp, string cpValue, string possibleMove)>, string[])>
        ParseStockfishOutput(string fen, int depth, string stockfishPath)
        {
            string stockfishFEN = await RunStockfish(fen, depth, stockfishPath);

            File.WriteAllText("StockfishOutput.txt", string.Empty);

            using (StreamWriter writer = new("StockfishOutput.txt", true))
            {
                writer.WriteLine(stockfishFEN);
            }

            string[] lines = stockfishFEN.Split('\n').Skip(2).ToArray();
            var infoLines = lines.Where(line => line.TrimStart().StartsWith($"info depth {depth}"));

            List<(string cp, string cpValue, string possibleMove)> moveCatalog = new();
            List<(string cp, string cpValue, string possibleMove)> moveCatalogReserve = new();

            foreach (var line in infoLines)
            {
                string[] parts = line.Split(' ');

                if (parts.Length >= 22)
                {
                    string depthVal = parts[2];   // Depth value

                    if (int.TryParse(depthVal, out int parsedDepth) && parsedDepth >= depth)
                    {
                        string cp = parts[8];   // Centipawn label
                        string cpValue = parts[9];   // Evaluated centipawn value
                        string possibleMove = parts[21];   // Possible move

                        moveCatalog.Add((cp, cpValue, possibleMove));
                    }
                    else if (int.TryParse(depthVal, out int parsedDepthCatch) && parsedDepthCatch == 1)
                    {
                        string cp = parts[8];
                        string cpValue = parts[9];
                        string possibleMove = parts[21];

                        moveCatalogReserve.Add((cp, cpValue, possibleMove));
                    }
                }
            }

            return (moveCatalog, moveCatalogReserve, lines);  // Return lines along with move lists
        }



        private bool ShouldPlayMatingMove(int elo, int mateIn)
        {
            Dictionary<int, double> mateChances = new()
            {
                { 3700, 1.00 },
                { 3000, mateIn <= 7 ? 1.00 : (mateIn == 8 ? 0.9999 : (mateIn == 9 ? 0.9995 : (mateIn == 10 ? 0.999 : (mateIn == 11 ? 0.997 : (mateIn == 12 ? 0.995 : (mateIn == 13 ? 0.99 : (mateIn == 14 ? 0.97 : (mateIn == 15 ? 0.95 : 0.90)))))))) },
                { 2800, mateIn == 1 ? 1.00 : (mateIn == 2 ? 1.00 : (mateIn == 3 ? 1.00 : (mateIn == 4 ? 1.00 : (mateIn == 5 ? 0.9999 : (mateIn == 6 ? 0.9995 : (mateIn == 7 ? 0.999 : (mateIn == 8 ? 0.995 : (mateIn == 9 ? 0.98 : (mateIn == 10 ? 0.95 : (mateIn == 11 ? 0.92 : (mateIn == 12 ? 0.88 : 0.80))))))))))) },
                { 2600, mateIn == 1 ? 1.00 : (mateIn == 2 ? 0.9999 : (mateIn == 3 ? 0.9998 : (mateIn == 4 ? 0.9995 : (mateIn == 5 ? 0.999 : (mateIn == 6 ? 0.995 : (mateIn == 7 ? 0.98 : (mateIn == 8 ? 0.95 : (mateIn == 9 ? 0.92 : (mateIn == 10 ? 0.88 : (mateIn == 11 ? 0.80 : (mateIn == 12 ? 0.72 : 0.65))))))))))) },
                { 2400, mateIn == 1 ? 0.9999 : (mateIn == 2 ? 0.9995 : (mateIn == 3 ? 0.999 : (mateIn == 4 ? 0.995 : (mateIn == 5 ? 0.98 : (mateIn == 6 ? 0.96 : (mateIn == 7 ? 0.92 : (mateIn == 8 ? 0.85 : (mateIn == 9 ? 0.75 : 0.65)))))))) },
                { 2200, mateIn == 1 ? 0.999 : (mateIn == 2 ? 0.998 : (mateIn == 3 ? 0.995 : (mateIn == 4 ? 0.985 : (mateIn == 5 ? 0.96 : (mateIn == 6 ? 0.92 : (mateIn == 7 ? 0.85 : 0.75)))))) },
                { 2000, mateIn == 1 ? 0.998 : (mateIn == 2 ? 0.995 : (mateIn == 3 ? 0.98 : (mateIn == 4 ? 0.95 : (mateIn == 5 ? 0.90 : 0.75)))) },
                { 1800, mateIn == 1 ? 0.995 : (mateIn == 2 ? 0.98 : (mateIn == 3 ? 0.95 : (mateIn == 4 ? 0.90 : (mateIn == 5 ? 0.80 : 0.60)))) },
                { 1600, mateIn == 1 ? 0.99 : (mateIn == 2 ? 0.95 : (mateIn == 3 ? 0.90 : (mateIn == 4 ? 0.80 : (mateIn == 5 ? 0.70 : 0.50)))) },
                { 1400, mateIn == 1 ? 0.97 : (mateIn == 2 ? 0.90 : (mateIn == 3 ? 0.80 : (mateIn == 4 ? 0.65 : (mateIn == 5 ? 0.50 : 0.30)))) },
                { 1200, mateIn == 1 ? 0.90 : (mateIn == 2 ? 0.80 : (mateIn == 3 ? 0.65 : (mateIn == 4 ? 0.50 : 0.25))) },
                { 1000, mateIn == 1 ? 0.80 : (mateIn == 2 ? 0.65 : (mateIn == 3 ? 0.50 : (mateIn == 4 ? 0.35 : 0.15))) },
                { 900, mateIn == 1 ? 0.72 : (mateIn == 2 ? 0.52 : (mateIn == 3 ? 0.42 : (mateIn == 4 ? 0.28 : 0.10))) },
                { 800, mateIn == 1 ? 0.58 : (mateIn == 2 ? 0.43 : (mateIn == 3 ? 0.32 : (mateIn == 4 ? 0.18 : 0.06))) },
                { 700, mateIn == 1 ? 0.38 : (mateIn == 2 ? 0.17 : (mateIn == 3 ? 0.06 : 0.00)) },
            };

            foreach (var (eloThreshold, probability) in mateChances.OrderByDescending(x => x.Key))
            {
                if (elo >= eloThreshold)
                {
                    return new Random().NextDouble() < probability;
                }
            }

            return false;
        }



        // Where script is sent after executing a move
        public async Task CentralMoveHub()
        {
            _moving = false;

            if (_robotComm)
            {
                if (_move == 0)   // White just moved
                {
                    if (!string.IsNullOrEmpty(_rcBlackBits))   // Black needs to move a piece
                    {
                        await _blackRobot.SendDataAsync(_rcBlackBits);
                        await _whiteRobot.SendDataAsync(_rcWhiteBits);
                    }

                    else
                    {
                        await _whiteRobot.SendDataAsync(_rcWhiteBits);
                    }
                }

                else
                {
                    if (!string.IsNullOrEmpty(_rcWhiteBits))
                    {
                        await _whiteRobot.SendDataAsync(_rcWhiteBits);
                        await _blackRobot.SendDataAsync(_rcBlackBits);
                    }

                    else
                    {
                        await _blackRobot.SendDataAsync(_rcBlackBits);
                    }                  
                }
                
                InfoSymbol.Visibility = Visibility.Collapsed;   // Hide "Move in progress" popup
                InProgressText.Visibility = Visibility.Collapsed;
                MoveInProgRect.Visibility = Visibility.Collapsed;
            }

            if (string.IsNullOrEmpty(_rcPastWhiteBits) && !string.IsNullOrEmpty(_rcWhiteBits))
            {
                _rcPastWhiteBits = _rcWhiteBits;
            }

            else if (!string.IsNullOrEmpty(_rcPastWhiteBits) && !string.IsNullOrEmpty(_rcWhiteBits))
            {
                _rcPastWhiteBits += $"\n{_rcWhiteBits}";
            }

            if (string.IsNullOrEmpty(_rcPastBlackBits) && !string.IsNullOrEmpty(_rcBlackBits))
            {
                _rcPastBlackBits = _rcBlackBits;
            }

            else if (!string.IsNullOrEmpty(_rcPastBlackBits) && !string.IsNullOrEmpty(_rcBlackBits))
            {
                _rcPastBlackBits += $"\n{_rcBlackBits}";
            }

            _rcWhiteBits = "";
            _rcBlackBits = "";

            if (!_isPaused && !_endGame)
            {
                PauseButton.IsEnabled = true;     

                if (_mode == 1)
                {
                    _moving = true;
                    _userTurn = false;

                    ComputerMove();
                }

                else if (_mode == 2)
                {
                    if ((_selectedColor.Content.ToString() == "White" && _move == 0) || (_selectedColor.Content.ToString() == "Black" && _move == 1))
                    {
                        _moving = true;
                        _userTurn = false;
                        ComputerMove();
                    }

                    else
                    {
                        _userTurn = true;
                        Chess_Board.IsHitTestVisible = true;

                        if (_selectedColor.Content.ToString() == "White")
                        {
                            EnableImagesWithTag("WhitePiece", true);
                            EnableImagesWithTag("BlackPiece", false);
                        }

                        else
                        {
                            EnableImagesWithTag("WhitePiece", false);
                            EnableImagesWithTag("BlackPiece", true);
                        }
                    }
                }

                else
                {
                    _userTurn = true;
                    Chess_Board.IsHitTestVisible = true;

                    if (_move == 1)
                    {
                        EnableImagesWithTag("WhitePiece", true);
                        EnableImagesWithTag("BlackPiece", false);
                    }
                    else
                    {
                        EnableImagesWithTag("WhitePiece", false);
                        EnableImagesWithTag("BlackPiece", true);
                    }
                    
                }
            }

            else
            {
                if (_isPaused && _holdResume)
                {
                    _inactivityTimer.Start();

                    _holdResume = false;
                    Play_Type.IsEnabled = true;
                    ResumeButton.IsEnabled = true;
                    EpsonRCConnection.IsEnabled = true;

                    InfoSymbol.Visibility = Visibility.Collapsed;   // Hide "Move in progress" popup
                    InProgressText.Visibility = Visibility.Collapsed;
                    MoveInProgRect.Visibility = Visibility.Collapsed;

                    if (_selectedPlayType.Content.ToString() == "Com Vs. Com")
                    {
                        CvC.IsEnabled = true;
                    }

                    else
                    {
                        UvCorUvU.IsEnabled = true;
                    }
                }

                if (_endGame && _robotComm)
                {
                    await ClearBoard();
                    _whiteRobot.Disconnect();
                    _blackRobot.Disconnect();
                }
            }
        }



        // Calculates FEN code for current position
        public void FENCode()
        {
            _previousFen = _fen;
            _fen = string.Empty;
            int emptyCount = 0;
            int PieceFound = 0;
            int castle = 0;
            _whiteMaterial = 0;
            _blackMaterial = 0;

            for (int fRow = 0; fRow < Chess_Board.RowDefinitions.Count; fRow++)   // Iterates through each row on board
            {
                for (int fColumn = 0; fColumn < Chess_Board.ColumnDefinitions.Count; fColumn++)   // Iterates through each column on board
                {
                    foreach (Image image in Chess_Board.Children.OfType<Image>())
                    {
                        int fPieceRow = Grid.GetRow(image);
                        int fPieceColumn = Grid.GetColumn(image);

                        if (fRow == fPieceRow && fColumn == fPieceColumn)   // If coordinates of image match tested square
                        {
                            PieceFound = 1;

                            if (emptyCount != 0)
                            {
                                _fen += $"{emptyCount}";
                                emptyCount = 0;
                            }

                            if (image.Name.StartsWith("WhitePawn"))
                            {
                                _fen += "P";
                                _whiteMaterial++;
                                break;
                            }

                            if (image.Name.StartsWith("WhiteRook"))
                            {
                                _fen += "R";
                                _whiteMaterial += 5;
                                break;
                            }

                            if (image.Name.StartsWith("WhiteKnight"))
                            {
                                _fen += "N";
                                _whiteMaterial += 3;
                                break;
                            }

                            if (image.Name.StartsWith("WhiteBishop"))
                            {
                                _fen += "B";
                                _whiteMaterial += 3;
                                break;
                            }

                            if (image.Name.StartsWith("WhiteQueen"))
                            {
                                _fen += "Q";
                                _whiteMaterial += 9;
                                break;
                            }

                            if (image.Name.StartsWith("WhiteKing"))
                            {
                                _fen += "K";
                                break;
                            }

                            if (image.Name.StartsWith("BlackPawn"))
                            {
                                _fen += "p";
                                _blackMaterial++;
                                break;
                            }

                            if (image.Name.StartsWith("BlackRook"))
                            {
                                _fen += "r";
                                _blackMaterial += 5;
                                break;
                            }

                            if (image.Name.StartsWith("BlackKnight"))
                            {
                                _fen += "n";
                                _blackMaterial += 3;
                                break;
                            }

                            if (image.Name.StartsWith("BlackBishop"))
                            {
                                _fen += "b";
                                _blackMaterial += 3;
                                break;
                            }

                            if (image.Name.StartsWith("BlackQueen"))
                            {
                                _fen += "q";
                                _blackMaterial += 9;
                                break;
                            }

                            if (image.Name.StartsWith("BlackKing"))
                            {
                                _fen += "k";
                                break;
                            }
                        }
                    }

                    if (PieceFound == 0)
                    {
                        emptyCount++;
                    }

                    PieceFound = 0;
                }

                if (emptyCount != 0)
                {
                    _fen += $"{emptyCount}";
                    emptyCount = 0;
                }

                if (fRow != 7)
                {
                    _fen += "/";
                }
            }

            if (_move == 1)   // Color to move
            {
                _fen += " w ";
            }

            else
            {
                _fen += " b ";
            }

            if (_cWK == 0)   // Castling rights
            {
                if (_cWR1 == 0 && _cWR2 == 0)
                {
                    _fen += "KQ";
                    castle = 1;
                }

                if (_cWR1 == 0 && _cWR2 != 0)
                {
                    _fen += "Q";
                    castle = 1;
                }

                if (_cWR1 != 0 && _cWR2 == 0)
                {
                    _fen += "K";
                    castle = 1;
                }
            }

            if (_cBK == 0)
            {
                if (_cBR1 == 0 && _cBR2 == 0)
                {
                    _fen += "kq";
                    castle = 1;
                }

                if (_cBR1 == 0 && _cBR2 != 0)
                {
                    _fen += "q";
                    castle = 1;
                }

                if (_cBR1 != 0 && _cBR2 == 0)
                {
                    _fen += "k";
                    castle = 1;
                }
            }

            if (castle == 0)
            {
                _fen += "-";
            }

            if (EnPassantSquare.Count == 1)   // En Passant square
            {
                if (_move == 1)
                {
                    _fen += $" {(char)('a' + _newColumn)}";
                    _fen += $"{_newRow + 3}";
                }

                if (_move == 0)
                {
                    _fen += $" {(char)('a' + _newColumn)}";
                    _fen += $"{_newRow - 1}";
                }
            }

            else
            {
                _fen += " -";
            }

            _fen += $" {_halfmove}";   // Halfmove number
            _fen += $" {_fullmove}";   // Fullmove number

            System.Diagnostics.Debug.WriteLine($"{_fen}");
        }



        // Prompts Stockfish to evaluate current position
        static async Task<string> RunStockfish(string fen, int depth, string stockfishPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = stockfishPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo };
            var outputBuilder = new StringBuilder();

            process.OutputDataReceived += (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);

                    if (e.Data.StartsWith("bestmove"))
                    {
                        process.StandardInput.WriteLine("quit");
                        process.StandardInput.Flush();
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();

            var writer = process.StandardInput;
            if (!writer.BaseStream.CanWrite)
                return "Unable to write to Stockfish.";

            await writer.WriteLineAsync($"setoption name MultiPV value 40");
            await writer.WriteLineAsync($"position fen {fen}");
            await writer.WriteLineAsync($"go depth {depth}");

            await process.WaitForExitAsync();
            return outputBuilder.ToString();
        }



        // Calculates if the position is check or checkmate for notation
        static async Task<string> CheckCalculator(string fen, string stockfishPath)
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = stockfishPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                CreateNoWindow = true
            };

            var process = new Process { StartInfo = startInfo };
            var outputBuilder = new StringBuilder();
            bool isCheckmate = false;
            bool isCheck = false;

            process.OutputDataReceived += async (sender, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                {
                    outputBuilder.AppendLine(e.Data);

                    if (e.Data.StartsWith("bestmove (none)"))
                    {
                        isCheckmate = true;     
                    }

                    else if (e.Data.StartsWith("Checkers:"))
                    {
                        string checkersData = e.Data.Substring(9).Trim();

                        if (!string.IsNullOrEmpty(checkersData) && !isCheckmate)
                        {
                            isCheck = true;
                        }                      
                    }
                }
            };

            process.Start();
            process.BeginOutputReadLine();

            var writer = process.StandardInput;
            if (!writer.BaseStream.CanWrite)
                return "Unable to write to Stockfish.";

            await writer.WriteLineAsync($"position fen {fen}");
            await writer.WriteLineAsync($"go depth 1");
            await writer.WriteLineAsync("d");
            await writer.WriteLineAsync("quit");
            await writer.FlushAsync();
            await process.WaitForExitAsync();

            if (isCheckmate)
            {
                return "#";
            }

            else
            {
                return isCheck ? "+" : "";
            }              
        }



        // Writes FEN code to Fen_codes.txt game log
        public async Task WriteFENCode()
        {
            using StreamWriter writer = new(_fenFilePath, true);

            int File1;
            int File2;
            int Rank1;
            int Rank2;

            if (!String.IsNullOrEmpty(_startPosition))
            {
                File1 = _oldColumn + 1;
                File2 = _newColumn + 1;
                Rank1 = 8 - _oldRow;
                Rank2 = 8 - _newRow;

                _pickBit1 = File1 - 1 + ((Rank1 - 1) * 8);
                _pickBit2 = File2 - 1 + ((Rank2 - 1) * 8);
                _placeBit1 = File2 - 1 + ((Rank2 - 1) * 8) + 64;

                if (_capture || _enPassant)   // If a piece was captured
                {
                    //startPosition = $"{startPosition}x";   // Add 'x' to notation. Ex: b4xc5

                    if (_takenPiece.Contains("Pawn"))
                    {
                        char pawnNo = _takenPiece[9];
                        int pawnNumber = int.Parse(pawnNo.ToString()) - 1;

                        _placeBit2 = _pawnPlace[pawnNumber];
                    }

                    else if (_takenPiece.Contains("Queen"))
                    {
                        char queenNo = _takenPiece[10];
                        int queenNumber = int.Parse(queenNo.ToString()) - 1;

                        _placeBit2 = _queenPlace[queenNumber];
                    }

                    else if (_takenPiece.Contains("Knight"))
                    {
                        char knightNo = _takenPiece[11];
                        int knightNumber = int.Parse(knightNo.ToString()) - 1;

                        _placeBit2 = _knightPlace[knightNumber];
                    }

                    else if (_takenPiece.Contains("Rook"))
                    {
                        char rookNo = _takenPiece[9];
                        int rookNumber = int.Parse(rookNo.ToString()) - 1;

                        _placeBit2 = _rookPlace[rookNumber];
                    }

                    else if (_takenPiece.Contains("Bishop"))
                    {
                        char bishopNo = _takenPiece[11];
                        int bishopNumber = int.Parse(bishopNo.ToString()) - 1;

                        _placeBit2 = _bishopPlace[bishopNumber];
                    }

                    if (_move == 0)   // White just moved
                    {
                        _rcWhiteBits = $"{_pickBit1}, {_placeBit1}";
                        _rcBlackBits = $"{_pickBit2}, {_placeBit2}";
                    }

                    else   // Black just moved
                    {
                        _rcWhiteBits = $"{_pickBit2}, {_placeBit2}";
                        _rcBlackBits = $"{_pickBit1}, {_placeBit1}";
                    }

                    if (_enPassant)   // If a piece was captured via En Passant
                    {
                        //endPosition = $"{endPosition} e.p.";   // Appends 'e.p.' to notation. Ex: b5xc6 e.p.

                        if (_move == 0)   // White just moved
                        {
                            _rcWhiteBits = $"{_pickBit1}, {_placeBit1}";
                            _rcBlackBits = $"{_pickBit2 - 8}, {_placeBit2}";
                        }

                        else   // Black just moved
                        {
                            _rcWhiteBits = $"{_pickBit2 + 8}, {_placeBit2}";
                            _rcBlackBits = $"{_pickBit1}, {_placeBit1}";
                        }
                    }
                }

                else
                {
                    if (_move == 0)   // White just moved
                    {
                        _rcWhiteBits = $"{_pickBit1}, {_placeBit1}";
                    }

                    else   // Black just moved
                    {
                        _rcBlackBits = $"{_pickBit1}, {_placeBit1}";
                    }
                }

                if (_promoted)   // If a pawn was promoted
                {
                    _endPosition = $"{_endPosition}{_promotionPiece}";   // Appends promotion letter to end of notation

                    char pawnNo = _promotedPawn[9];
                    int pawnNumber = int.Parse(pawnNo.ToString()) - 1;

                    _placeBit2 = _pawnPlace[pawnNumber];

                    if (_promotionPiece == 'q')
                    {
                        char queenNo = _activePiece[10];
                        int queenNumber = int.Parse(queenNo.ToString()) - 1;
                        
                        _promotedTo = "Q";
                        _pickBit3 = _queenPick[queenNumber];
                    }

                    else if (_promotionPiece == 'n')
                    {
                        char knightNo = _activePiece[11];
                        int knightNumber = int.Parse(knightNo.ToString()) - 1;

                        _promotedTo = "N";
                        _pickBit3 = _knightPick[knightNumber];
                    }

                    else if (_promotionPiece == 'r')
                    {
                        char rookNo = _activePiece[9];
                        int rookNumber = int.Parse(rookNo.ToString()) - 1;

                        _promotedTo = "R";
                        _pickBit3 = _rookPick[rookNumber];
                    }

                    else
                    {
                        char bishopNo = _activePiece[11];
                        int bishopNumber = int.Parse(bishopNo.ToString()) - 1;

                        _promotedTo = "B";
                        _pickBit3 = _bishopPick[bishopNumber];
                    }

                    if (_capture)
                    {
                        if (_takenPiece.Contains("Queen"))
                        {
                            char queenNo = _takenPiece[10];
                            int queenNumber = int.Parse(queenNo.ToString()) - 1;

                            _placeBit3 = _queenPlace[queenNumber];
                        }

                        else if (_takenPiece.Contains("Knight"))
                        {
                            char knightNo = _takenPiece[11];
                            int knightNumber = int.Parse(knightNo.ToString()) - 1;

                            _placeBit3 = _knightPlace[knightNumber];
                        }

                        else if (_takenPiece.Contains("Rook"))
                        {
                            char rookNo = _takenPiece[9];
                            int rookNumber = int.Parse(rookNo.ToString()) - 1;

                            _placeBit3 = _rookPlace[rookNumber];
                        }

                        else
                        {
                            char bishopNo = _takenPiece[11];
                            int bishopNumber = int.Parse(bishopNo.ToString()) - 1;

                            _placeBit3 = _bishopPlace[bishopNumber];
                        }

                        if (_move == 0)   // White just moved
                        {
                            _rcWhiteBits = $"{_pickBit1}, {_placeBit2}, {_pickBit3}, {_placeBit1}";
                            _rcBlackBits = $"{_pickBit2}, {_placeBit3}";
                        }

                        else   // Black just moved
                        {
                            _rcWhiteBits = $"{_pickBit2}, {_placeBit3}";
                            _rcBlackBits = $"{_pickBit1}, {_placeBit2}, {_pickBit3}, {_placeBit1}";
                        }
                    }

                    else
                    {
                        if (_move == 0)   // White just moved
                        {
                            _rcWhiteBits = $"{_pickBit1}, {_placeBit2}, {_pickBit3}, {_placeBit1}";
                        }

                        else   // Black just moved
                        {
                            _rcBlackBits = $"{_pickBit1}, {_placeBit2}, {_pickBit3}, {_placeBit1}";
                        }
                    }
                }

                if (_kingCastle)
                {
                    if (_move == 0)   // White just moved
                    {
                        _startPosition = "e1";
                        _endPosition = "g1";

                        _rcWhiteBits = "4, 70, 7, 69";
                    }

                    else   // Black just moved
                    {
                        _startPosition = "e8";
                        _endPosition = "g8";

                        _rcBlackBits = "60, 126, 63, 125";
                    }
                }

                if (_queenCastle)
                {
                    if (_move == 0)   // White just moved
                    {
                        _startPosition = "e1";
                        _endPosition = "c1";

                        _rcWhiteBits = "4, 66, 0, 67";
                    }

                    else   // Black just moved
                    {
                        _startPosition = "e8";
                        _endPosition = "c8";

                        _rcBlackBits = "60, 122, 56, 123";
                    }
                }

                _executedMove = $"{_startPosition}{_endPosition}";
                System.Diagnostics.Debug.Write($"\nPlayed Move: {_executedMove}\n");
                System.Diagnostics.Debug.Write($"rcWhiteBits: {_rcWhiteBits}\n");
                System.Diagnostics.Debug.Write($"rcBlackBits: {_rcBlackBits}\n");

                string checkModifier = await CheckCalculator(_fen, _stockfishPath!);

                _pgnMove = UCItoPGNConverter.Convert(_previousFen, _executedMove, _kingCastle, _queenCastle, _enPassant, _promoted, _promotedTo, checkModifier);
                System.Diagnostics.Debug.Write("PGN Move: " + _pgnMove);

                if (_pieceSounds)
                {
                    string sound = _pgnMove.Contains('#') ? "GameEnd" :
                                   _pgnMove.Contains('+') ? "PieceCheck" :
                                   _pgnMove.Contains('=') ? "PiecePromote" :
                                   _pgnMove.Contains('x') ? "PieceCapture" :
                                   _pgnMove.Contains('-') ? "PieceCastle" :
                                   (_mode == 1 || (_mode == 2 &&
                                   ((_selectedColor.Content.ToString() == "White" && _move == 0) ||
                                    (_selectedColor.Content.ToString() == "Black" && _move == 1))))
                                       ? "PieceOpponent"
                                       : "PieceMove";

                    PlaySound(sound);
                }
            }

            _capture = false;   // Reset all flags to false
            _capturedPiece = null;
            _enPassantCreated = false;
            _enPassant = false;
            _promoted = false;
            _kingCastle = false;
            _queenCastle = false;

            writer.WriteLine($"\nMove Played: {_pgnMove}   Resulting Position: {_fen}");

            string[] fenParts = _fen.Split(' ');
            string currentFEN = $"{fenParts[0]} {fenParts[1]};";
            _gameFens.Add(currentFEN);

            Dictionary<string, int> fenCounts = _gameFens.Select(fen => fen[..fen.LastIndexOf(';')]).GroupBy(fen => fen).ToDictionary(group => group.Key, group => group.Count());

            if (fenCounts.Any(pair => pair.Value >= 3))   // Checks for 3 of any instance for three-fold repetition
            {
                _threefoldRepetition = true;
            }

            Color borderColor = (Color)ColorConverter.ConvertFromString("#FFD0D0D0");   // Move table for evaluation interface
            SolidColorBrush borderBrush = new(borderColor);
            FontFamily fontFamily = new("Sans Serif Collection");
            FontWeight fontWeight = FontWeights.Bold;

            RowDefinition newRowDefinition = new()
            {
                Height = new GridLength(30)
            };

            Border newBorder = new()
            {
                BorderThickness = new Thickness(0.5),
                BorderBrush = borderBrush,
            };

            TextBlock newMoveNumber = new()
            {
                Text = $"{_fullmove}.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = borderBrush,
                FontFamily = fontFamily,
                FontSize = 14,
            };

            TextBlock newWhiteMove = new()
            {
                Text = $"{_pgnMove}",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = borderBrush,
                FontFamily = fontFamily,
                FontSize = 14,
                FontWeight = fontWeight,
                Padding = new Thickness(10, 0, 0, 0),
            };

            TextBlock newBlackMove = new()
            {
                Text = $"{_pgnMove}",
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = borderBrush,
                FontFamily = fontFamily,
                FontSize = 14,
                FontWeight = fontWeight,
                Padding = new Thickness(10, 0, 0, 0),
            };

            if (_move == 0)
            {
                Grid.SetRow(newBorder, _fullmove);
                Grid.SetColumnSpan(newBorder, 3);
                Grid.SetRow(newMoveNumber, _fullmove);
                Grid.SetColumn(newMoveNumber, 0);
                Grid.SetRow(newWhiteMove, _fullmove);
                Grid.SetColumn(newWhiteMove, 1);

                Moves.RowDefinitions.Add(newRowDefinition);
                Moves.Children.Add(newBorder);
                Moves.Children.Add(newMoveNumber);
                Moves.Children.Add(newWhiteMove);

                File.AppendAllText(_pgnFilePath, $"{_fullmove}. {_pgnMove} ");
            }

            else
            {
                Grid.SetRow(newBlackMove, _fullmove - 1);
                Grid.SetColumn(newBlackMove, 2);

                Moves.Children.Add(newBlackMove);

                File.AppendAllText(_pgnFilePath, $"{_pgnMove} ");
            }
        }



        // Sets up pieces for game
        public async Task SetupBoard()
        {
            _boardSet = true;

            EnableImagesWithTag("WhitePiece", false);
            EnableImagesWithTag("BlackPiece", false);

            if (_fullmove == 1 && _move == 1)   // Game just started
            {
                for (int i = 1; i < 9; i++)   // Pawn setup
                {
                    _pickBit1 = _pawnPick[i - 1];
                    _placeBit1 = _whitePawnOrigin[i - 1];

                    if (i == 1)   // If this is first entry
                    {
                        _rcWhiteBits = $"{_pickBit1}, {_placeBit1}";
                    }

                    else
                    {
                        _rcWhiteBits += $", {_pickBit1}, {_placeBit1}";
                    }

                    _pickBit1 = _pawnPick[i - 1];
                    _placeBit1 = _blackPawnOrigin[i - 1];

                    if (i == 1)   // If this is first entry
                    {
                        _rcBlackBits = $"{_pickBit1}, {_placeBit1}";
                    }

                    else
                    {
                        _rcBlackBits += $", {_pickBit1}, {_placeBit1}";
                    }
                }

                for (int i = 1; i < 3; i++)   // Rook, knight, and bishop setup
                {
                    _pickBit1 = _rookPick[i - 1];
                    _placeBit1 = _whiteRookOrigin[i - 1];
                    _rcWhiteBits += $", {_pickBit1}, {_placeBit1}";

                    _placeBit1 = _blackRookOrigin[i - 1];
                    _rcBlackBits += $", {_pickBit1}, {_placeBit1}";

                    _pickBit1 = _knightPick[i - 1];
                    _placeBit1 = _whiteKnightOrigin[i - 1];
                    _rcWhiteBits += $", {_pickBit1}, {_placeBit1}";

                    _placeBit1 = _blackKnightOrigin[i - 1];
                    _rcBlackBits += $", {_pickBit1}, {_placeBit1}";

                    _pickBit1 = _bishopPick[i - 1];
                    _placeBit1 = _whiteBishopOrigin[i - 1];
                    _rcWhiteBits += $", {_pickBit1}, {_placeBit1}";

                    _placeBit1 = _blackBishopOrigin[i - 1];
                    _rcBlackBits += $", {_pickBit1}, {_placeBit1}";
                }

                _pickBit1 = _queenPick[0];
                _placeBit1 = _whiteQueenOrigin;
                _rcWhiteBits += $", {_pickBit1}, {_placeBit1}";

                _placeBit1 = _blackQueenOrigin;
                _rcBlackBits += $", {_pickBit1}, {_placeBit1}";

                _pickBit1 = _kingPick;
                _placeBit1 = _whiteKingOrigin;
                _rcWhiteBits += $", {_pickBit1}, {_placeBit1}";

                _placeBit1 = _blackKingOrigin;
                _rcBlackBits += $", {_pickBit1}, {_placeBit1}";
            }

            else   // Game is in progress
            {
                for (int fRow = 0; fRow < Chess_Board.RowDefinitions.Count; fRow++)
                {
                    for (int fColumn = 0; fColumn < Chess_Board.ColumnDefinitions.Count; fColumn++)
                    {
                        foreach (Image image in Chess_Board.Children.OfType<Image>())
                        {
                            int fPieceRow = Grid.GetRow(image);
                            int fPieceColumn = Grid.GetColumn(image);

                            if (fRow == fPieceRow && fColumn == fPieceColumn)   // If coordinates of image match tested square
                            {
                                int File1 = fPieceColumn + 1;
                                int Rank1 = 8 - fPieceRow;
                                string foundPiece = image.Name;

                                _placeBit1 = File1 - 1 + ((Rank1 - 1) * 8) + 64;

                                if (image.Name.Contains("Pawn"))   // Pawn was found
                                {
                                    char pawnNo = foundPiece[9];
                                    int pawnNumber = int.Parse(pawnNo.ToString()) - 1;

                                    _pickBit1 = _pawnPick[pawnNumber];
                                }

                                else if (image.Name.Contains("Rook"))   // Rook was found
                                {
                                    char rookNo = foundPiece[9];
                                    int rookNumber = int.Parse(rookNo.ToString()) - 1;

                                    _pickBit1 = _rookPick[rookNumber];
                                }

                                else if (image.Name.Contains("Knight"))   // Knight was found
                                {
                                    char knightNo = foundPiece[11];
                                    int knightNumber = int.Parse(knightNo.ToString()) - 1;

                                    _pickBit1 = _knightPick[knightNumber];
                                }

                                else if (image.Name.Contains("Bishop"))   // Bishop was found
                                {
                                    char bishopNo = foundPiece[11];
                                    int bishopNumber = int.Parse(bishopNo.ToString()) - 1;

                                    _pickBit1 = _bishopPick[bishopNumber];
                                }

                                else if (image.Name.Contains("Queen"))   // Queen was found
                                {
                                    char queenNo = foundPiece[10];
                                    int queenNumber = int.Parse(queenNo.ToString()) - 1;

                                    _pickBit1 = _queenPick[queenNumber];
                                }

                                else   // King was found
                                {
                                    _pickBit1 = _kingPick;
                                }

                                if (image.Name.StartsWith("White"))   // White piece was found
                                {
                                    if (string.IsNullOrEmpty(_rcWhiteBits))
                                    {
                                        _rcWhiteBits = $"{_pickBit1}, {_placeBit1}";
                                    }

                                    else
                                    {
                                        _rcWhiteBits += $", {_pickBit1}, {_placeBit1}";
                                    }
                                }

                                else   // Black piece was found
                                {
                                    if (string.IsNullOrEmpty(_rcBlackBits))
                                    {
                                        _rcBlackBits = $"{_pickBit1}, {_placeBit1}";
                                    }

                                    else
                                    {
                                        _rcBlackBits += $", {_pickBit1}, {_placeBit1}";
                                    }
                                }                      
                            }
                        }
                    }
                }
            }

            await _whiteRobot.SendDataAsync(_rcWhiteBits);
            await _blackRobot.SendDataAsync(_rcBlackBits);

            _rcWhiteBits = "";
            _rcBlackBits = "";

            EnableImagesWithTag("WhitePiece", true);
            EnableImagesWithTag("BlackPiece", true);
        }



        // Resets Epson chess board
        public async Task ClearBoard()
        {
            _boardSet = false;

            for (int fRow = 0; fRow < Chess_Board.RowDefinitions.Count; fRow++)   // Iterates through each row on board
            {
                for (int fColumn = 0; fColumn < Chess_Board.ColumnDefinitions.Count; fColumn++)   // Iterates through each column on board
                {
                    foreach (Image image in Chess_Board.Children.OfType<Image>())
                    {
                        int fPieceRow = Grid.GetRow(image);
                        int fPieceColumn = Grid.GetColumn(image);

                        if (fRow == fPieceRow && fColumn == fPieceColumn)   // If coordinates of image match tested square
                        {
                            int File1 = fPieceColumn + 1;
                            int Rank1 = 8 - fPieceRow;
                            string foundPiece = image.Name;

                            _pickBit1 = File1 - 1 + ((Rank1 - 1) * 8);

                            if (image.Name.Contains("Pawn"))   // Pawn was found
                            {
                                char pawnNo = foundPiece[9];
                                int pawnNumber = int.Parse(pawnNo.ToString()) - 1;

                                _placeBit1 = _pawnPlace[pawnNumber];
                            }

                            else if (image.Name.Contains("Rook"))   // Rook was found
                            {
                                char rookNo = foundPiece[9];
                                int rookNumber = int.Parse(rookNo.ToString()) - 1;

                                _placeBit1 = _rookPlace[rookNumber];
                            }

                            else if (image.Name.Contains("Knight"))   // Knight was found
                            {
                                char knightNo = foundPiece[11];
                                int knightNumber = int.Parse(knightNo.ToString()) - 1;

                                _placeBit1 = _knightPlace[knightNumber];
                            }

                            else if (image.Name.Contains("Bishop"))   // Bishop was found
                            {
                                char bishopNo = foundPiece[11];
                                int bishopNumber = int.Parse(bishopNo.ToString()) - 1;

                                _placeBit1 = _bishopPlace[bishopNumber];
                            }

                            else if (image.Name.Contains("Queen"))   // Queen was found
                            {
                                char queenNo = foundPiece[10];
                                int queenNumber = int.Parse(queenNo.ToString()) - 1;

                                _placeBit1 = _queenPlace[queenNumber];
                            }

                            else   // King was found
                            {
                                _placeBit1 = _kingPlace;
                            }

                            if (image.Name.StartsWith("White"))   // White piece was found
                            {
                                if (string.IsNullOrEmpty(_rcWhiteBits))
                                {
                                    _rcWhiteBits = $"{_pickBit1}, {_placeBit1}";
                                }

                                else
                                {
                                    _rcWhiteBits += $", {_pickBit1}, {_placeBit1}";
                                }
                            }

                            else   // Black piece was found
                            {
                                if (string.IsNullOrEmpty(_rcBlackBits))
                                {
                                    _rcBlackBits = $"{_pickBit1}, {_placeBit1}";
                                }

                                else
                                {
                                    _rcBlackBits += $", {_pickBit1}, {_placeBit1}";
                                }
                            }                                          
                        }
                    }
                }
            }

            await _whiteRobot.SendDataAsync(_rcWhiteBits);
            await _blackRobot.SendDataAsync(_rcBlackBits);

            _rcWhiteBits = "";
            _rcBlackBits = "";
        }
    }
}
// The #1 Boyfriend EVER made this... I am so proud of him and he is one of the smartest people I know! I love him more than I can say. <3