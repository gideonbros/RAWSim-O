using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using RAWSimO.Toolbox;
using RAWSimO.Core.IO;
using RAWSimO.Core.Bots;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.Waypoints;
using System.Threading;

namespace RAWSimO.Core.Control
{
    /// <summary>
    /// class representing a mate scheduler
    /// </summary>
    public partial class MateScheduler : IUpdateable
    {

        #region Mission control

        public class BotOrderInfo
        {
            // works only for unique addresses (TODO)
            public Mutex ClassMutex;
            public List<string> orderAddresses;
            public List<bool> completedAddresses;
            public List<LinkedList<int>> assistances;
            public LinkedList<string> addresses;
            // maps addresses to index in the orderAddresses
            public Dictionary<string, int> addressToIndex;
            public Dictionary<string, LinkedList<int>> assignment;
            int orderID;
            int botID;
            public BotOrderInfo(int newOrderID, int thisBotID)
            {
                orderAddresses = new List<string>();
                completedAddresses = new List<bool>();
                assistances = new List<LinkedList<int>>();
                addressToIndex = new Dictionary<string, int>();
                addresses = new LinkedList<string>();
                assignment = new Dictionary<string, LinkedList<int>>();
                ClassMutex = new Mutex();
                orderID = newOrderID;
                botID = thisBotID;
            }

            public static Dictionary<string, int> podColors;
            public static Dictionary<string, int> podLocked;
            public static Mutex PodColorMutex = new Mutex();

            public static int GetPodColorKey(string adr)
            {
                PodColorMutex.WaitOne();
                int colorKey = podColors[adr];
                PodColorMutex.ReleaseMutex();
                return colorKey;
            }
            public static int GetPodLocked(string adr)
            {
                PodColorMutex.WaitOne();
                int botLock = podLocked[adr];
                PodColorMutex.ReleaseMutex();
                return botLock;
            }

            public int GetOrderID() { return orderID; }

            public List<string> GetOrderAddresses() { return orderAddresses.ToList(); }

            /// <summary>
            /// Returns the status of the item inside the current order.
            /// </summary>
            /// <param name="i">Index of the item in the current order</param>
            /// <returns>Tuple consisting of:<br/> 
            ///     Item1 = (bool) item opened<br/>
            ///     Item2 = (int) assigned picker ID if there is one, -1 otherwise<br/>
            ///     Item3 = (bool) item completed<br/>
            ///     Item4 = (bool) item locked (robot is sure to come to this item)<br/>
            /// </returns>
            public Tuple<bool, int, bool, bool> GetInfoOnItem(int i)
            {
                ClassMutex.WaitOne();
                bool opened = true;
                int mateID = -1;
                bool completed = true;
                bool locked = false;
                // a new order can open for bot in BotTask, but addresses are still not filled
                // and when Visualization calls, the index is out of range
                if (i < orderAddresses.Count)
                {
                    string adr = orderAddresses[i];
                    int lockID = GetPodLocked(adr);
                    locked = lockID > -1 && lockID == botID;
                    opened = addresses.Contains(adr);
                    mateID = assistances.Count > 0 ? assistances[i].Last.Value : -1;
                    completed = completedAddresses[i];
                }
                ClassMutex.ReleaseMutex();
                return new Tuple<bool, int, bool, bool>(opened, mateID, completed, locked);
            }

            public static void UpdatePodColor(string adr, int colorKey)
            {
                PodColorMutex.WaitOne();
                podColors[adr] = colorKey;
                PodColorMutex.ReleaseMutex();
            }
            public static void PodLock(string adr, int botLock)
            {
                PodColorMutex.WaitOne();
                podLocked[adr] = botLock;
                PodColorMutex.ReleaseMutex();
            }
            public void UpdatePodLock(string adr, int botLock)
            {
                BotOrderInfo.PodLock(adr, botLock);
            }
            public void AddPickingAddress(string address)
            {
                ClassMutex.WaitOne();
                assignment.Add(address, new LinkedList<int>());
                assignment[address].AddLast(-1);
                addresses.AddLast(address);

                addressToIndex.Add(address, orderAddresses.FindIndex(a => a == address));
                assistances[addressToIndex[address]].AddLast(-1);
                //Console.WriteLine("Bot " + this.botID);
                //Console.WriteLine("ass.: " + String.Join(", ", assignment.Keys.ToList()));
                //Console.WriteLine("adr.: " + String.Join(", ", addresses)); 
                ClassMutex.ReleaseMutex();

                BotOrderInfo.UpdatePodColor(address, 0);
            }
            public void UpdateAssignedPicker(string address, int mateID)
            {
                ClassMutex.WaitOne();
                assignment[address].AddLast(mateID);
                assistances[addressToIndex[address]].AddLast(mateID);
                ClassMutex.ReleaseMutex();

                BotOrderInfo.UpdatePodColor(address, mateID);
            }
            public void ClearPickerAssignmentsAfterIndex(int position)
            {
                for (int i = position; i < addresses.Count; ++i)
                {
                    string adr = addresses.ElementAt(i);
                    assignment[adr].AddLast(-1);
                    assistances[addressToIndex[adr]].AddLast(-1);

                    BotOrderInfo.UpdatePodColor(adr, 0);
                }
            }
            public List<Tuple<string, int>> GetAssignmentList()
            {
                List<Tuple<string, int>> assignmentList = new List<Tuple<string, int>>();
                ClassMutex.WaitOne();
                if (addresses.Count > 0)
                {
                    foreach (string adr in addresses)
                    {
                        assignmentList.Add(new Tuple<string, int>(adr, assignment[adr].Last.Value));
                    }
                }
                else
                {
                    assignmentList.Add(new Tuple<string, int>("-----", -1));
                }
                ClassMutex.ReleaseMutex();
                return assignmentList;
            }
            public Tuple<string, int> GetFirstAssignment()
            {
                Tuple<string, int> firstAssignment;
                ClassMutex.WaitOne();
                if (addresses.Count > 0)
                {
                    firstAssignment = new Tuple<string, int>(addresses.First.Value, assignment[addresses.First.Value].Last.Value);
                }
                else
                {
                    firstAssignment = new Tuple<string, int>("-----", -1);
                }
                ClassMutex.ReleaseMutex();
                return firstAssignment;
            }
            public List<Tuple<string, bool, int, bool>> GetStatus()
            {
                List<Tuple<string, bool, int, bool>> statusTabel = new List<Tuple<string, bool, int, bool>>();
                ClassMutex.WaitOne();
                for (int i = 0; i < orderAddresses.Count; ++i)
                {
                    string adr = orderAddresses[i];
                    bool opened = false, completed = false;
                    int mateID = -1;
                    if (addresses.Contains(adr))
                    {
                        opened = true;
                        mateID = assignment[adr].Last.Value;
                        completed = completedAddresses[i];
                    }
                    statusTabel.Add(new Tuple<string, bool, int, bool>(adr, opened, mateID, completed));
                }
                ClassMutex.ReleaseMutex();
                return statusTabel;
            }
            // removes all addresses after and including the given one
            public void RemoveAfterAddress(string address)
            {
                ClassMutex.WaitOne();
                while (addresses.Last.Value != address)
                {
                    assignment.Remove(addresses.Last.Value);
                    addressToIndex.Remove(addresses.Last.Value);
                    BotOrderInfo.UpdatePodColor(addresses.Last.Value, -1);
                    addresses.RemoveLast();
                }
                assignment.Remove(addresses.Last.Value);
                addressToIndex.Remove(addresses.Last.Value);
                BotOrderInfo.UpdatePodColor(addresses.Last.Value, -1);
                addresses.RemoveLast();
                ClassMutex.ReleaseMutex();
            }
            // remove all after and including
            public void RemoveAfterIndex(int i)
            {
                ClassMutex.WaitOne();
                string address = addresses.ElementAt(i);
                ClassMutex.ReleaseMutex();
                RemoveAfterAddress(address);
            }
            public void RemoveAtIndex(int i)
            {
                ClassMutex.WaitOne();
                string address = addresses.ElementAt(i);
                ClassMutex.ReleaseMutex();
                RemoveAddress(address);
            }

