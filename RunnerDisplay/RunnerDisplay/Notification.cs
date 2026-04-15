using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;

namespace RunnerDisplay
{
    internal class Notification
    {
        public string PartName { get; set; } // part name needing refill
        public int BinID { get; set; } // bin ID that needs refilling or was refilled
        public string Location { get; set; } // location (workstation name) of the bin
        public string TimeStamp { get; set; } // time stamp for the action

        public Brush StatusColor { get; set; } // status will be green for a refill and red for a needs refill notification
    }
}


// StatusColor =  (Brush)new BrushConverter().ConvertFrom("#E5989B"); // for RED status (needs refill notification)
// StatusColor = (Brush)new BrushConverter().ConvertFrom("#CCD5AE"); // for GREEN status (refilled notification)