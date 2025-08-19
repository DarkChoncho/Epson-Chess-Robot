using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Project
{
    public enum RobotColor
    {
        White,
        Black
    }

    public enum RobotState
    {
        Boot,
        Ready,
        Running,
        Error,
        Disconnected
    }

    public enum GameMode
    {
        ComVsCom = 0,
        UserVsCom = 1,
        UserVsUser = 2
    }

    public enum DeltaType
    {
        Piece,
        Square
    }

    public enum PromotionPiece
    {
        Rook,
        Knight,
        Bishop,
        Queen
    }

    public enum GameOutcome
    {
        None,
        CheckmateWhite,
        CheckmateBlack,
        Stalemate,
        FiftyMoveDraw,
        ThreefoldDraw,
        InsufficientMaterialDraw
    }
}
