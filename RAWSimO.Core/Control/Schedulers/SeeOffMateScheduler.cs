using RAWSimO.Core.Elements;
using RAWSimO.Core.Waypoints;
using System.Linq;

namespace RAWSimO.Core.Control
{
    public class SeeOffMateScheduler : MateScheduler
    {
        public SeeOffMateScheduler(Instance instance, string loggerPath) : base(instance, loggerPath)
        {
            AssistInfo = new SeeOffAssistLocations(Instance, this);
        }

        #region Core
        /// <summary>
        /// Gets info about current <paramref name="mate"/> assignment. Used in See-off scheduling
        /// </summary>
        /// <param name="mate"><see cref="MateBot"/> whose assignment is being searched</param>
        /// <param name="location">Out parameter which represents location at which <paramref name="mate"/> is expected. Is set to <see langword="null"/> if no such location exists </param>
        /// <param name="predictedArrivalTime">Out parameter which represents the expected time of assist start on <paramref name="location"/>. Is set to NaN if no time is registered</param>
        /// <param name="bot">Out parameter which represents <see cref="Bot"/> which <paramref name="mate"/> is going to assist at <paramref name="location"/>. Is set to <see langword="null"/> if no such <see cref="Bot"/> can be found</param>
        private void GetCurrentAssignment(MateBot mate, out Waypoint location, out double predictedArrivalTime, out Bot bot)
        {
            //try to get bot assisted by mate
            bot = AssistInfo.GetBotsAssistedBy(mate).FirstOrDefault();
            //if it failed, nullify out parameters and return
            if (bot == null)
            {
                predictedArrivalTime = double.NaN;
                location = null;
                return;
            }
            //try to get location at which mate will assist newBot
            location = AssistInfo.GetAssistLocation(bot, mate);
            //if it failed, nullify remaining out parameters
            if (location == null)
            {
                predictedArrivalTime = double.NaN;
                return;
            }
            //try to get predicted arrival time
            predictedArrivalTime = AssistInfo[bot, location];

            //if predicted arrival time is double.NaN, calculate the time it takes for mate to reach location
            if (predictedArrivalTime == double.MaxValue)
                predictedArrivalTime = Instance.Controller.PathManager.PredictArrivalTime(mate, location, true);

            return;
        }
        #endregion

        #region Events
        /// <summary>
        /// Reacts on bot going to resting location
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> which is going to resting location</param>
        public override void NotifyBotGoingToRestingLocation(Bot bot)
        {
            //ignore this event in see off scheduling
        }
        /// <summary>
        /// Reacts on Bot going to <see cref="OutputPalletStand"/>
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> that is going to output pallet stand</param>
        public override void NotifyBotGoingToOutputPalletStand(Bot bot)
        {
            AssistInfo.RemoveAssistantOf(bot);
        }
        /// <summary>
        /// Reacts on assist end
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> getting assistance</param>
        /// <param name="assistant"><see cref="MateBot"/> giving assistance</param>
        internal override void NotifyAssistEnded(Bot bot, MateBot assistant)
        {
            AvailableMates.Add(assistant);
            assistant.SwitchesPerAssists.AddLast(assistant.SwitchesThisAssist);
            assistant.SwitchesThisAssist = 0;
            // remove only the first assist location, do not remove assistant info in see - off scheduling
            AssistInfo.RemoveAssistLocation(bot);
        }
        #endregion

        #region IUpdateables
        public override void Update(double lastTime, double currentTime)
        {
            var availableMates = GetMatesInNeedOfAssignment(currentTime);

            foreach (var mate in availableMates)
            {
                //Try to get current assignment
                GetCurrentAssignment(mate, out Waypoint location, out double predictedArrivalTime, out Bot newBot);

                //if newBot is null then mate is not registered as an assistant, we can find new Bot to assist
                if (newBot == null)
                    FindBestAvailableLocation(mate, out location, out predictedArrivalTime, out newBot);

                //Set time of this search
                TimeOfLastSeach[mate] = currentTime;

                //if FindBestAvailableLocation failed, continue
                if (location == null || newBot == null)
                    continue;

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
                        //or if mate was going to assist the same bot, it is trying to switch to a future location
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

                //log assignment of mate to bot at location
                LogNewAssignemnt(newBot, location, mate, predictedArrivalTime);

                //create new task and update arrival time
                AssistTask task = new AssistTask(Instance, mate, location, newBot);
                UpdateArrivalTime(newBot, location, predictedArrivalTime);

                //assign new task to mate
                mate.AssignTask(task);
                AssistInfo.AssistantAssigned(newBot, location, mate);

                // this is additional fix fo status table
                // since the previous line removes all assistants, even from the current bot
                // so ClearPickerAssignmentsAfterIndex is called on the current bot and all
                // assistance is removed
                string adr = Instance.Controller.MateScheduler.GetBotCurrentItemAddress(newBot, location);
                Instance.Controller.MateScheduler.itemTable[newBot.ID].UpdateAssignedPicker(adr, mate.ID);
            }
        }
        #endregion
    }
}
