using RAWSimO.Core.Bots;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Waypoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Core.Control
{   /// <summary>
    /// Class which represents Mate scheduler based on hungarian algorithm
    /// </summary>
    class HungarianMateScheduler : MateScheduler 
    {
        public HungarianMateScheduler(Instance instance, string loggerPath) : base(instance, loggerPath)
        {
            HungarianMatrix = new HungarianMatrix(Instance.MateBots);
        }
        /// <summary>
        /// Updates this object
        /// </summary>
        /// <param name="lastTime">last time</param>
        /// <param name="currentTime">current time</param>
        public override void Update(double lastTime, double currentTime)
        {
            //if there aren't any available mates, exit 
            if (!GetMatesInNeedOfAssignment(currentTime).Any())
                return;

            var potentialLocations = new List<Waypoint>(HungarianMatrix.Locations);

            foreach(var mate in Instance.MateBots)
            {
                //sort potential locations by distance to mate
                potentialLocations.Sort((Waypoint x, Waypoint y) => {
                    double distanceX = mate.GetL1Distance(x);
                    double distanceY = mate.GetL1Distance(y);
                    return distanceX < distanceY ? -1 : distanceX == distanceY ? 0 : 1;
                });

                int amount = Math.Max(5, (int)Math.Ceiling(0.25 * potentialLocations.Count));

                //prepare dict where predicted arrival times to mateLocations will be stored
                Dictionary<Waypoint, double> PredictedArrivalTimes = new Dictionary<Waypoint, double>(amount);

                //predict arrival time for every mateLocation
                foreach(var location in potentialLocations.Take(amount).Distinct())
                {
                    var arrivalTime = Instance.Controller.PathManager.PredictArrivalTime(mate, location, true);
                    PredictedArrivalTimes.Add(location, arrivalTime);
                }

                //update hungarian matrix for this mate
                HungarianMatrix.UpdateArrivalTime(mate, PredictedArrivalTimes);
            }

            //calculate Assignment and save it in a map
            var assignmentMap = HungarianMatrix.CalculateAssignment();

            //Assign tasks based on assignment map
            AssignTasks(assignmentMap, currentTime);
        }

        /// <summary>
        /// Updates arrival time of <paramref name="bot"/> at <paramref name="destination"/>
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> whose arrival time will be updated</param>
        /// <param name="destination">location which time will be updated</param>
        /// <param name="arrivalTime">new arrival time</param>
        public override void UpdateArrivalTime(Bot bot, Waypoint destination, double arrivalTime)
        {
            base.UpdateArrivalTime(bot, destination, arrivalTime);
            if((bot as MovableStation).CurrentBotStateType != Bots.BotStateType.WaitingForMate) //if bot is waiting, no need to update
                HungarianMatrix.UpdateArrivalTime(destination, bot, arrivalTime);
        }

        /// <summary>
        /// Updates arrival time of <paramref name="mate"/> at <paramref name="bot"/>'s  <paramref name="destination"/>
        /// </summary>
        /// <param name="bot">Bot that is being assisted</param>
        /// <param name="mate">Mate that is giving assistance</param>
        /// <param name="destination">assist location</param>
        /// <param name="arrivalTime">new mate arrival time</param>
        public override void UpdateMateArrivalTime(Bot bot, MateBot mate, Waypoint destination, double arrivalTime) =>
                base.UpdateMateArrivalTime(bot, mate, destination, arrivalTime);

        /// <summary>
        /// Logs that <paramref name="bot"/> will need assistnace at <paramref name="destinationWaypoint"/>. It is also possible to log <paramref name="futureRequest"/>
        /// </summary>
        /// <param name="bot">Bot requesting assistance</param>
        /// <param name="destinationWaypoint">Location of assistance</param>
        /// <param name="futureRequest">Bool indicating if bot currently needs assistance or in the future</param>
        public override void RequestAssistance(Bot bot, Waypoint destinationWaypoint, bool futureRequest)
        {
            base.RequestAssistance(bot, destinationWaypoint, futureRequest);
            HungarianMatrix.Add(new Tuple<Waypoint, Bot>(destinationWaypoint, bot));
        }

        /// <summary>
        /// Helper method which assigns task based on <paramref name="assignmentMap"/>
        /// </summary>
        /// <param name="assignmentMap">Map based on which assignment will be done</param>
        /// <param name="currentTime">current time</param>
        private void AssignTasks(Dictionary<MateBot, Tuple<Waypoint, Bot>> assignmentMap, double currentTime)
        {
            foreach(var mateTuple in assignmentMap)
            {
                var mate = mateTuple.Key;
                var location = mateTuple.Value.Item1;
                var newBot = mateTuple.Value.Item2;

                //Set time of this search
                TimeOfLastSeach[mate] = currentTime;

                //sanity check
                if (location == null || newBot == null)
                    continue;

                //check if mate is already in assist proces
                if (mate.CurrentBotStateType == Bots.BotStateType.WaitingForStation)
                    continue;

                //check if some mate is already assigned to assist newBot at location
                if(AssistInfo.IsSomeoneAssisting(newBot, location) == true)
                    continue;

                //if mate is currently doing assist task, check if it is assisting the same bot
                if (mate.CurrentTask is AssistTask)
                {
                    AssistTask currentTask = mate.CurrentTask as AssistTask;
                    var oldBot = currentTask.BotToAssist;
                    var oldWP = currentTask.Waypoint;

                    //if the same bot was chosen
                    if (oldBot == newBot && (
                        //if mate is already at the location, ignore
                        (mate.CurrentWaypoint == location && mate.DestinationWaypoint == null) ||
                        //or if mate is going to the location, ignore
                        mate.DestinationWaypoint == location ||
                        //or if mate was going to assist the same bot, and it is trying to switch to a future location
                        //of the same bot, switching to past locations will happend since AssistInfo[destination]
                        //will become null
                        AssistInfo.AssistOrder(newBot, oldWP) <= AssistInfo.AssistOrder(newBot, location)
                        ))
                    {
                        //call OnAssistantAssigned() so that bot can wake up if it is resting
                        newBot.OnAssistantAssigned();
                        continue;
                    }
                }

                //mate is going to location different from previous
                mate.SwitchesThisAssist++;
                if (mate.SwitchesThisAssist >= Instance.SettingConfig.MaxNumberOfMateSwitches) //can be greater due to aborting
                    AvailableMates.Remove(mate); //this mate will no longer be taken into account until he finishes the given assist

                //create new task and assign new task to mate
                AssistTask task = new AssistTask(Instance, mate, location, newBot);
                mate.AssignTask(task);
                AssistInfo.AssistantAssigned(newBot, location, mate);
            }

        }

        #region Events
        /// <summary>
        /// Reacts on bot going to resting location
        /// </summary>
        /// <param name="bot">bot going to resting location</param>
        public override void NotifyBotGoingToRestingLocation(Bot bot)
        {
            HungarianMatrix.Remove(bot);
            base.NotifyBotGoingToRestingLocation(bot);
        }

        /// <summary>
        /// Reacts on assist start
        /// </summary>
        /// <param name="bot">Bot getting assistance</param>
        /// <param name="assistant">mate giving assistance</param>
        internal override void NotifyAssistStarted(Bot bot, MateBot assistant)
        {
            //do the base operations
            base.NotifyAssistStarted(bot, assistant);
            //remove asociated column in hungarian matrix
            HungarianMatrix.Remove(assistant.CurrentWaypoint, bot);
        }

        internal override void NotifyAssistEnded(Bot bot, MateBot assistant)
        {
            base.NotifyAssistEnded(bot, assistant);
            // removing columns should be here and not in assist started
            // but putting it here causes Hungarian to break
        }
        /// <summary>
        /// Event that is triggered when <paramref name="bot"/> requests an assist on <paramref name="location"/> that is different from what was expected.
        /// Removes info about <paramref name="locations"/> from HungarianMatrix
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> that triggered the event</param>
        /// <param name="location">location on which assist was requested</param>
        protected override void NotifyFutureRequestSkipped(Bot bot, IEnumerable<Waypoint> locations)
        {
            foreach (var location in locations)
                HungarianMatrix.Remove(location, bot);
        }

        /// <summary>
        /// Reacts on <paramref name="bot"/>'s registered locations being removed. 
        /// Removes info related to <paramref name="bot"/> and <paramref name="removedLocations"/> stored in HungarianMatrix
        /// </summary>
        /// <param name="bot">Bot whose locations were removed</param>
        /// <param name="removedLocations">Locations that were removed</param>
        protected override void NotifyLocationsRemoved(Bot bot, LinkedList<Tuple<Waypoint, double>> removedLocations)
        {
            foreach (var node in removedLocations)
                HungarianMatrix.Remove(node.Item1, bot);
        }
        #endregion

        #region Properties
        /// <summary>
        /// Hungarian matrix used by this scheduler
        /// </summary>
        private HungarianMatrix HungarianMatrix { get; set; }
        #endregion
    }
}
