using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Project
{
    public class EloSettings
    {
        public int Depth { get; set; }
        public int CpLossThreshold { get; set; }
        public double BellCurvePercentile { get; set; }
        public int CriticalMoveConversion { get; set; }

        public static EloSettings GetSettings(int elo)
        {
            // Initialize default settings
            var settings = new EloSettings
            {
                Depth = 1,
                CpLossThreshold = 800,
                BellCurvePercentile = 25,
                CriticalMoveConversion = 100
            };

            // Adjust settings based on Elo
            if (elo <= 200)
            {
                settings.Depth = 2;
                settings.CpLossThreshold = int.MaxValue;
                settings.BellCurvePercentile = 25;
                settings.CriticalMoveConversion = 10;
            }
            else if (elo <= 300)
            {
                settings.Depth = 3;
                settings.CpLossThreshold = int.MaxValue;
                settings.BellCurvePercentile = 35;
                settings.CriticalMoveConversion = 20;
            }
            else if (elo <= 400)
            {
                settings.Depth = 4;
                settings.CpLossThreshold = 800;
                settings.BellCurvePercentile = 40;
                settings.CriticalMoveConversion = 35;
            }
            else if (elo <= 500)
            {
                settings.Depth = 4;
                settings.CpLossThreshold = 700;
                settings.BellCurvePercentile = 50;
                settings.CriticalMoveConversion = 50;
            }
            else if (elo <= 600)
            {
                settings.Depth = 5;
                settings.CpLossThreshold = 650;
                settings.BellCurvePercentile = 55;
                settings.CriticalMoveConversion = 60;
            }
            else if (elo <= 700)
            {
                settings.Depth = 6;
                settings.CpLossThreshold = 600;
                settings.BellCurvePercentile = 60;
                settings.CriticalMoveConversion = 70;
            }
            else if (elo <= 800)
            {
                settings.Depth = 6;
                settings.CpLossThreshold = 550;
                settings.BellCurvePercentile = 65;
                settings.CriticalMoveConversion = 75;
            }
            else if (elo <= 900)
            {
                settings.Depth = 7;
                settings.CpLossThreshold = 450;
                settings.BellCurvePercentile = 70;
                settings.CriticalMoveConversion = 80;
            }
            else if (elo <= 1000)
            {
                settings.Depth = 7;
                settings.CpLossThreshold = 400;
                settings.BellCurvePercentile = 75;
                settings.CriticalMoveConversion = 85;
            }
            else if (elo <= 1200)
            {
                settings.Depth = 8;
                settings.CpLossThreshold = 300;
                settings.BellCurvePercentile = 80;
            }
            else if (elo <= 1400)
            {
                settings.Depth = 9;
                settings.CpLossThreshold = 250;
                settings.BellCurvePercentile = 85;
            }
            else if (elo <= 1600)
            {
                settings.Depth = 10;
                settings.CpLossThreshold = 150;
                settings.BellCurvePercentile = 90;
            }
            else if (elo <= 1800)
            {
                settings.Depth = 11;
                settings.CpLossThreshold = 100;
                settings.BellCurvePercentile = 93;
            }
            else if (elo <= 2000)
            {
                settings.Depth = 12;
                settings.CpLossThreshold = 50;
                settings.BellCurvePercentile = 96;
            }
            else if (elo <= 2200)
            {
                settings.Depth = 14;
                settings.CpLossThreshold = 30;
                settings.BellCurvePercentile = 98;
            }
            else if (elo <= 2400)
            {
                settings.Depth = 16;
                settings.CpLossThreshold = 15;
                settings.BellCurvePercentile = 99;
            }
            else if (elo <= 2600)
            {
                settings.Depth = 14;
                settings.CpLossThreshold = 10;
                settings.BellCurvePercentile = 99.5;
            }
            else if (elo <= 2800)
            {
                settings.Depth = 18;
                settings.CpLossThreshold = 10;
                settings.BellCurvePercentile = 99.5;
            }
            else if (elo <= 3000)
            {
                settings.Depth = 20;
                settings.CpLossThreshold = 5;
                settings.BellCurvePercentile = 99.7;
            }
            else
            {
                settings.Depth = 24;
                settings.CpLossThreshold = 0;
                settings.BellCurvePercentile = 100;
            }

            return settings;
        }
    }
}