            /// <summary>
            /// Used with BotSelfAssist to artificially remove the data of the
            /// picking location when the mission has to be aborted.
            /// In the usual case, MateScheduler takes care of clearing the resources
            /// when the mission to that same destination is invoked again.
            /// </summary>
            public void ClearCurrentLocation()
            {
                ClassMutex.WaitOne();
                string adr = addresses.Last.Value;
                addresses.Remove(adr);
                assignment.Remove(adr);
                BotOrderInfo.UpdatePodColor(adr, -1);
                assistances[addressToIndex[adr]].AddLast(-1);
                addressToIndex.Remove(adr);
                ClassMutex.ReleaseMutex();
            }

            public void ClearLocations()
            {
                ClassMutex.WaitOne();
                assignment.Clear();
                foreach (string adr in addresses)
                {
                    BotOrderInfo.UpdatePodColor(adr, -1);
                }
                addresses.Clear();
                ClassMutex.ReleaseMutex();
            }
            public void CompleteLocationAtIndex(int i)
            {

                ClassMutex.WaitOne();
                string address = addresses.ElementAt(i);
                completedAddresses[addressToIndex[address]] = true;
                ClassMutex.ReleaseMutex();
            }
            public void RemoveLastAddress()
            {
                ClassMutex.WaitOne();
                string adr = addresses.Last.Value;
                ClassMutex.ReleaseMutex();
                RemoveAddress(adr);
            }
            public void RemoveAddress(string address)
            {
                ClassMutex.WaitOne();
                assignment.Remove(address);
                addressToIndex.Remove(address);
                BotOrderInfo.UpdatePodColor(address, -1);
                addresses.Remove(address);
                ClassMutex.ReleaseMutex();
            }
            public void AddAddress(string address)
            {
                ClassMutex.WaitOne();
                orderAddresses.Add(address);
                completedAddresses.Add(false);
                assistances.Add(new LinkedList<int>());
                assistances.Last().AddLast(-1);
                ClassMutex.ReleaseMutex();
            }
        }

        public Dictionary<int, BotOrderInfo> itemTable = new Dictionary<int, BotOrderInfo>();

        public void NewOrderInItemTable(int botID, int newOrderID)
        {
            itemTable[botID] = new BotOrderInfo(newOrderID, botID);
        }

        public string PrintOrderStatus()
        {
            List<string> status_list = GetOrderStatusStringList();
            string info_str = "Item table:\n";
            info_str += String.Join("\n", status_list);
            return info_str;
         }

        public List<string> GetOrderStatusStringList()
        {
            List<string> status_list = new List<string>();
            try
            {
                foreach (int k in itemTable.Keys)
                {
                    string order_status_str = String.Format("Bot {0,2}: |", k);
                    BotOrderInfo boi = itemTable[k];
                    for (int i = 0; i < boi.orderAddresses.Count; ++i)
                    {
                        string adr = boi.orderAddresses[i];
                        int lockID = BotOrderInfo.GetPodLocked(adr);
                        bool locked = lockID > -1 && lockID == k;
                        bool opened = boi.addresses.Contains(adr);
                        int mateID = boi.assistances.Count > 0 ? boi.assistances[i].Last.Value : -1;
                        bool completed = boi.completedAddresses[i];

                        string con = completed ? "x" : (locked ? "=" : (opened ? "~" : " "));
                        string mateIDstr = mateID.ToString();
                        if (mateID == -1)
                        {
                            if (!(opened || locked))
                                mateIDstr = " ";
                            else mateIDstr = "_";
                        }
                        order_status_str += String.Format("{0}{2}{1,2}|", adr, mateIDstr, con);
                   }
                   status_list.Add(order_status_str);
                }

            }
            catch (Exception ex)
            {
                status_list.Add(ex.Message);
            }
            return status_list;
        }

