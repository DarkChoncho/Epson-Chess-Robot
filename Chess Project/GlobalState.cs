using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Project
{
    public static class GlobalState
    {
        public static bool WhiteConnected { get; set; }
        public static bool BlackConnected { get; set; }
        public static RobotState WhiteState { get; set; } = RobotState.Disconnected;
        public static RobotState BlackState { get; set; } = RobotState.Disconnected;
    }
}
