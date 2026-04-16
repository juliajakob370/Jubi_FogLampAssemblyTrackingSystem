/*
 * FILE           : Notification.cs
 * PROJECT        : Project Manufacturing - RunnerDisplay
 * PROGRAMMERS    : Julia Jakob & Bibi Murwared
 * FIRST VERSION  : 2026-04-15
 * DESCRIPTION    : Model used to display runner log notifications in the UI
 */

using System.Windows.Media;

namespace RunnerDisplay
{
    internal class Notification
    {
        public string PartName { get; set; } = string.Empty; // part name needing refill
        public int BinID { get; set; } // bin ID that needs refilling or was refilled
        public string Location { get; set; } = string.Empty; // workstation / station name
        public string TimeStamp { get; set; } = string.Empty; // timestamp shown in the grid
        public Brush StatusColor { get; set; } = Brushes.Red; // red = needs refill, green = refilled
    }
}