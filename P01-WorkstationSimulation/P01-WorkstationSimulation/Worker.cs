/*
 * FILE           : Worker.cs
 *  PROJECT       : Project Manufacturing P01 > Workstation Simulation (Milestone 2)
 *  PROGRAMMERS   : Julia Jakob
 *  FIRST VERSION : 2026-03-27
 *  DESCRIPTION   : Class for Workers
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace P01_WorkstationSimulation
{
    public class Worker
    {
        public int WorkerID { 
            get; 
            set; 
        }
        public string WorkerName
        {
            get;
            set;
        } = string.Empty; // default so that it stops warning about null
    }
}