        public string GetBotCurrentItemAddress(Bot bot, Waypoint assistantDestination)
        {
            // Update the current item destination for the bot in the status table 
            var currentTask = (MultiPointGatherTask)bot.CurrentTask;
            int index = currentTask.Locations.FindIndex(l => l.ID == assistantDestination.ID);

            if (currentTask.PodItems.Count <= index || index == -1)
                return "-----";

            string address = currentTask.PodItems[index].location;

            // NOTE: this works partially and only alongside
            // the change in MovableStation GetLocationAfter function
            // NOTE: It can lead to some wrong assignment visualization
            address = (bot as BotNormal).SwarmState.currentAddress;
            return address;
        }
        #endregion

        #region core
        /// <summary>
        /// creates new mate scheduler
        /// </summary>
        /// <param name="instance">falsecurrent instance</param>
        public MateScheduler(Instance instance, string loggerPath)
        {
            Instance = instance;
            AllMates = new List<MateBot>(Instance.MateBots);
            AvailableMates = new List<MateBot>(Instance.MateBots);
            AssistInfo = new AssistLocations(Instance, this);
            TimeOfLastSeach = new Dictionary<MateBot, double>();
            ZoneEnabled = Instance.SettingConfig.ZonesEnabled;
            ReserveSameAssistLocation = Instance.SettingConfig.ReserveSameAssistLocation;
            ReserveNextAssistLocation = Instance.SettingConfig.ReserveNextAssistLocation;
            foreach (var mate in AvailableMates)
            {
                TimeOfLastSeach.Add(mate, 0.0);
            }
            PredictionDepth = Instance.SettingConfig.MateSchedulerPredictionDepth;

            if (!string.IsNullOrEmpty(loggerPath) && Logger == null)
                Logger = new StreamWriter(loggerPath);

            foreach (MovableStation bot in instance.MovableStations)
            {
                NewOrderInItemTable(bot.ID, -1);
                //pickingLocationsQueue.Add(bot.ID, new Queue<string>());
            }
            BotOrderInfo.podColors = new Dictionary<string, int>();
            BotOrderInfo.podLocked = new Dictionary<string, int>();
            foreach (string adr in Instance.Pods.Select(p => p.Address))
            {
                if (adr == "") continue;
                BotOrderInfo.podColors.Add(adr, -1);
                BotOrderInfo.podLocked.Add(adr, -1);
            }
        }
        /// <summary>
        /// Gets assistant to a given <paramref name="bot"/>
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> whose assistant will be returned</param>
        /// <returns></returns>
        public Bot GetAssistant(Bot bot)
        {
            return AssistInfo.GetAssistant(bot);
        }
        /// <summary>
        /// assignes <see cref="MateBot"/> to assist <see cref="Bot"/> on a given <see cref="Waypoint"/>
        /// </summary>
        /// <param name="bot">Bot which needs assistance</param>
        /// <param name="destinationWaypoint">Waypoint on which assistance will be needed</param>
        public virtual void RequestAssistance(Bot bot, Waypoint destinationWaypoint, bool futureRequest)
        {
            AssistInfo.AddAssistLocation(bot, destinationWaypoint, futureRequest);
            // previous line will throw an exception, if not we were succesful in assigning new assistant

            // AddNextLocation -> AddFutureLocation
            // AddFutureLocation -> MS.RequestAssistance -> AddAssistLocation
            // This is the only place where AddAssistLocation is called
            // and all potential locations are contained AssistanceLocations

        }

