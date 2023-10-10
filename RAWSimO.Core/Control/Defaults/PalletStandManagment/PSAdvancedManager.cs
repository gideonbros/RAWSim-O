using RAWSimO.Core.Bots;
using RAWSimO.Core.Control;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Waypoints;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;

namespace RAWSimO.Core.Control.Defaults.PalletStandManagment
{
    /// <summary>
    /// Advanced pallet stand manager which uses Arrival time heuristics with A*.
    /// </summary>
    public class PSAdvancedManager : PalletStandManager
    {
        /// <summary>
        /// Constructor which sets Instance.
        /// </summary>
        /// <param name="instance"></param>
        public PSAdvancedManager(Instance instance)
        {
            Instance = instance;
        }

        /// <summary>
        /// Finds the closest input pallet stand for the given <paramref name="bot"/>
        /// </summary>
        /// <param name="bot">Bot which needs the input pallet stand</param>
        /// <param name="firstItemWP" Position of first item </param>
        /// <returns>Input pallet stand waypoint</returns>
        public override Waypoint GetClosestInputPalletStandWaypoint(BotNormal bot, Waypoint firstItemWp)
        {
            BotNormal dummyBot = new BotNormal(bot);
            dummyBot.ID = Instance.layoutConfiguration.MovableStationCount + Instance.layoutConfiguration.MateBotCount + 1;

            // Closest input pallet stand
            var waypointLocations = Instance.InputPalletStands.ConvertAll(s => s.Waypoint);
            Waypoint inputPalletStandLocation = Instance.findClosestLocation(waypointLocations, bot.CurrentWaypoint);

            // Go to another intput pallet stand if it is more time-efficient than waiting for its turn on the closest one
            double minTimeDistance = double.MaxValue;
            double minTimeDistanceToPS = double.MaxValue;

            Instance.InputPalletStands.ForEach(ps => {
                double timeDistanceToPS = -Instance.Controller.CurrentTime;
                timeDistanceToPS += Instance.Controller.PathManager.PredictArrivalTimeHeuristics(bot, ps.Waypoint, true);
                double timeDistance = timeDistanceToPS;
                // Console.Write($"{bot.ID} ::::{ps.ID}  --- {timeDistance}, number of IncomingBots {ps.IncomingBots}");
                
                if (firstItemWp != null)
                {
                    dummyBot.X = ps.X;
                    dummyBot.Y = ps.Y;
                    dummyBot.CurrentWaypoint = ps.Waypoint;
                    double timeToFirstItem = -Instance.Controller.CurrentTime;
                    timeToFirstItem += Instance.Controller.PathManager.PredictArrivalTimeHeuristics(dummyBot, firstItemWp, true);
                    // Console.Write($"  --- to first item {timeToFirstItem} ");
                    
                    timeDistance += timeToFirstItem;
                }

                timeDistance += ps.IncomingBots * Instance.SettingConfig.IPSPalletDuration;

                // Console.Write($".. sum {timeDistance}\n");

                // If current time equals to minTime, then pick nearest location
                if (Math.Abs(timeDistance - minTimeDistance) < 0.2)
                {
                    if (timeDistanceToPS < minTimeDistanceToPS)
                    {
                        minTimeDistance = timeDistance;
                        minTimeDistanceToPS = timeDistanceToPS;
                        inputPalletStandLocation = ps.Waypoint;
                    }

                }
                else if (timeDistance < minTimeDistance)
                {
                    minTimeDistance = timeDistance;
                    minTimeDistanceToPS = timeDistanceToPS;
                    inputPalletStandLocation = ps.Waypoint;
                } 
                
            });

            // Console.WriteLine(".");
            ++inputPalletStandLocation.InputPalletStand.IncomingBots;
            return inputPalletStandLocation;

        }

        /// <summary>
        /// Finds the input pallet stand for the given <paramref name="bot"/>
        /// </summary>
        /// <param name="bot">Bot which needs the input pallet stand</param>
        /// <param name="task">Task which has waypoints</param>
        /// <returns>Input pallet stand waypoint</returns>
        public override Waypoint GetInputPalletStandWaypoint(BotNormal bot, MultiPointGatherTask task)
        {
            return GetClosestInputPalletStandWaypoint(bot, task.Locations.First());
        }

        /// <summary>
        /// Finds the output pallet stand for the given <paramref name="bot"/>
        /// </summary>
        /// <param name="bot">Bot which needs the output pallet stand</param>
        /// <returns>Output pallet stand waypoint</returns>
        public override Waypoint GetOutputPalletStandWaypoint(BotNormal bot)
        {
            
            // Output pallet stand location from file (if it exists)
            if (bot is MovableStation && bot.CurrentTask is MultiPointGatherTask && (bot.CurrentTask as MultiPointGatherTask).DropWaypoint != null)
            {
                return (bot.CurrentTask as MultiPointGatherTask).DropWaypoint;
            }

            // Closest output pallet stand
            var waypointLocations = Instance.OutputPalletStands.ConvertAll(s => s.Waypoint);
            Waypoint outputPalletStandLocation = Instance.findClosestLocation(waypointLocations, bot.CurrentWaypoint);

            // Go to another output pallet stand if it is more time-efficient than waiting for its turn on the closest one
            double minTimeDistance = double.MaxValue;

            Instance.OutputPalletStands.ForEach(ps => {
                double timeDistance = -Instance.Controller.CurrentTime;
                timeDistance += Instance.Controller.PathManager.PredictArrivalTimeHeuristics(bot, ps.Waypoint, true) + ps.IncomingBots * Instance.SettingConfig.OSPalletDuration;

                if (timeDistance < minTimeDistance)
                {
                    minTimeDistance = timeDistance;
                    outputPalletStandLocation = ps.Waypoint;
                }
            });

            ++outputPalletStandLocation.OutputPalletStand.IncomingBots;
            return outputPalletStandLocation;
        }

        private Instance Instance { get; set; }

    }
}
