using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chess_Project.Configuration
{
    public class Preferences
    {
        public string Background { get; set; } = "Cosmos";
        public string Pieces { get; set; } = "NeoWood";
        public string Board { get; set; } = "IcySea";
        public bool PieceSounds { get; set; } = false;
        public bool ConfirmMove { get; set; } = true;
        public bool EpsonRC { get; set; } = false;
        public bool CognexVision { get; set; } = false;
    }
}
