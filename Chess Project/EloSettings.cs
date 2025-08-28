namespace Chess_Project
{
    /// <summary>
    /// Difficulty/profile parameters used to tune the engine or move-selection
    /// behavior to approximate a target Elo strength.
    /// </summary>
    /// <remarks>
    /// <list type="bullet">
    ///     <item><description><see cref="Depth"/> controls search effort (in plies) and typically rises with Elo.</description></item>
    ///     <item><description><see cref="CpLossThreshold"/> caps how many centipawns of evaluation loss are allowed when selecting a sub-optimal move to emulate human inaccuracies; lower values play more accurately.</description></item>
    ///     <item><description><see cref="BellCurvePercentile"/> biases move choice toward stronger candidates by sampling from a distribution (higher percentile ⇒ stronger moves).</description></item>
    ///     <item><description><see cref="CriticalMoveConversion"/> expresses, as a percentage (0-100), the willingness to "convert" in critical/tactical positions (e.g., prefer the engine's top move instead of a sampled alternative). Higher values play more "clinical" chess under pressure.</description></item>
    /// </list>
    /// ✅ Updated on 8/28/2025
    /// </remarks>
    public class EloSettings
    {
        /// <summary>
        /// Maximum search depth (in plies) or an equivalent knob controlling engine effort.
        /// Higher depths generally produce stronger play.
        /// </summary>
        public int Depth { get; set; }

        /// <summary>
        /// Maximum allowed evaluation drop, in centipawns, from the engine's best move when
        /// deliberately picking a weaker alternative to emulate human play. Use larger values
        /// (e.g., <see cref="int.MaxValue"/>) to effectively disable this guard.
        /// </summary>
        public int CpLossThreshold { get; set; }

        /// <summary>
        /// Percentile (0-100) used to bias randomization toward better moves when sampling
        /// from a quality distribution. Larger values prefer higher-quality candidates.
        /// </summary>
        public double BellCurvePercentile { get; set; }

        /// <summary>
        /// Percentage (0-100) indicating how often, in critical positions, the system should
        /// override randomness and play the strongest (engine) move.
        /// </summary>
        public int CriticalMoveConversion { get; set; }

        /// <summary>
        /// Returns an <see cref="EloSettings"/> profile pre-tuned for the specified target Elo.
        /// </summary>
        /// <param name="elo">Target rating to emulate (e.g., 200-3000+). The mapping is stepwise by Elo band.</param>
        /// <returns>
        /// A settings object whose <see cref="Depth"/>, <see cref="CpLossThreshold"/>,
        /// <see cref="BellCurvePercentile"/>, and <see cref="CriticalMoveConversion"/> reflect
        /// the selected strength tier.
        /// </returns>
        /// <remarks>
        /// The mapping increases search depth and tightens accuracy as Elo grows (lower
        /// <see cref="CpLossThreshold"/>, higher <see cref="BellCurvePercentile"/>). For lower
        /// Elo bands, <see cref="CriticalMoveConversion"/> is reduced to allow more mistakes;
        /// for higher bands, it remains at the default value set during initialization unless
        /// explicitly adjusted in code.
        /// <para>✅ Updated on 8/28/2025</para>
        /// </remarks>
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
                settings.CriticalMoveConversion = 90;
            }
            else if (elo <= 1400)
            {
                settings.Depth = 9;
                settings.CpLossThreshold = 250;
                settings.BellCurvePercentile = 85;
                settings.CriticalMoveConversion = 97;
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