        /// <summary>
        /// Updates predicted arrival time of a <see cref="MovableStation"/>
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> for which time is being updated</param>
        /// <param name="destinationWP"><see cref="Waypoint"/> on which <paramref name="bot"/> is going</param>
        /// <param name="arrivalTime">New arrival time for <paramref name="bot"/></param>
        public virtual void UpdateArrivalTime(Bot bot, Waypoint destinationWP, double arrivalTime)
        {
            AssistInfo.UpdateArrivalTime(bot, destinationWP, arrivalTime);
        }
        /// <summary>
        /// Updates predicted arrival time of <see cref="MateBot"/>
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> that <see cref="MateBot"/> is going to assist</param>
        /// <param name="destination"><see cref="Waypoint"/> at which <see cref="MateBot"/> will assist <paramref name="bot"/></param>
        /// <param name="arrivalTime">Predicted time at which <see cref="MateBot"/> will arrive at <paramref name="destination"/></param>
        public virtual void UpdateMateArrivalTime(Bot bot, MateBot mate, Waypoint destination, double arrivalTime)
        {
            try
            {
                AssistInfo.UpdateArrivalTime(bot, destination, arrivalTime);
            }
            catch (KeyNotFoundException e)
            {//if Mate arrived at the aborted location, ignore it, it will move on it's own
                if (e.Message != "Waypoint was not found")
                    throw e;
            }
        }
        /// <summary>
        /// Finds best assist location for a given <paramref name="mate"/>
        /// </summary>
        /// <param name="mate"><see cref="MateBot"/> for which assist location is being searched</param>
        /// <returns>Assist location</returns>
        protected void FindBestAvailableLocation(MateBot mate, out Waypoint bestLocation, out double predictedTime, out Bot bestBot)
        {
            //potential locations are all available locations plus current destination waypoint
            List<Waypoint> potentialLocations = new List<Waypoint>(AssistInfo.AvailableLocations);
            //if mate is moving to assist assisting add it's current destination to potential locations
            if (mate.DestinationWaypoint != null && !mate.IsBreaking && mate.CurrentTask is AssistTask &&
                !potentialLocations.Contains(mate.DestinationWaypoint) && AssistInfo.ValidLocations.Contains(mate.DestinationWaypoint))
                potentialLocations.Add(mate.DestinationWaypoint);
            //if mate is waiting to assist at the assist location
            if (mate.CurrentBotStateType == BotStateType.WaitingForStation && mate.CurrentTask is AssistTask &&
                !potentialLocations.Contains(mate.CurrentWaypoint) && AssistInfo.ValidLocations.Contains(mate.CurrentWaypoint) && 
                AssistInfo.IsAssisting(mate, (mate.CurrentTask as AssistTask).BotToAssist, mate.CurrentWaypoint))
                potentialLocations.Add(mate.CurrentWaypoint);
            // additional corrections added due to the new type of bot blocking
            // TODO: the informations about the current plans for each bot should be central
            if (mate.CurrentBotStateType == BotStateType.WaitingForStation && mate.CurrentTask is AssistTask &&
                potentialLocations.Contains(mate.CurrentWaypoint) &&
                (mate.CurrentTask as AssistTask).BotToAssist.BlockedLoopFrequencey > 5.0)
                potentialLocations.Remove(mate.CurrentWaypoint);
            if (mate.CurrentBotStateType == BotStateType.MoveToAssist &&
                mate.BlockedLoopFrequencey > 5.0 && potentialLocations.Contains(mate.DestinationWaypoint))
                potentialLocations.Remove(mate.DestinationWaypoint);

            // remove reserved location for other mates
            if (ReserveSameAssistLocation || ReserveNextAssistLocation)
                foreach (var reservation in MatesAndReservedLocations)
                    if (mate != reservation.MateBot)
                        potentialLocations.Remove(reservation.ReservedAssistLocation);

            // remove locations that are not mate's zones
            if (ZoneEnabled)
            {
                var locationsToRemove = potentialLocations.Where(item => !mate.Zones.Any(item2 => item2 == item.Zone)).ToList();
                foreach (var location in locationsToRemove)
                    potentialLocations.Remove(location);
            }

            LogPotentialLocations(mate, potentialLocations);

            //reduce the number of potential locations 
            potentialLocations.Sort((Waypoint x, Waypoint y) =>
            {
                double distanceX = mate.GetL1Distance(x);
                double distanceY = mate.GetL1Distance(y);
                return distanceX < distanceY ? -1 : distanceX == distanceY ? 0 : 1;
            });

            int amountToTake = Math.Max(5, (int)Math.Ceiling(0.25 * potentialLocations.Count));
            potentialLocations = potentialLocations.Take(amountToTake).ToList();

            //search for the best potential location
            double bestTime = double.PositiveInfinity;
            bestLocation = null;
            bestBot = null;
            foreach (var location in potentialLocations)
            {
                Bot bot;
                double currentBestArrivalTime = double.PositiveInfinity, mateArrivalTime = double.PositiveInfinity;

                //decide which bot needs help at location
                //mate is moving to location
                if (mate.DestinationWaypoint == location && mate.CurrentBotStateType == BotStateType.MoveToAssist &&
                    mate.CurrentTask is AssistTask && AssistInfo.IsAssisting(mate, (mate.CurrentTask as AssistTask).BotToAssist, location))
                {
                    Bot currentBot = null;
                    try
                    {
                        currentBot = (mate.CurrentTask as AssistTask).BotToAssist;
                        currentBestArrivalTime = AssistInfo[currentBot, location];
                        mateArrivalTime = Instance.Controller.PathManager.PredictArrivalTime(mate, location, false);
                        currentBestArrivalTime = Math.Max(currentBestArrivalTime, mateArrivalTime);
                    }
                    catch (ArgumentNullException e)
                    {//if argument is null, rethrow the exception
                        throw e;
                    }
                    catch (KeyNotFoundException)
                    {//ms went to resting location, mate should find new location
                        currentBestArrivalTime = double.PositiveInfinity;
                        mateArrivalTime = double.PositiveInfinity;
                    }
              
                    catch (ArgumentException)
                    {//mate is going to location which is not registered in AssistLocations, new location should now be picked
                        currentBestArrivalTime = double.PositiveInfinity;
                        mateArrivalTime = double.PositiveInfinity;
                    }

                    var newBot = AssistInfo[location];

                    // if only current bot requested assistance at location then newBot will be null
                    if (newBot == null)
                    {
                        bot = currentBot;
                    }
                    else//at least two bots requested assistance at the same location
                    {
                        var newArrivalTime = AssistInfo[newBot, location];
                        if (newArrivalTime < currentBestArrivalTime)
                        {
                            currentBestArrivalTime = newArrivalTime;
                            bot = newBot;
                            mateArrivalTime = Instance.Controller.PathManager.PredictArrivalTime(mate, location, false);
                        }
                        else
                        {
                            bot = currentBot;
                        }
                    }
                }//mate is waiting for station at the location
                else if (mate.CurrentWaypoint == location && mate.DestinationWaypoint == null && mate.CurrentBotStateType == BotStateType.WaitingForStation &&
                         mate.CurrentTask is AssistTask && AssistInfo.IsAssisting(mate, (mate.CurrentTask as AssistTask).BotToAssist, location))
                {
                    Bot currentBot = null;
                    try
                    {
                        currentBot = (mate.CurrentTask as AssistTask).BotToAssist;
                        currentBestArrivalTime = AssistInfo[currentBot, location];
                    }
                    catch (ArgumentNullException e)
                    {//if argument is null, rethrow the exception
                        throw e;
                    }
                    catch (KeyNotFoundException)
                    {//ms went to resting location, mate should find new location
                        currentBestArrivalTime = double.PositiveInfinity;
                    }
                    catch (ArgumentException)
                    {//mate is going to location which is not registered in AssistLocations, new location should now be picked
                        currentBestArrivalTime = double.PositiveInfinity;
                    }

                    var newBot = AssistInfo[location];
                    mateArrivalTime = 0; //mate is already there

                    // if only current bot requested assistance at location then newBot will be null
                    if (newBot == null)
                    {
                        bot = currentBot;
                    }
                    else//at least two bots requested assistance at the same location
                    {
                        var newArrivalTime = AssistInfo[newBot, location];
                        if (newArrivalTime < currentBestArrivalTime)
                        {
                            currentBestArrivalTime = newArrivalTime;
                            bot = newBot;
                        }
                        else
                        {
                            bot = currentBot;
                        }
                    }
                }//mate is doing some other assist task
                else if (mate.CurrentTask is AssistTask)
                {
                    var currentLocation = (mate.CurrentTask as AssistTask).Waypoint;
                    var currentBot = (mate.CurrentTask as AssistTask).BotToAssist;
                    var newBot = AssistInfo[location];
                    //if newBot == currentBot and locations comes after currentLocation, then ignore this location
                    if (newBot == currentBot && newBot != null &&
                        AssistInfo.AssistOrder(currentBot, currentLocation) < AssistInfo.AssistOrder(currentBot, location))
                    {
                        continue;
                    }

                    if (newBot == null) throw new Exception("location should not be taken at this point!");
                    bot = newBot;
                    currentBestArrivalTime = AssistInfo[bot, location];
                    mateArrivalTime = Instance.Controller.PathManager.PredictArrivalTime(mate, location, true);
                }
                else//mate is going somewhere else. If it decides to go to location, it should be to assist someone new
                {
                    bot = AssistInfo[location]; //returns bot with unassigned assistance
                    if (bot == null) continue;
                    currentBestArrivalTime = AssistInfo[bot, location];
                    mateArrivalTime = Instance.Controller.PathManager.PredictArrivalTime(mate, location, true);
                }

                if (Math.Max(currentBestArrivalTime, mateArrivalTime) < bestTime)
                {
                    bestTime = Math.Max(currentBestArrivalTime, mateArrivalTime);
                    bestLocation = location;
                    bestBot = bot;
                }
            }

            //save time to return
            predictedTime = bestTime;

            //see if mate was going to assist some other bot
            var oldBotToAssist = AssistInfo.GetBotsAssistedBy(mate).FirstOrDefault();
            if (oldBotToAssist != null)
            {
                Waypoint oldLocation = AssistInfo.GetAssistLocation(oldBotToAssist, mate);
                double oldBestTime;

                if ((oldLocation == mate.DestinationWaypoint || (mate.DestinationWaypoint == null && mate.CurrentWaypoint == oldLocation)) &&
                    mate.CurrentTask is AssistTask && (mate.CurrentTask as AssistTask).Waypoint == oldLocation && oldLocation != null)
                {
                    oldBestTime = AssistInfo[oldBotToAssist, oldLocation];
                }
                else
                    oldBestTime = bestTime;

                // blocked loop frequency needs to be close to zero
                if (oldBestTime - bestTime < Instance.SettingConfig.MateSwitchingThreshold && oldBotToAssist.BlockedLoopFrequencey < 0.01) //if new time is not at least 30 seconds faster, don't switch goal
                {   //roll-back to previous best
                    bestLocation = null;
                    predictedTime = double.PositiveInfinity;
                    bestBot = null;
                }
            }
            LogBestLocation(mate, bestLocation, predictedTime);
            return;
        }
        /// <summary>
        /// current instance
        /// </summary>
        protected Instance Instance { get; set; }
        /// <summary>
        /// List of all <see cref="MateBot"> objects
        /// </summary>
        protected List<MateBot> AllMates { get; set; }
        /// <summary>
        /// List of all <see cref="MatesAndReservedLocations"> objects
        /// </summary>
        protected List<MateReservations> MatesAndReservedLocations { get; set; }
        /// <summary>
        /// List of all currently available <see cref="MateBot"> objects  
        /// </summary>
        protected List<MateBot> AvailableMates { get; set; }
        /// <summary>
        /// Object which holds all info about assist locations
        /// </summary>
        public AssistLocations AssistInfo { get; set; }

