using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Project
{
    public static class GlobalState
    {
        public static bool WhiteEpsonConnected { get; set; } = false;
        public static bool WhiteCognexConnected { get; set; } = false;
        public static bool BlackEpsonConnected { get; set; } = false;
        public static bool BlackCognexConnected { get; set; } = false;
        public static RobotState WhiteState { get; set; } = RobotState.Disconnected;
        public static RobotState BlackState { get; set; } = RobotState.Disconnected;
    }
}
