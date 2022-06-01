using RAWSimO.Core.Configurations;
using RAWSimO.Core.Items;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Waypoints;
using RAWSimO.Core.Geometrics;
using RAWSimO.Core.Metrics;
using RAWSimO.Toolbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RAWSimO.Core.Control.Defaults.TaskAllocation
{
    public class DummyBotManager : BotManager
    {
        public double RellocationInterval{ get; set; }

        public DummyBotManager(Instance instance, double reallocation_interval = 30.0) : base(instance)
        {
            Instance = instance;
            RellocationInterval = reallocation_interval;
        }
        /// <summary>
        /// The next event when this element has to be updated.
        /// </summary>
        /// <param name="currentTime">The current time of the simulation.</param>
        /// <returns>The next time this element has to be updated.</returns>
        public override double GetNextEventTime(double currentTime)
        {
           return RellocationInterval + currentTime; //maybe double.MaxValue ??
        }
        /// <summary>
        /// Signals the current time to the mechanism. The mechanism can decide to block the simulation thread in order consume remaining real-time.
        /// </summary>
        /// <param name="currentTime">The current simulation time.</param>
        public override void SignalCurrentTime(double currentTime)
        {
           /* Not necessary */
        }
        /// <summary>
        /// Updates the element to the specified time.
        /// </summary>
        /// <param name="lastTime">The time before the update.</param>
        /// <param name="currentTime">The time to update to.</param>
        public override void Update(double lastTime, double currentTime)
        {
            /*Do nothing, when bot finishes his task it will triget call chain that will lead
            to GetNextTask()*/
        }
        /// <summary>
        /// Gets the next task for the specified bot.
        /// </summary>
        /// <param name="bot">The bot to get a task for.</param>
        protected override void GetNextTask(Bot bot)
        {
            MovableStation station = null;
            if(bot is MovableStation){
                station = bot as MovableStation;
                if (station.AssignedOrders.Count() == 0)
                {
                    SendToRest(bot);
                    return;
                }
                EnqueueMultiPointGather(station, station.AssignedOrders.First()); 
            }else{
                return;
            }

        }
        /// <summary>
        /// Finds the input pallet stand for the given <paramref name="bot"/>
        /// </summary>
        /// <param name="bot">Bot which needs the input pallet stand</param>
        /// <returns>Input pallet stand waypoint</returns>
        public Waypoint GetInputPalletStandLocation(MultiPointGatherTask task)
        {
            // Closest input pallet stand
            var waypointLocations = Instance.InputPalletStands.ConvertAll(s => s.Waypoint);
            var inputPalletStandLocation = Instance.findClosestLocation(waypointLocations, task.Locations.First());

            // Go to another intput pallet stand if it is more time-efficient than waiting for its turn on the closest one
            var moreEfficientLocations = Instance.InputPalletStands.FindAll(s => s.IncomingBots <= (inputPalletStandLocation.InputPalletStand.IncomingBots - 3));
            if (moreEfficientLocations.Any())
                inputPalletStandLocation = Instance.findClosestLocation(moreEfficientLocations.ConvertAll(ips => ips.Waypoint), task.Locations.First());

            ++inputPalletStandLocation.InputPalletStand.IncomingBots;
            return inputPalletStandLocation;

        }
        /// <summary>
        /// Finds the output pallet stand for the given <paramref name="bot"/>
        /// </summary>
        /// <param name="bot">Bot which needs the output pallet stand</param>
        /// <returns>Output pallet stand waypoint</returns>
        public Waypoint GetOutputPalletStandLocation(Bot bot)
        {
            // Output pallet stand location from file (if it exists)
            if(bot is MovableStation && bot.CurrentTask is MultiPointGatherTask && (bot.CurrentTask as MultiPointGatherTask).DropWaypoint != null)
            {
                return (bot.CurrentTask as MultiPointGatherTask).DropWaypoint;
            }

            // Closest output pallet stand
            var waypointLocations = Instance.OutputPalletStands.ConvertAll(s => s.Waypoint);
            var outputPalletStandLocation = Instance.findClosestLocation(waypointLocations, bot.CurrentWaypoint);

            // Go to another output pallet stand if it is more time-efficient than waiting for its turn on the closest one
            var moreEfficientLocations = Instance.OutputPalletStands.FindAll(s => s.IncomingBots <= (outputPalletStandLocation.OutputPalletStand.IncomingBots - 2));
            if (moreEfficientLocations.Any())
                outputPalletStandLocation = Instance.findClosestLocation(moreEfficientLocations.ConvertAll(ops => ops.Waypoint), bot.CurrentWaypoint);

            ++outputPalletStandLocation.OutputPalletStand.IncomingBots;
            return outputPalletStandLocation;
        }
        /// <summary>
        /// Enqueues rest task
        /// </summary>
        /// <param name="bot">Bot that will be sent to rest</param>
        public void SendToRest(Bot bot)
        {
            Waypoint restLocation = null;
            //if bot already was in rest, let him rest at the same location if it is still unused
            if (bot.StatLastState == Bots.BotStateType.Rest && bot.LastRestLocation != null &&
                Instance.ResourceManager.UnusedRestingLocations.Contains(bot.LastRestLocation))
            {
                restLocation = bot.LastRestLocation;
            }
            else    //send the bot to rest at the side
            {
                var restLocations = Instance.ResourceManager.UnusedRestingLocations;
                if(restLocations.Count() != 0)
                    restLocation = restLocations.ElementAt(Instance.Randomizer.NextInt(restLocations.Count()));
            }

            if (restLocation != null)
                EnqueueRest(bot, restLocation);
        }
    }
}