        /// <summary>
        /// Holds the last time a search for assist location has been done for a given mate
        /// </summary>
        protected Dictionary<MateBot, double> TimeOfLastSeach { get; set; }
        /// <summary>
        /// Time in seconds after which we check if matebot has any closer assistance to give
        /// </summary>
        protected readonly double AssistLocationReoptimizatioinPeriod = 4.0;
        /// <summary>
        /// Number of future items
        /// </summary>
        private static int PredictionDepth { get; set; }
        /// <summary>
        /// Indicates whether zone picking will be enabled
        /// </summary>
        private bool ZoneEnabled { get; set; }
        /// <summary>
        /// Indicates whether Reserve same assist location will be enabled
        /// </summary>
        private bool ReserveSameAssistLocation { get; set; }
        /// <summary>
        /// Indicates whether Reserve assist locations for mates will be enabled
        /// </summary>
        private bool ReserveNextAssistLocation { get; set; }

        #endregion

        #region Events
        /// <summary>
        /// Reacts on <see cref="AbortingTask"/> being assigned to <see cref="MateBot"/>
        /// </summary>
        /// <param name="mate"><see cref="MateBot"/> which got assigned <see cref="AbortingTask"/></param>
        public void NotifyMateAbortingTaskAssigned(MateBot mate)
        {
            //if aborting task was assigned to mate, then it is no longer participating in assistance so we
            //should remove all the related info
            AssistInfo.RemoveMateInfo(mate);
            //if mate whose assistance was aborted is not in available mates, add it so it can do
            //other stuff if needed
            if (!AvailableMates.Contains(mate)) AvailableMates.Add(mate);
        }
        /// <summary>
        /// Reacts on bot going to resting location
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> which is going to resting location</param>
        public virtual void NotifyBotGoingToRestingLocation(Bot bot)
        {
            AssistInfo.RemoveInfoAbout(bot);
            // clear all locations, since it is going to rest
            if (itemTable[bot.ID].addresses.Count > 0)
                itemTable[bot.ID].RemoveAfterIndex(0);
            //itemTable[bot.ID].ClearLocations();
        }
        /// <summary>
        /// Reacts on Bot going to <see cref="OutputPalletStand"/>
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> that is going to output pallet stand</param>
        public virtual void NotifyBotGoingToOutputPalletStand(Bot bot)
        {
            //ignore this event in base scheduling
        }
        /// <summary>
        /// Reacts on assist start  
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> getting assistance</param>
        /// <param name="assistant"><see cref="MateBot"/> giving assistance</param>
        internal virtual void NotifyAssistStarted(Bot bot, MateBot assistant)
        {
            //if assistant is still in Available Mates, remove it
            if (AvailableMates.Contains(assistant))
                AvailableMates.Remove(assistant);
        }
        /// <summary>
        /// Reacts on assist end
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> getting assistance</param>
        /// <param name="assistant"><see cref="MateBot"/> giving assistance</param>
        internal virtual void NotifyAssistEnded(Bot bot, MateBot assistant)
        {
            AvailableMates.Add(assistant);
            assistant.SwitchesPerAssists.AddLast(assistant.SwitchesThisAssist);
            assistant.SwitchesThisAssist = 0;
            AssistInfo.RemoveAssistanceTo(bot, assistant, false);
        }
        /// <summary>
        /// Event that is triggered when <paramref name="bot"/> requests an assist on <paramref name="location"/> that is different from what was expected
        /// </summary>
        /// <param name="bot"><see cref="Bot"/> that triggered the event</param>
        /// <param name="location">location on which assist was requested</param>
        protected virtual void NotifyFutureRequestSkipped(Bot bot, IEnumerable<Waypoint> locations)
        {
            /* Not implemented in base scheduler */
        }
        /// <summary>
        /// Reacts on <paramref name="bot"/>'s registered locations being removed
        /// </summary>
        /// <param name="bot">Bot whose locations were removed</param>
        /// <param name="removedLocations">Locations that were removed</param>
        protected virtual void NotifyLocationsRemoved(Bot bot, LinkedList<Tuple<Waypoint, double>> removedLocations)
        {
            /* Not implemented in base scheduler */
        }
        #endregion

