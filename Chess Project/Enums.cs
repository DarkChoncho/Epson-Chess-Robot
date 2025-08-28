namespace Chess_Project
{
    /// <summary>
    /// Indicates which chess side (piece color) is in use.
    /// </summary>
    public enum Color
    {
        White,
        Black
    }

    /// <summary>
    /// Indicates current state of the Epson robot.
    /// </summary>
    public enum RobotState
    {
        Boot,
        Ready,
        Running,
        Error,
        Disconnected
    }

    /// <summary>
    /// Indicates which game mode is currently active.
    /// </summary>
    public enum GameMode
    {
        ComVsCom = 0,
        UserVsCom = 1,
        UserVsUser = 2
    }
}