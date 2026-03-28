/*
 * FILE           : Parts.cs
 *  PROJECT       : Project Manufacturing P01 > Workstation Simulation (Milestone 2)
 *  PROGRAMMERS   : Julia Jakob
 *  FIRST VERSION : 2026-03-27
 *  DESCRIPTION   : Class for Parts
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P01_WorkstationSimulation
{
    public class Part
    {
        public int PartID
        {
            get; 
            set;
        }

        public string PartName
        {
            get; 
            set;
        } = string.Empty; // default so that it stops warning about null
    }
}