        #region Logger
        /// <summary>
        /// Logging helper function which writes <see cref="MateBot"/> identification, time and potential locations
        /// </summary>
        /// <param name="mate">Mate on which <see cref="MateScheduler"/> is currently working</param>
        /// <param name="locations"><see cref="List{T}"/> on locations <see cref="MateScheduler"/> is taking into consideration</param>
        protected void LogPotentialLocations(MateBot mate, List<Waypoint> locations)
        {
            if (LoggerIsNull()) return;
            if (locations.Count() != 0)
            {
                var currentTime = TimeSpan.FromSeconds(Instance.Controller.CurrentTime).ToString(IOConstants.TIMESPAN_FORMAT_HUMAN_READABLE_DAYS);
                // Console.WriteLine("============================================");
                // Console.WriteLine(currentTime + " || " + mate.ToString());
                // Console.WriteLine("Mate (x,y) = (" + mate.X.ToString(IOConstants.FORMATTER) + ", " + mate.Y.ToString(IOConstants.FORMATTER)
                //                     + ");current waypoint: " + mate.CurrentWaypoint?.ToString()
                //                     + ";  destination waypoint: " + mate.DestinationWaypoint?.ToString());
                // Console.WriteLine("Potential locations: ");
                // foreach (var location in locations)
                //     Console.WriteLine("      " + location.GetInfoRowColumn() + " - " + location.ToString() + " - L1 distance: " + mate.GetL1Distance(location).ToString()
                //                         + " - valid location: " + AssistInfo.ValidLocations.Contains(location).ToString());

                Logger.WriteLine("============================================");
                Logger.WriteLine(currentTime + " || " + mate.ToString());
                Logger.WriteLine("Mate (x,y) = (" + mate.X.ToString(IOConstants.FORMATTER) + ", " + mate.Y.ToString(IOConstants.FORMATTER)
                                    + ");current waypoint: " + mate.CurrentWaypoint?.ToString()
                                    + ";  destination waypoint: " + mate.DestinationWaypoint?.ToString());
                Logger.WriteLine("Potential locations: ");
                foreach (var location in locations)
                    Logger.WriteLine("      " + location.ToString() + " - L1 distance: " + mate.GetL1Distance(location).ToString()
                                        + " - valid location: " + AssistInfo.ValidLocations.Contains(location).ToString());
                Logger.Flush();
            }
        }
        /// <summary>
        /// Logging helper function which writes destination chosen for <paramref name="mate"/> by <see cref="MateScheduler"/>
        /// </summary>
        /// <param name="mate"><see cref="MateBot"/> for which destination was chosen</param>
        /// <param name="bestLocation"><see cref="Waypoint"/> which was chosen</param>
        /// <param name="_predictedTime"> Predicted assist start time</param>
        protected void LogBestLocation(MateBot mate, Waypoint bestLocation, double _predictedTime)
        {
            if (LoggerIsNull()) return;
            string predictedTime;
            switch (_predictedTime)
            {
                case double.PositiveInfinity:
                    predictedTime = double.PositiveInfinity.ToString();
                    break;
                case double.MaxValue:
                    predictedTime = double.MaxValue.ToString();
                    break;
                default:
                    predictedTime = TimeSpan.FromSeconds(_predictedTime).ToString(IOConstants.TIMESPAN_FORMAT_HUMAN_READABLE_DAYS);
                    break;
            }

            if (bestLocation != null)
            {
                // Console.WriteLine("Chosen destination: ");
                // Console.WriteLine("      R/C:" + bestLocation.GetInfoRowColumn() + " - " + bestLocation.ToString() + ", assist estimated at " + predictedTime);

                Logger.WriteLine("Chosen destination: ");
                Logger.WriteLine("      " + bestLocation.ToString() + ", assist estimated at " + predictedTime);
                Logger.Flush();
            }
        }

