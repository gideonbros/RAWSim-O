using RAWSimO.Core.Bots;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Waypoints;
using System;
using System.Linq;
using System.Threading;

namespace RAWSimO.Core.Control.Defaults.PalletStandManagment
{
    /// <summary>
    /// Advanced pallet stand manager which uses euclidean distance to calculate nearest pallet stand. This algorithm is greedy.
    /// </summary>
    public class PSEuclideanGreedyManager : PalletStandManager
    {
        /// <summary>
        /// Constructor which sets Instance.
        /// </summary>
        /// <param name="instance"></param>
        public PSEuclideanGreedyManager(Instance instance)
        {
            Instance = instance;
        }
        
        /// <summary>
        /// Finds the closest input pallet stand for the given <paramref name="bot"/>
        /// </summary>
        /// <param name="bot">Bot which needs the input pallet stand</param>
        /// <param name="firstItemWP" Position of first item </param>
        /// <returns>Input pallet stand waypoint</returns>
        public override Waypoint GetClosestInputPalletStandWaypoint(BotNormal bot, Waypoint firstItemWP)
        {
            // Closest input pallet stand
            var waypointLocations = Instance.InputPalletStands.ConvertAll(s => s.Waypoint);
            var inputPalletStandLocation = Instance.findClosestLocation(waypointLocations, bot.CurrentWaypoint);

            // Go to another intput pallet stand if it is more time-efficient than waiting for its turn on the closest one
            var moreEfficientLocations = Instance.InputPalletStands.FindAll(s => s.IncomingBots < inputPalletStandLocation.InputPalletStand.IncomingBots);
            if (moreEfficientLocations.Any())
            {
                inputPalletStandLocation = Instance.findClosestLocation(moreEfficientLocations.ConvertAll(ips => ips.Waypoint), bot.CurrentWaypoint);

            }

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
            var outputPalletStandLocation = Instance.findClosestLocation(waypointLocations, bot.CurrentWaypoint);

            // Go to another output pallet stand if it is more time-efficient than waiting for its turn on the closest one
            var moreEfficientLocations = Instance.OutputPalletStands.FindAll(s => s.IncomingBots < outputPalletStandLocation.OutputPalletStand.IncomingBots);
            if (moreEfficientLocations.Any())
                outputPalletStandLocation = Instance.findClosestLocation(moreEfficientLocations.ConvertAll(ops => ops.Waypoint), bot.CurrentWaypoint);

            ++outputPalletStandLocation.OutputPalletStand.IncomingBots;
            return outputPalletStandLocation;
        }

        private Instance Instance { get; set; }
    }
}
