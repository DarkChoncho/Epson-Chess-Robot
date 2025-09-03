using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace Chess_Project
{
    public sealed class GameSession : INotifyPropertyChanged
    {
        #region Board/Position Collections

        private List<Tuple<int, int>> _imageCoordinates = [];
        private List<Tuple<int, int>> _enPassantSquare = [];
        private List<string> _gameFens = [];

        public List<Tuple<int, int>> ImageCoordinates { get => _imageCoordinates; set => Set(ref _imageCoordinates, value); }
        public List<Tuple<int, int>> EnPassantSquare { get => _enPassantSquare; set => Set(ref _enPassantSquare, value); }
        public List<string> GameFens { get => _gameFens; set => Set(ref _gameFens, value); }

        #endregion

        #region Move & Notation Metadata

        private string? _promotedPawn = null;
        private string? _promotedTo = null;
        private string? _activePiece = null;
        private string? _takenPiece = null;
        private string? _fen = null;
        private string? _previousFen = null;

        public string? PromotedPawn { get => _promotedPawn; set => Set(ref _promotedPawn, value); }
        public string? PromotedTo { get => _promotedTo; set => Set(ref _promotedTo, value); }
        public string? ActivePiece { get => _activePiece; set => Set(ref _activePiece, value); }
        public string? TakenPiece { get => _takenPiece; set => Set(ref _takenPiece, value); }
        public string? Fen { get => _fen; set => Set(ref _fen, value); }
        public string? PreviousFen { get => _previousFen; set => Set(ref _previousFen, value); }

        #endregion

        #region Epson RC+ Bit Signals (Pick/Place)

        private int? _pickBit1 = null;
        private int? _pickBit2 = null;
        private int? _pickBit3 = null;
        private int? _placeBit1 = null;
        private int? _placeBit2 = null;
        private int? _placeBit3 = null;

        private string? _whiteBits = null;
        private string? _blackBits = null;
        private string? _prevWhiteBits = null;
        private string? _prevBlackBits = null;

        private List<int> _completedWhiteBits = [];
        private List<int> _completedBlackBits = [];

        public int? PickBit1 { get => _pickBit1; set => Set(ref _pickBit1, value); }
        public int? PickBit2 { get => _pickBit2; set => Set(ref _pickBit2, value); }
        public int? PickBit3 { get => _pickBit3; set => Set(ref _pickBit3, value); }
        public int? PlaceBit1 { get => _placeBit1; set => Set(ref _placeBit1, value); }
        public int? PlaceBit2 { get => _placeBit2; set => Set(ref _placeBit2, value); }
        public int? PlaceBit3 { get => _placeBit3; set => Set(ref _placeBit3, value); }

        public string? WhiteBits { get => _whiteBits; set => Set(ref _whiteBits, value); }
        public string? BlackBits { get => _blackBits; set => Set(ref _blackBits, value); }
        public string? PrevWhiteBits { get => _prevWhiteBits; set => Set(ref _prevWhiteBits, value); }
        public string? PrevBlackBits { get => _prevBlackBits; set => Set(ref _prevBlackBits, value); }

        public List<int> CompletedWhiteBits { get => _completedWhiteBits; set => Set(ref _completedWhiteBits, value); }
        public List<int> CompletedBlackBits { get => _completedBlackBits; set => Set(ref _completedBlackBits, value); }

        #endregion

        #region Castling Flags

        private int _cWK = 0;
        private int _cWR1 = 0;
        private int _cWR2 = 0;
        private int _cBK = 0;
        private int _cBR1 = 0;
        private int _cBR2 = 0;
        private bool _kingCastle = false;
        private bool _queenCastle = false;

        public int CWK { get => _cWK; set => Set(ref _cWK, value); }
        public int CWR1 { get => _cWR1; set => Set(ref _cWR1, value); }
        public int CWR2 { get => _cWR2; set => Set(ref _cWR2, value); }
        public int CBK { get => _cBK; set => Set(ref _cBK, value); }
        public int CBR1 { get => _cBR1; set => Set(ref _cBR1, value); }
        public int CBR2 { get => _cBR2; set => Set(ref _cBR2, value); }
        public bool KingCastle { get => _kingCastle; set => Set(ref _kingCastle, value); }
        public bool QueenCastle { get => _queenCastle; set => Set(ref _queenCastle, value); }

        #endregion

        #region Capture & Promotion Counters/Flags

        private int _numWN = 3;
        private int _numWB = 3;
        private int _numWR = 3;
        private int _numWQ = 2;
        private int _numBN = 3;
        private int _numBB = 3;
        private int _numBR = 3;
        private int _numBQ = 2;

        private bool _capture = false;
        private bool _enPassantCreated = false;
        private bool _enPassant = false;
        private bool _promoted = false;
        private char _promotionPiece;

        public int NumWN { get => _numWN; set => Set(ref _numWN, value); }
        public int NumWB { get => _numWB; set => Set(ref _numWB, value); }
        public int NumWR { get => _numWR; set => Set(ref _numWR, value); }
        public int NumWQ { get => _numWQ; set => Set(ref _numWQ, value); }
        public int NumBN { get => _numBN; set => Set(ref _numBN, value); }
        public int NumBB { get => _numBB; set => Set(ref _numBB, value); }
        public int NumBR { get => _numBR; set => Set(ref _numBR, value); }
        public int NumBQ { get => _numBQ; set => Set(ref _numBQ, value); }

        public bool Capture { get => _capture; set => Set(ref _capture, value); }
        public bool EnPassantCreated { get => _enPassantCreated; set => Set(ref _enPassantCreated, value); }
        public bool EnPassant { get => _enPassant; set => Set(ref _enPassant, value); }
        public bool Promoted { get => _promoted; set => Set(ref _promoted, value); }
        public char PromotionPiece { get => _promotionPiece; set => Set(ref _promotionPiece, value); }

        #endregion

        #region Turn & State Tracking

        private int _move = 1;
        private int _halfmove = 0;
        private int _fullmove = 1;

        private bool _userTurn = false;
        private bool _moveInProgress = false;
        private bool _holdResume = false;
        private bool _wasPlayable = false;
        private bool _wasResumable = false;
        private bool _isPaused = false;
        private bool _boardSet = false;

        public int Move { get => _move; set => Set(ref _move, value); }
        public int Halfmove { get => _halfmove; set => Set(ref _halfmove, value); }
        public int Fullmove { get => _fullmove; set => Set(ref _fullmove, value); }

        public bool UserTurn { get => _userTurn; set => Set(ref _userTurn, value); }
        public bool MoveInProgress { get => _moveInProgress; set => Set(ref _moveInProgress, value); }
        public bool HoldResume { get => _holdResume; set => Set(ref _holdResume, value); }
        public bool WasPlayable { get => _wasPlayable; set => Set(ref _wasPlayable, value); }
        public bool WasResumable { get => _wasResumable; set => Set(ref _wasResumable, value); }
        public bool IsPaused { get => _isPaused; set => Set(ref _isPaused, value); }
        public bool BoardSet { get => _boardSet; set => Set(ref _boardSet, value); }

        #endregion

        #region Engine/CPU Flags

        private bool _topEngineMove;

        public bool TopEngineMove { get => _topEngineMove; set => Set(ref _topEngineMove, value); }

        #endregion

        #region Stockfish Evaluation

        private int _whiteMaterial = 0;
        private int _blackMaterial = 0;
        private double _quantifiedEvaluation = 10;
        private string _displayedAdvantage = "0.0";

        public int WhiteMaterial { get => _whiteMaterial; set => Set(ref _whiteMaterial, value); }
        public int BlackMaterial { get => _blackMaterial; set => Set(ref _blackMaterial, value); }
        public double QuantifiedEvaluation { get => _quantifiedEvaluation; set => Set(ref _quantifiedEvaluation, value); }
        public string DisplayedAdvantage { get => _displayedAdvantage; set => Set(ref _displayedAdvantage, value); }

        #endregion

        #region Game End Flags

        private bool _endGame = false;
        private bool _threefoldRepetition = false;

        public bool EndGame { get => _endGame; set => Set(ref _endGame, value); }
        public bool ThreefoldRepetition { get => _threefoldRepetition; set => Set(ref _threefoldRepetition, value); }

        #endregion

        /// <summary>Resets all necessary values.</summary>
        public void ResetGame()
        {
            PickBit1 = PickBit2 = PickBit3 = PlaceBit1 = PlaceBit2 = PlaceBit3 = null;
            WhiteBits = BlackBits = PrevWhiteBits = PrevBlackBits = null;
            CompletedWhiteBits = CompletedBlackBits = [];

            PromotedPawn = PromotedTo = ActivePiece = TakenPiece = Fen = PreviousFen = null;

            CWK = CWR1 = CWR2 = CBK = CBR1 = CBR2 = 0;
            KingCastle = QueenCastle = false;

            NumWN = NumWB = NumWR = NumBN = NumBB = NumBR = 3;
            NumWQ = NumBQ = 2;

            Capture = EnPassantCreated = EnPassant = Promoted = false;

            TopEngineMove = false;

            Move = Fullmove = 1;
            Halfmove = 0;

            UserTurn = MoveInProgress = HoldResume = WasPlayable = WasResumable = IsPaused = BoardSet = false;

            WhiteMaterial = BlackMaterial = 0;
            QuantifiedEvaluation = 10;
            DisplayedAdvantage = "0.0";

            EndGame = ThreefoldRepetition = false;

            ImageCoordinates.Clear();
            EnPassantSquare.Clear();
            GameFens.Clear();
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        private bool Set<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }
    }
}