        /// <summary>
        /// Logging helper function which writes info about which <paramref name="bot"/> will <paramref name="mate"/> be assisting and at which <paramref name="location"/>
        /// </summary>
        /// <param name="bot"></param>
        /// <param name="location"></param>
        /// <param name="mate"></param>
        protected void LogNewAssignemnt(Bot bot, Waypoint location, MateBot mate, double _predictedTime)
        {
            if (LoggerIsNull()) return;

            string predictedTime;
            switch (_predictedTime)
            {
                case double.PositiveInfinity:
                    predictedTime = double.PositiveInfinity.ToString();
                    break;
                case double.MaxValue:
                    predictedTime = double.MaxValue.ToString();
                    break;
                default:
                    predictedTime = TimeSpan.FromSeconds(_predictedTime).ToString(IOConstants.TIMESPAN_FORMAT_HUMAN_READABLE_DAYS);
                    break;
            }

            string oldTime = "";

            if (mate.CurrentTask is AssistTask)
            {
                var mateTask = mate.CurrentTask as AssistTask;
                try
                {
                    oldTime = TimeSpan.FromSeconds(AssistInfo[mateTask.BotToAssist, mateTask.Waypoint]).ToString(IOConstants.TIMESPAN_FORMAT_HUMAN_READABLE_DAYS);
                }
                catch
                {
                }
            }
            // Console.WriteLine(mate.ToString() + " received new assist task! ");
            // Console.WriteLine(mate.ToString() + " current waypoint:  " + mate.CurrentWaypoint.GetInfoRowColumn() + " - " + mate.CurrentWaypoint.ToString() +
            //                  "     destination waypoint: " + mate.DestinationWaypoint?.ToString() +
            //                  "     state: " + mate.CurrentBotStateType.ToString());
            // Console.WriteLine(bot.ToString() + " current waypoint:  " + bot.CurrentWaypoint.GetInfoRowColumn() + " - " + bot.CurrentWaypoint.ToString() +
            //                  "     destination waypoint: " + bot.TargetWaypoint?.ToString() +
            //                  "     state: " + bot.GetInfoState());
            // Console.WriteLine("assist at location:   " + location.GetInfoRowColumn() + " - " + location.ToString() +
            //                  "      old predicted time:  " + oldTime +
            //                  "      new predicted time:  " + predictedTime);
            // Console.WriteLine("List of available mates: ");

            Logger.WriteLine(mate.ToString() + " received new assist task! ");
            Logger.WriteLine(mate.ToString() + " current waypoint:  " + mate.CurrentWaypoint.ToString() +
                             "     destination waypoint: " + mate.DestinationWaypoint?.ToString() +
                             "     state: " + mate.CurrentBotStateType.ToString());
            Logger.WriteLine(bot.ToString() + " current waypoint:  " + bot.CurrentWaypoint.ToString() +
                             "     destination waypoint: " + bot.TargetWaypoint?.ToString() +
                             "     state: " + bot.GetInfoState());
            Logger.WriteLine("assist at location:   " + location.ToString() +
                             "      old predicted time:  " + oldTime +
                             "      new predicted time:  " + predictedTime);
            Logger.Flush();
        }
        /// <summary>
        /// Checks if  Mate scheduler logger is null
        /// </summary>
        /// <returns><see langword="true"/> if logger is null. <see langword="false"/> otherwise</returns>
        private bool LoggerIsNull()
        {
            return Logger == null;
        }
        /// <summary>
        /// Logger for MateScheduler 
        /// </summary>
        private StreamWriter Logger { get; set; }
        #endregion

        #region IUpdateable members
        /// <summary>
        /// The next event when this element has to be updated.
        /// </summary>
        /// <param name="currentTime">The current time of the simulation.</param>
        /// <returns>The next time this element has to be updated.</returns>
        public double GetNextEventTime(double currentTime) { return double.PositiveInfinity; }

        /// <summary>
        /// Updates the element to the specified time.
        /// </summary>
        /// <param name="lastTime">The time before the update.</param>
        /// <param name="currentTime">The time to update to.</param>
        public virtual void Update(double lastTime, double currentTime)
        {
            // all potential locations
            var potentialLocations = AssistInfo.AvailableLocations.ToList();

            var availableMates = GetMatesInNeedOfAssignment(currentTime).ToList();

            var mateSnapshot = SortAvailableMatesByTheDistance(availableMates, potentialLocations);

            MatesAndReservedLocations = new List<MateReservations>();

            UpdateReservedSameLocationForMatesInAssistance(availableMates, potentialLocations);

            UpdateReserveNextLocationForMates(potentialLocations);

            foreach (var mateWaypoint in mateSnapshot)
            //foreach (var mate in availableMates)
            {
                MateBot mate = mateWaypoint.Item1;

                //if there are no available locations left then we have nothing else to do, exit update 
                if (AssistInfo.AvailableLocationsCount == 0) return;
                //find best location for mate with reserved location info
                FindBestAvailableLocation(mate, out Waypoint location, out double predictedArrivalTime, out Bot newBot);

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
            }
        }

        /// <summary>
        /// Returns sorted available mates by the distance to open potential locations.
        /// First mate in the list is the closest one to the potential location.
        /// </summary>
        /// <param name="availableMates"></param>
        /// <param name="potentialLocations"></param>
        /// <returns>List of mates with resepective distance to the closest potential location</returns>
        private List<Tuple<MateBot, double>> SortAvailableMatesByTheDistance(List<MateBot> availableMates, List<Waypoint> potentialLocations)
        {
            //Deprecated "smart" Mate scheduler -> sorting by closest mates first
            //List of mates and their distances to the closest potential location
            var mateSnapshot = availableMates.Select(mate => new Tuple<MateBot, double>(mate,
                potentialLocations.Count() > 0 ? potentialLocations.Select(location => mate.CurrentWaypoint.GetDistance(location)).Min() : 0)).ToList();

            //Sort them by the distance
            mateSnapshot.Sort((Tuple<MateBot, double> a, Tuple<MateBot, double> b) =>
            {
                return Math.Abs(a.Item2 - b.Item2) < 0.01 ? 0 : a.Item2 < b.Item2 ? -1 : 1;
            });
            //deal with mates that are closest to potential locations first
            //otherwise the forloop iteration starts with some list that can assign an idle mate that first
            //entered the list.to some locatio far awat but that is closes to him
            return mateSnapshot;
        }

