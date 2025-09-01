using Chess_Project.Configuration;
using System;
using System.Collections.Generic;
using System.Data;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Media;
using System.Text;
using System.Threading;
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
        public GameSession Session { get; } = new();
        private GameOver? _gameOver;
        private TaskCompletionSource<bool>? _userMoveTcs;
        private bool _isStarting;
        private CancellationTokenSource? _gameCts;
        private CancellationTokenSource? _loopCts;
        private TaskCompletionSource<bool>? _loopStoppedTcs;

        #region Epson Configuration (local)

        private readonly string _whiteRobotIp = "192.168.0.2";
        private readonly string _blackRobotIp = "192.168.0.3";
        private readonly int _whiteRobotPort = 5000;
        private readonly int _blackRobotPort = 5000;

        private readonly double[] _whiteRobotBaseDeltas = [-1.218, -67.697];
        private readonly double[] _blackRobotBaseDeltas = [0.616, -80.406];
        private readonly double _whiteDeltaScalar = 0.095952;
        private readonly double _blackDeltaScalar = 0.0895833;

        #endregion

        #region Cognex Configuration (local)

        private readonly string _whiteCognexIp = "192.168.0.12";
        private readonly string _blackCognexIp = "192.168.0.13";
        private readonly int _whiteCognexTcpPort = 23;
        private readonly int _blackCognexTcpPort = 23;
        private readonly int _whiteCognexListenPort = 3000;
        private readonly int _blackCognexListenPort = 3001;

        #endregion

        #region Epson Pick/Place & Origins (constants)

        // Off-board pick locations
        private readonly int[] _pawnPick = [136, 137, 138, 139, 140, 141, 142, 143];
        private readonly int[] _rookPick = [128, 135, 148, 149, 151, 151];
        private readonly int[] _knightPick = [129, 134, 156, 157, 158, 159];
        private readonly int[] _bishopPick = [130, 133, 152, 153, 154, 155];
        private readonly int[] _queenPick = [131, 144, 145, 146, 147];
        private readonly int _kingPick = 132;

        // Off-board place locations
        private readonly int[] _pawnPlace = [168, 169, 170, 171, 172, 173, 174, 175];
        private readonly int[] _rookPlace = [160, 167, 180, 181, 182, 183];
        private readonly int[] _knightPlace = [161, 166, 188, 189, 190, 191];
        private readonly int[] _bishopPlace = [162, 165, 184, 185, 186, 187];
        private readonly int[] _queenPlace = [163, 176, 177, 178, 179];
        private readonly int _kingPlace = 164;

        // On-board place locations
        private readonly int[] _whitePawnOrigin = [72, 73, 74, 75, 76, 77, 78, 79];
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

        #endregion

        #region Services (runtime)

        private DispatcherTimer _inactivityTimer;
        private readonly Dictionary<string, SoundPlayer> _soundPlayer = [];

        private EpsonController _whiteRobot;
        private EpsonController _blackRobot;
        private CognexController _whiteCognex;
        private CognexController _blackCognex;

        #endregion

        #region Board/Position Collections (mixed)

        public List<Tuple<int, int>> ImageCoordinates { get => Session.ImageCoordinates; set => Session.ImageCoordinates = value; }
        public List<Tuple<int, int>> EnPassantSquare { get => Session.EnPassantSquare; set => Session.EnPassantSquare = value; }
        private List<string> GameFens { get => Session.GameFens; set => Session.GameFens = value; }

        private sealed class PieceInit
        {
            public required Image Img { get; set; }
            public required string Name { get; init; }
            public required int Row { get; init; }
            public required int Col { get; init; }
            public required int Z { get; init; }
            public required bool Enabled { get; init; }
            public object? Tag { get; init; }
        }

        private Dictionary<string, PieceInit> _initialPieces = [];

        #endregion

        #region Move & Notation Metadata (session-backed)

        private string? PromotedPawn { get => Session.PromotedPawn; set => Session.PromotedPawn = value; }
        private string? PromotedTo { get => Session.PromotedTo; set => Session.PromotedTo = value; }
        private string? ActivePiece { get => Session.ActivePiece; set => Session.ActivePiece = value; }
        private string? TakenPiece { get => Session.TakenPiece; set => Session.TakenPiece = value; }
        private string? Fen { get => Session.Fen; set => Session.Fen = value; }
        private string? PreviousFen { get => Session.PreviousFen; set => Session.PreviousFen = value; }

        #endregion

        #region Epson RC+ Bit Signals (session-backed)

        private int? PickBit1 { get => Session.PickBit1; set => Session.PickBit1 = value; }
        private int? PickBit2 { get => Session.PickBit2; set => Session.PickBit2 = value; }
        private int? PickBit3 { get => Session.PickBit3; set => Session.PickBit3 = value; }
        private int? PlaceBit1 { get => Session.PlaceBit1; set => Session.PlaceBit1 = value; }
        private int? PlaceBit2 { get => Session.PlaceBit2; set => Session.PlaceBit2 = value; }
        private int? PlaceBit3 { get => Session.PlaceBit3; set => Session.PlaceBit3 = value; }

        private string? WhiteBits { get => Session.WhiteBits; set => Session.WhiteBits = value; }
        private string? BlackBits { get => Session.BlackBits; set => Session.BlackBits = value; }
        private string? PrevWhiteBits { get => Session.PrevWhiteBits; set => Session.PrevWhiteBits = value; }
        private string? PrevBlackBits { get => Session.PrevBlackBits; set => Session.PrevBlackBits = value; }

        #endregion

        #region Castling Flags (session-backed)

        private int CWK { get => Session.CWK; set => Session.CWK = value; }
        private int CWR1 { get => Session.CWR1; set => Session.CWR1 = value; }
        private int CWR2 { get => Session.CWR2; set => Session.CWR2 = value; }
        private int CBK { get => Session.CBK; set => Session.CBK = value; }
        private int CBR1 { get => Session.CBR1; set => Session.CBR1 = value; }
        private int CBR2 { get => Session.CBR2; set => Session.CBR2 = value; }
        private bool KingCastle { get => Session.KingCastle; set => Session.KingCastle = value; }
        private bool QueenCastle { get => Session.QueenCastle; set => Session.QueenCastle = value; }

        #endregion

        #region Capture & Promotion Counters/Flags (session-backed)

        private int NumWN { get => Session.NumWN; set => Session.NumWN = value; }
        private int NumWB { get => Session.NumWB; set => Session.NumWB = value; }
        private int NumWR { get => Session.NumWR; set => Session.NumWR = value; }
        private int NumWQ { get => Session.NumWQ; set => Session.NumWQ = value; }
        private int NumBN { get => Session.NumBN; set => Session.NumBN = value; }
        private int NumBB { get => Session.NumBB; set => Session.NumBB = value; }
        private int NumBR { get => Session.NumBR; set => Session.NumBR = value; }
        private int NumBQ { get => Session.NumBQ; set => Session.NumBQ = value; }

        private bool Capture { get => Session.Capture; set => Session.Capture = value; }
        private bool EnPassantCreated { get => Session.EnPassantCreated; set => Session.EnPassantCreated = value; }
        private bool EnPassant { get => Session.EnPassant; set => Session.EnPassant = value; }
        private bool Promoted { get => Session.Promoted; set => Session.Promoted = value; }
        private char PromotionPiece { get => Session.PromotionPiece; set => Session.PromotionPiece = value; }

        #endregion

        #region Turn & State Tracking (session-backed)

        private int Move { get => Session.Move; set => Session.Move = value; }
        private int Halfmove { get => Session.Halfmove; set => Session.Halfmove = value; }
        private int Fullmove { get => Session.Fullmove; set => Session.Fullmove = value; }

        private bool UserTurn { get => Session.UserTurn; set => Session.UserTurn = value; }
        private bool MoveInProgress { get => Session.MoveInProgress; set => Session.MoveInProgress = value; }
        private bool HoldResume { get => Session.HoldResume; set => Session.HoldResume = value; }
        private bool WasPlayable { get => Session.WasPlayable; set => Session.WasPlayable = value; }
        private bool WasResumable { get => Session.WasResumable; set => Session.WasResumable = value; }
        private bool IsPaused { get => Session.IsPaused; set => Session.IsPaused = value; }
        private bool BoardSet { get => Session.BoardSet; set => Session.BoardSet = value; }
        
        #endregion

        #region Engine/CPU Flags (session-backed)

        private bool TopEngineMove { get => Session.TopEngineMove; set => Session.TopEngineMove = value; }

        #endregion

        #region Stockfish Evaluation (session-backed)

        private int WhiteMaterial { get => Session.WhiteMaterial; set => Session.WhiteMaterial = value; }
        private int BlackMaterial { get => Session.BlackMaterial; set => Session.BlackMaterial = value; }
        private double QuantifiedEvaluation { get => Session.QuantifiedEvaluation; set => Session.QuantifiedEvaluation = value; }
        private string DisplayedAdvantage { get => Session.DisplayedAdvantage; set => Session.DisplayedAdvantage = value; }

        #endregion

        #region Game End Flags (session-backed)

        private bool EndGame { get => Session.EndGame; set => Session.EndGame = value; }
        private bool ThreefoldRepetition { get => Session.ThreefoldRepetition; set => Session.ThreefoldRepetition = value; }

        #endregion

        #region UI Combo Box Selections (local)

        private ComboBoxItem? _selectedPlayType;
        private ComboBoxItem? _selectedElo;
        private ComboBoxItem? _selectedColor;
        private ComboBoxItem? _selectedWhiteElo;
        private ComboBoxItem? _selectedBlackElo;

        #endregion

        #region UI Piece Selections (local)

        private Image? _clickedPawn = null;
        private Image? _clickedKnight = null;
        private Image? _clickedBishop = null;
        private Image? _clickedRook = null;
        private Image? _clickedQueen = null;
        private Image? _clickedKing = null;
        private Image? _capturedPiece = null;
        private Image? _selectedPiece = null;

        #endregion

        #region Game Settings Counters/Flags (local)

        private GameMode _gameMode = GameMode.Blank;

        private bool _pieceSounds;
        private bool _moveConfirm;
        private bool _robotMotion;
        private bool _cameraVision;

        private readonly int _timeoutDuration = 30;

        #endregion

        #region Working Coordinates & Notation Scratch (local)

        private int _oldRow;
        private int _oldCol;
        private int _newRow;
        private int _newCol;

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

        private int _whiteKingRow;
        private int _whiteKingCol;
        private int _blackKingRow;
        private int _blackKingCol;

        #endregion

        #region Board Orientation (local)

        private int _flip;
        private int _theta;

        #endregion    

        #region File Paths (local)

        private readonly string _executableDirectory;
        private string? _stockfishPath;
        private readonly string _fenFilePath = "FEN_Codes.txt";
        private readonly string _pgnFilePath = "GamePGN.pgn";
        private string _backgroundImagePath;
        private string _boardImagePath;
        private Preferences _preferences;

        #endregion

        #region Program Initialization

        /// <summary>
        /// Constructs the main window and performs one-time app setup:
        /// <list type="bullet">
        ///     <item><description>Loads user preferences.</description></item>
        ///     <item><description>Preloads sounds.</description></item>
        ///     <item><description>Starts the inactivity timer.</description></item>
        ///     <item><description>Initializes robot/camera connections.</description></item>
        ///     <item><description>Applies the selected theme.</description></item>
        ///     <item><description>Wires a global mouse handler to reset inactivity.</description></item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// <para>✅ Updated on 8/29/2025</para>
        /// </remarks>
        public MainWindow()
        {
            InitializeComponent();
            _executableDirectory = AppDomain.CurrentDomain.BaseDirectory;

            InitializeUserPreferences();
            InitializeSounds();
            SetupInactivityTimer();
            InitializeConnections();
            ApplyThemeFormatting();

            this.PreviewMouseDown += MainWindow_PreviewMouseDown;
        }

        /// <summary>
        /// Loads persisted user preferences, applies them to UI and runtime flags,
        /// and resolves all asset paths (pieces, board, background). Falls back to
        /// defaults if loading fails. Also snapshots the initial board layout once
        /// the visual tree is ready.
        /// </summary>
        /// <remarks>✅ Updated on 8/29/2025</remarks>
        private void InitializeUserPreferences()
        {
            // Resolve Stockfish path and exit if missing (fatal)
            _stockfishPath = System.IO.Path.Combine(_executableDirectory, "Stockfish.exe");

            if (!File.Exists(_stockfishPath))
            {
                var msg = $"Stockfish executable not found at:\n{_stockfishPath}\n\n" +
                           "The game cannot run without Stockfish.";
                ChessLog.LogFatal(msg);
                MessageBox.Show(msg, "Fatal Error - Stockfish Missing", MessageBoxButton.OK, MessageBoxImage.Error);

                Application.Current.Shutdown(-1);
                return;  // keep compiler happy; Shutdown tears down the app
            }

            // Load preferences with fallback
            try
            {
                _preferences = PreferencesManager.Load();
            }
            catch (Exception ex)
            {
                ChessLog.LogWarning("Failed to load preferences. Using defaults.", ex);
                _preferences = new Preferences();
            }

            // Apply toggles (keep internal flags in sync with UI)
            Sounds.IsChecked = _pieceSounds = _preferences.PieceSounds;
            ConfirmMove.IsChecked = _moveConfirm = _preferences.ConfirmMove;
            EpsonMotion.IsChecked = _robotMotion = _preferences.EpsonMotion;
            CognexVision.IsChecked = _cameraVision = _preferences.CognexVision;

            // Build asset roots
            string assetRoot = System.IO.Path.Combine(_executableDirectory, "Assets");
            string piecesRoot = System.IO.Path.Combine(assetRoot, "Pieces", _preferences.Pieces);

            // Fill per-piece image paths
            SetPieceImagePaths(piecesRoot);

            // Board & background skins
            _backgroundImagePath = System.IO.Path.Combine(_executableDirectory, "Assets", "Backgrounds", $"{_preferences.Background}.png");
            _boardImagePath = System.IO.Path.Combine(_executableDirectory, "Assets", "Boards", $"{_preferences.Board}.png");

            // Snapshot initial board once the tree is ready (avoids race with XAML load)
            Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    try { SnapshotInitialBoard(); }
                    catch (Exception ex) { ChessLog.LogWarning("Failed to snapshot board.", ex); }
                }),
                System.Windows.Threading.DispatcherPriority.Loaded
            );
        }

        /// <summary>
        /// Captures the initial state of all chess piece <see cref="Image"/> elements
        /// on the board and stores them in <see cref="_initialPieces"/> for later reset.
        /// </summary>
        /// <remarks>
        /// This method iterates through <see cref="Chess_Board"/> children, filtering
        /// only those tagged as "WhitePiece" or "BlackPiece". For each piece it records:
        /// <list type="bullet">
        ///     <item><description>Name</description></item>
        ///     <item><description>Grid row and column</description></item>
        ///     <item><description>Z-index</description></item>
        ///     <item><description>Enabled state</description></item>
        ///     <item><description>Tag</description></item>
        /// </list>
        /// These snapshots allow the board to be restored to its original setup
        /// (e.g., after a game reset) without reloading from XAML.
        /// Call this once after the UI has been fully initialized and pieces are loaded.
        /// <para>✅ Written on 8/28/2025</para>
        /// </remarks>
        private void SnapshotInitialBoard()
        {
            _initialPieces = Chess_Board.Children
                .OfType<Image>()
                .Where(i => Equals(i.Tag, "WhitePiece") || Equals(i.Tag, "BlackPiece"))
                .ToDictionary(
                    i => i.Name,
                    i => new PieceInit
                    {
                        Img = i,
                        Name = i.Name,
                        Row = Grid.GetRow(i),
                        Col = Grid.GetColumn(i),
                        Z = Panel.GetZIndex(i),
                        Enabled = i.IsEnabled,
                        Tag = i.Tag
                    });
        }

        /// <summary>
        /// Preloads chess sound effects into memory for fast playback.
        /// </summary>
        /// <remarks>
        /// Looks for a fixed set of *.wav files in <c>Assets/Sounds</c>. Missing files are logged and skipped.
        /// Disposes any previously loaded players before reloading.
        /// <para>✅ Updated on 8/29/2025</para>
        /// </remarks>
        private void InitializeSounds()
        {
            string soundsDirectory = System.IO.Path.Combine(_executableDirectory, "Assets", "Sounds");
            if (!Directory.Exists(soundsDirectory))
            {
                ChessLog.LogWarning($"Sounds directory not found: {soundsDirectory}");
                return;
            }

            string[] soundNames =
            [
                "GameEnd", "GameStart", "PieceCapture", "PieceCastle", "PieceCheck",
                "PieceIllegal", "PieceMove", "PieceOpponent", "PiecePromote"
            ];

            foreach (var sound in soundNames)
            {
                string path = System.IO.Path.Combine(soundsDirectory, $"{sound}.wav");
                if (!File.Exists(path))
                {
                    ChessLog.LogWarning($"Sound file not found: {path}");
                    continue;
                }

                SoundPlayer player = new(path);
                player.Load();
                _soundPlayer[sound] = player;
            }
        }

        /// <summary>
        /// Creates (if needed) and starts the UI-thread inactivity timer that fires after
        /// <see cref="_timeoutDuration"/> seconds of inactivity.
        /// </summary>
        /// <remarks>
        /// Uses <see cref="DispatcherTimer"/> (UI thread). The handler <see cref="InactivityTimer_Tick"/>
        /// is (re)attached safely so repeated calls won't double-subscribe.
        /// <para>✅ Updated on 8/29/2025</para>
        /// </remarks>
        private void SetupInactivityTimer()
        {
            _inactivityTimer ??= new DispatcherTimer(DispatcherPriority.Background)
            {
                Interval = TimeSpan.FromSeconds(_timeoutDuration)
            };

            // Ensure we don't end up with duplicate handlers if called more than once
            _inactivityTimer.Tick -= InactivityTimer_Tick;
            _inactivityTimer.Tick += InactivityTimer_Tick;

            _inactivityTimer.Start();
        }

        /// <summary>
        /// Instantiates Cognex camera and Epson robot controller objects for the white and black sides
        /// using configured IPs/ports, then kicks off the initial connection routine in the background.
        /// </summary>
        /// <remarks>
        /// This method replaces any existing controller instances. If <see cref="HandleInitialConnectionsAsync"/>
        /// throws, its exceptions are observed inside that method. Since the call is not awaited here,
        /// connection progress occurs asynchronously after this method returns.
        /// <para>✅ Updated on 8/29/2025</para>
        /// </remarks>
        private void InitializeConnections()
        {
            // Clean up old instances to avoid socket leaks if this is called more than once
            _whiteRobot?.Disconnect();
            _blackRobot?.Disconnect();
            _whiteCognex?.Disconnect();
            _blackCognex?.Disconnect();

            _whiteCognex = new CognexController(_whiteCognexIp, _whiteCognexTcpPort, ChessColor.White);
            _blackCognex = new CognexController(_blackCognexIp, _blackCognexTcpPort, ChessColor.Black);
            _whiteRobot = new EpsonController(_whiteRobotIp, _whiteRobotPort, _whiteRobotBaseDeltas, _whiteDeltaScalar, ChessColor.White, _whiteCognex, _whiteCognexListenPort);
            _blackRobot = new EpsonController(_blackRobotIp, _blackRobotPort, _blackRobotBaseDeltas, _blackDeltaScalar, ChessColor.Black, _blackCognex, _blackCognexListenPort);
            _ = HandleInitialConnectionsAsync();
        }

        /// <summary>
        /// Attempts initial connections for Epson robots and Cognex cameras based on the current
        /// toggle flags (<see cref="_robotMotion"/> and <see cref="_cameraVision"/>), while updating
        /// UI status indicators and persisting the resulting states to preferences.
        /// Displays a busy overlay during the operation and always restores UI interactivity in <see langword="finally"/>.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><description>Sets status lights to Yellow while connecting; Green on success, Red on failure or when the corresponding feature is disabled.</description></item>
        ///     <item><description>Connection attempts for white/black devices run in parallel via <see cref="Task.WhenAll"/>.</description></item>
        ///     <item><description>Persists <see cref="_preferences.EpsonMotion"/> and <see cref="_preferences.CognexVision"/> based on the final connection result.</description></item>
        ///     <item><description>If both features are disabled, no connection is attempted and all lights are set to red.</description></item>
        /// </list>
        /// Any exceptions thrown by inner calls are expected to be handled by those methods;
        /// otherwise they will bubble up to the caller.
        /// <para>✅ Updated on 8/29/2025</para>
        /// </remarks>
        /// <returns>A task that completes when all requested connection work and UI updates are finished.</returns>
        private async Task HandleInitialConnectionsAsync()
        {
            if (_robotMotion || _cameraVision)
            {
                DisableEpsonElements();
                UpdateRectangleClip(65, Visibility.Visible, 75);

                // "Attempting" feedback
                SetEpsonStatusLight(ChessColor.White, _robotMotion ? Brushes.Yellow : Brushes.Red);
                SetEpsonStatusLight(ChessColor.Black, _robotMotion ? Brushes.Yellow : Brushes.Red);
                SetCognexStatusLight(ChessColor.White, _cameraVision ? Brushes.Yellow : Brushes.Red);
                SetCognexStatusLight(ChessColor.Black, _cameraVision ? Brushes.Yellow : Brushes.Red);

                try
                {
                    // Epson robots
                    if (_robotMotion)
                    {
                        var whiteRobot = _whiteRobot?.ConnectAsync() ?? Task.CompletedTask;
                        var blackRobot = _blackRobot?.ConnectAsync() ?? Task.CompletedTask;
                        await Task.WhenAll(whiteRobot, blackRobot);

                        _robotMotion = GlobalState.WhiteEpsonConnected && GlobalState.BlackEpsonConnected;
                        EpsonMotion.IsChecked = _robotMotion;

                        SetEpsonStatusLight(ChessColor.White, GlobalState.WhiteEpsonConnected ? Brushes.Green : Brushes.Red);
                        SetEpsonStatusLight(ChessColor.Black, GlobalState.BlackEpsonConnected ? Brushes.Green : Brushes.Red);
                    }

                    // Cognex cameras
                    if (_cameraVision)
                    {
                        var whiteCognex = _whiteCognex?.ConnectAsync() ?? Task.CompletedTask;
                        var blackCognex = _blackCognex?.ConnectAsync() ?? Task.CompletedTask;
                        await Task.WhenAll(whiteCognex, blackCognex);

                        _cameraVision = GlobalState.WhiteCognexConnected && GlobalState.BlackCognexConnected;
                        CognexVision.IsChecked = _cameraVision;

                        SetCognexStatusLight(ChessColor.White, GlobalState.WhiteCognexConnected ? Brushes.Green : Brushes.Red);
                        SetCognexStatusLight(ChessColor.Black, GlobalState.BlackCognexConnected ? Brushes.Green : Brushes.Red);
                    }

                    // Persist the final state of the toggles
                    _preferences.EpsonMotion = _robotMotion;
                    _preferences.CognexVision = _cameraVision;
                    PreferencesManager.Save(_preferences);
                }
                finally
                {
                    UpdateRectangleClip(50, Visibility.Collapsed, 60);
                    EnableEpsonElements();
                }          
            }
            else
            {
                SetEpsonStatusLight(ChessColor.White, Brushes.Red);
                SetEpsonStatusLight(ChessColor.Black, Brushes.Red);
                SetCognexStatusLight(ChessColor.White, Brushes.Red);
                SetCognexStatusLight(ChessColor.Black, Brushes.Red);
            }
        }

        #endregion

        #region Theme and Preference Methods

        /// <summary>
        /// Resolves and assings the image paths for all chess pieces (white and black)
        /// from the specified theme directory.
        /// </summary>
        /// <param name="piecePath">The absolute path to themed piece image directory.</param>
        /// <remarks>
        /// Uses reflection to set the corresponding private backing fields in <see cref="MainWindow"/>.
        /// Logs a fatal error via <see cref="ChessLog"/> if any expected asset is missing.
        /// <para>✅ Updated on 6/11/2025</para>
        /// </remarks>
        private void SetPieceImagePaths(string piecePath)
        {
            string[] pieces = ["Pawn", "Knight", "Bishop", "Rook", "Queen", "King"];

            foreach (string piece in pieces)
            {
                string whitePath = System.IO.Path.Combine(piecePath, $"White{piece}.png");
                string blackPath = System.IO.Path.Combine(piecePath, $"Black{piece}.png");

                if (!File.Exists(whitePath))
                {
                    var msg = $"File path executable not found at:\n{whitePath}\n\n" +
                               "The game cannot run without this file path.";
                    ChessLog.LogFatal(msg);
                    MessageBox.Show(msg, "Fatal Error - White Path Missing", MessageBoxButton.OK, MessageBoxImage.Error);

                    Application.Current.Shutdown(-1);
                    return;  // keep compiler happy; Shutdown tears down the app
                }

                if (!File.Exists(blackPath))
                {
                    var msg = $"File path executable not found at:\n{blackPath}\n\n" +
                               "The game cannot run without this file path.";
                    ChessLog.LogFatal(msg);
                    MessageBox.Show(msg, "Fatal Error - Black Path Missing", MessageBoxButton.OK, MessageBoxImage.Error);

                    Application.Current.Shutdown(-1);
                    return;  // keep compiler happy; Shutdown tears down the app
                }

                typeof(MainWindow).GetField($"white{piece}ImagePath",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(this, whitePath);

                typeof(MainWindow).GetField($"black{piece}ImagePath",
                        System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                    ?.SetValue(this, blackPath);
            }
        }

        /// <summary>
        /// Applies visual theme preferences to the background, piece set, and board style
        /// by updating the corresponding <see cref="ComboBox"/> selections in the UI.
        /// </summary>
        /// <remarks>
        /// Reads the values from the loaded <see cref="_preferences"/> object and normalizes
        /// them through <see cref="FormatThemeName"/> before applying with
        /// <see cref="SetComboBoxSelection"/>. This ensures the UI accurately reflects the 
        /// user's saved theme choices when the application starts or preferences are reloaded.
        /// <para>✅ Updated on 6/11/2025</para>
        /// </remarks>
        private void ApplyThemeFormatting()
        {
            SetComboBoxSelection(BackgroundSelection, FormatThemeName(_preferences.Background));
            SetComboBoxSelection(PieceSelection, FormatThemeName(_preferences.Pieces));
            SetComboBoxSelection(BoardSelection, FormatThemeName(_preferences.Board));
        }

        /// <summary>
        /// Normalizes a theme name into a human-readable format by inserting spaces
        /// between lowercase and uppercase letter transitions (camel case splitting).
        /// </summary>
        /// <param name="input">The raw theme name (may be <see langword="null"/> or empty).</param>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
        /// <returns>
        /// A formatted string with spaces inserted between camel case segments.
        /// Returns <see cref="string.Empty"/> if <paramref name="input"/> if <see langword="null"/>
        /// or whitespace.
        /// </returns>
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
        /// Applies the user's configured background image to a target <see cref="Grid"/> control.
        /// </summary>
        /// <param name="sender">The <see cref="Grid"/> that should receive the background image.</param>
        /// <param name="e">Optional routed event arguments (not used).</param>
        /// <remarks>
        /// Logs a warning and exits gracefully if the background path is unset or the file is missing.
        /// Ensures the UI remains responsive even if the background asset cannot be found.
        /// <para>✅ Updated on 6/11/2025</para>
        /// </remarks>
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
        /// Resolves and applies the correct image asset to a chess piece <see cref="Image"/> control.
        /// </summary>
        /// <param name="sender">The <see cref="Image"/> control representing a chess piece.</param>
        /// <param name="e">Optional routed event arguments (unused).</param>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><description>Uses the current piece theme from <see cref="_preferences"/> to build the image path.</description></item>
        ///     <item><description>Logs a warning if the theme is not set or the image file is missing, leaving the piece blank.</description></item>
        ///     <item><description>Intended to be used as the <c>Loaded</c> event handler for chess piece images in XAML.</description></item>
        /// </list>
        /// ✅ Updated on 6/11/2025
        /// </remarks>
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
        /// Applies the currently selected board theme to the chessboard grid.
        /// </summary>
        /// <param name="sender">The <see cref="Grid"/> representing the board UI.</param>
        /// <param name="e">Optional routed event arguments (unused).</param>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><description>Loads and applies the board background image from <see cref="_boardImagePath"/>.</description></item>
        ///     <item><description>Maps the active board theme (from <see cref="_preferences.Board"/>) to predefined light/dark colors.</description></item>
        ///     <item><description>Updates rank/file label colors using <see cref="ApplyTextBlockColors"/> if conversion succeeds.</description></item>
        ///     <item><description>Logs an error if the board path is invalid, and logs warnings if the theme is missing or color conversion fails.</description></item>
        /// </list>
        /// Intended to be used as a <c>Loaded</c> event handler for the chessboard grid in XAML.
        /// <para>✅ Updated on 6/10/2025</para>
        /// </remarks>
        private void LoadBoard(object sender, RoutedEventArgs? e)
        {
            if (sender is not Grid board || string.IsNullOrWhiteSpace(_boardImagePath))
            {
                ChessLog.LogWarning("Board image path is missing or invalid. Blank board will be applied.");
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
        /// Applies the given brush to the board's coordinate labels depending on square color.
        /// </summary>
        /// <param name="lightBrush">Brush for labels on light squares.</param>
        /// <param name="darkBrush">Brush for labels on dark squares.</param>
        /// <remarks>
        /// Groups coordinate labels by light and dark square alignment, then applies the matching brush.
        /// Exits early if either brush is null.
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
        /// Responds to a theme selection change by updating the relevant preferences,
        /// saving it to persistent storage, and reapplying the updated theme settings.
        /// </summary>
        /// <param name="sender">The <see cref="ComboBox"> that triggered the event.</param>
        /// <param name="e">The event arguments.</param>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void ThemeChange(object sender, EventArgs e)
        {
            if (sender is not ComboBox comboBox)
                return;

            // Extract selected theme and normalize spacing
            string selectedTheme = (comboBox.SelectedItem as ComboBoxItem)?.Content?.ToString()?.Replace(" ", "") ?? "";

            if (string.IsNullOrEmpty(selectedTheme))
                return;

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
        /// Applies visual updates for the theme category that changed,
        /// updating either the background, piece set, or board appearance.
        /// </summary>
        /// <param name="comboBox">The <see cref="ComboBox"/> containing the newly selected theme.</param>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><c>BackgroundSelection</c> → Reloads the main background image.</item>
        ///     <item><c>PieceSelection</c> → Reloads all visible chess piece images.</item>
        ///     <item><c>BoardSelection</c> → Reapplies the board surface and coordinate label colors.</item>
        /// </list>
        /// <para>Event args are not required since this method directly invokes the appropriate load functions.</para>
        /// ✅ Updated on 7/18/2025
        /// </remarks>
        private void ApplyThemeChanges(ComboBox comboBox)
        {
            if (comboBox is null)
                return;

            if (comboBox == BackgroundSelection)
            {
                LoadBackground(Screen, null);
            }
            else if (comboBox == PieceSelection)
            {
                foreach (Image piece in Chess_Board.Children.OfType<Image>())
                    LoadImage(piece, null);
            }
            else if (comboBox == BoardSelection)
            {
                LoadBoard(Chess_Board, null);
            }
            // else: ignore unrelated ComboBoxes
        }

        #endregion

        #region Main Game Logic

        /// <summary>
        /// Handles the Play button click by starting a new game.
        /// The heavy lifting is delegated to <see cref="StartGameAsync"/> to keep this
        /// event handler minimal and responsive.
        /// </summary>
        /// <param name="sender">The source of the event, typically the Play button.</param>
        /// <param name="e">The event arguments associated with the click.</param>
        /// <remarks>
        /// Exceptions thrown during game startup are caught and logged via <see cref="ChessLog"/>
        /// to prevent UI crashes. The actual game initialization logic resides in
        /// <see cref="StartGameAsync"/>.
        /// <para>✅ Updated on 8/31/2025</para>
        /// </remarks>
        public async void Play_ClickAsync(object sender, EventArgs e)
        {
            // Event handler stays tiny: kick off and log problems, don't do the work here
            try
            {
                await StartGameAsync();
            }
            catch (Exception ex)
            {
                ChessLog.LogError("Failed to play game.", ex);
            }
        }

        /// <summary>
        /// Starts a new chess game: applies initial UI state, initializes FEN/PGN logs,
        /// performs optional robot setup, derives the selected <see cref="_gameMode"/>,
        /// and launches the main game loop in the background.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///     
        ///     <item>Stops the inactivity timer and plays the “GameStart” sound.</item>
        ///     <item>Hides the setup panels, disables setup controls, and enables the pause UI when ready.</item>
        ///     <item>Clears annotations, enables pieces, and initializes FEN/PGN (FEN file is truncated).</item>
        ///     <item>If robot motion is enabled and the board isn’t set, displays a setup popup and awaits <see cref="SetupBoard"/>.</item>
        ///     <item>Determines user/computer turn rules based on the selected mode and color, including an initial board flip if needed.</item>
        ///     <item>Creates a fresh cancellation token for the game loop and starts <see cref="RunGameLoopAsync"/> fire-and-forget.</item>
        /// </list>
        /// Any unexpected errors are logged and rethrown after cleanup.
        /// <para>✅ Written on 8/31/2025</para>
        /// </remarks>
        /// <exception cref="Exception">Unexpected failures during startup or loop launch.</exception>
        private async Task StartGameAsync()
        {
            // Re-entrancy guard
            if (_isStarting) return;
            _isStarting = true;

            try
            {
                _inactivityTimer.Stop();
                PlaySound("GameStart");

                // Read selections safely
                _selectedPlayType = (ComboBoxItem)Play_Type.SelectedItem;
                _selectedColor = (ComboBoxItem)Color.SelectedItem;
                string? playType = _selectedPlayType?.Content?.ToString();
                string? playerColor = _selectedColor?.Content?.ToString();

                if (string.IsNullOrWhiteSpace(playType))
                {
                    ChessLog.LogWarning("No game mode selected.");
                    return;
                }

                // UI: initial state
                Game_Start.Visibility = Visibility.Collapsed;
                Game_Start.IsEnabled = false;
                CvC.Visibility = Visibility.Collapsed;
                CvC.IsEnabled = false;
                UvCorUvU.Visibility = Visibility.Collapsed;
                UvCorUvU.IsEnabled = false;
                PlayButton.Visibility = Visibility.Collapsed;
                PlayButton.IsEnabled = false;
                PauseButton.IsEnabled = false;  // enable after setup
                ResumeButton.Visibility = Visibility.Visible;
                EpsonMotion.IsEnabled = false;

                EnableAllPieces();
                EraseAnnotations();

                // Initialize FEN/PGN (async where possible)
                CreateFenCode();
                await File.WriteAllTextAsync(_fenFilePath, string.Empty, CancellationToken.None);
                WritePGNFile();

                // Decide move
                _gameMode =
                    playType == "Com Vs. Com" ? GameMode.ComVsCom :
                    playType == "User Vs. Com" ? GameMode.UserVsCom :
                                                 GameMode.UserVsUser;

                // Robot-controlled setup
                if (_robotMotion && !BoardSet)
                {
                    ShowSetupPopup(true);
                    try
                    {
                        await SetupBoard();
                    }
                    finally
                    {
                        ShowSetupPopup(false);
                    }
                }

                // Let the UI render the above changes before heavier work
                await Task.Yield();

                // Mode-specific state
                switch (_gameMode)
                {
                    case GameMode.ComVsCom:
                        {
                            UserTurn = false;
                            Chess_Board.IsHitTestVisible = false;
                            break;
                        }

                    case GameMode.UserVsCom:
                        {
                            // Flip once if needed
                            if (!string.IsNullOrEmpty(playerColor) &&
                                ((_flip == 0 && playerColor == "Black") ||
                                 (_flip == 1 && playerColor == "White")))
                            {
                                FlipBoard();
                                UpdateEvalBar();
                            }

                            if (playerColor == "Black")
                            {
                                UserTurn = false;
                                Chess_Board.IsHitTestVisible = false;
                            }
                            else
                            {
                                UserTurn = true;
                                Chess_Board.IsHitTestVisible = true;
                                EnableImagesWithTag("WhitePiece", true);
                                EnableImagesWithTag("BlackPiece", false);
                            }
                            break;
                        }

                    case GameMode.UserVsUser:
                    default:
                        {
                            UserTurn = true;
                            Chess_Board.IsHitTestVisible = true;
                            EnableImagesWithTag("WhitePiece", true);
                            EnableImagesWithTag("BlackPiece", false);
                            break;
                        }
                }

                PauseButton.IsEnabled = true;

                _loopCts?.Cancel();
                _loopCts = new CancellationTokenSource();

                // Create a fresh “loop stopped” signal
                _loopStoppedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                // Fire and forget; the loop itself will signal when it fully exits
                _ = RunGameLoopAsync(_loopCts.Token);
            }
            catch (OperationCanceledException)
            {
                // normal on cancel; optional log
            }
            catch (Exception ex)
            {
                ChessLog.LogError("Operation crashed.", ex);
                throw;
            }
            finally
            {
                _isStarting = false;
            }
        }

        /// <summary>
        /// Pauses the game: cancels the active loop, blocks input, shows the start panel,
        /// and enables the appropriate setup controls for the current mode. If a move
        /// is mid-flight, waits briefly for the loop to finish the critical moving section.
        /// </summary>
        /// <param name="sender">The source of the event, typically the Pause button.</param>
        /// <param name="e">The event arguments associated with the click.</param>
        /// <remarks>✅ Updated on 8/31/2025</remarks>
        private async void Pause_ClickAsync(object sender, EventArgs e)
        {
            // Re-entrancy guard
            if (IsPaused) return;
            IsPaused = true;

            // Cancel loop & any pending user move waiters
            _loopCts?.Cancel();
            _userMoveTcs?.TrySetCanceled();

            // UI: block interactions immediately
            Chess_Board.IsHitTestVisible = false;
            PauseButton.IsEnabled = false;
            EnableImagesWithTag("WhitePiece", false);
            EnableImagesWithTag("BlackPiece", false);
            EraseAnnotations();
            DeselectPieces();

            // Show main panel
            Game_Start.Visibility = Visibility.Visible;
            Game_Start.IsEnabled = true;

            // If a move is in its non-interruptible section, wait for the loop to signal it has stopped
            if (_loopStoppedTcs is not null && MoveInProgress)
            {
                ShowMoveInProgressPopup(true);
                await _loopStoppedTcs.Task;
                ShowMoveInProgressPopup(false);
            }

            // If pause landed exactly on a game-ending move, let the new-game funnel take over
            if (EndGame)
                return;

            // Reveal the correct setup group and enable its controls
            if (_gameMode == GameMode.ComVsCom)
            {
                CvC.Visibility = Visibility.Visible;
                CvC.IsEnabled = true;

                WhiteCpuElo.IsEnabled = true;
                BlackCpuElo.IsEnabled = true;
            }
            if (_gameMode == GameMode.UserVsCom || _gameMode == GameMode.UserVsUser)
            {
                UvCorUvU.Visibility = Visibility.Visible;
                UvCorUvU.IsEnabled = true;

                Elo.IsEnabled = _gameMode == GameMode.UserVsCom;
                Color.IsEnabled = _gameMode == GameMode.UserVsCom;
            }

            // Let user resume / change connectivity
            Play_Type.IsEnabled = true;
            ResumeButton.IsEnabled = true;
            QuitButton.Visibility = Visibility.Visible;
            QuitButton.IsEnabled = true;
            EpsonMotion.IsEnabled = true;

            // Kick the inactivity countdown while paused
            _inactivityTimer.Start();
        }

        /// <summary>
        /// Handles the Resume button click by resuming a game in progress.
        /// The heavy lifting is delegated to <see cref="ResumeGame"/> to keep this
        /// event handler minimal and responsive.
        /// </summary>
        /// <param name="sender">The source of the event, typically the Resume button.</param>
        /// <param name="e">The event arguments associated with the click.</param>
        /// <remarks>
        /// Exceptions thrown during game resumption are caught and logged via <see cref="ChessLog"/>
        /// to prevent UI crashes. The actual game resumption logic resides in
        /// <see cref="ResumeGame"/>.
        /// <para>✅ Updated on 8/31/2025</para>
        /// </remarks>
        private async void Resume_ClickAsync(object sender, EventArgs e)
        {
            // Event handler stays tiny: kick off and log problems, don't do the work here
            try
            {
                await ResumeGame();
            }
            catch (Exception ex)
            {
                ChessLog.LogError("Failed to resume game.", ex);
            }
        }

        /// <summary>
        /// Resumes a paused chess game by restoring UI state, reinitializing
        /// gameplay settings, and restarting the main game loop in the background.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///     <item>Stops the inactivity timer and hides the start/setup panels.</item>
        ///     <item>Determines the active <see cref="_gameMode"/> and sets baord interactivity
        ///           and piece enablement based on user/computer turns.</item>
        ///     <item>Resets pause/resume/quit button states and clears transient UI elements.</item>
        ///     <item>Starts a fresh <see cref="CancellationTokenSource"/> and signals a new
        ///           loop task (<see cref="RunGameLoopAsync"/>) to process moves.</item>
        ///     <item>Designed as a "fire-and-forget" entry point: the game loop runs independently,
        ///           and this method returns immediately once setup is complete.</item>
        /// </list>
        /// ✅ Written on 8/31/2025
        /// </remarks>
        /// <returns>A completed <see cref="Task"/> once resume initialization finishes.</returns>
        /// <exception cref="Exception">Any unexpected setup or loop-start errors are logged and rethrown.</exception>
        private Task ResumeGame()
        {
            // Re-entrancy guard
            if (_isStarting) return Task.CompletedTask;
            _isStarting = true;

            try
            {
                _inactivityTimer.Stop();

                // Read selections safely
                _selectedPlayType = (ComboBoxItem)Play_Type.SelectedItem;
                _selectedColor = (ComboBoxItem)Color.SelectedItem;
                string? playType = _selectedPlayType?.Content?.ToString();
                string? playerColor = _selectedColor?.Content?.ToString();

                if (string.IsNullOrWhiteSpace(playType))
                {
                    ChessLog.LogWarning("No game mode selected.");
                    return Task.CompletedTask;
                }

                // UI: initial state
                Game_Start.Visibility = Visibility.Collapsed;
                Game_Start.IsEnabled = false;
                CvC.Visibility = Visibility.Collapsed;
                CvC.IsEnabled = false;
                UvCorUvU.Visibility = Visibility.Collapsed;
                UvCorUvU.IsEnabled = false;

                PauseButton.IsEnabled = false;  // enable after setup
                ResumeButton.IsEnabled = false;
                QuitButton.Visibility = Visibility.Collapsed;
                QuitButton.IsEnabled = false;
                EpsonMotion.IsEnabled = false;

                // Decide move
                _gameMode =
                    playType == "Com Vs. Com" ? GameMode.ComVsCom :
                    playType == "User Vs. Com" ? GameMode.UserVsCom :
                                                 GameMode.UserVsUser;

                switch (_gameMode)
                {
                    case GameMode.ComVsCom:
                        {
                            UserTurn = false;
                            Chess_Board.IsHitTestVisible = false;
                            break;
                        }

                    case GameMode.UserVsCom:
                        {
                            // Flip once if needed
                            if (!string.IsNullOrEmpty(playerColor) &&
                                ((_flip == 0 && playerColor == "Black") ||
                                 (_flip == 1 && playerColor == "White")))
                            {
                                FlipBoard();
                                UpdateEvalBar();
                            }

                            if ((playerColor == "White" && Move == 1) || (playerColor == "Black" && Move == 0))
                            {
                                UserTurn = true;
                                Chess_Board.IsHitTestVisible = true;
                                EnableImagesWithTag("WhitePiece", Move == 1);
                                EnableImagesWithTag("BlackPiece", Move == 0);
                            }
                            else
                            {
                                UserTurn = false;
                                Chess_Board.IsHitTestVisible = false;
                            }
                            break;
                        }

                    case GameMode.UserVsUser:
                    default:
                        {
                            UserTurn = true;
                            Chess_Board.IsHitTestVisible = true;
                            EnableImagesWithTag("WhitePiece", Move == 1);
                            EnableImagesWithTag("BlackPiece", Move == 0);
                            break;
                        }
                }

                PauseButton.IsEnabled = true;
                IsPaused = false;

                _loopCts?.Cancel();
                _loopCts = new CancellationTokenSource();

                // Create a fresh “loop stopped” signal
                _loopStoppedTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

                // Fire and forget; the loop itself will signal when it fully exits
                _ = RunGameLoopAsync(_loopCts.Token);
                return Task.CompletedTask;
            }
            catch (OperationCanceledException)
            {
                return Task.CompletedTask;
            }
            catch (Exception ex)
            {
                ChessLog.LogError("Operation crashed.", ex);
                throw;
            }
            finally
            {
                _isStarting = false;
            }
        }

        /// <summary>
        /// Main game loop. Repeats turn execution until the game ends or a pause is requested,
        /// handling all three modes (<see cref="GameMode.ComVsCom"/>, <see cref="GameMode.UserVsCom"/>,
        /// <see cref="GameMode.UserVsUser"/>), animations, bookkeeping, logging, and (optional) robot I/O.
        /// </summary>
        /// <param name="ct">
        /// A <see cref="CancellationToken"/> used to cancel pending awaits (e.g., engine computation or
        /// user-move waits). When cancellation is requested, the loop exits cleanly after any non-interruptible
        /// section completes.
        /// </param>
        /// <remarks>
        /// For computer turns, calls <see cref="ComputerMoveAsync"/> to select a move, then animates via
        /// <see cref="MovePieceAsync"/> (and <see cref="MoveCastleRookAsync"/> when castling). For user
        /// turns, the loop waits on <see cref="_userMoveTcs.Task"/>, which is completed by the UI input path;
        /// if move confirmation if disabled, the move animation runs here as well.
        /// <para>
        /// After each move, the loop updates callouts, finalizes board state (<see cref="FinalizeMoveAsync"/>),
        /// writes logs (<see cref="DocumentMoveAsync"/>), checks for mate (<see cref="CheckmateVerifierAsync"/>),
        /// refreshes evaluation/UI, and, when enabled, sends robot bit patterns via <see cref="SendRobotBitsAsync"/>
        /// while appending to bit history.
        /// </para>
        /// <para>
        /// Exit paths:
        /// <list type="bullet">
        ///     <item><description><c><see cref="EndGame"/> == <see langword="true"/></c> → optional board cleanup, brief delay, then <see cref="NewGameFunnel"/>.</description></item>
        ///     <item><description><c><see cref="IsPaused"/> == <see langword="true"/></c> → signals <see cref="_loopStoppedTcs"/> so the Pause handler can continue.</description></item>
        /// </list>
        /// </para>
        /// <para>✅ Written on 8/31/2025</para>
        /// </remarks>
        /// <returns></returns>
        private async Task RunGameLoopAsync(CancellationToken ct)
        {
            try
            {
                while (!EndGame && !IsPaused)
                {
                    switch (_gameMode)
                    {
                        case GameMode.ComVsCom:
                            {
                                Chess_Board.IsHitTestVisible = false;

                                var piece = await ComputerMoveAsync(ct);
                                if (ct.IsCancellationRequested && piece == null)
                                {
                                    ChessLog.LogError("Pause requested. Aborting move safely.");
                                    return;
                                }
                                else if (piece == null)
                                {
                                    ChessLog.LogError("No active piece returned.");
                                    return;
                                }
                                _selectedPiece = piece;

                                // Animate computer move
                                Grid.SetRow(_selectedPiece, _oldRow);
                                Grid.SetColumn(_selectedPiece, _oldCol);
                                await MovePieceAsync(_selectedPiece, _newRow, _newCol, _oldRow, _oldCol);
                                break;
                            }

                        case GameMode.UserVsCom:
                            {
                                if (!UserTurn)
                                {
                                    Chess_Board.IsHitTestVisible = false;

                                    var piece = await ComputerMoveAsync(ct);
                                    if (ct.IsCancellationRequested && piece == null)
                                    {
                                        ChessLog.LogError("Pause requested. Aborting move safely.");
                                        return;
                                    }
                                    else if (piece == null)
                                    {
                                        ChessLog.LogError("No active piece returned.");
                                        return;
                                    }
                                    _selectedPiece = piece;

                                    // Animate computer move
                                    Grid.SetRow(_selectedPiece, _oldRow);
                                    Grid.SetColumn(_selectedPiece, _oldCol);
                                    await MovePieceAsync(_selectedPiece, _newRow, _newCol, _oldRow, _oldCol);
                                }
                                else
                                {
                                    Chess_Board.IsHitTestVisible = true;
                                    EnableImagesWithTag("WhitePiece", Move == 1);
                                    EnableImagesWithTag("BlackPiece", Move == 0);

                                    _userMoveTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                                    await _userMoveTcs.Task;

                                    if (!_moveConfirm)
                                    {
                                        // Animate computer move
                                        Grid.SetRow(_selectedPiece, _oldRow);
                                        Grid.SetColumn(_selectedPiece, _oldCol);
                                        await MovePieceAsync(_selectedPiece, _newRow, _newCol, _oldRow, _oldCol);
                                    }
                                }

                                UserTurn = !UserTurn;
                                break;
                            }

                        case GameMode.UserVsUser:
                            {
                                Chess_Board.IsHitTestVisible = true;
                                EnableImagesWithTag("WhitePiece", Move == 1);
                                EnableImagesWithTag("BlackPiece", Move == 0);

                                _userMoveTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                                await _userMoveTcs.Task;

                                if (!_moveConfirm)
                                {
                                    // Animate computer move
                                    Grid.SetRow(_selectedPiece, _oldRow);
                                    Grid.SetColumn(_selectedPiece, _oldCol);
                                    await MovePieceAsync(_selectedPiece, _newRow, _newCol, _oldRow, _oldCol);
                                }
                                break;
                            }
                    }

                    await FinalizeMoveAsync();
                    await DocumentMoveAsync();
                    await CheckmateVerifierAsync();
                    UpdateEvalBar();
                    DeselectPieces();

                    if (_robotMotion)
                    {
                        await SendRobotBitsAsync();

                        PrevWhiteBits = AppendToHistory(PrevWhiteBits, WhiteBits);
                        PrevBlackBits = AppendToHistory(PrevBlackBits, BlackBits);
                        WhiteBits = string.Empty;
                        BlackBits = string.Empty;
                    }

                    MoveInProgress = false;
                }

                if (EndGame)
                {
                    if (_robotMotion)
                    {
                        ShowCleanupPopup(true);
                        await ClearBoard();
                        ShowCleanupPopup(false);
                    }

                    await Task.Delay(10000, CancellationToken.None);
                    NewGameFunnel();
                    return;
                }
                else if (IsPaused)
                {
                    _loopStoppedTcs?.TrySetResult(true);
                }
            }
            catch (OperationCanceledException)
            {
                ChessLog.LogInformation("Pause requested while waiting for user move. Aborting loop safely.");
                _userMoveTcs = null;
                return;
            }
            catch (Exception ex)
            {
                ChessLog.LogError("Run Game Loop failed.", ex);
            }
        }

        /// <summary>
        /// Chooses and executes the engine's move using Stockfish, scaled by the configured ELO.
        /// Temporarily disables user interaction, queries Stockfish, filters candidate moves by a
        /// mistake window, optionally forces mating lines, then applies the selected move (including
        /// promotions) to the board and routes it through the normal move managers.
        /// </summary>
        /// <param name="ct">
        /// An optional <see cref="CancellationToken"/> used to cancel the move computation early
        /// (e.g., when the game is paused).
        /// </param>
        /// <remarks>
        /// Pipeline:
        /// <list type="number">
        ///     <item><description>Lock UI and add a short human-like delay.</description></item>
        ///     <item><description>Derive engine settings from ELO (depth, mistake threshold, Gaussian skew, critical-moment odds).</description></item>
        ///     <item><description>Call Stockfish and parse principal/candidate moves with centipawn (CP) or mate scores.</description></item>
        ///     <item><description>Build a candidate list restricted by a CP-loss window (tighter in the opening).</description></item>
        ///     <item><description>Critical-moment logic may collapse the choice set to top moves.</description></item>
        ///     <item><description>If a mating line exists for the side to move, optionally force the top engine move.</description></item>
        ///     <item><description>Otherwise select a move via a Gaussian-biased pick towards stronger moves.</description></item>
        ///     <item><description>Apply the move (including promotions) and hand off to <see cref="PawnMoveManagerAsync"/> or <see cref="MoveManagerAsync"/>.</description></item>
        /// </list>
        /// Notes:
        /// <list type="bullet">
        ///     <item><description>Handles both "a2a4" and "a7a8q" (promotion) formats.</description></item>
        ///     <item><description>Does not block the UI thread; Stockfish work is awaited.</description></item>
        ///     <item><description>Respects cached "clicked piece" fields to keep downstream logic unchanged.</description></item>
        /// </list>
        /// ✅ Updated on 8/31/2025
        /// </remarks>
        /// <returns>
        /// The <see cref="Image"/> representing the moved piece, or <see langword="null"/> if no move was made
        /// (e.g., pause, cancel, or engine failure).
        /// </returns>
        private async Task<Image?> ComputerMoveAsync(CancellationToken ct = default)
        {
            // Lock out user input while the engine is thinking
            EnableImagesWithTag("WhitePiece", false);
            EnableImagesWithTag("BlackPiece", false);

            // Early exit if paused/canceled
            if (IsPaused || ct.IsCancellationRequested) return null;

            try
            {
                // Small human-ish delay
                await Task.Delay(Random.Shared.Next(1000, 4501), ct);
                if (IsPaused || ct.IsCancellationRequested) return null;

                // Resolve ELO / difficulty (defensive null checks)
                if (_gameMode == GameMode.UserVsCom && _selectedElo is null) return null;
                if (_gameMode == GameMode.ComVsCom && (_selectedWhiteElo is null || _selectedBlackElo is null)) return null;

                int cpuElo =
                    _gameMode == GameMode.UserVsCom
                        ? int.Parse(_selectedElo.Content.ToString()!)
                        : (Move == 1
                            ? int.Parse(_selectedWhiteElo.Content.ToString()!)
                            : int.Parse(_selectedBlackElo.Content.ToString()!));

                var settings = EloSettings.GetSettings(cpuElo);
                int searchDepth = settings.Depth;
                int cpLossThreshold = settings.CpLossThreshold;
                double bellCurvePercentile = settings.BellCurvePercentile;
                int criticalMoveConversion = settings.CriticalMoveConversion;

                // Query Stockfish
                var (primary, reserve, lines) = await ParseStockfishOutputAsync(Fen, searchDepth, _stockfishPath!, ct);
                if (IsPaused || ct.IsCancellationRequested) return null;

                // Fallback if nothing parsed in primary
                if (primary.Count == 0)
                {
                    primary.AddRange(reserve);
                    reserve.Clear();
                }
                if (primary.Count == 0) return null;  // Safety

                // Sort "best to worst" by CP, with mate for/against at extremes
                var sorted = primary.OrderByDescending(m =>
                {
                    if (m.cp.StartsWith("mate"))
                        return m.cpValue.StartsWith('-') ? int.MinValue : int.MaxValue;
                    return int.Parse(m.cpValue);
                }).ToList();

                var (cp, cpValue, possibleMove) = sorted[0];

                // Build candidate list with mistake window
                var moves = new List<(string cp, string cpValue, string possibleMove)>();
                int maxCpValue = sorted
                    .Where(m => !m.cp.StartsWith("mate"))
                    .Select(m => int.Parse(m.cpValue))
                    .DefaultIfEmpty(0)
                    .First();

                foreach (var m in sorted)
                {
                    if (m.cp.StartsWith("mate"))
                    {
                        // Include mates-for; include mates-against only if list is tiny
                        if (!m.cpValue.StartsWith('-') || sorted.Count < 3)
                            moves.Add(m);
                        continue;
                    }

                    if (!int.TryParse(m.cpValue, out int cpVal)) continue;

                    int diff = Math.Abs(cpVal - maxCpValue);
                    int threshold = Fullmove < 6 ? cpLossThreshold / 8 : cpLossThreshold;

                    if (diff <= threshold)
                        moves.Add(m);
                }

                if (IsPaused || ct.IsCancellationRequested) return null;

                MoveInProgress = true;

                //  Mating override (force top engine move sometimes)
                if (moves.Count > 0 && moves[0].cp == "mate" && !TopEngineMove)
                {
                    int mateIn = Math.Abs(int.Parse(moves[0].cpValue));
                    if (ShouldPlayMatingMove(cpuElo, mateIn))
                        TopEngineMove = true;
                }

                // Determine selected SAN-like string from either forced top move or the Gaussian pick
                string sel = SelectMoveString(moves, sorted, TopEngineMove, bellCurvePercentile, criticalMoveConversion);

                // Parse “a2a4” or “a7a8q”
                if (sel.Length == 4)
                {
                    _startPosition = sel[..2];
                    _endPosition = sel[2..];
                }
                else if (sel.Length == 5)
                {
                    PromotionPiece = sel[^1];
                    _startPosition = sel[..2];
                    _endPosition = sel[2..4];
                }
                else
                {
                    System.Diagnostics.Debug.WriteLine("Move string not recognized.");
                    return null;
                }

                // Convert to grid coords
                _oldRow = 8 - int.Parse(_startPosition[1].ToString());
                _oldCol = _startPosition[0] - 'a';
                _newRow = 8 - int.Parse(_endPosition[1].ToString());
                _newCol = _endPosition[0] - 'a';

                // Find the piece and “virtually” place it on destination
                Image? selectedPiece = null;
                foreach (var img in Chess_Board.Children.OfType<Image>())
                {
                    if (Grid.GetRow(img) == _oldRow && Grid.GetColumn(img) == _oldCol)
                    {
                        selectedPiece = img;
                        Grid.SetRow(selectedPiece, _newRow);
                        Grid.SetColumn(selectedPiece, _newCol);
                        break;
                    }
                }
                if (selectedPiece is null) return null;

                // Route to pawn/non-pawn manager
                if (selectedPiece.Name.Contains("Pawn"))
                {
                    _clickedPawn = selectedPiece;
                    await PawnMoveManagerAsync(selectedPiece);
                }
                else
                {
                    _clickedKnight = selectedPiece.Name.Contains("Knight") ? selectedPiece : null;
                    _clickedBishop = selectedPiece.Name.Contains("Bishop") ? selectedPiece : null;
                    _clickedRook = selectedPiece.Name.Contains("Rook") ? selectedPiece : null;
                    _clickedQueen = selectedPiece.Name.Contains("Queen") ? selectedPiece : null;
                    _clickedKing = selectedPiece.Name.Contains("King") ? selectedPiece : null;

                    await MoveManagerAsync(selectedPiece);
                }

                return selectedPiece;
            }
            catch (OperationCanceledException)
            {
                // Treat as a normal pause/cancel
                return null;
            }
        }

        /// <summary>
        /// Returns a probabilistic decision on whether the engine should force a mating
        /// continuation (i.e., pick the top "mate in N" move) based on the current
        /// strength setting (<paramref name="elo"/>) and the detected mate length
        /// (<paramref name="mateIn"/>).
        /// </summary>
        /// <param name="elo">Engine strength used to pick a probability table (e.g., 1200, 2000, 3000).</param>
        /// <param name="mateIn">Positive mate length (M1, M2, …). Values &lt;= 0 are clamped to 1.</param>
        /// <returns>
        /// <see langword="true"/> if a random draw falls under the configured probability for the
        /// (<paramref name="elo"/>, <paramref name="mateIn"/>) pair; otherwise <see langword="false"/>.
        /// </returns>
        /// <remarks>
        /// Uses a tiered table by ELO threshold and piecewise probabilities by mate length.
        /// Randomness uses <see cref="Random.Shared"/> to avoid per-call allocations.
        /// <para>✅ Updated on 8/19/2025</para>
        /// </remarks>
        private static bool ShouldPlayMatingMove(int elo, int mateIn)
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

        /// <summary>
        /// Manages a pawn move end-to-end: applies captures (including en passant),
        /// updates castling rights, handles promotion, optionally animates and confirms
        /// the move with the user, and finalizes game state (FEN, checkmate check, selection).
        /// </summary>
        /// <param name="activePawn">The pawn being moved. Must be a valid piece <see cref="Image"/> on the board.</param>
        /// <remarks>
        /// Steps:
        /// <list type="number">
        ///     <item>Snapshot state (positions, castling rights), resolve captures.</item>
        ///     <item>Apply en passant capture and eligibility when applicable.</item>
        ///     <item>Handle promotion if the pawn reaches its last rank.</item>
        ///     <item>If confirm moves is enabled, animate → confirm → finalize or undo.</item>
        ///     <item>Otherwise finalize immediately (update FEN, verify checkmate, clear selection).</item>
        /// </list>
        /// <para>✅ Updated on 8/31/2025</para>
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the move is finalized successfully; <see langword="false"/> if the move was undone
        /// (e.g. user rejected the configuration).
        /// </returns>
        public async Task<bool> PawnMoveManagerAsync(Image activePawn)
        {
            // Snapshot board state & castling rights for potential undo.
            await PiecePositions();
            int[] castlingRightsSnapshot = [CWR1, CWK, CWR2, CBR1, CBK, CBR2];

            _pawnName = activePawn.Name;

            // Captures & castling rights
            await HandlePieceCapture(activePawn);  // sets _capturedPiece if any
            await HandleEnPassantCapture();  // resolves en passant capture if applicable
            await DisableCastlingRights(activePawn, _capturedPiece);

            // En passant eligibility & promotion
            EnPassantCreated = false;

            if (Move == 1)  // white just moved
            {
                if (_oldRow - _newRow == 2)
                {
                    EnPassantSquare.Clear();
                    EnPassantSquare.Add(Tuple.Create(_newRow + 1, _newCol));
                    EnPassantCreated = true;
                }
                else if (_newRow == 0)  // promotion
                {
                    await PawnPromote(activePawn, Move);
                }
            }
            else  // black just moved
            {
                if (_newRow - _oldRow == 2)
                {
                    EnPassantSquare.Clear();
                    EnPassantSquare.Add(Tuple.Create(_newRow - 1, _newCol));
                    EnPassantCreated = true;
                }
                else if (_newRow == 7)  // promotion
                {
                    await PawnPromote(activePawn, Move);
                }
            }

            // Optional user confirmation path
            if (UserTurn && _moveConfirm)
            {
                // Callout proposed move
                SelectedPiece(_oldRow, _oldCol);
                SelectedPiece(_newRow, _newCol);

                // Animate from old to new (board already updated by caller)
                Grid.SetRow(activePawn, _oldRow);
                Grid.SetColumn(activePawn, _oldCol);
                await MovePieceAsync(activePawn, _newRow, _newCol, _oldRow, _oldCol);

                bool confirmed = await WaitForConfirmationAsync();
                EraseAnnotations();

                if (!confirmed)
                {
                    UndoMove(activePawn, castlingRightsSnapshot);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Orchestrates a non-pawn move: updates position, applies captures/castling constraints,
        /// optionally shows and animates a proposed move for user confirmation, and then
        /// finalizes or reverts the move.
        /// </summary>
        /// <param name="activePiece">The piece being moved. Must be a valid piece <see cref="Image"/> on the board.</param>
        /// <remarks>
        /// <list type="number">
        ///     <item>Snapshot state (positions, castling rights), resolve captures.</item>
        ///     <item>Apply king-specific pre-processing (e.g., castling logic).</item>
        ///     <item>Resolve captures and disable castling rights if needed.</item>
        ///     <item>If confirm moves is enabled, animate → confirm → finalize or undo.</item>
        ///     <item>Otherwise finalize immediately (update FEN, verify checkmate, clear selection).</item>
        /// </list>
        /// <para>✅ Updated on 8/31/2025</para>
        /// </remarks>
        /// <returns>
        /// <see langword="true"/> if the move is finalized successfully; <see langword="false"/> if the move was undone
        /// (e.g. user rejected the configuration).
        /// </returns>
        public async Task<bool> MoveManagerAsync(Image activePiece)
        {
            // Snapshot board state & castling rights for potential undo.
            await PiecePositions();
            int[] castlingRightsSnapshot = [CWR1, CWK, CWR2, CBR1, CBK, CBR2];

            // King-specific pre-processing (e.g., castling)
            if (activePiece.Name.Contains("King"))
                KingMoveManagerAsync(activePiece);

            // Captures and castling rights
            await HandlePieceCapture(activePiece);
            await DisableCastlingRights(activePiece, _capturedPiece);

            // Optional user confirmation path
            if (UserTurn && _moveConfirm)
            {
                // Callout proposed move
                if (KingCastle || QueenCastle)
                {
                    SelectedPiece(_oldRow, _oldCol);
                    SelectedPiece(_newRow, KingCastle ? 7 : 0);
                }
                else
                {
                    SelectedPiece(_oldRow, _oldCol);
                    SelectedPiece(_newRow, _newCol);
                }

                // Animate from old to new (board already updated by caller)
                Grid.SetRow(activePiece, _oldRow);
                Grid.SetColumn(activePiece, _oldCol);
                await MovePieceAsync(activePiece, _newRow, _newCol, _oldRow, _oldCol);

                bool confirmed = await WaitForConfirmationAsync();
                EraseAnnotations();

                if (!confirmed)
                {
                    UndoMove(activePiece, castlingRightsSnapshot);
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Handles castling moves: if the king shifts exactly two files, relocates the rook
        /// to its castled square and sets the appropriate castling flag for downstream logic.
        /// </summary>
        /// <param name="activePiece">
        /// The king piece being moved. Must be valid <see cref="Image"/> on the board.
        /// </param>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><description>Assumes the move has already been validated as legal castling.</description></item>
        ///     <item><description>By convention, Rook1 = queenside rook (file a), Rook2 = kingside rook (file h).</description></item>
        ///     <item>Relocates the rook to the correct square adjacent to the king's new position.</item>
        /// </list>
        /// <para>✅ Updated on 9/1/2025</para>
        /// </remarks>
        public async void KingMoveManagerAsync(Image activePiece)
        {
            // Not a castling move unless the king shifts exactly two files
            if (Math.Abs(_oldCol - _newCol) != 2)
                return;

            // Mark which castle type occurred for downstream logic/visuals
            bool isKingside = _oldCol < _newCol;
            if (isKingside)
                KingCastle = true;
            else
                QueenCastle = true;

            // Determine which side (White/Black) from the active piece name
            bool isWhite = activePiece.Name.StartsWith("White") == true;

            // Pick rook name based on side and castle direction
            string rookName =
                isWhite
                    ? (isKingside ? "WhiteRook2" : "WhiteRook1")
                    : (isKingside ? "BlackRook2" : "BlackRook1");

            // Kingside → to the left of the king; queenside → to the right of the king
            int rookEndColumn = isKingside ? _newCol - 1 : _newCol + 1;

            var rook = Chess_Board.Children
                .OfType<Image>()
                .FirstOrDefault(img => img.Name == rookName);

            if (rook != null)
            {
                Grid.SetRow(rook, _newRow);
                Grid.SetColumn(rook, rookEndColumn);
                await MovePieceAsync(rook, _newRow, rookEndColumn, _oldRow, isKingside ? 7 : 0);
            }
        }

        /// <summary>
        /// Handles capture logic when the moving piece's destination square
        /// contains an opposing piece.
        /// </summary>
        /// <param name="activePiece">
        /// The piece being moved (remains untouched if a capture occurs).
        /// </param>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><description>If an opposing piece occupies (<see cref="_newRow"/>, <see cref="_newCol"/>), it is recorded as <see cref="TakenPiece"/> and removed from the board.</description></item>
        ///     <item><description><see cref="_capturedPiece"/> is updated and <see cref="Capture"/> is set to <see langword="true"/>.</description></item>
        ///     <item><description>The method itself does not alter the moving piece, only the target square.</description></item>
        ///     <item><description>Always returns a completed <see cref="Task"/> to fit into async workflows.</description></item>
        /// </list>
        /// <para>✅ Updated on 9/1/2025</para>
        /// </remarks>
        private Task HandlePieceCapture(Image activePiece)
        {
            // Does any image currently sit on the destination square
            bool occupied = ImageCoordinates.Any(coord => coord.Item1 == _newRow && coord.Item2 == _newCol);
            if (!occupied) return Task.CompletedTask;

            // Locate the captured piece
            var captured = Chess_Board.Children
                .OfType<Image>()
                .FirstOrDefault(img => img != activePiece && Grid.GetRow(img) == _newRow && Grid.GetColumn(img) == _newCol);

            if (captured is null) return Task.CompletedTask;

            // Store captured piece name and image
            TakenPiece = captured.Name;
            _capturedPiece = captured;

            // Remove visually & logically
            _capturedPiece.Visibility = Visibility.Collapsed;
            _capturedPiece.IsHitTestVisible = false;
            _capturedPiece.IsEnabled = false;
            Chess_Board.Children.Remove(_capturedPiece);

            Capture = true;
            return Task.CompletedTask;
        }

        /// <summary>
        /// Executes an en passant capture when a pawn moves onto the designated en passant square.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><description>Checks if (<see cref="_newRow"/>, <see cref="_newCol"/>) matches the current <see cref="EnPassantSquare"/>.</description></item>
        ///     <item><description>If valid, identifies the opposing pawn that advanced two squares last move and removes it from the board.</description></item>
        ///     <item><description>Records the captured pawn in <see cref="TakenPiece"/> and <see cref="_capturedPiece"/>, and sets <see cref="EnPassant"/> to <see langword="true"/>.</description></item>
        ///     <item><description>Clears the en passant state after the capture.</description></item>
        ///     <item><description>Always returns a completed <see cref="Task"/> to integrate into async workflows.</description></item>
        /// </list>
        /// <para>✅ Updated on 9/1/2025</para>
        /// </remarks>
        private Task HandleEnPassantCapture()
        {
            // Is the destination square currently flagged as en passant
            bool isEnPassant = EnPassantSquare.Any(coord => coord.Item1 == _newRow && coord.Item2 == _newCol);
            if (!isEnPassant) return Task.CompletedTask;

            // Determine the row of the pawn to capture (the pawn that advanced two squares last move)
            int capturedRow = (Move == 1) ? _newRow + 1 : _newRow - 1;

            // Locate the captured pawn
            var capturedPawn = Chess_Board.Children
                .OfType<Image>()
                .FirstOrDefault(img => Grid.GetRow(img) == capturedRow && Grid.GetColumn(img) == _newCol);

            if (capturedPawn != null)
            {
                // Store captured piece name and image
                TakenPiece = capturedPawn.Name;
                _capturedPiece = capturedPawn;

                // Remove visually & logically
                _capturedPiece.Visibility = Visibility.Collapsed;
                _capturedPiece.IsHitTestVisible = false;
                _capturedPiece.IsEnabled = false;
                Chess_Board.Children.Remove(_capturedPiece);

                EnPassant = true;
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Clears castling rights when the relevant king or rook moves, or when that rook is captured.
        /// </summary>
        /// <param name="activePiece">The piece that just moved (may be <see langword="null"/> for capture-only flows).</param>
        /// <param name="capturedPiece">The piece that was captured, if any.</param>
        /// <remarks>
        /// Sets the following flags permanently when triggered:
        /// <list type="bullet">
        ///     <item><description><see cref="CKW"/> if white king has moved.</description></item>
        ///     <item><description><see cref="CWR1"/> if white queenside rook has been moved/been captured.</description></item>
        ///     <item><description><see cref="CWR2"/> if white kingside rook has been moved/been captured.</description></item>
        ///     <item><description><see cref="CKW"/> if black king has moved.</description></item>
        ///     <item><description><see cref="CWR1"/> if black queenside rook has been moved/been captured.</description></item>
        ///     <item><description><see cref="CWR2"/> if black kingside rook has been moved/been captured.</description></item>
        /// </list>
        /// <para>
        /// Assumes starting rooks are named <c>WhiteRook1</c>/<c>WhiteRook2</c> and
        /// <c>BlackRook1</c>/<c>BlackRook2</c>. Promoted rooks should not use these names.
        /// </para>
        /// <para>✅ Updated on 9/1/2025</para>
        /// </remarks>
        /// <returns></returns>
        private Task DisableCastlingRights(Image? activePiece, Image? capturedPiece)
        {
            if (activePiece == null) return Task.CompletedTask;

            // Local helper: map a piece name to its castling flag and set it
             void DisableByName(string name)
            {
                if (name.StartsWith("WhiteKing")) { CWK = 1; return; }
                if (name.StartsWith("BlackKing")) { CBK = 1; return; }
                if (name.StartsWith("WhiteRook1")) { CWR1 = 1; return; }
                if (name.StartsWith("WhiteRook2")) { CWR2 = 1; return; }
                if (name.StartsWith("BlackRook1")) { CBR1 = 1; return; }
                if (name.StartsWith("BlackRook2")) { CBR2 = 1; return; }
            }

            // Disable because the moving piece is a king/rook
            DisableByName(activePiece.Name);

            // Disable because a rook got captured
            if (capturedPiece is not null)
                DisableByName(capturedPiece.Name);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Handles pawn promotion for user or engine:
        /// updates the pawn's image, logical name, input handler, and per-side counters.
        /// Show a modal chooser when it's the user's turn; the engine uses the preselected.
        /// <see cref="PromotionPiece"/> value (r/n/b/q).
        /// </summary>
        /// <param name="activePawn">The pawn being promoted.</param>
        /// <param name="move">Side to move: 1 = White, 0 = Black.</param>
        /// <remarks>
        /// Side effects:
        /// <list type="bullet">
        ///     <item><description>Sets <see cref="Promoted"/> and <see cref="PromotedPawn"/>.</description></item>
        ///     <item><description>Replaces the pawn's image and name (e.g., <c>WhiteQueen3</c>), rebinds MouseUp to the new piece handler.</description></item>
        ///     <item><description>Increments the corresponding piece counter (e.g., <c>NumWQ</c>).</description></item>
        ///     <item><description>On user turns, blocks on a modal promotion dialog; on engine turns, uses <see cref="PromotionPiece"/>.</description></item>
        /// </list>
        /// <para>✅ Updated on 9/1/2025</para>
        /// </remarks>
        /// <returns></returns>
        public Task PawnPromote(Image activePawn, int move)
        {
            // Build theme-dependent image paths once
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

            bool isWhite = move == 1;
            Promoted = true;
            PromotedPawn = activePawn.Name;

            // Decide the promotion choice
            char choice;
            if (UserTurn)
            {
                // Show chooser restricted to the current side
                var dialog = new Promotion(imagePaths)
                {
                    Owner = this,
                    WindowStartupLocation = WindowStartupLocation.CenterOwner
                };

                // Toggle visibility by side
                dialog.WhiteQueen.Visibility = isWhite ? Visibility.Visible : Visibility.Collapsed;
                dialog.WhiteKnight.Visibility = isWhite ? Visibility.Visible : Visibility.Collapsed;
                dialog.WhiteRook.Visibility = isWhite ? Visibility.Visible : Visibility.Collapsed;
                dialog.WhiteBishop.Visibility = isWhite ? Visibility.Visible : Visibility.Collapsed;
                dialog.BlackQueen.Visibility = isWhite ? Visibility.Collapsed : Visibility.Visible;
                dialog.BlackKnight.Visibility = isWhite ? Visibility.Collapsed : Visibility.Visible;
                dialog.BlackRook.Visibility = isWhite ? Visibility.Collapsed : Visibility.Visible;
                dialog.BlackBishop.Visibility = isWhite ? Visibility.Collapsed : Visibility.Visible;

                dialog.ShowDialog();

                var btn = dialog.ClickedButtonName ?? string.Empty;
                if (btn.StartsWith("Rook", StringComparison.Ordinal)) choice = 'r';
                else if (btn.StartsWith("Knight", StringComparison.Ordinal)) choice = 'n';
                else if (btn.StartsWith("Bishop", StringComparison.Ordinal)) choice = 'b';
                else if (btn.StartsWith("Queen", StringComparison.Ordinal)) choice = 'q';
                else
                {
                    // No selection (dialog dismissed) — keep queen as a sane default or abort.
                    choice = 'q';
                }

                PromotionPiece = choice; // keep in sync for downstream logic/PGN if you use it
            }
            else
            {
                // Engine path: PromotionPiece must be preset ('q' default is typical)
                choice = PromotionPiece != '\0' ? PromotionPiece : 'q';
            }

            ApplyPromotion(activePawn, isWhite, choice, imagePaths);
            ActivePiece = activePawn.Name;
            return Task.CompletedTask;

            // Local helper
            static int ImageIndex(bool white, char kind)
            {
                // imagePaths layout (even = white, odd = black):
                // 0 W Rook, 1 B Rook, 2 W Knight, 3 B Knight, 4 W Bishop, 5 B Bishop, 6 W Queen, 7 B Queen
                int baseIdx = white ? 0 : 1;
                int offset = kind switch
                {
                    'r' => 0,
                    'n' => 2,
                    'b' => 4,
                    _ => 6 // 'q' or anything else → queen
                };
                return baseIdx + offset;
            }

            // Local helper
            void ApplyPromotion(Image pawn, bool white, char kind, List<string> paths)
            {
                int idx = ImageIndex(white, kind);
                if (idx < 0 || idx >= paths.Count)
                    ChessLog.LogWarning($"Promotion image index {idx} out of range (paths count {paths.Count}).");

                string imgPath = (idx >= 0 && idx < paths.Count) ? paths[idx] : string.Empty;
                if (!string.IsNullOrEmpty(imgPath) && File.Exists(imgPath))
                    pawn.Source = new BitmapImage(new Uri(imgPath, UriKind.Absolute));
                else
                    ChessLog.LogWarning($"Promotion image missing: {imgPath}");

                if (white)
                {
                    switch (kind)
                    {
                        case 'r':
                            pawn.Name = $"WhiteRook{NumWR++}";
                            pawn.MouseUp -= ChessPawn_Click; pawn.MouseUp += ChessRook_Click; break;
                        case 'n':
                            pawn.Name = $"WhiteKnight{NumWN++}";
                            pawn.MouseUp -= ChessPawn_Click; pawn.MouseUp += ChessKnight_Click; break;
                        case 'b':
                            pawn.Name = $"WhiteBishop{NumWB++}";
                            pawn.MouseUp -= ChessPawn_Click; pawn.MouseUp += ChessBishop_Click; break;
                        default: // 'q'
                            pawn.Name = $"WhiteQueen{NumWQ++}";
                            pawn.MouseUp -= ChessPawn_Click; pawn.MouseUp += ChessQueen_Click; break;
                    }
                }
                else
                {
                    switch (kind)
                    {
                        case 'r':
                            pawn.Name = $"BlackRook{NumBR++}";
                            pawn.MouseUp -= ChessPawn_Click; pawn.MouseUp += ChessRook_Click; break;
                        case 'n':
                            pawn.Name = $"BlackKnight{NumBN++}";
                            pawn.MouseUp -= ChessPawn_Click; pawn.MouseUp += ChessKnight_Click; break;
                        case 'b':
                            pawn.Name = $"BlackBishop{NumBB++}";
                            pawn.MouseUp -= ChessPawn_Click; pawn.MouseUp += ChessBishop_Click; break;
                        default: // 'q'
                            pawn.Name = $"BlackQueen{NumBQ++}";
                            pawn.MouseUp -= ChessPawn_Click; pawn.MouseUp += ChessQueen_Click; break;
                    }
                }
            }
        }

        /// <summary>
        /// Reverts a tentative move after a user rejection.
        /// Restores the moved piece's location, castling rights, any captured piece,
        /// and promotion state (sprite, name, handlers, counters).
        /// </summary>
        /// <param name="activePiece">The piece that was moved.</param>
        /// <param name="castlingRights">Prior castling flags in order: WR1, WK, WR2, BR1, BK, BR2.</param>
        /// <remarks>✅ Updated on 9/1/2025</remarks>
        private void UndoMove(Image activePiece, int[] castlingRights)
        {
            if (activePiece is null) return;

            // Re-enable board interaction
            Chess_Board.IsHitTestVisible = true;

            // Put the moved piece back
            Grid.SetRow(activePiece, _oldRow);
            Grid.SetColumn(activePiece, _oldCol);

            // Restore castling rights (defensive: ensure array shape)
            if (castlingRights is { Length: 6 })
            {
                CWR1 = castlingRights[0];
                CWK  = castlingRights[1];
                CWR2 = castlingRights[2];
                CBR1 = castlingRights[3];
                CBK  = castlingRights[4];
                CBR2 = castlingRights[5];
            }

            // If the move was a castle attempt, put the rook back
            if (KingCastle || QueenCastle)
                HandleRookReset();

            // If a capture or en passant was undone, restore the captured piece
            if ((Capture || EnPassant) && _capturedPiece is not null)
            {
                _capturedPiece.Visibility = Visibility.Visible;
                _capturedPiece.IsHitTestVisible = true;
                _capturedPiece.IsEnabled = true;

                if (!Chess_Board.Children.Contains(_capturedPiece))
                    Chess_Board.Children.Add(_capturedPiece);
            }

            // If a promotion was undone, restore the pawn's sprite, name, events, and counts
            if (Promoted)
            {
                bool whiteToMove = (Move == 1);

                string pawnImagePath = System.IO.Path.Combine(
                    _executableDirectory, "Assets", "Pieces",
                    _preferences.Pieces, $"{(whiteToMove ? "White" : "Black")}Pawn.png");

                if (File.Exists(pawnImagePath))
                    _clickedPawn.Source = new BitmapImage(new Uri(pawnImagePath));

                // Restore name
                _clickedPawn.Name = _pawnName;

                // Restore handlers: pawn regains Pawn handler, remove promoted-piece handler
                _clickedPawn.MouseUp += ChessPawn_Click;

                if (_clickedButtonName.StartsWith("Queen", StringComparison.Ordinal))
                {
                    _clickedPawn.MouseUp -= ChessQueen_Click;
                    if (whiteToMove) NumWQ--; else NumBQ--;
                }
                else if (_clickedButtonName.StartsWith("Knight", StringComparison.Ordinal))
                {
                    _clickedPawn.MouseUp -= ChessKnight_Click;
                    if (whiteToMove) NumWN--; else NumBN--;
                }
                else if (_clickedButtonName.StartsWith("Rook", StringComparison.Ordinal))
                {
                    _clickedPawn.MouseUp -= ChessRook_Click;
                    if (whiteToMove) NumWR--; else NumBR--;
                }
                else if (_clickedButtonName.StartsWith("Bishop", StringComparison.Ordinal))
                {
                    _clickedPawn.MouseUp -= ChessBishop_Click;
                    if (whiteToMove) NumWB--; else NumBB--;
                }
            }

            // Clear selection and transient flags
            DeselectPieces();
            _capturedPiece = null;
            Capture = false;
            EnPassant = false;
            Promoted = false;
            KingCastle = false;
            QueenCastle = false;
        }

        /// <summary>
        /// Resets the rook to its original square if a castling move is undone,
        /// then clears the castling flags.
        /// </summary>
        /// <remarks>✅ Updated on 9/1/2025</remarks>
        private void HandleRookReset()
        {
            // Only applicable if a castle was in effect
            if ((!KingCastle && !QueenCastle))
                return;

            string rookName = (Move == 1) ?
                (KingCastle ? "WhiteRook2" : "WhiteRook1") :
                (KingCastle ? "BlackRook2" : "BlackRook1");

            var rook = Chess_Board.Children.OfType<Image>().FirstOrDefault(img => img.Name == rookName);
            if (rook != null)
            {
                Grid.SetRow(rook, _oldRow);
                Grid.SetColumn(rook, KingCastle ? 7 : 0);
            }
        }

        /// <summary>
        /// Finalizes a move: flips the side to move, updates clocks and FEN,
        /// and clears the en passant square unless one was created this move.
        /// </summary>
        /// <remarks>✅ Updated on 9/1/2025</remarks>
        private Task FinalizeMoveAsync()
        {
            // Switch side to move
            Move = 1 - Move;

            // Compute "to" for callout once
            int toRow = (KingCastle || QueenCastle) ? _oldRow : _newRow;
            int toCol = KingCastle ? 7 : QueenCastle ? 0 : _newCol;
            MoveCallout(_oldRow, _oldCol, toRow, toCol);

            // Clear en passant square unless one was created by a double pawn push
            if (!EnPassantCreated) EnPassantSquare.Clear();

            // Halfmove clock: reset on capture or pawn move; otherwise increment
            Halfmove = (Capture || _clickedPawn != null) ? 0 : Halfmove + 1;

            // Fullmove number increases after Black completes a move
            if (Move == 1) Fullmove++;

            // Record SAN/PGN start square; update FEN snapshot.
            _startFile = (char)('a' + _oldCol);
            _startRank = (8 - _oldRow).ToString();
            _startPosition = $"{_startFile}{_startRank}";
            CreateFenCode();
            return Task.CompletedTask;
        }
        
        /// <summary>
        /// Rebuilds the FEN string (<see cref="Fen"/>) for the current UI board state and updates
        /// material counts. The method:
        /// <list type="bullet">
        ///     <item><description>Scans all pieces (<see cref="Image"/>s) once to build an 8x8 map.</description></item>
        ///     <item><description>Compresses empty runs per rank to produce the piece-placement field.</description></item>
        ///     <item><description>Appends side-to-move from <see cref="Move"/> (<c>w</c> when <see cref="Move"/> == 1, else <c>b</c>).</description></item>
        ///     <item><description>Derives castling rights from <see cref="CWK"/>, <see cref="CWR1"/>, <see cref="CWR2"/>, <see cref="CBK"/>, <see cref="CBR1"/>, and <see cref="CBR2"/> order KQkq; <c>-</c> if none).</description></item>
        ///     <item><description>Emits the en passant target square from <see cref="EnPassantSquare"/> (or <c>-</c>).</description></item>
        ///     <item><description>Appends the halfmove clock <see cref="Halfmove"/> and fullmove number <see cref="Fullmove"/>.</description></item>
        /// </list>
        /// </summary>
        /// <remarks>
        /// Also sets <see cref="PreviousFen"/> (old value) and recomputes <see cref="WhiteMaterial"/>/<see cref="BlackMaterial"/>.
        /// <para>✅ Updated on 8/20/2025</para>
        /// </remarks>
        private void CreateFenCode()
        {
            PreviousFen = Fen;
            WhiteMaterial = 0;
            BlackMaterial = 0;

            // Build a quick lookup of what occupies each grid cell
            int rows = Chess_Board.RowDefinitions.Count;
            int cols = Chess_Board.ColumnDefinitions.Count;
            var board = new char[rows, cols];

            foreach (var img in Chess_Board.Children.OfType<Image>())
            {
                int r = Grid.GetRow(img);
                int c = Grid.GetColumn(img);

                // Map image name to FEN char, and accumulate material
                char ch = MapNameToFenAndAccumulate(img.Name);
                if (ch != '\0') board[r, c] = ch;
            }

            // Piece placement (rank 8 to 1 corresponds to grid rows 0..7 in the layout)
            var sb = new StringBuilder(64);
            for (int r = 0; r < rows; r++)
            {
                int empty = 0;
                for (int c = 0; c < cols; c++)
                {
                    char ch = board[r, c];
                    if (ch == '\0')
                    {
                        empty++;
                    }
                    else
                    {
                        if (empty > 0) { sb.Append(empty); empty = 0; }
                        sb.Append(ch);
                    }
                }
                if (empty > 0) sb.Append(empty);
                if (r != rows - 1) sb.Append('/');
            }

            // Side to move
            sb.Append(Move == 1 ? " w " : " b ");

            // Castling rights (KQkq order; '-' if none)
            int startLen = sb.Length;
            if (CWK == 0)
            {
                if (CWR2 == 0) sb.Append('K');
                if (CWR1 == 0) sb.Append('Q');
            }
            if (CBK == 0)
            {
                if (CBR2 == 0) sb.Append('k');
                if (CBR1 == 0) sb.Append('q');
            }
            if (sb.Length == startLen) sb.Append('-');

            // En passant target
            if (EnPassantSquare.Count == 1)
            {
                sb.Append(' ');
                sb.Append((char)('a' + _newCol));
                sb.Append(Move == 1 ? (_newRow + 3).ToString() : (_newRow - 1).ToString());
            }
            else
            {
                sb.Append(" -");
            }

            // Halfmove and fullmove
            sb.Append(' ').Append(Halfmove).Append(' ').Append(Fullmove);

            Fen = sb.ToString();
            System.Diagnostics.Debug.WriteLine($"\n\nFEN: {Fen}");

            // Local helper
            char MapNameToFenAndAccumulate(string name)
            {
                // Expect names like "WhitePawn1", "BlackQueen2", etc.
                if (name.StartsWith("White"))
                {
                    if (name.Contains("Pawn")) { WhiteMaterial += 1; return 'P'; }
                    if (name.Contains("Knight")) { WhiteMaterial += 3; return 'N'; }
                    if (name.Contains("Bishop")) { WhiteMaterial += 3; return 'B'; }
                    if (name.Contains("Rook")) { WhiteMaterial += 5; return 'R'; }
                    if (name.Contains("Queen")) { WhiteMaterial += 9; return 'Q'; }
                    if (name.Contains("King")) { return 'K'; }
                }
                else if (name.StartsWith("Black"))
                {
                    if (name.Contains("Pawn")) { BlackMaterial += 1; return 'p'; }
                    if (name.Contains("Knight")) { BlackMaterial += 3; return 'n'; }
                    if (name.Contains("Bishop")) { BlackMaterial += 3; return 'b'; }
                    if (name.Contains("Rook")) { BlackMaterial += 5; return 'r'; }
                    if (name.Contains("Queen")) { BlackMaterial += 9; return 'q'; }
                    if (name.Contains("King")) { return 'k'; }
                }
                return '\0';
            }
        }

        #endregion

        #region Stockfish Querying & Parsing

        /// <summary>
        /// Runs Stockfish with the given FEN and depth, parses its output, 
        /// and extracts evaluated moves.
        /// </summary>
        /// <param name="fen">FEN string representing the current board state.</param>
        /// <param name="depth">Search depth to request from Stockfish.</param>
        /// <param name="stockfishPath">Filesystem path to the Stockfish engine executable.</param>
        /// <returns>
        /// A tuple of:
        /// <list type="bullet">
        ///     <item><description><c>main</c>: Moves evaluated at or beyond the requested depth (tuple of score label <c>("cp" | "mate")</c>, score value, UCI move).</description></item>
        ///     <item><description><c>reserve</c>: Fallback moves (typically from shallow depth = 1).</description></item>
        ///     <item><description><c>lines</c>: All raw Stockfish output lines after header trimming.</description></item>
        /// </list>
        /// <para>✅ Updated on 8/31/2025</para>
        /// </returns>
        private static async Task<(List<(string cp, string cpValue, string possibleMove)>, List<(string cp, string cpValue, string possibleMove)>, string[])> ParseStockfishOutputAsync(string fen, int depth, string stockfishPath, CancellationToken ct = default)
        {
            string stockfishOut = await StockfishMovesAnalysisAsync(fen, depth, stockfishPath, ct: ct);

            // Checkpoint after big awaits / CPU bursts
            ct.ThrowIfCancellationRequested();

            // Skip the initial two banner lines
            string[] lines = [.. stockfishOut.Split('\n').Skip(2)];

            var main = new List<(string cp, string cpValue, string possibleMove)>();
            var reserve = new List<(string cp, string cpValue, string possibleMove)>();

            foreach (string line in lines)
            {
                if (ct.IsCancellationRequested) ct.ThrowIfCancellationRequested();

                if (!line.TrimStart().StartsWith("info", StringComparison.OrdinalIgnoreCase))
                    continue;

                if (TryParseInfoLine(line, out int parsedDepth, out string scoreLabel, out string scoreValue, out string pvMove))
                {
                    if (parsedDepth >= depth)
                        main.Add((scoreLabel, scoreValue, pvMove));
                    else if (parsedDepth == 1)
                        reserve.Add((scoreLabel, scoreValue, pvMove));
                }
            }

            // Fallback: if nothing reached the requested depth, keep whatever we got
            return (main, reserve, lines);

            // Local helper
            static bool TryParseInfoLine(string line, out int parsedDepth, out string scoreLabel, out string scoreValue, out string pvMove)
            {
                parsedDepth = 0;
                scoreLabel = "";
                scoreValue = "";
                pvMove = "";

                // Tokenize by whitespace
                var parts = line.Split((char[])null!, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 6) return false;

                // Find "depth X"
                int depthIdx = Array.IndexOf(parts, "depth");
                if (depthIdx >= 0 && depthIdx + 1 < parts.Length && int.TryParse(parts[depthIdx + 1], out int d))
                    parsedDepth = d;

                // Find "score <cp|mate> <value>"
                int scoreIdx = Array.IndexOf(parts, "score");
                if (scoreIdx >= 0 && scoreIdx + 2 < parts.Length)
                {
                    string label = parts[scoreIdx + 1];
                    string value = parts[scoreIdx + 2];

                    if ((label == "cp" || label == "mate") && IsSignedNumber(value))
                    {
                        scoreLabel = label == "mate" ? "mate" : "cp";
                        scoreValue = value;
                    }
                }

                // Find "pv <firstmove> ..."
                int pvIdx = Array.IndexOf(parts, "pv");
                if (pvIdx >= 0 && pvIdx + 1 < parts.Length)
                {
                    // The first move in PV is enough
                    pvMove = parts[pvIdx + 1];
                }

                // Valid only if we got a depth, a score, and a PV move
                return parsedDepth > 0 && !string.IsNullOrEmpty(scoreLabel) && !string.IsNullOrEmpty(scoreValue) && !string.IsNullOrEmpty(pvMove);

                static bool IsSignedNumber(string s)
                {
                    // Allows "123", "-45", "+7"
                    if (string.IsNullOrEmpty(s)) return false;
                    int start = (s[0] == '-' || s[0] == '+') ? 1 : 0;
                    if (start >= s.Length) return false;
                    for (int i = start; i < s.Length; i++)
                        if (!char.IsDigit(s[i])) return false;
                    return true;
                }
            }
        }

        /// <summary>
        /// Runs a single UCI analysis with Stockfish and returns the engine's full stdout.
        /// Sends:
        /// <list type="bullet">
        ///     <item><description>setoption name MultiPV value <paramref name="multiPV"/>;</description></item>
        ///     <item><description>position fen <paramref name="fen"/>;</description></item>
        ///     <item><description>go depth <paramref name="depth"/>;</description></item>
        /// </list>
        /// This method streams output until it encounters "bestmove", then sends "quit",
        /// awaits process exit, and returns everything printed by the engine.
        /// </summary>
        /// <param name="fen">The current position in FEN format.</param>
        /// <param name="depth">Search depth to use for "go depth".</param>
        /// <param name="stockfishPath">Absolute path to the Stockfish executable.</param>
        /// <param name="multiPV">Number of principal variations to request (default 40).</param>
        /// <param name="ct">Optional token to cancel the run.</param>
        /// <remarks>✅ Updated on 8/31/2025</remarks>
        /// <returns>The complete stdout captured from Stockfish for this query.</returns>
        private static async Task<string> StockfishMovesAnalysisAsync(string fen, int depth, string stockfishPath, int multiPV = 40, CancellationToken ct = default)
        {
            if (string.IsNullOrWhiteSpace(stockfishPath) || !File.Exists(stockfishPath))
                return "Stockfish executable not found.";

            if (string.IsNullOrWhiteSpace(fen))
                return "FEN was empty.";

            var startInfo = new ProcessStartInfo
            {
                FileName = stockfishPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            var output = new StringBuilder(4096);  // generous headroom
            var bestMoveSeen = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            using var process = new Process { StartInfo = startInfo, EnableRaisingEvents = true };

            DataReceivedEventHandler? stdoutHandler = null;
            DataReceivedEventHandler? stderrHandler = null;

            try
            {
                // Stream output
                stdoutHandler = (_, e) =>
                {
                    if (string.IsNullOrEmpty(e.Data)) return;

                    output.AppendLine(e.Data);

                    // Once bestmove appears, we can ask the engine to quit.
                    if (e.Data.StartsWith("bestmove", StringComparison.Ordinal))
                    {
                        try
                        {
                            // Sending quit twice is harmless; we guard with TrySetResult below
                            process.StandardInput.WriteLine("quit");
                            process.StandardInput.Flush();
                        }
                        catch { /* process may already be exiting */ }

                        bestMoveSeen.TrySetResult(true);
                    }
                };

                // (Optional) capture stderr as well
                stderrHandler = (_, e) =>
                {
                    if (!string.IsNullOrEmpty(e.Data))
                        output.AppendLine(e.Data);
                };

                process.OutputDataReceived += stdoutHandler;
                process.ErrorDataReceived += stderrHandler;

                if (!process.Start())
                    throw new InvalidOperationException("Failed to start Stockfish process.");

                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var reg = ct.Register(() =>
                {
                    // Cancel the best-move waiter so Task.WhenAny can complete it immediately
                    bestMoveSeen.TrySetCanceled(ct);
                    try { if (!process.HasExited) process.Kill(); } catch { /* ignore */ }
                });

                // UCI handshake
                if (!process.StandardInput.BaseStream.CanWrite)
                    throw new IOException("Unable to write to Stockfish sdtin.");

                await process.StandardInput.WriteLineAsync("uci");
                await process.StandardInput.FlushAsync(ct);

                // Wait until we see "uciok" in stdout (quick polling loop from buffer)
                // If you want zero polling, swap to a read-line loop; keeping your event model:
                while (!ct.IsCancellationRequested && !output.ToString().Contains("\nuciok"))
                    await Task.Delay(10, ct);

                await process.StandardInput.WriteLineAsync("isready");
                await process.StandardInput.FlushAsync(ct);
                while (!ct.IsCancellationRequested && !output.ToString().Contains("\nreadyok"))
                    await Task.Delay(10, ct);

                await process.StandardInput.WriteLineAsync($"setoption name MultiPV value {multiPV}");
                await process.StandardInput.WriteLineAsync($"position fen {fen}");
                await process.StandardInput.WriteLineAsync($"go depth {depth}");
                await process.StandardInput.FlushAsync(ct);

                // Wait until we either see bestmove or the process exits (cancellable)
                var exitTask = process.WaitForExitAsync(ct);
                await Task.WhenAny(bestMoveSeen.Task, exitTask);

                // If bestmove never arrived but the process is still running, ask politely then await exit
                if (!process.HasExited)
                {
                    try { process.StandardInput.WriteLine("quit"); process.StandardInput.Flush(); } catch { }
                    await exitTask; // will throw OperationCanceledException if ct was cancelled
                }

                ct.ThrowIfCancellationRequested();
                return output.ToString();
            }
            finally
            {
                if (stdoutHandler is not null) process.OutputDataReceived -= stdoutHandler;
                if (stderrHandler is not null) process.ErrorDataReceived -= stderrHandler;
                try { if (!process.HasExited) process.Kill(); } catch { }
            }
        }

        /// <summary>
        /// Asks Stockfish whether the given FEN position is check or checkmate,
        /// returning a SAN-ready marker:
        /// <list type="bullet">
        ///     <item><description><c>"#"</c> for checkmate</description></item>
        ///     <item><description><c>"+"</c> for check</description></item>
        ///     <item><description><c>""</c> for neither</description></item>
        /// </list>
        /// This spawns the Stockfish process, sends "position", "d" (to get "Checkers:"), and
        /// a shallow "go depth 1" (to see if "bestmove (none)" appears), then parses the output.
        /// </summary>
        /// <param name="fen">The position in FEN format.</param>
        /// <param name="stockfishPath">Absolute path to the Stockfish executable.</param>
        /// <returns><c>"#"</c>, <c>"+"</c>, or <c>""</c> depending on the state.</returns>
        /// <remarks>✅ Updated on 8/20/2025</remarks>
        private static async Task<string> StockfishCheckAnalysisAsync(string fen, string stockfishPath)
        {
            if (string.IsNullOrWhiteSpace(stockfishPath) || !File.Exists(stockfishPath))
                return "Stockfish executable not found.";

            if (string.IsNullOrWhiteSpace(fen))
                return "FEN was empty.";

            var startInfo = new ProcessStartInfo
            {
                FileName = stockfishPath,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true
            };

            bool isCheckmate = false;
            bool isCheck = false;
            var outputBuilder = new StringBuilder();

            using var process = new Process { StartInfo = startInfo };

            // Parse lines as they arrive (no 'async' here to avoid async-void pitfalls)
            process.OutputDataReceived += (_, e) =>
            {
                var line = e.Data;
                if (string.IsNullOrEmpty(line)) return;

                outputBuilder.AppendLine(line);

                // If Stockfish reports no legal move, it's mate/stalemate; we disambiguate using "Checkers:"
                if (line.StartsWith("bestmove (none)", StringComparison.OrdinalIgnoreCase))
                {
                    // We'll still rely on the "Checkers:" line to tell check vs stalemate,
                    // but if there's no checkers and no bestmove, it's stalemate (no marker).
                    // We'll set isCheckmate=true here and let "Checkers:" flip isCheck if needed.
                    isCheckmate = true;
                }
                else if (line.StartsWith("Checkers:", StringComparison.OrdinalIgnoreCase))
                {
                    // "Checkers:" is followed by coordinates if in check; blank means not in check.
                    // Example: "Checkers: e4"  => in check.
                    // Example: "Checkers:"      => not in check.
                    string data = line.Length > 9 ? line[9..].Trim() : string.Empty;
                    if (!string.IsNullOrEmpty(data))
                    {
                        isCheck = true;
                    }
                }
            };

            process.ErrorDataReceived += (_, e) =>
            {
                if (!string.IsNullOrEmpty(e.Data))
                    outputBuilder.AppendLine("[stderr] " + e.Data);
            };

            try
            {
                process.Start();
                process.BeginOutputReadLine();
                process.BeginErrorReadLine();

                using var writer = process.StandardInput;

                if (!writer.BaseStream.CanWrite)
                    return "Unable to write to Stockfish.";

                // Minimal sequence for check/checkmate detection:
                // 1) Load position
                // 2) Print details (to get "Checkers:")
                // 3) Search shallowly to see if bestmove exists
                await writer.WriteLineAsync($"position fen {fen}");
                await writer.WriteLineAsync("d");             // prints "Checkers:" line
                await writer.WriteLineAsync("go depth 1");    // emits "bestmove ..." (or "(none)")
                await writer.WriteLineAsync("quit");
                await writer.FlushAsync();

                await process.WaitForExitAsync();
            }
            catch (Exception ex)
            {
                return $"Stockfish error: {ex.Message}";
            }

            // Disambiguation:
            // - If no bestmove and we are in check => checkmate ("#")
            // - If bestmove exists and we are in check => check ("+")
            // - If no bestmove and NOT in check => stalemate (return "")
            // - Else => neither (return "")
            if (isCheckmate && isCheck) return "#";
            if (!isCheckmate && isCheck) return "+";
            return "";
        }

        /// <summary>
        /// Selects a UCI move from the engine's candidates.
        /// <list type="bullet">
        ///     <item><description>If there is only one move or <paramref name="topEngineMove"/> is true, always returns the best move.</description></item>
        ///     <item><description>Otherwise, checks the top 2-3 moves for a "critical moment" (≥ 300 cp gap).</description></item>
        ///     <item><description>If found, trims the move list based on <paramref name="criticalMoveConversion"/> (best only, best two, or fall to #3-end)</description></item>
        ///     <item><description>If not found, keeps the full candidate set.</description></item>
        ///     <item><description>The final move is chosen using a Gaussian distribution biased by <paramref name="bellCurvePercentile"/>, favoring stronger moves while still allowing weaker ones occasionally.</description></item>
        /// </list>
        /// </summary>
        /// <param name="moves">Filtered candidate moves (best to worst) within a CP window).</param>
        /// <param name="sorted">Primary move list sorted strictly best to worst; index 0 is the engine's top move.</param>
        /// <param name="topEngineMove">Forces picking the engine's top move (e.g., in mating sequences).</param>
        /// <param name="bellCurvePercentile">0-100: higher biases toward stronger (lower index) choices. 90 means "hug the top moves".</param>
        /// <param name="criticalMoveConversion">0-100: chance to "convert" a critical moment and keep only the best; otherwise "miss" it and degrade to a worse move among the top few.</param>
        /// <remarks>✅ Updated on 8/19/2025</remarks>
        /// <returns>UCI move string like "e2e4" ot "a7a8q".</returns>
        private static string SelectMoveString(List<(string cp, string cpValue, string possibleMove)> moves, List<(string cp, string cpValue, string possibleMove)> sorted, bool topEngineMove, double bellCurvePercentile, int criticalMoveConversion)
        {
            // Safety checks
            if (sorted.Count == 0) return string.Empty;
            if (moves.Count == 0) return sorted[0].possibleMove.TrimEnd('\r');

            // Forced top line or single option, pick best immediately
            if (moves.Count == 1 || topEngineMove)
                return sorted[0].possibleMove.TrimEnd('\r');

            // “Critical moment” gating among top 2–3 moves
            const int criticalCpThreshold = 300;
            var top3 = moves.Take(3).ToList();

            if (top3.Count >= 2 &&
                int.TryParse(top3[0].cpValue, out int cp1) &&
                int.TryParse(top3[1].cpValue, out int cp2))
            {
                int diff12 = Math.Abs(cp1 - cp2);
                if (diff12 >= criticalCpThreshold)
                {
                    // Flip a coin against conversion chance
                    if (Random.Shared.Next(101) > criticalMoveConversion)
                    {
                        // Missed the moment, drop the best (fall to #2+)
                        if (moves.Count > 1) moves.RemoveAt(0);
                    }
                    else
                    {
                        // Converted the moment, keep only the best
                        moves.RemoveRange(1, Math.Max(0, moves.Count - 1));
                    }
                }
                else if (top3.Count == 3 && int.TryParse(top3[2].cpValue, out int cp3))
                {
                    int diff23 = Math.Abs(cp2 - cp3);
                    if (diff23 >= criticalCpThreshold)
                    {
                        if (Random.Shared.Next(101) > criticalMoveConversion)
                        {
                            // Missed between #2 and #3, fall to #3+
                            if (moves.Count > 2) moves.RemoveRange(0, 2);
                        }
                        else
                        {
                            // Converted the moment, keep only the two best moves
                            moves.RemoveRange(2, Math.Max(0, moves.Count - 2));
                        }
                    }
                }
            }

            // Gaussian pick biased by bellCurvePercentile (higher = more conservative)
            int count = moves.Count;
            if (count == 1)
                return moves[0].possibleMove.TrimEnd('\r');

            // Higher percentile, mean closer to the top (index 0)
            double meanIndex = Math.Round((1 - (bellCurvePercentile / 100.0)) * (count - 1));
            double stdDev = Math.Max(1.0, count / 4.0);

            int idx;
            do
            {
                // Box–Muller
                double u1 = 1.0 - Random.Shared.NextDouble();
                double u2 = 1.0 - Random.Shared.NextDouble();
                double z = Math.Sqrt(-2.0 * Math.Log(u1)) * Math.Sin(2.0 * Math.PI * u2);
                double sample = meanIndex + stdDev * z;
                idx = (int)Math.Round(sample);
            } while (idx < 0 || idx >= count);

            return moves[idx].possibleMove.TrimEnd('\r');
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
        /// <remarks>✅ Updated on 8/31/2025</remarks>
        private Task MovePieceAsync(Image piece, int newRow, int newColumn, int oldRow, int oldColumn)
        {
            // Ensure we're on the UI thread
            if (!Application.Current.Dispatcher.CheckAccess())
                return Application.Current.Dispatcher.InvokeAsync(
                    () => MovePieceAsync(piece, newRow, newColumn, oldRow, oldColumn)
                ).Task;

            // 1) Set LOGICAL state first so FEN can read the new board immediately after await.
            Grid.SetRow(piece, newRow);
            Grid.SetColumn(piece, newColumn);

            // 2) Compute pixel deltas and animate a translate back to zero (visual illusion of movement).
            double cellW = Chess_Board.ColumnDefinitions[0].ActualWidth;
            double cellH = Chess_Board.RowDefinitions[0].ActualHeight;
            double dx = (newColumn - oldColumn) * cellW;
            double dy = (newRow - oldRow) * cellH;

            var tt = new TranslateTransform();
            var rt = new RotateTransform { Angle = (_flip == 1 ? 180 : 0) };

            var tg = new TransformGroup();
            tg.Children.Add(rt);
            tg.Children.Add(tt);
            piece.RenderTransform = tg;

            var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

            var animX = new DoubleAnimation
            {
                From = -dx,
                To = 0,
                Duration = TimeSpan.FromMilliseconds(150),
                FillBehavior = FillBehavior.Stop
            };
            var animY = new DoubleAnimation
            {
                From = -dy,
                To = 0,
                Duration = animX.Duration,
                FillBehavior = FillBehavior.Stop
            };

            int completed = 0;
            EventHandler? onDone = null;
            onDone = (_, __) =>
            {
                if (Interlocked.Increment(ref completed) == 2)
                {
                    // Snap back to logical position and finish
                    piece.RenderTransform = null;
                    // detach handlers (defensive; Completed fires once, but good hygiene)
                    animX.Completed -= onDone;
                    animY.Completed -= onDone;
                    tcs.TrySetResult(true);
                }
            };

            animX.Completed += onDone;
            animY.Completed += onDone;

            tt.BeginAnimation(TranslateTransform.XProperty, animX);
            tt.BeginAnimation(TranslateTransform.YProperty, animY);

            return tcs.Task;
        }

        /// <summary>
        /// Animates the rook during castling based on which side (king/queen) and who just moved.
        /// </summary>
        /// <param name="isWhite">True if Black made the king move; otherwise White did.</param>
        /// <param name="kingside">True for kingside castling, false for queenside.</param>
        /// <remarks>✅ Updated on 8/31/2025</remarks>
        private async Task MoveCastleRookAsync(bool isWhite, bool kingside)
        {
            string rookName = isWhite
                ? (kingside ? "WhiteRook2" : "WhiteRook1")
                : (kingside ? "BlackRook2" : "BlackRook1");

            int rookStartCol = kingside ? 7 : 0;
            int rookTargetCol = kingside ? _newCol - 1 : _newCol + 1;

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
        /// <remarks>✅ Updated on 6/11/2025</remarks>
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
        /// <remarks>✅ Updated on 6/11/2025</remarks>
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
        /// <remarks>✅ Updated on 6/11/2025</remarks>
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
            _selectedWhiteElo = (ComboBoxItem)WhiteCpuElo.SelectedItem;
            _selectedBlackElo = (ComboBoxItem)BlackCpuElo.SelectedItem;

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
                WhiteCpuElo.IsEnabled = !isUserVsCom;
                BlackCpuElo.IsEnabled = !isUserVsCom;
                PlayButton.IsEnabled = false;
                ResumeButton.IsEnabled = false;

                // Clear irrelevant selections
                if (isUserVsCom)
                {
                    WhiteCpuElo.SelectedItem = null;
                    BlackCpuElo.SelectedItem = null;
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
                WhiteCpuElo.IsEnabled = false;
                BlackCpuElo.IsEnabled = false;

                // Resume or play based on pause state
                if (!IsPaused)
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
            _selectedWhiteElo = WhiteCpuElo.SelectedItem as ComboBoxItem;
            _selectedBlackElo = BlackCpuElo.SelectedItem as ComboBoxItem;

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
        /// <para>✅ Updated on 8/31/2025</para>
        /// </remarks>
        private async void InactivityTimer_Tick(object? sender, EventArgs e)
        {
            _inactivityTimer.Stop();

            // If both Epson robots are not already connected
            if (!GlobalState.WhiteEpsonConnected || !GlobalState.BlackEpsonConnected)
            {
                await EpsonConnectAsync(EpsonMotion);
            }

            // Start or resume the game
            if (GlobalState.WhiteEpsonConnected && GlobalState.BlackEpsonConnected)
            {
                // Randomize difficulty and set mode
                await AssignRandomElo();
                Play_Type.SelectedIndex = (int)GameMode.ComVsCom;

                // Reset UI state
                ToggleUIState(false);
                PlayButton.IsEnabled = true;
                ResumeButton.IsEnabled = false;
                Elo.SelectedItem = null;
                Color.SelectedItem = null;

                if (!IsPaused)
                {
                    ChessLog.LogInformation("Inactivity timeout reached. Starting new game.");
                    _ = StartGameAsync();
                }
                else
                {
                    ChessLog.LogInformation("Inactivity timout reached. Resuming game.");
                    _ = ResumeGame();
                }
            }
            else
            {
                _inactivityTimer.Start();
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
            if ((isWhite && Move == 0) || (!isWhite && Move == 1))
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
            if ((isWhite && Move == 0) || (!isWhite && Move == 1))
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
            if ((isWhite && Move == 0) || (!isWhite && Move == 1))
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
            if ((isWhite && Move == 0) || (!isWhite && Move == 1))
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
            if ((isWhite && Move == 0) || (!isWhite && Move == 1))
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
            if ((isWhite && Move == 0) || (!isWhite && Move == 1))
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
            _selectedPiece =
                _clickedPawn ?? _clickedKnight ?? _clickedBishop ??
                _clickedRook ?? _clickedQueen ?? _clickedKing;

            if (_selectedPiece == null) return;
            if (sender is not Button clickedSquare) return;

            // Prevent double-click races during this handler
            var boardWasEnabled = Chess_Board.IsHitTestVisible;
            Chess_Board.IsHitTestVisible = false;

            // Snap TCS in case the loop replaces it mid-handler
            var tcs = _userMoveTcs;

            // From & To grid coords
            _oldRow = Grid.GetRow(_selectedPiece);
            _oldCol = Grid.GetColumn(_selectedPiece);
            _newRow = Grid.GetRow(clickedSquare);
            _newCol = Grid.GetColumn(clickedSquare);

            // Human-readable (e.g., "e4")
            _endFile = (char)(_newCol + 'a');
            _endRank = (8 - _newRow).ToString();
            _endPosition = $"{_endFile}{_endRank}";

            bool IsValidMove()
            {
                if (ReferenceEquals(_selectedPiece, _clickedPawn))
                    return new PawnValidMove.PawnValidation(Chess_Board, this)
                        .ValidateMove(_oldRow, _oldCol, _newRow, _newCol, Move);

                if (ReferenceEquals(_selectedPiece, _clickedKnight))
                    return new KnightValidMove.KnightValidation()
                        .ValidateMove(_oldRow, _oldCol, _newRow, _newCol);

                if (ReferenceEquals(_selectedPiece, _clickedBishop))
                    return new BishopValidMove.BishopValidation(Chess_Board, this)
                        .ValidateMove(_oldRow, _oldCol, _newRow, _newCol);

                if (ReferenceEquals(_selectedPiece, _clickedRook))
                    return new RookValidMove.RookValidation(Chess_Board, this)
                        .ValidateMove(_oldRow, _oldCol, _newRow, _newCol);

                if (ReferenceEquals(_selectedPiece, _clickedQueen))
                    return new QueenValidMove.QueenValidation(Chess_Board, this)
                        .ValidateMove(_oldRow, _oldCol, _newRow, _newCol);

                if (ReferenceEquals(_selectedPiece, _clickedKing))
                    return new KingValidMove.KingValidation(Chess_Board, this)
                        .ValidateMove(_oldRow, _oldCol, _newRow, _newCol, Move,
                                      CWK, CBK, CWR1, CWR2, CBR1, CBR2);

                return false;
            }

            if (!IsValidMove())
            {
                if (_pieceSounds) PlaySound("PieceIllegal");
                Chess_Board.IsHitTestVisible = boardWasEnabled;
                return;
            }

            // Tentative board change for check verification
            Grid.SetRow(_selectedPiece, _newRow);
            Grid.SetColumn(_selectedPiece, _newCol);

            // Track king squares
            int prevWhiteKingRow = _whiteKingRow, prevWhiteKingCol = _whiteKingCol;
            int prevBlackKingRow = _blackKingRow, prevBlackKingCol = _blackKingCol;

            if (_selectedPiece.Name.StartsWith("WhiteKing", StringComparison.Ordinal))
            {
                _whiteKingRow = _newRow;
                _whiteKingCol = _newCol;
            }
            else if (_selectedPiece.Name.StartsWith("BlackKing", StringComparison.Ordinal))
            {
                _blackKingRow = _newRow;
                _blackKingCol = _newCol;
            }

            try
            {
                // Ensure the move does not leave your king in check
                var checkVerification = new CheckVerification(Chess_Board, this);
                bool positionOk = checkVerification.ValidatePosition(
                    _whiteKingRow, _whiteKingCol, _blackKingRow, _blackKingCol, _newRow, _newCol, Move);

                if (!positionOk)
                {
                    // Revert tentative move + king tracking
                    Grid.SetRow(_selectedPiece, _oldRow);
                    Grid.SetColumn(_selectedPiece, _oldCol);

                    _whiteKingRow = prevWhiteKingRow; _whiteKingCol = prevWhiteKingCol;
                    _blackKingRow = prevBlackKingRow; _blackKingCol = prevBlackKingCol;

                    if (_pieceSounds) PlaySound("PieceIllegal");
                    Chess_Board.IsHitTestVisible = boardWasEnabled;
                    return;
                }

                ActivePiece = _selectedPiece.Name;

                bool makeMove;
                // Dispatch to the correct move manager
                if (_selectedPiece.Name.Contains("Pawn", StringComparison.Ordinal))
                    makeMove = await PawnMoveManagerAsync(_selectedPiece);
                else
                    makeMove = await MoveManagerAsync(_selectedPiece);

                // Complete the user turn
                if (makeMove)
                    tcs?.TrySetResult(true);
            }
            catch (Exception ex)
            {
                // Revert tentative move on any failure
                Grid.SetRow(_selectedPiece, _oldRow);
                Grid.SetColumn(_selectedPiece, _oldCol);

                _whiteKingRow = prevWhiteKingRow; _whiteKingCol = prevWhiteKingCol;
                _blackKingRow = prevBlackKingRow; _blackKingCol = prevBlackKingCol;

                ChessLog.LogError("Failed to complete move.", ex);
            }
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
            await EpsonConnectAsync(checkBox);
            _inactivityTimer.Start();
        }

        private async void CognexVisionAsync(object sender, EventArgs e)
        {
            if (sender is not CheckBox checkBox)
                return;

            DisableCognexElements();
            if (!GlobalState.WhiteCognexConnected) { SetCognexStatusLight(Chess_Project.ChessColor.White, Brushes.Yellow); }
            if (!GlobalState.BlackCognexConnected) { SetCognexStatusLight(Chess_Project.ChessColor.Black, Brushes.Yellow); }

            // Begin animated feedback
            UpdateRectangleClip(65, Visibility.Visible, 75);

            // Toggle CameraComm state and attempt communication
            _cameraVision = !_cameraVision;
            await CognexConnectAsync(checkBox);

            CognexVision.IsEnabled = true;
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
        /// Sets the fill color of one robot status indicator to reflect connection state.
        /// </summary>
        /// <param name="color">The respective robot color.</param>
        /// <param name="statusColor">The brush color to apply to the robot's status light.</param>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><c>Green</c>: Connected</item>
        ///     <item><c>Red</c>: Disconnected</item>
        ///     <item><c>Yellow</c>: Attempting connection</item>
        /// </list>
        /// <para>✅ Updated on 7/18/2025</para>
        /// </remarks>
        private void SetEpsonStatusLight(ChessColor color, Brush statusColor)
        {
            if (color == ChessColor.White) { WhiteEpsonStatus.Fill = statusColor; }
            if (color == ChessColor.Black) { BlackEpsonStatus.Fill = statusColor; }
        }

        /// <summary>
        /// Sets the fill color of one camera status indicator to reflect connection state.
        /// </summary>
        /// <param name="color">The respective camera color.</param>
        /// <param name="statusColor">The brush color to apply to the camera's status light.</param>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><c>Green</c>: Connected</item>
        ///     <item><c>Red</c>: Disconnected</item>
        ///     <item><c>Yellow</c>: Attempting connection</item>
        /// </list>
        /// <para>✅ Updated on 8/29/2025</para>
        /// </remarks>
        private void SetCognexStatusLight(ChessColor color, Brush statusColor)
        {
            if (color == ChessColor.White) { WhiteCognexStatus.Fill = statusColor; }
            if (color == ChessColor.Black) { BlackCognexStatus.Fill = statusColor; }
        }

        /// <summary>
        /// Disables key UI elements during an Epson RC+ connection attempt.
        /// </summary>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void DisableEpsonElements()
        {
            EpsonMotion.IsChecked = false;
            EpsonMotion.IsEnabled = false;
            Play_Type.IsEnabled = false;

            TogglePlayTypeUI(false);
        }

        /// <summary>
        /// Disables key UI elements during a Cognex connection attempt.
        /// </summary>
        /// <remarks>✅ Updated on 8/29/2025</remarks>
        private void DisableCognexElements()
        {
            CognexVision.IsChecked = false;
            CognexVision.IsEnabled = false;
        }

        /// <summary>
        /// Re-enables key UI elements after completing an Epson RC+ connection attempt.
        /// </summary>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void EnableEpsonElements()
        {
            Play_Type.IsEnabled = true;
            EpsonMotion.IsEnabled = true;

            RestorePlayState();
            TogglePlayTypeUI(true);
        }

        /// <summary>
        /// Updates the clipping geometry of the <see cref="ConnectionRect"/> rectangle 
        /// to maintain smooth animation effects during connection state transitions.
        /// </summary>
        /// <param name="rectHeight">The new height to apply to <see cref="ConnectionRect"/>.</param>
        /// <param name="visibility">The visibility state to apply to <see cref="AttemptingConnection"/>.</param>
        /// <param name="clipHeight">The height of the clipping rectangle geometry.</param>
        /// <remarks>
        /// Adjusts both the visual size of <see cref="ConnectionRect"/> and the clipping region's
        /// dimensions/corner radius to ensure consistent animations when showing or hiding connection UI.
        /// <para>✅ Updated on 8/29/2025</para>
        /// </remarks>
        private void UpdateRectangleClip(int rectHeight, Visibility visibility, int clipHeight)
        {
            ConnectionRect.Height = rectHeight;
            AttemptingConnection.Visibility = visibility;

            if (FindName("ConnectionRect") is Rectangle epsonRCRect && epsonRCRect.Clip is RectangleGeometry clipGeometry)
            {
                clipGeometry.Rect = new Rect(0, -10, 180, clipHeight);
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
            WhiteCpuElo.IsEnabled = !userPlaying;
            BlackCpuElo.IsEnabled = !userPlaying;
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
        public Task PiecePositions()
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
                    _whiteKingCol = column;
                }
                else if (image.Name.StartsWith("BlackKing"))
                {
                    _blackKingRow = row;
                    _blackKingCol = column;
                }
            }

            return Task.CompletedTask;
        }

        /// <summary>
        /// Selects the specified theme in a ComboBox if it exists.
        /// </summary>
        /// <param name="comboBox">The ComboBox to update.</param>
        /// <param name="selectedTheme">The theme to select.</param>
        /// <remarks>✅ Updated on 6/11/2025</remarks>
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
            if (IsPaused && ResumeButton.IsEnabled)
            {
                WasResumable = true;
                ResumeButton.IsEnabled = false;
            }
            else if (PlayButton.IsEnabled)
            {
                WasPlayable = true;
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
            if (WasResumable)
            {
                WasResumable = false;
                ResumeButton.IsEnabled = true;
            }
            else if (WasPlayable)
            {
                WasPlayable = false;
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
            if (IsPaused)
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
        private Task AssignRandomElo()
        {
            // Temporarily unsubscribe to prevent triggering logic during changes
            WhiteCpuElo.SelectionChanged -= CheckDropdownSelections;
            BlackCpuElo.SelectionChanged -= CheckDropdownSelections;

            Random rng = new();
            WhiteCpuElo.SelectedIndex = rng.Next(WhiteCpuElo.Items.Count);
            BlackCpuElo.SelectedIndex = rng.Next(BlackCpuElo.Items.Count);

            // Manually update the cached fields since the handler didn’t run
            _selectedWhiteElo = (ComboBoxItem?)WhiteCpuElo.SelectedItem;
            _selectedBlackElo = (ComboBoxItem?)BlackCpuElo.SelectedItem;

            // Re-subscribe after assignments
            WhiteCpuElo.SelectionChanged += CheckDropdownSelections;
            BlackCpuElo.SelectionChanged += CheckDropdownSelections;

            return Task.CompletedTask;
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

        #region Writers & Senders

        /// <summary>
        /// Writes the PGN file header with metadata based on the selected game mode and players.
        /// Includes event name, site, date, round, player names, result placeholder, and ELOs when applicable.
        /// </summary>
        /// <remarks>✅ Updated on 7/18/2025</remarks>
        private void WritePGNFile()
        {
            File.WriteAllText(_pgnFilePath, "[Event \"Chess Match\"]\n");
            File.AppendAllText(_pgnFilePath, "[Site \"Tyler's Chess Program\"]\n");
            File.AppendAllText(_pgnFilePath, $"[Date \"{DateTime.Now:yyyy.MM.dd}\"]\n");
            File.AppendAllText(_pgnFilePath, "[Round \"1\"]\n");

            switch (_gameMode)
            {
                case GameMode.ComVsCom:  // Com Vs. Com
                    File.AppendAllText(_pgnFilePath, $"[White \"Bot\"]\n[Black \"Bot\"]\n[Result \"*\"]\n[WhiteElo \"{_selectedWhiteElo?.Content}\"]\n[BlackElo \"{_selectedBlackElo?.Content}\"]\n\n");
                    break;

                case GameMode.UserVsCom:  // User Vs. Com
                    if (_selectedColor?.Content.ToString() == "White")
                    {
                        File.AppendAllText(_pgnFilePath, $"[White \"User\"]\n[Black \"Bot\"]\n[Result \"*\"]\n[BlackElo \"{_selectedElo?.Content}\"]\n\n");
                    }
                    else
                    {
                        File.AppendAllText(_pgnFilePath, $"[White \"Bot\"]\n[Black \"User\"]\n[Result \"*\"]\n[WhiteElo \"{_selectedElo?.Content}\"]\n\n");
                    }
                    break;

                case GameMode.UserVsUser:  // User Vs. User
                default:
                    File.AppendAllText(_pgnFilePath, $"[White \"User\"]\n[Black \"User\"]\n[Result \"*\"]\n\n");
                    break;
            }
        }

        /// <summary>
        /// Commits the just-played move to all "documentation" channels:
        /// <list type="bullet">
        ///     <item><description>Computes Epson bit codes for pick/place (incl. captures, en passant, promotions, and castling) and </description></item>
        ///     <item><description>Builds the executed UCI move string and queries Stockfish for a check/checkmate modifier.</description></item>
        ///     <item><description>Converts the move to PGN, plays the appropriate sound, and appends to FEN/PGN logs.</description></item>
        ///     <item><description>Updates the in-app move table UI and tracks threefold repetition state.</description></item>
        /// </list>
        /// Resets transient flags at the end (capture/promotion/castling/en passant).
        /// </summary>
        /// <remarks>✅ Updated on 8/20/2025</remarks>
        private async Task DocumentMoveAsync()
        {
            using StreamWriter writer = new(_fenFilePath, append: true);

            // Local helpers
            static int ParseDigitAt(string s, int index) => int.Parse(s[index].ToString());
            static int SquareToBitIndex(int file1to8, int rank1to8) => (file1to8 - 1) + ((rank1to8 - 1) * 8);

            if (string.IsNullOrEmpty(_startPosition))
                goto finalize_and_log;

            // Files/ranks are 1-based in bit mapping
            int file1 = _oldCol + 1;
            int file2 = _newCol + 1;
            int rank1 = 8 - _oldRow;
            int rank2 = 8 - _newRow;

            PickBit1 = SquareToBitIndex(file1, rank1);
            PickBit2 = SquareToBitIndex(file2, rank2);
            PlaceBit1 = SquareToBitIndex(file2, rank2) + 64;

            // Captures / En Passant
            if (Capture || EnPassant)
            {
                // Map taken piece to its off-board place bit
                if (TakenPiece.Contains("Pawn"))
                {
                    int pawnNumber = ParseDigitAt(TakenPiece, 9) - 1;
                    PlaceBit2 = _pawnPlace[pawnNumber];
                }
                else if (TakenPiece.Contains("Knight"))
                {
                    int knightNumber = ParseDigitAt(TakenPiece, 11) - 1;
                    PlaceBit2 = _knightPlace[knightNumber];
                }
                else if (TakenPiece.Contains("Bishop"))
                {
                    int bishopNumber = ParseDigitAt(TakenPiece, 11) - 1;
                    PlaceBit2 = _bishopPlace[bishopNumber];
                }
                else if (TakenPiece.Contains("Rook"))
                {
                    int rookNumber = ParseDigitAt(TakenPiece, 9) - 1;
                    PlaceBit2 = _rookPlace[rookNumber];
                }
                else if (TakenPiece.Contains("Queen"))
                {
                    int queenNumber = ParseDigitAt(TakenPiece, 10) - 1;
                    PlaceBit2 = _queenPlace[queenNumber];
                }

                if (Move == 0)  // White just moved
                {
                    WhiteBits = $"{PickBit1}, {PlaceBit1}";
                    BlackBits = $"{PickBit2}, {PlaceBit2}";
                }
                else  // Black just moved
                {
                    WhiteBits = $"{PickBit2}, {PlaceBit2}";
                    BlackBits = $"{PickBit1}, {PlaceBit1}";
                }

                // En Passant adjustment
                if (EnPassant)
                {
                    if (Move == 0)  // White just moved
                    {
                        WhiteBits = $"{PickBit1}, {PlaceBit1}";
                        BlackBits = $"{PickBit2 - 8}, {PlaceBit2}";
                    }

                    else  // Black just moved
                    {
                        WhiteBits = $"{PickBit2 + 8}, {PlaceBit2}";
                        BlackBits = $"{PickBit1}, {PlaceBit1}";
                    }
                }
            }
            else
            {
                // Non-capture move
                if (Move == 0)
                    WhiteBits = $"{PickBit1}, {PlaceBit1}";
                else
                    BlackBits = $"{PickBit1}, {PlaceBit1}";
            }

            // Promotion
            if (Promoted)
            {
                _endPosition = $"{_endPosition}{PromotionPiece}";

                // Off-board place bit for the pawn that left the board
                {
                    int pawnNumber = ParseDigitAt(PromotedPawn, 9) - 1;
                    PlaceBit2 = _pawnPlace[pawnNumber];
                }

                // Pick bit for the promoted piece type (from off-board location)
                if (PromotionPiece == 'k')
                {
                    int knightNumber = ParseDigitAt(ActivePiece, 11) - 1;
                    PromotedTo = "Q";
                    PickBit3 = _knightPick[knightNumber];
                }
                else if (PromotionPiece == 'b')
                {
                    int bishopNumber = ParseDigitAt(ActivePiece, 11) - 1;
                    PromotedTo = "B";
                    PickBit3 = _bishopPick[bishopNumber];
                }
                else if (PromotionPiece == 'r')
                {
                    int rookNumber = ParseDigitAt(ActivePiece, 9) - 1;
                    PromotedTo = "R";
                    PickBit3 = _rookPick[rookNumber];
                }
                else if (PromotionPiece == 'q')
                {
                    int queenNumber = ParseDigitAt(ActivePiece, 10) - 1;
                    PromotedTo = "Q";
                    PickBit3 = _queenPick[queenNumber];
                }

                if (Capture)
                {
                    // Off-board place bit for the captured (non-pawn) piece
                    if (TakenPiece.Contains("Knight"))
                    {
                        int knightNumber = ParseDigitAt(TakenPiece, 11) - 1;
                        PlaceBit3 = _knightPlace[knightNumber];
                    }
                    else if (TakenPiece.Contains("Bishop"))
                    {
                        int bishopNumber = ParseDigitAt(TakenPiece, 11) - 1;
                        PlaceBit3 = _bishopPlace[bishopNumber];
                    }
                    else if (TakenPiece.Contains("Rook"))
                    {
                        int rookNumber = ParseDigitAt(TakenPiece, 9) - 1;
                        PlaceBit3 = _rookPlace[rookNumber];
                    }
                    else if (TakenPiece.Contains("Queen"))
                    {
                        int queenNumber = ParseDigitAt(TakenPiece, 10) - 1;
                        PlaceBit3 = _queenPlace[queenNumber];
                    }

                    if (Move == 0)  // White just moved
                    {
                        WhiteBits = $"{PickBit1}, {PlaceBit2}, {PickBit3}, {PlaceBit1}";
                        BlackBits = $"{PickBit2}, {PlaceBit3}";
                    }
                    else  // Black just moved
                    {
                        WhiteBits = $"{PickBit2}, {PlaceBit3}";
                        BlackBits = $"{PickBit1}, {PlaceBit2}, {PickBit3}, {PlaceBit1}";
                    }
                }
                else
                {
                    if (Move == 0)  // White just moved
                        WhiteBits = $"{PickBit1}, {PlaceBit2}, {PickBit3}, {PlaceBit1}";
                    else  // Black just moved
                        BlackBits = $"{PickBit1}, {PlaceBit2}, {PickBit3}, {PlaceBit1}";
                }
            }

            // Castling bit patterns
            if (KingCastle)
            {
                if (Move == 0) // White just moved
                {
                    _startPosition = "e1";
                    _endPosition = "g1";
                    WhiteBits = "4, 70, 7, 69";
                }
                else            // Black just moved
                {
                    _startPosition = "e8";
                    _endPosition = "g8";
                    BlackBits = "60, 126, 63, 125";
                }
            }
            else if (QueenCastle)
            {
                if (Move == 0) // White just moved
                {
                    _startPosition = "e1";
                    _endPosition = "c1";
                    WhiteBits = "4, 66, 0, 67";
                }
                else            // Black just moved
                {
                    _startPosition = "e8";
                    _endPosition = "c8";
                    BlackBits = "60, 122, 56, 123";
                }
            }

            // Compose move, PGN, sound
            _executedMove = $"{_startPosition}{_endPosition}";
            string checkModifier = await StockfishCheckAnalysisAsync(Fen, _stockfishPath!);
            _pgnMove = UCItoPGNConverter.Convert(PreviousFen, _executedMove, KingCastle, QueenCastle, EnPassant, Promoted, PromotedTo, checkModifier);


            bool isComputer = _gameMode == GameMode.ComVsCom ? true : _gameMode == GameMode.UserVsCom ? (_selectedColor.Content.ToString() == "White" && Move == 0 || _selectedColor.Content.ToString() == "Black" && Move == 1 ? true : false) : false;
            if (_pieceSounds)
            {
                string sound =
                    _pgnMove.Contains('#') ? "GameEnd" :
                    _pgnMove.Contains('+') ? "PieceCheck" :
                    _pgnMove.Contains('=') ? "PiecePromote" :
                    _pgnMove.Contains('x') ? "PieceCapture" :
                    _pgnMove.Contains('-') ? "PieceCastle" :
                    isComputer ? "PieceOpponent" : "PieceMove";

                PlaySound(sound);
            }

        finalize_and_log:

            // Reset transient flags (exact same set/order)
            Capture = false;
            _capturedPiece = null;
            _selectedPiece = null;
            EnPassantCreated = false;
            EnPassant = false;
            Promoted = false;
            KingCastle = false;
            QueenCastle = false;

            // Append line to FEN log file
            writer.WriteLine($"\nMove Played: {_pgnMove}   Resulting Position: {Fen}");

            // Track threefold repetition
            string[] fenParts = Fen.Split(' ');
            string currentFEN = $"{fenParts[0]} {fenParts[1]};";
            GameFens.Add(currentFEN);

            var fenCounts = GameFens
                .Select(f => f[..f.LastIndexOf(';')])
                .GroupBy(f => f)
                .ToDictionary(g => g.Key, g => g.Count());

            if (fenCounts.Any(p => p.Value >= 3))
                ThreefoldRepetition = true;

            // Update the evaluation interface move table (unchanged)
            System.Windows.Media.Color borderColor = (System.Windows.Media.Color)ColorConverter.ConvertFromString("#FFD0D0D0");
            SolidColorBrush borderBrush = new(borderColor);
            FontFamily fontFamily = new("Sans Serif Collection");
            FontWeight fontWeight = FontWeights.Bold;

            RowDefinition newRowDefinition = new() { Height = new GridLength(30) };

            Border newBorder = new()
            {
                BorderThickness = new Thickness(0.5),
                BorderBrush = borderBrush,
            };

            TextBlock newMoveNumber = new()
            {
                Text = $"{Fullmove}.",
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                Foreground = borderBrush,
                FontFamily = fontFamily,
                FontSize = 14,
            };

            TextBlock newWhiteMove = new()
            {
                Text = _pgnMove,
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
                Text = _pgnMove,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                Foreground = borderBrush,
                FontFamily = fontFamily,
                FontSize = 14,
                FontWeight = fontWeight,
                Padding = new Thickness(10, 0, 0, 0),
            };

            if (Move == 0)
            {
                Grid.SetRow(newBorder, Fullmove);
                Grid.SetColumnSpan(newBorder, 3);
                Grid.SetRow(newMoveNumber, Fullmove);
                Grid.SetColumn(newMoveNumber, 0);
                Grid.SetRow(newWhiteMove, Fullmove);
                Grid.SetColumn(newWhiteMove, 1);

                Moves.RowDefinitions.Add(newRowDefinition);
                Moves.Children.Add(newBorder);
                Moves.Children.Add(newMoveNumber);
                Moves.Children.Add(newWhiteMove);

                File.AppendAllText(_pgnFilePath, $"{Fullmove}. {_pgnMove} ");
            }
            else
            {
                Grid.SetRow(newBlackMove, Fullmove - 1);
                Grid.SetColumn(newBlackMove, 2);

                Moves.Children.Add(newBlackMove);

                File.AppendAllText(_pgnFilePath, $"{_pgnMove} ");
            }
        }

        /// <summary>
        /// Sends the accumulated robot command bits to the appropriate robots,
        /// sequencing by which side just moved.
        /// </summary>
        /// <remarks>
        /// <list type="bullet">
        ///     <item><description>When White just moved (<c><see cref="Move"/> == 0</c> after <see cref="FinalizeMoveAsync"/>), sends Black’s bits first (if any), then White’s.</description></item>
        ///     <item><description>When Black just moved (<c><see cref="Move"/> == 1</c>), sends White’s bits first (if any), then Black’s.</description></item>
        ///     <item><description>Shows a “move in progress” popup before and after transmission.</description></item>
        /// </list>
        /// </remarks>
        /// <para>✅ Updated on 8/29/2025</para>
        /// <returns></returns>
        private async Task SendRobotBitsAsync()
        {
            ShowMoveInProgressPopup(true);

            // Since FinalizeMoveAsync flips Move by this state, now Move == 0 is if white is moving
            if (Move == 0)
            {
                if (!string.IsNullOrEmpty(BlackBits))
                    await _blackRobot.SendDataAsync(BlackBits);

                await _whiteRobot.SendDataAsync(WhiteBits);
            }
            else
            {
                if (!string.IsNullOrEmpty(WhiteBits))
                    await _whiteRobot.SendDataAsync(WhiteBits);

                await _blackRobot.SendDataAsync(BlackBits);
            }

            ShowMoveInProgressPopup(false);
        }

        /// <summary>
        /// Appends a new set of robot command bits to the existing history string.
        /// </summary>
        /// <param name="history">The current history string (may be empty).</param>
        /// <param name="bits">The new command bits to append.</param>
        /// <remarks>✅ Updated on 7/14/2025</remarks>
        /// <returns>
        /// The updated history string with <paramref name="bits"/> appended on a new line,
        /// or <see cref="string.Empty"/> if <paramref name="bits"/> was null or empty.
        /// </returns>
        private static string? AppendToHistory(string history, string bits)
        {
            if (string.IsNullOrEmpty(bits)) return string.Empty;
            if (string.IsNullOrEmpty(history)) history = bits;
            else history += "\n" + bits;

            return history;
        }

        #endregion

        #region Game Restarting

        

        #endregion


        /// <summary>
        /// Attempts communication and updates Epson RC+ connection setting in the "Preferences" file.
        /// </summary>
        /// <param name="checkBox">The sender object triggering the connection attempt.</param>
        /// <returns>An asynchronous task representing the connection attempt.</returns>
        private async Task EpsonConnectAsync(object sender)  // ✅
        {
            if (sender is not CheckBox checkBox)
                return;

            DisableEpsonElements();
            if (!GlobalState.WhiteEpsonConnected) { SetEpsonStatusLight(ChessColor.White, Brushes.Yellow); }
            if (!GlobalState.BlackEpsonConnected) { SetEpsonStatusLight(ChessColor.Black, Brushes.Yellow); }

            // Preserve resume/play state
            StorePlayState();

            // Toggle RobotComm state and attempt communication
            _robotMotion = !_robotMotion;

            if (checkBox.IsChecked.HasValue && _robotMotion)  // If user is trying to connect to Epson robots
            {
                // Begin animated feedback
                UpdateRectangleClip(65, Visibility.Visible, 75);

                // Attempt to connect
                if (!GlobalState.WhiteEpsonConnected) { await _whiteRobot.ConnectAsync(); }
                if (!GlobalState.BlackEpsonConnected) { await _blackRobot.ConnectAsync(); }

                if (GlobalState.WhiteEpsonConnected && GlobalState.BlackEpsonConnected)  // Successfully connected to both Epson robots
                {
                    _preferences.EpsonMotion = _robotMotion = true;
                    PreferencesManager.Save(_preferences);

                    checkBox.IsChecked = true;
                    SetEpsonStatusLight(ChessColor.White, Brushes.Green);
                    SetEpsonStatusLight(ChessColor.Black, Brushes.Green);

                    UpdateRectangleClip(50, Visibility.Collapsed, 60);

                    if (!BoardSet)
                    {
                        ShowSetupPopup(true);
                        await SetupBoard();
                        ShowSetupPopup(false);
                    }

                    // Process any prior moves before proceeding
                    if (!string.IsNullOrEmpty(PrevWhiteBits))
                    {
                        ShowMoveInProgressPopup(true);

                        string[] whiteBitLines = PrevWhiteBits.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                        foreach (var line in whiteBitLines)
                            await _whiteRobot.SendDataAsync(line);

                        if (!string.IsNullOrEmpty(PrevBlackBits))
                        {
                            string[] blackBitLines = PrevWhiteBits.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
                            foreach (var line in blackBitLines)
                                await _blackRobot.SendDataAsync(line);
                        }

                        ShowMoveInProgressPopup(false);
                    }
                }
                else
                {
                    _preferences.EpsonMotion = _robotMotion = false;
                    PreferencesManager.Save(_preferences);

                    checkBox.IsChecked = false;
                    if (!GlobalState.WhiteEpsonConnected) { SetEpsonStatusLight(ChessColor.White, Brushes.Red); }
                    if (!GlobalState.BlackEpsonConnected) { SetEpsonStatusLight(ChessColor.Black, Brushes.Red); }

                    UpdateRectangleClip(50, Visibility.Collapsed, 60);
                }
            }
            else
            {
                _preferences.EpsonMotion = _robotMotion = false;
                PreferencesManager.Save(_preferences);

                checkBox.IsChecked = false;
                SetEpsonStatusLight(Chess_Project.ChessColor.White, Brushes.Red);
                SetEpsonStatusLight(Chess_Project.ChessColor.Black, Brushes.Red);

                if ((int)_gameMode != 0)  // Cleanup process if necessary
                {
                    ShowCleanupPopup(true);
                    await ClearBoard();
                    ShowCleanupPopup(false);
                }

                _whiteRobot.Disconnect();
                _blackRobot.Disconnect();
            }


            // Restore UI
            EnableEpsonElements();

            EpsonMotion.IsEnabled = true;
        }

        private async Task CognexConnectAsync(object sender)
        {
            // Ensure sender is a CheckBox
            if (sender is not CheckBox checkBox)
            {
                if (!GlobalState.WhiteCognexConnected) { SetCognexStatusLight(Chess_Project.ChessColor.White, Brushes.Red); }
                if (!GlobalState.BlackCognexConnected) { SetCognexStatusLight(Chess_Project.ChessColor.Black, Brushes.Red); }
            }
            else if (checkBox.IsChecked.HasValue && _cameraVision)
            {
                // Begin animated feedback
                UpdateRectangleClip(65, Visibility.Visible, 75);

                // Attempt to connect
                if (!GlobalState.WhiteCognexConnected) { await _whiteCognex.ConnectAsync(); }
                if (!GlobalState.BlackCognexConnected) { await _blackCognex.ConnectAsync(); }

                if (GlobalState.WhiteCognexConnected && GlobalState.BlackCognexConnected)  // Successfully connected to both Cognex cameras
                {
                    _preferences.CognexVision = _cameraVision = true;
                    PreferencesManager.Save(_preferences);

                    checkBox.IsChecked = true;
                    SetCognexStatusLight(Chess_Project.ChessColor.White, Brushes.Green);
                    SetCognexStatusLight(Chess_Project.ChessColor.Black, Brushes.Green);

                    UpdateRectangleClip(50, Visibility.Collapsed, 60);
                }
                else
                {
                    _preferences.CognexVision = _cameraVision = false;
                    PreferencesManager.Save(_preferences);

                    checkBox.IsChecked = false;
                    if (!GlobalState.WhiteCognexConnected) { SetCognexStatusLight(Chess_Project.ChessColor.White, Brushes.Red); }
                    if (!GlobalState.BlackCognexConnected) { SetCognexStatusLight(Chess_Project.ChessColor.Black, Brushes.Red); }

                    UpdateRectangleClip(50, Visibility.Collapsed, 60);
                }
            }
            else
            {
                _preferences.CognexVision = _cameraVision = false;
                PreferencesManager.Save(_preferences);

                checkBox.IsChecked = false;
                SetCognexStatusLight(Chess_Project.ChessColor.White, Brushes.Red);
                SetCognexStatusLight(Chess_Project.ChessColor.Black, Brushes.Red);

                _whiteCognex.Disconnect();
                _blackCognex.Disconnect();
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

            WhiteAdvantage.Text = DisplayedAdvantage;
            BlackAdvantage.Text = DisplayedAdvantage;

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
            bool whiteIsWinning = (_flip == 0) ? (QuantifiedEvaluation <= 10) : (QuantifiedEvaluation > 10);

            if (whiteIsWinning)
            {
                WhiteAdvantage.Visibility = Visibility.Visible;
                BlackAdvantage.Visibility = Visibility.Collapsed;
                WhiteAdvantage.Foreground = new SolidColorBrush(_flip == 0 ? Colors.Black : (System.Windows.Media.Color)ColorConverter.ConvertFromString("#FFD0D0D0"));
            }
            else
            {
                WhiteAdvantage.Visibility = Visibility.Collapsed;
                BlackAdvantage.Visibility = Visibility.Visible;
                BlackAdvantage.Foreground = new SolidColorBrush(_flip == 0 ? (System.Windows.Media.Color)ColorConverter.ConvertFromString("#FFD0D0D0") : Colors.Black);
            }

            // Set bar colors based on the flipped evaluation
            EvalBar.Fill = new SolidColorBrush(_flip == 0 ? (System.Windows.Media.Color)ColorConverter.ConvertFromString("#FFD0D0D0") : Colors.Black);
            AdvantageGauge.Fill = new SolidColorBrush(_flip == 0 ? Colors.Black : (System.Windows.Media.Color)ColorConverter.ConvertFromString("#FFD0D0D0"));
            AdvantageGauge.Clip = clipGeometry;

            // Compute animation height, flipping evaluation for the bar height
            double oldHeight = double.IsNaN(AdvantageGauge.Height) ? EvalBar.Height / 2 : AdvantageGauge.Height;
            double newHeight = (EvalBar.Height / 20) * (_flip == 0 ? QuantifiedEvaluation : (20 - QuantifiedEvaluation));

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
        /// 
        /// </summary>
        public async Task CheckmateVerifierAsync()
        {
            using StockfishCall stockfishResponse = new(_stockfishPath!);
            string stockfishFEN = await Task.Run(() => stockfishResponse.GetStockfishResponse(Fen));
            string[] lines = [.. stockfishFEN.Split('\n').Skip(2)];
            string[] infoLines = [.. lines.Where(line => line.TrimStart().StartsWith("info"))];
            string accurateEvaluationLine = infoLines.LastOrDefault() ?? "Most accurate evaluation line not found";
            string[] accurateEvaluation = accurateEvaluationLine.Split(' ');

            if (accurateEvaluation[5].StartsWith('0'))   // If game has been won
            {
                DisplayedAdvantage = "1-0";

                if (Move == 0)
                {
                    QuantifiedEvaluation = 0;
                }
                else
                {
                    QuantifiedEvaluation = 20;
                }
            }
            else if (accurateEvaluation[8].StartsWith("mate") && accurateEvaluation.Length > 9)   // If there is a mating sequence present
            {
                if (accurateEvaluation[9].StartsWith('-'))
                {
                    DisplayedAdvantage = $"M{accurateEvaluation[9][1..]}";
                }
                else
                {
                    DisplayedAdvantage = $"M{accurateEvaluation[9]}";
                }

                if (Move == 0)   // White just moved
                {
                    if (accurateEvaluation[9].StartsWith('-'))
                    {
                        QuantifiedEvaluation = 0;
                    }
                    else
                    {
                        QuantifiedEvaluation = 20;
                    }
                }
                else   // Black just moved
                {
                    if (accurateEvaluation[9].StartsWith('-'))
                    {
                        QuantifiedEvaluation = 20;
                    }
                    else
                    {
                        QuantifiedEvaluation = 0;
                    }
                }
            }
            else
            {
                QuantifiedEvaluation = double.Parse(accurateEvaluation[9].ToString()) / 100;
                DisplayedAdvantage = Math.Abs(QuantifiedEvaluation).ToString("0.0");

                if (Move == 0)   // White just moved
                {
                    QuantifiedEvaluation = 10 + QuantifiedEvaluation;

                    if (QuantifiedEvaluation < 1)
                    {
                        QuantifiedEvaluation = 1;
                    }
                    else if (QuantifiedEvaluation > 19)
                    {
                        QuantifiedEvaluation = 19;
                    }
                }
                else   // Black just moved
                {
                    QuantifiedEvaluation = 10 - QuantifiedEvaluation;

                    if (QuantifiedEvaluation < 1)
                    {
                        QuantifiedEvaluation = 1;
                    }
                    else if (QuantifiedEvaluation > 19)
                    {
                        QuantifiedEvaluation = 19;
                    }
                }
            }

            string bestMoveLine = lines.FirstOrDefault(line => line.TrimStart().StartsWith("bestmove")) ?? "Bestmove line not found";
            string[] parts = bestMoveLine.Split(' ');

            if (parts[1].StartsWith("(none)"))   // If there are no best moves then game is over
            {
                EndGame = true;

                string outcomeLine = lines[^2];
                string[] partsOutcome = outcomeLine.Split(' ');

                if (infoLines.Any(line => line.Contains("mate")))  // If Stockfish calculates a checkmate
                {
                    if (Move == 0)   // Checkmate for white
                    {
                        _gameOver = new();
                        _gameOver.WinnerText.Text = $"White wins by checkmate in {Fullmove} moves!";
                        _gameOver.Owner = this;
                        _gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        _gameOver.Show();
                        Debug.WriteLine("White wins by checkmate");
                    }
                    else   // Checkmate for black
                    {
                        _gameOver = new();
                        _gameOver.WinnerText.Text = $"Black wins by checkmate in {Fullmove - 1} moves!";
                        _gameOver.Owner = this;
                        _gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        _gameOver.Show();
                        Debug.WriteLine("Black wins by checkmate");
                    }
                }
                else if (infoLines.Any(line => line.Contains("cp")))   // If Stockfish calculates a stalemate
                {
                    DisplayedAdvantage = ".5 - .5";
                    QuantifiedEvaluation = 10;

                    if (Move == 0)
                    {
                        _gameOver = new();
                        _gameOver.WinnerText.Text = $"Game ends in a stalemate after {Fullmove} moves";
                        _gameOver.Owner = this;
                        _gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        _gameOver.Show();
                        Debug.WriteLine("Stalemate");
                    }

                    else
                    {
                        _gameOver = new();
                        _gameOver.WinnerText.Text = $"Game ends in a stalemate after {Fullmove - 1} moves";
                        _gameOver.Owner = this;
                        _gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                        _gameOver.Show();
                        Debug.WriteLine("Stalemate");
                    }
                }
            }
            else if (Halfmove == 100)   // If fifty-move rule occurs
            {
                EndGame = true;

                DisplayedAdvantage = ".5 - .5";
                QuantifiedEvaluation = 10;

                _gameOver = new();
                _gameOver.WinnerText.Text = "The game is a draw due to the fifty-move rule,\n" +
                                           "as there have been no pawn movements\n" +
                                           "or captures in the last fifty full turns.";
                _gameOver.Owner = this;
                _gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _gameOver.Show();
                Debug.WriteLine("Draw due to fifty-move rule");
            }
            else if (ThreefoldRepetition)   // If threefold repetition occurs
            {
                EndGame = true;

                DisplayedAdvantage = ".5 - .5";
                QuantifiedEvaluation = 10;

                _gameOver = new();
                _gameOver.WinnerText.Text = "The game is a draw due to threemove repetition,\n" +
                                           "as the same position was reached three\n" +
                                           "times with the same color to move each time.";
                _gameOver.Owner = this;
                _gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                _gameOver.Show();
                Debug.WriteLine("Draw due to threefold repetition");
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
                    EndGame = true;

                    DisplayedAdvantage = ".5 - .5";
                    QuantifiedEvaluation = 10;

                    _gameOver = new();
                    _gameOver.WinnerText.Text = "The game is a draw due to insufficient material,\n" +
                                                "as neither side has enough remaining pieces\n" +
                                                "on the board to force a checkmate.";
                    _gameOver.Owner = this;
                    _gameOver.WindowStartupLocation = WindowStartupLocation.CenterOwner;
                    _gameOver.Show();
                    Debug.WriteLine("Draw due to insufficient material");
                    return;
                }
            }
        }


        // Sets up pieces for game
        public async Task SetupBoard()
        {
            BoardSet = true;

            EnableImagesWithTag("WhitePiece", false);
            EnableImagesWithTag("BlackPiece", false);

            if (Fullmove == 1 && Move == 1)   // Game just started
            {
                for (int i = 1; i < 9; i++)   // Pawn setup
                {
                    PickBit1 = _pawnPick[i - 1];
                    PlaceBit1 = _whitePawnOrigin[i - 1];

                    if (i == 1)   // If this is first entry
                    {
                        WhiteBits = $"{PickBit1}, {PlaceBit1}";
                    }

                    else
                    {
                        WhiteBits += $", {PickBit1}, {PlaceBit1}";
                    }

                    PickBit1 = _pawnPick[i - 1];
                    PlaceBit1 = _blackPawnOrigin[i - 1];

                    if (i == 1)   // If this is first entry
                    {
                        BlackBits = $"{PickBit1}, {PlaceBit1}";
                    }

                    else
                    {
                        BlackBits += $", {PickBit1}, {PlaceBit1}";
                    }
                }

                for (int i = 1; i < 3; i++)   // Rook, knight, and bishop setup
                {
                    PickBit1 = _rookPick[i - 1];
                    PlaceBit1 = _whiteRookOrigin[i - 1];
                    WhiteBits += $", {PickBit1}, {PlaceBit1}";

                    PlaceBit1 = _blackRookOrigin[i - 1];
                    BlackBits += $", {PickBit1}, {PlaceBit1}";

                    PickBit1 = _knightPick[i - 1];
                    PlaceBit1 = _whiteKnightOrigin[i - 1];
                    WhiteBits += $", {PickBit1}, {PlaceBit1}";

                    PlaceBit1 = _blackKnightOrigin[i - 1];
                    BlackBits += $", {PickBit1}, {PlaceBit1}";

                    PickBit1 = _bishopPick[i - 1];
                    PlaceBit1 = _whiteBishopOrigin[i - 1];
                    WhiteBits += $", {PickBit1}, {PlaceBit1}";

                    PlaceBit1 = _blackBishopOrigin[i - 1];
                    BlackBits += $", {PickBit1}, {PlaceBit1}";
                }

                PickBit1 = _queenPick[0];
                PlaceBit1 = _whiteQueenOrigin;
                WhiteBits += $", {PickBit1}, {PlaceBit1}";

                PlaceBit1 = _blackQueenOrigin;
                BlackBits += $", {PickBit1}, {PlaceBit1}";

                PickBit1 = _kingPick;
                PlaceBit1 = _whiteKingOrigin;
                WhiteBits += $", {PickBit1}, {PlaceBit1}";

                PlaceBit1 = _blackKingOrigin;
                BlackBits += $", {PickBit1}, {PlaceBit1}";
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

                                PlaceBit1 = File1 - 1 + ((Rank1 - 1) * 8) + 64;

                                if (image.Name.Contains("Pawn"))   // Pawn was found
                                {
                                    char pawnNo = foundPiece[9];
                                    int pawnNumber = int.Parse(pawnNo.ToString()) - 1;

                                    PickBit1 = _pawnPick[pawnNumber];
                                }

                                else if (image.Name.Contains("Rook"))   // Rook was found
                                {
                                    char rookNo = foundPiece[9];
                                    int rookNumber = int.Parse(rookNo.ToString()) - 1;

                                    PickBit1 = _rookPick[rookNumber];
                                }

                                else if (image.Name.Contains("Knight"))   // Knight was found
                                {
                                    char knightNo = foundPiece[11];
                                    int knightNumber = int.Parse(knightNo.ToString()) - 1;

                                    PickBit1 = _knightPick[knightNumber];
                                }

                                else if (image.Name.Contains("Bishop"))   // Bishop was found
                                {
                                    char bishopNo = foundPiece[11];
                                    int bishopNumber = int.Parse(bishopNo.ToString()) - 1;

                                    PickBit1 = _bishopPick[bishopNumber];
                                }

                                else if (image.Name.Contains("Queen"))   // Queen was found
                                {
                                    char queenNo = foundPiece[10];
                                    int queenNumber = int.Parse(queenNo.ToString()) - 1;

                                    PickBit1 = _queenPick[queenNumber];
                                }

                                else   // King was found
                                {
                                    PickBit1 = _kingPick;
                                }

                                if (image.Name.StartsWith("White"))   // White piece was found
                                {
                                    if (string.IsNullOrEmpty(WhiteBits))
                                    {
                                        WhiteBits = $"{PickBit1}, {PlaceBit1}";
                                    }

                                    else
                                    {
                                        WhiteBits += $", {PickBit1}, {PlaceBit1}";
                                    }
                                }

                                else   // Black piece was found
                                {
                                    if (string.IsNullOrEmpty(BlackBits))
                                    {
                                        BlackBits = $"{PickBit1}, {PlaceBit1}";
                                    }

                                    else
                                    {
                                        BlackBits += $", {PickBit1}, {PlaceBit1}";
                                    }
                                }                      
                            }
                        }
                    }
                }
            }

            await _whiteRobot.HighSpeedAsync();
            await _blackRobot.HighSpeedAsync();

            // Kick both off concurrently
            var whiteTask = _whiteRobot.SendDataAsync(WhiteBits);
            var blackTask = _blackRobot.SendDataAsync(BlackBits);

            // Wait until BOTH complete (or throw)
            await Task.WhenAll(whiteTask, blackTask);

            await _whiteRobot.LowSpeedAsync();
            await _blackRobot.LowSpeedAsync();

            // Safe to clear/continue only after both finished
            WhiteBits = "";
            BlackBits = "";

            EnableImagesWithTag("WhitePiece", true);
            EnableImagesWithTag("BlackPiece", true);
        }



        // Resets Epson chess board
        public async Task ClearBoard()
        {
            BoardSet = false;

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

                            PickBit1 = File1 - 1 + ((Rank1 - 1) * 8);

                            if (image.Name.Contains("Pawn"))   // Pawn was found
                            {
                                char pawnNo = foundPiece[9];
                                int pawnNumber = int.Parse(pawnNo.ToString()) - 1;

                                PlaceBit1 = _pawnPlace[pawnNumber];
                            }

                            else if (image.Name.Contains("Rook"))   // Rook was found
                            {
                                char rookNo = foundPiece[9];
                                int rookNumber = int.Parse(rookNo.ToString()) - 1;

                                PlaceBit1 = _rookPlace[rookNumber];
                            }

                            else if (image.Name.Contains("Knight"))   // Knight was found
                            {
                                char knightNo = foundPiece[11];
                                int knightNumber = int.Parse(knightNo.ToString()) - 1;

                                PlaceBit1 = _knightPlace[knightNumber];
                            }

                            else if (image.Name.Contains("Bishop"))   // Bishop was found
                            {
                                char bishopNo = foundPiece[11];
                                int bishopNumber = int.Parse(bishopNo.ToString()) - 1;

                                PlaceBit1 = _bishopPlace[bishopNumber];
                            }

                            else if (image.Name.Contains("Queen"))   // Queen was found
                            {
                                char queenNo = foundPiece[10];
                                int queenNumber = int.Parse(queenNo.ToString()) - 1;

                                PlaceBit1 = _queenPlace[queenNumber];
                            }

                            else   // King was found
                            {
                                PlaceBit1 = _kingPlace;
                            }

                            if (image.Name.StartsWith("White"))   // White piece was found
                            {
                                if (string.IsNullOrEmpty(WhiteBits))
                                {
                                    WhiteBits = $"{PickBit1}, {PlaceBit1}";
                                }

                                else
                                {
                                    WhiteBits += $", {PickBit1}, {PlaceBit1}";
                                }
                            }

                            else   // Black piece was found
                            {
                                if (string.IsNullOrEmpty(BlackBits))
                                {
                                    BlackBits = $"{PickBit1}, {PlaceBit1}";
                                }

                                else
                                {
                                    BlackBits += $", {PickBit1}, {PlaceBit1}";
                                }
                            }                                          
                        }
                    }
                }
            }

            await _whiteRobot.HighSpeedAsync();
            await _blackRobot.HighSpeedAsync();

            await _whiteRobot.SendDataAsync(WhiteBits);
            await _blackRobot.SendDataAsync(BlackBits);

            WhiteBits = "";
            BlackBits = "";
        }

        private async void QuitGame(object sender, EventArgs e)
        {
            // Ensure the game doesn't auto-resume after inactivity timeout
            _inactivityTimer.Stop();

            // Hide quit game button
            QuitButton.Visibility = Visibility.Collapsed;
            QuitButton.IsEnabled = false;

            if (_robotMotion)
            {
                ShowCleanupPopup(true);
                await ClearBoard();
                ShowCleanupPopup(false);
            }

            NewGameFunnel();
        }

        // Where ClearBoard (from CentralMoveHub) and QuitGame funnel together
        private void NewGameFunnel()
        {
            if (!Dispatcher.CheckAccess())
                Dispatcher.Invoke(() => _gameOver?.Close());
            else
                _gameOver?.Close();

            ResumeButton.Visibility = Visibility.Collapsed;
            ResumeButton.IsEnabled = false;
            PauseButton.IsEnabled = false;

            PlayButton.Visibility = Visibility.Visible;
            PlayButton.IsEnabled = false;

            Game_Start.Visibility = Visibility.Visible;
            Game_Start.IsEnabled = true;
            Chess_Board.IsHitTestVisible = false;

            UvCorUvU.Visibility = Visibility.Visible;
            UvCorUvU.IsEnabled = true;
            CvC.Visibility = Visibility.Collapsed;
            CvC.IsEnabled = false;
            Play_Type.SelectedIndex = (int)GameMode.Blank;
            Elo.SelectedItem = null;
            Color.SelectedItem = null;

            ConfigureNewGame();
        }

        private void ConfigureNewGame()
        {
            EraseAnnotations();
            MoveCallout(9, 9, 9, 9);
            ReinstantiateBoard();
            ResetMoveTable();

            Session.ResetGame();
            UpdateEvalBar();

            _inactivityTimer.Start();
        }

        public void ReinstantiateBoard()
        {
            if (_initialPieces is null) return;

            // Remove any piece images currently on the board
            var toRemove = Chess_Board.Children
                .OfType<Image>()
                .Where(i => Equals(i.Tag, "WhitePiece") || Equals(i.Tag, "BlackPiece"))
                .ToList();

            foreach (var img in toRemove)
                Chess_Board.Children.Remove(img);

            // Re-add and restore each original piece
            foreach (var kv in _initialPieces)
            {
                var p = kv.Value;
                var img = p.Img;

                if (!Chess_Board.Children.Contains(img))
                    Chess_Board.Children.Add(img);

                // Restore original identity and placement
                img.Name = p.Name;
                img.Tag = p.Tag;
                img.IsEnabled = p.Enabled;
                Grid.SetRow(img, p.Row);
                Grid.SetColumn(img, p.Col);
                Panel.SetZIndex(img, p.Z);
                img.Visibility = Visibility.Visible;

                // Rewire handlers based on (restored) name
                AttachClickHandlerByName(img);

                // Refresh the bitmap from your theme/pieces folder
                LoadImage(img, new RoutedEventArgs());
            }

            // Reset your session counters back to “starting set”
            Session.NumWR = Session.NumWN = Session.NumWB = 2;
            Session.NumBR = Session.NumBN = Session.NumBB = 2;
            Session.NumWQ = Session.NumBQ = 1;
            // Pawns are handled by name/placement; your other counters (if any) can be reset here too.
        }

        private void AttachClickHandlerByName(Image img)
        {
            // Remove all piece handlers first
            img.MouseUp -= ChessPawn_Click;
            img.MouseUp -= ChessRook_Click;
            img.MouseUp -= ChessKnight_Click;
            img.MouseUp -= ChessBishop_Click;
            img.MouseUp -= ChessQueen_Click;
            img.MouseUp -= ChessKing_Click;

            string n = img.Name;
            if (n.Contains("Pawn")) img.MouseUp += ChessPawn_Click;
            else if (n.Contains("Rook")) img.MouseUp += ChessRook_Click;
            else if (n.Contains("Knight")) img.MouseUp += ChessKnight_Click;
            else if (n.Contains("Bishop")) img.MouseUp += ChessBishop_Click;
            else if (n.Contains("Queen")) img.MouseUp += ChessQueen_Click;
            else if (n.Contains("King")) img.MouseUp += ChessKing_Click;
        }

        private const int MovesHeaderRows = 1;

        private void ResetMoveTable()
        {
            // Remove dynamic children
            for (int i = Moves.Children.Count - 1; i >= 0; i--)
            {
                var el = Moves.Children[i];
                if (Grid.GetRow(el) >= MovesHeaderRows)
                    Moves.Children.RemoveAt(i);
            }

            // Trim extra body rows we added during the game
            while (Moves.RowDefinitions.Count > MovesHeaderRows)
                Moves.RowDefinitions.RemoveAt(Moves.RowDefinitions.Count - 1);
        }
    }
}
// The #1 Boyfriend EVER made this... I am so proud of him and he is one of the smartest people I know! I love him more than I can say. <3