/*
 * FILE           : Worker.cs
 *  PROJECT       : Project Manufacturing P01 > Workstation Simulation (Milestone 2)
 *  PROGRAMMERS   : Julia Jakob & Bibi Murwared
 *  FIRST VERSION : 2026-03-27
 *  DESCRIPTION   : Class for Workers
*/
using System;

namespace P01_WorkstationSimulation
{
    public class Worker
    {
        public int WorkerID { get; set; }

        public string WorkerName { get; set; } = string.Empty;

        public int SkillLevelID { get; set; }

        public string SkillLevelName { get; set; } = string.Empty;

        public decimal Speed { get; set; }

        public decimal DefectRate { get; set; }

        public int StationID { get; set; }
    }
}