        /// <summary>
        /// Reserves location for mate on the same loacation where this mate is assisting if exists.
        /// </summary>
        /// <param name="availableMates"></param>
        /// <param name="potentialLocations"></param>
        /// <returns></returns>
        private void UpdateReservedSameLocationForMatesInAssistance(List<MateBot> availableMates, List<Waypoint> potentialLocations)
        {
            if (ReserveSameAssistLocation)
            {
                // Get all mates in assistance
                var matesInAssistance = AllMates.Except(availableMates).ToList();

                // Match mates in assistance with potential locations for picking if mate.CurrentWaypoint is equal to potential location
                MatesAndReservedLocations = matesInAssistance.Select(mate => new MateReservations
                        (mate, potentialLocations.FirstOrDefault(location =>
                        (mate.CurrentBotStateType == BotStateType.WaitingForStation && location.GetInfoID() == mate.StateQueuePeek().DestinationWaypoint.GetInfoID()) ||
                        (mate.CurrentBotStateType == BotStateType.MoveToAssist && location.GetInfoID() == mate.StateQueuePeek().DestinationWaypoint.GetInfoID())), 0)).ToList();
                
                // Remove invalid/null reeservations
                MatesAndReservedLocations = MatesAndReservedLocations.Where(reservation => reservation.ReservedAssistLocation != null).ToList();
            }
        }

        /// <summary>
        /// Reserves best next location for mates.
        /// </summary>
        /// <param name="potentialLocations"></param>
        private void UpdateReserveNextLocationForMates(List<Waypoint> potentialLocations)
        {
            if (ReserveNextAssistLocation)
            {
                var tempListMatesAndLocations = new List<MateReservations>();

                // Add mates with reserved same assist locations to tempList
                tempListMatesAndLocations.AddRange(MatesAndReservedLocations);

                // For all mates calculate L1 distance to all potential locations
                foreach (var mate in AllMates)
                    tempListMatesAndLocations.AddRange(potentialLocations.Select(location => new MateReservations(mate, location, mate.GetL1Distance(location))).ToList());

                // After sorting the pair with the smallest distance from the potential location will be first on the list
                tempListMatesAndLocations.Sort((x, y) => x.L1Distance.CompareTo(y.L1Distance));

                for (int i = 0; i < AllMates.Count() - 1 && tempListMatesAndLocations.Count() != 0; i++)
                {
                    // Take mate with the smallest distance from the potential location
                    var perfectMatchMateLocation = tempListMatesAndLocations.FirstOrDefault();

                    // Remove all locations from tempList for mate that is choosen for reservation with smallest distance to potential location
                    var removeThisReservationsFromTempList = tempListMatesAndLocations.Where(item => item.MateBot == perfectMatchMateLocation.MateBot || 
                        item.ReservedAssistLocation == perfectMatchMateLocation.ReservedAssistLocation).ToList();
                    foreach (var reservation in removeThisReservationsFromTempList)
                        tempListMatesAndLocations.Remove(reservation);

                    // Check if choosen location is already reserved. If isn't add it to the reseravtion list
                    var mateContainsReservation = MatesAndReservedLocations.FirstOrDefault(reservation => reservation.MateBot == perfectMatchMateLocation.MateBot);
                    if (mateContainsReservation == null)
                        MatesAndReservedLocations.Add(perfectMatchMateLocation);
                }
            }
        }

        public static Mutex UnassignedMatesMutex = new Mutex();

        public List<MateBot> lastUnassignedMates = new List<MateBot>();
        public List<MateBot> GetLastUnassignedMates()
        {
            UnassignedMatesMutex.WaitOne();
            List<MateBot> list = lastUnassignedMates.ToList();
            UnassignedMatesMutex.ReleaseMutex();
            return list;
        }
        public List<MateBot> GetMatesInNeedOfAssignment(double currentTime)
        {
            UnassignedMatesMutex.WaitOne();
            List<MateBot> matesNeedingAssignment = AvailableMates.Where(mate =>
                    //bot is idle
                    mate.CurrentTask.Type == BotTaskType.None ||
                    mate.CurrentTask.Type == BotTaskType.Rest ||
                    mate.StateQueueCount == 0 ||
                    //bot is moving to assist but enough time has passed so new search will be done
                    ((mate.CurrentBotStateType == BotStateType.MoveToAssist ||
                    mate.CurrentBotStateType == BotStateType.WaitingForStation)
                    && (currentTime - TimeOfLastSeach[mate] > AssistLocationReoptimizatioinPeriod)) ||
                    // moving to assist bu stuck in block-unblock loop
                    mate.CurrentBotStateType == BotStateType.MoveToAssist && mate.BlockedLoopFrequencey > 5).ToList();
            lastUnassignedMates = matesNeedingAssignment;
            UnassignedMatesMutex.ReleaseMutex();
            return matesNeedingAssignment;
        }
        #endregion
    }
    /// <summary>
    /// MateBot and list of reserved location for this object
    /// </summary>
    public class MateReservations
    {
        /// <summary>
        /// MateBot
        /// </summary>
        public MateBot MateBot { get; set; }
        /// <summary>
        /// Reserved same location where MateBot is currently assisting
        /// </summary>
        public Waypoint ReservedAssistLocation { get; set; }
        /// <summary>
        /// Distance to next item
        /// </summary>
        public double L1Distance { get; set; }

        public MateReservations(MateBot mate, Waypoint reservedLocation)
        {
            this.MateBot = mate;
            this.ReservedAssistLocation = reservedLocation;
        }
        public MateReservations(MateBot mate, Waypoint reservedLocation, double l1Distance)
        {
            this.MateBot = mate;
            this.ReservedAssistLocation = reservedLocation;
            this.L1Distance = l1Distance;
        }
    }
}
