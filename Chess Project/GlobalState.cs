namespace Chess_Project
{
    /// <summary>
    /// Process-wide connection and runtime status flags for both robots/cameras.
    /// </summary>
    /// <remarks>
    /// These flags are updated by the Epson and Cognex controllers at connection/disconnection
    /// time and can be read by UI/state machines to drive behavior.
    /// 
    /// <para><b>Thread-safety</b></para>
    /// Properties are simply static setters/getters and are not synchronized. If multiple
    /// threads write/read concurrently, consider guarding updates (e.g., with a lock) or using
    /// a single atomic state object per side.
    /// <para>✅ Updated on 8/28/2025</para>
    /// </remarks>
    public static class GlobalState
    {
        #region White Side

        /// <summary>
        /// <see langword="true"/> if the white-side Epson controller is connected;
        /// otherwise, <see langword="false"/>.
        /// </summary>
        public static bool WhiteEpsonConnected { get; set; } = false;

        /// <summary>
        /// <see langword="true"/> if the white-side Cognex camera is connected;
        /// otherwise, <see langword="false"/>.
        /// </summary>
        public static bool WhiteCognexConnected { get; set; } = false;

        /// <summary>
        /// Current high-level state for the white-side robot (e.g., Connected/Disconnected/Busy).
        /// </summary>
        public static RobotState WhiteState { get; set; } = RobotState.Disconnected;

        #endregion

        #region Black Side

        /// <summary>
        /// <see langword="true"/> if the black-side Epson controller is connected;
        /// otherwise, <see langword="false"/>.
        /// </summary>
        public static bool BlackEpsonConnected { get; set; } = false;

        /// <summary>
        /// <see langword="true"/> if the black-side Cognex camera is connected;
        /// otherwise, <see langword="false"/>.
        /// </summary>
        public static bool BlackCognexConnected { get; set; } = false;

        /// <summary>
        /// Current high-level state for the black-side robot (e.g., Connected/Disconnected/Busy).
        /// </summary>
        public static RobotState BlackState { get; set; } = RobotState.Disconnected;

        #endregion
    }
}