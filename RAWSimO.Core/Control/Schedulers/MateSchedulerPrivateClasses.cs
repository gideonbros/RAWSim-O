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
using RAWSimO.Core.Control.Filters;

namespace RAWSimO.Core.Control
{
    /// <summary>
    /// class representing a mate scheduler
    /// </summary>
    public partial class MateScheduler : IUpdateable
    {
        /// <summary>
        /// private singleton class used to store info about assist locations
        /// </summary>
        public class AssistLocations : IEnumerable<Tuple<Bot, Waypoint, double>>
        {
            #region Constructor & Indexers
            internal AssistLocations(Instance instance, MateScheduler mateScheduler)
            {
                AssistanceLocations = new Dictionary<Bot, LinkedList<Tuple<Waypoint, double>>>();
                Assistant = new Dictionary<Bot, LinkedList<MateBot>>();
                foreach (Bot bot in instance.MovableStations)
                {
                    Assistant.Add(bot, new LinkedList<MateBot>());
                }
                Instance = instance;
                MateScheduler = mateScheduler;
                BotsInProcessOfAborting = new HashSet<MateBot>();
            }
            /// <summary>
            /// Indexer for getting and setting the time when <paramref name="bot"/> arrives on <paramref name="wp"/>
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> whose info will be accessed</param>
            /// <param name="wp"><see cref="Waypoint"/> where <paramref name="bot"/> will arrive</param>
            /// <returns>Time of arrival</returns>
            internal double this[Bot bot, Waypoint wp]
            {
                get
                {
                    //parameter check
                    if (bot == null)
                        throw new ArgumentNullException(nameof(bot), " parameter was null!");
                    if (wp == null)
                        throw new ArgumentNullException(nameof(wp), " parameter was null!");
                    if (!AssistanceLocations.ContainsKey(bot))
                        throw new KeyNotFoundException(nameof(bot) + " was not found!");
                    //getting
                    foreach (var wpTimePair in AssistanceLocations[bot])
                        if (wpTimePair.Item1 == wp)
                            return wpTimePair.Item2;
                    //failed to get
                    throw new ArgumentException("Waypoint " + nameof(wp) + "was not found", nameof(wp));
                }
                set
                {
                    //parameter check
                    if (bot == null)
                        throw new ArgumentNullException(nameof(bot), "parameter was null!");
                    if (wp == null)
                        throw new ArgumentNullException(nameof(wp), "parameter was null!");
                    if (double.IsNaN(value))
                        throw new ArgumentNullException(nameof(value), "parameter was NaN!");
                    if (!AssistanceLocations.ContainsKey(bot))
                        return;

                    //setting
                    for (var node = AssistanceLocations[bot].First; node != null; node = node.Next)
                        if (node.Value.Item1 == wp)
                        {
                            var oldValue = node.Value.Item2;
                            if (oldValue >= double.MaxValue) //location is still not valid, set normally
                            {
                                node.Value = new Tuple<Waypoint, double>(wp, value);
                            }
                            else //value of the node needs to be updated along with the values of all the next nodes
                            {
                                node.Value = new Tuple<Waypoint, double>(wp, Math.Max(value, oldValue));
                                double valueDiff = Math.Max(value, oldValue) - oldValue;
                                IncrementNextNodes(node, valueDiff);
                            }


                            return;
                        }
                    //failed to set
                    throw new KeyNotFoundException("Waypoint was not found");
                }
            }
            /// <summary>
            /// Indexer which returns the first <see cref="Bot"/> that requested the assistance on <paramref name="wp"/> but has no assistance assigned
            /// </summary>
            /// <param name="wp"><see cref="Waypoint"/> on which asssit was requested</param>
            /// <returns><see cref="Bot"/> which requested the assistance on <see cref="Waypoint"/> <paramref name="wp"/></returns>
            internal virtual Bot this[Waypoint wp]
            {
                get
                {
                    if (wp == null)
                        throw new ArgumentNullException(nameof(wp), "parameter was null");
                    Bot returnBot = null;
                    double bestTime = double.MaxValue;
                    //for each bot search all assist locations to find which bot arrives quickest
                    foreach (var bot in Bots)
                    {
                        int pos = 0;
                        int assistantCount = Assistant[bot].Count;
                        for (var node = AssistanceLocations[bot].First; node != null; node = node.Next, pos++)
                        {
                            bool doesntHaveAssistanceAtLocation = (pos >= assistantCount);
                            if (doesntHaveAssistanceAtLocation && node.Value.Item1 == wp && node.Value.Item2 < bestTime)
                            {
                                returnBot = bot;
                                bestTime = node.Value.Item2;
                            }
                        }
                    }
                    return returnBot;
                }
            }
            #endregion

            #region IEnumerable implementation
            /// <summary>
            /// Implementation of IEnumerable interface, used in foreach loops
            /// </summary>
            public IEnumerator<Tuple<Bot, Waypoint, double>> GetEnumerator()
            {   //go through every bot, and for every bot go through every (location, time) pair
                //List<Bot> botList = Bots.ToList();
                foreach (var bot in Bots)
                //foreach (var bot in botList)
                {
                    if (AssistanceLocations[bot].Count == 0) continue;
                    for (var node = AssistanceLocations[bot].First; node != null; node = node.Next)
                        yield return new Tuple<Bot, Waypoint, double>(bot, node.Value.Item1, node.Value.Item2);
                }
            }
            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }
            #endregion

            #region Methods

            /// <summary>
            /// Stores <paramref name="destinationWaypoint"/> as a new assist location for a given <paramref name="bot"/>
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> which needs assistance</param>
            /// <param name="destinationWaypoint"><see cref="Waypoint"/> on which the assistance is needed</param>
            internal void AddAssistLocation(Bot bot, Waypoint destinationWaypoint, bool futureRequest)
            {
                //parameter check
                if (bot == null)
                    throw new ArgumentNullException(nameof(bot), "parameter was null");
                if (destinationWaypoint == null)
                    throw new ArgumentNullException(nameof(destinationWaypoint), "parameter was null");

                if (AssistanceLocations.ContainsKey(bot))
                {
                    //location was already added as a future request, no need to do anything
                    if (!futureRequest && AssistanceLocations[bot].First.Value.Item1 == destinationWaypoint)
                        return;

                    //first position should always be where bot is currently heading, if it is not, abort whole chain

                    int problemFlag = 0;

                    if (!futureRequest && AssistanceLocations[bot].Count > 0 &&
                        AssistanceLocations[bot].First.Value.Item1 != destinationWaypoint)
                    {
                        //get assistants that need aborting
                        var assistants = new HashSet<MateBot>(Assistant[bot]);
                        //ignore all the assistants that are already in aborting process
                        assistants.ExceptWith(BotsInProcessOfAborting);
                        //flag new assistants to be in aborting process
                        BotsInProcessOfAborting.UnionWith(assistants);

                        foreach (var assistant in assistants)
                        {   //start abort process on assistant
                            AbortAssistance(assistant);
                            //when assistant finishes it's abort process, unflag it
                            BotsInProcessOfAborting.Remove(assistant);
                        }

                        MateScheduler.NotifyFutureRequestSkipped(bot, GetRegisteredLocationsBefore(bot, destinationWaypoint));

                        //clear remaining AssistanceLocations[bot] (there should be only one --- a sole survivor)
                        AssistanceLocations[bot].Clear();

                        // called when the destination waypoint of the robot is not in AssistanceLocations so all locations are removed
                        // and only the destination waypoint is added, (added in RequestAssistance when this function is complete
                        MateScheduler.itemTable[bot.ID].RemoveAfterIndex(0);

                        problemFlag = 1;
                    }

                    if (AssistanceLocations[bot].Any(wd => wd.Item1 == destinationWaypoint)) return;

                    AssistanceLocations[bot].AddLast(new Tuple<Waypoint, double>(destinationWaypoint, double.MaxValue));
                    string adr = MateScheduler.GetBotCurrentItemAddress(bot, destinationWaypoint);
                    MateScheduler.itemTable[bot.ID].AddPickingAddress(adr);
                }
                else
                {
                    //create new AssistanceLocation entry
                    LinkedList<Tuple<Waypoint, double>> ll = new LinkedList<Tuple<Waypoint, double>>();
                    ll.AddLast(new Tuple<Waypoint, double>(destinationWaypoint, double.MaxValue));
                    AssistanceLocations.Add(bot, ll);
                    //MateScheduler.AddPickingLocation(bot.ID, MateScheduler.GetBotCurrentItemAddress(bot, destinationWaypoint), 0);
                    string adr = MateScheduler.GetBotCurrentItemAddress(bot, destinationWaypoint);
                    MateScheduler.itemTable[bot.ID].AddPickingAddress(adr);
                }
    
            }
            /// <summary>
            /// Updates predicted time to start assisting <paramref name="bot"/> on <paramref name="waypoint"/> 
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> which arrival time is updated</param>
            /// <param name="waypoint"><see cref="Waypoint"/> to where <paramref name="bot"/> is going</param>
            /// <param name="arrivalTime">New time at which the <paramref name="bot"/> is expected to arrive</param>
            internal void UpdateArrivalTime(Bot bot, Waypoint waypoint, double arrivalTime)
            {
                try
                {
                    this[bot, waypoint] = arrivalTime;
                }
                catch (ArgumentNullException e)
                {
                    throw e;
                }
                catch (KeyNotFoundException e)
                {
                    throw e;
                }
            }
            /// <summary>
            /// Adds <paramref name="mb"/> as a new assistant to <paramref name="bot"/> on a given <paramref name="waypoint"/>
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> to which assistant has been assigned</param>
            /// <param name="waypoint"><see cref="Waypoint"/> where assistance is assigned</param>
            /// <param name="mb"><see cref="MateBot"/> that will be assisting</param>
            internal void AssistantAssigned(Bot bot, Waypoint waypoint, MateBot mb)
            {
                //if matebot is registered as an assistant somewhere other then to bot, sanitize
                if (!BotsInProcessOfAborting.Contains(mb))
                    BotsInProcessOfAborting.Add(mb);
                RemoveMateInfo(mb);
                BotsInProcessOfAborting.Remove(mb);

                //assign mb to bot
                Assistant[bot].AddLast(mb);
                if (Assistant[bot].Count != AssistanceLocations[bot].Count) //sanity check
                    throw new Exception("something went wrong. New MateBot can only be assinged to the last location!");
                bot.OnAssistantAssigned();
                mb.OnBeingAssigned(Assistant[bot].Count);

                //add future location if needed
                AddFutureLocation(bot, waypoint);
            }

            public void RemoveFutureLocation(Waypoint location, Bot currentBot)
            {
                foreach (var bot in AssistanceLocations.Keys)
                {
                    // this is the bot that triggered removal
                    if (bot == currentBot) continue;
                    // if some other bot contains the location, remove them
                    int index = AssistanceLocations[bot].Select(t => t.Item1).ToList().IndexOf(location);
                    if (index != -1)
                    {
                        // TODO: how to know (wihtout index) to which mate is this location related
                        if (Assistant[bot].Count > index)
                        {
                            MateBot mate = Assistant[bot].ElementAt(index);
                            RemoveAssistanceTo(bot, mate, true);
                        }
                        AssistanceLocations[bot].RemoveLast();
                        // we also need to update the status table
                        // because it is not triggered automaticaly for above call
                        MateScheduler.itemTable[bot.ID].RemoveLastAddress();
                    }
                }
            }

            /// <summary>
            /// Removes <paramref name="mate"/> as an assistant to <paramref name="bot"/>
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> which assistance to is being aborted</param>
            /// <param name="mate"><see cref="MateBot"/> whose assistance is being removed</param>
            /// <param name="aborted">flag indicating that assistance is being removed</param>
            internal void RemoveAssistanceTo(Bot bot, MateBot mate, bool aborted)
            {
                int PositionIdx = 0;
                for (var node = Assistant[bot].First; node != null; node = node.Next, PositionIdx++)
                    if (node.Value == mate)//mb is registered as an assistant somewhere else, sanitize
                    {
                        RemoveAssistanceTo(bot, mate, PositionIdx, aborted);
                        if (!aborted)
                        {
                            AddNextLocation(bot);
                        }
                        break;
                    }
            }

            /// <summary>
            /// Registers new assist location from <paramref name="bot"/> if possible
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> whose location will be registered</param>
            private void AddNextLocation(Bot bot)
            {
                Waypoint lastLocation = Assistant[bot].Count > 0 ? bot.GetLocationAfter(Assistant[bot].Count) : bot.CurrentWaypoint; //bot has extra state for current item, that is why last location is on Assistant[bot].Count and not on Count - 1
                lastLocation ??= bot.CurrentWaypoint;

                AddFutureLocation(bot, lastLocation, 1); //since first location was deleted, add location with offset 1
            }

            /// <summary>
            /// If possible, registers location of <paramref name="bot"/> that comes after <paramref name="waypoint"/> 
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> whose location will be registered</param>
            /// <param name="waypoint">last <see cref="Waypoint"/> that was registered</param>
            protected virtual void AddFutureLocation(Bot bot, Waypoint waypoint, int locationOffset = 0)
            {
                //if bot has less assistance assigned then it's allowed prediction depth, add future location
                //Also, future locations will be added only if we are not using see-off scheduling strategy
                if (!AssistanceLocations.ContainsKey(bot) || AssistanceLocations[bot].Count < PredictionDepth)
                {
                    // add next location of this bot, if such exists, to available locations
                    Waypoint nextLocation = bot.GetLocationAfter(Assistant[bot].Count + locationOffset);
                    if (nextLocation != null)
                    {
                        MateScheduler.RequestAssistance(bot, nextLocation, true);
                        BotNormal tempBot = new BotNormal(bot as BotNormal)
                        {
                            CurrentWaypoint = waypoint,
                            X = waypoint.X,
                            Y = waypoint.Y
                        };
                        double currentArrivalTime;
                        try
                        {
                            currentArrivalTime = this[bot, waypoint];
                            MateScheduler.UpdateArrivalTime(bot, nextLocation, currentArrivalTime + MateBot.AverageAssistTime +
                                tempBot.Instance.Controller.PathManager.PredictArrivalTime(tempBot, nextLocation, true) //current + traversal time
                                - bot.Instance.Controller.CurrentTime);
                        }
                        catch (ArgumentException e)
                        {
                            if (e.ParamName == "wp") //waypoint was not present in AssistLocations, could be just removed by RemoveAssistance
                                MateScheduler.UpdateArrivalTime(bot, nextLocation,
                                    tempBot.Instance.Controller.PathManager.PredictArrivalTime(tempBot, nextLocation, true));
                        }

                    }
                }
            }

            /// <summary>
            /// Returns <see cref="Bot"/> that <paramref name="mb"/> was registered to assist
            /// </summary>
            /// <param name="mb"><see cref="MateBot"/> for which search is being performed</param>
            /// <returns><see cref="Bot"/> that <paramref name="mb"/> was registed to assist</returns>
            internal IEnumerable<Bot> GetBotsAssistedBy(MateBot mb)
            {
                foreach (var bot in Assistant.Keys)
                {
                    if (Assistant[bot].Count == 0) continue; //this bot has no registered assistance
                    for (var node = Assistant[bot].First; node != null; node = node.Next)
                        if (node.Value == mb)
                            yield return bot;
                }
            }
            /// <summary>
            /// Removes all assistance info about <paramref name="bot"/>
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> whose info will be removed</param>
            internal void RemoveInfoAbout(Bot bot)
            {
                //first abort assistance of every assistant (in the process assistant info is also removed)
                //don't start aborting process on assistants that are already in the process of aborting
                var assistants = new HashSet<MateBot>(Assistant[bot]);
                assistants.ExceptWith(BotsInProcessOfAborting);
                BotsInProcessOfAborting.UnionWith(assistants);
                //iterate only over new assistants in need of aborting
                foreach (var assistant in assistants)
                {
                    AbortAssistance(assistant);
                    BotsInProcessOfAborting.Remove(assistant);
                }
                //secondly, remove AssistanceLocation related info that was left 
                if (AssistanceLocations.ContainsKey(bot))
                    AssistanceLocations.Remove(bot);
            }
            /// <summary>
            /// Removes all assistance info about <paramref name="mate"/>
            /// </summary>
            /// <param name="mate"><see cref="MateBot"/> whose info will be removed</param>
            internal void RemoveMateInfo(MateBot mate)
            {
                //remove assistance info for every bot mate was register to assist 
                foreach (Bot bot in GetBotsAssistedBy(mate))
                {
                    RemoveAssistanceTo(bot, mate, true);
                }
            }
            /// <summary>
            /// Removes only <paramref name="bot"/>'s assist location at <paramref name="index"/>. Default <paramref name="index"/> is 0 
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> whose assist location will be removed</param>
            /// <param name="index">0-bsased index of the location to be removed</param>
            internal void RemoveAssistLocation(Bot bot, int index = 0)
            {
                // Called only from SeeOFFMS, remooving the first location assigned to bot
                MateScheduler.itemTable[bot.ID].CompleteLocationAtIndex(0);
                MateScheduler.itemTable[bot.ID].RemoveAtIndex(0);

                var locationToRemove = AssistanceLocations[bot].ElementAt(index);
                AssistanceLocations[bot].Remove(locationToRemove);
                //if no assistance location remains registered for bot, remove bot from data structure
                if (AssistanceLocations[bot].Count == 0) AssistanceLocations.Remove(bot);
            }
            /// <summary>
            /// Removes only the info about assistant of <paramref name="bot"/>. If second argument is not provided then the first assistant is removed
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> whose info about will be removed</param>
            /// <param name="mate">Optional parameter, if provided then <paramref name="mate"/> will be removed as assistant</param>
            internal void RemoveAssistantOf(Bot bot, MateBot mate = null)
            {
                //if mate is null, remove first assistant
                if (mate == null)
                    Assistant[bot].RemoveFirst();
                //else remove mate
                else
                    Assistant[bot].Remove(mate);
            }
            /// <summary>
            /// Gets which assist position in order is <paramref name="location"/> for a given <paramref name="bot"/>
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> which is in need of assistance at <paramref name="location"/></param>
            /// <param name="location"><see cref="Waypoint"/> at which <paramref name="bot"/> needs assistance</param>
            /// <returns></returns>
            internal int AssistOrder(Bot bot, Waypoint location)
            {
                int i = 1;
                for (var node = AssistanceLocations[bot].First; node != null; node = node.Next, i++)
                    if (node.Value.Item1 == location) return i;
                return int.MaxValue;
            }
            /// <summary>
            /// Gets assistant to <paramref name="bot"/> assigned on the first location
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> whose assistant will be returned</param>
            /// <returns>null if bot has no assistant<returns>
            internal MateBot GetAssistant(Bot bot)
            {
                return Assistant[bot].First?.Value;
            }
            /// <summary>
            /// Checks if <paramref name="mate"/> is registered as an assistant to <paramref name="bot"/> on a given <paramref name="location"/>
            /// </summary>
            /// <param name="mate"><see cref="MateBot"/> which is giving assistance</param>
            /// <param name="bot"><see cref="Bot"/> which is getting assistance</param>
            /// <param name="location"><see cref="Waypoint"/> at which <paramref name="mate"/> was suposed to give assistance to <paramref name="bot"/></param>
            /// <returns> <see langword="true"/> if <paramref name="mate"/> is giving assitance to <paramref name="bot"/> at a given <paramref name="location"/>. <see langword="false"/> otherwise</returns>
            internal bool IsAssisting(MateBot mate, Bot bot, Waypoint location)
            {
                if (!AssistanceLocations.ContainsKey(bot)) return false;
                //get registered locations for a given bot
                List<Tuple<Waypoint, double>> locations = AssistanceLocations[bot].ToList();
                List<MateBot> botAssistants = Assistant[bot].ToList();
                if (locations.Count == 0) return false;
                //search for a given location and get it's index
                List<Tuple<Waypoint, int>> indexedLocations = locations.Select((twd, i) => new Tuple<Waypoint, int>(twd.Item1, i)).
                    Where(twi => twi.Item1 == location).ToList();
                //if location is not registered for a bot, return false
                if (indexedLocations.Count() == 0) return false;
                //check if mate is registered as an assistant on any of the found locations
                foreach (var indexedLocation in indexedLocations)
                {
                    if (botAssistants.Count() <= indexedLocation.Item2) continue;
                    if (botAssistants.ElementAt(indexedLocation.Item2) == mate)
                        return true;
                }
                //mate was not registered
                return false;

            }
            /// <summary>
            /// 
            /// </summary>
            /// <param name="bot"></param>
            /// <param name="mate"></param>
            /// <returns></returns>
            internal Waypoint GetAssistLocation(Bot bot, MateBot mate)
            {
                if (!AssistanceLocations.ContainsKey(bot)) return null;
                int index = Assistant[bot].IndexOf(mate);
                if (index == -1) return null;
                if (AssistanceLocations[bot].Count <= index) return null;
                return AssistanceLocations[bot].ElementAt(index).Item1;
            }
            /// <summary>
            /// Checks if <paramref name="bot"/> already has assigned assistance at <paramref name="location"/>
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> whose assistance is checked</param>
            /// <param name="location"><see cref="Waypoint"/> where assistance will be checked</param>
            /// <returns><see langword="true"/> if someone is already registered as an assistant to <paramref name="bot"/>, <see langword="false"/> otherwise</returns>
            internal bool IsSomeoneAssisting(Bot bot, Waypoint location)
            {
                if (bot == null)
                    throw new ArgumentNullException(nameof(bot) + " was null!");
                if (location == null)
                    throw new ArgumentNullException(nameof(location) + " was null!");

                //if AssistanceLocations does not contain bot, then no one is assisting bot
                if (!AssistanceLocations.ContainsKey(bot))
                    return false;

                //search AssistanceLocations[bot] for location
                int position = 1;
                foreach(var entry in AssistanceLocations[bot])
                {
                    if (entry.Item1 == location)
                        break;
                    position++;
                }

                //if position is greater then count, it means loction was not present in AssistanceLocaiton[bot]
                if (position > AssistanceLocations[bot].Count)
                    return false;

                //if position is less or eaqual to assistant count then someone is registered as an assistant to location
                return position <= Assistant[bot].Count;
            }
            /// <summary>
            /// Retrurns locations which were registed by <paramref name="bot"/> before <paramref name="destinationWaypoint"/> was reigstered
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> that did the reservations</param>
            /// <param name="destinationWaypoint"></param>
            /// <returns></returns>
            protected IEnumerable<Waypoint> GetRegisteredLocationsBefore(Bot bot, Waypoint destinationWaypoint)
            {
                bool done = false;
                foreach(var location in AssistanceLocations[bot])
                {
                    if (done) continue;
                    if (location.Item1 == destinationWaypoint)
                    {
                        done = true;
                        continue;
                    }
                    yield return location.Item1;
                }
            }
            /// <summary>
            /// Removes <paramref name="mb"/> as an assistant of <paramref name="bot"/> at location index <paramref name="position"/> 
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> whose assistance will be removed</param>
            /// <param name="mb"><see cref="MateBot"/> who will no longer be assisting <paramref name="bot"/></param>
            /// <param name="position">Zero based index of the position where the assistance is being removed</param>
            /// <param name="aborted"><see cref="bool"/> which indicates if assistance is aborted or if it ended</param>
            private void RemoveAssistanceTo(Bot bot, MateBot mb, int position, bool aborted)
            {
                //parameter checking
                if (!AssistanceLocations.ContainsKey(bot))
                    throw new KeyNotFoundException("bot in variable " + nameof(bot) + " was not found!");
                if (AssistanceLocations[bot].Count <= position)
                    throw new ArgumentOutOfRangeException("there are no " + nameof(position) + " elements");
                if (Assistant[bot].Count <= position)
                    throw new ArgumentOutOfRangeException("there are no " + nameof(position) + " elements");
                if (Assistant[bot].ElementAt(position) != mb && Assistant[bot].ElementAt(position) != null)
                    throw new Exception("element at position" + nameof(position) + "is different then expected");

                //if assistance was aborted, then we need to abort all the future assists for this bot
                if (aborted)
                {   //remove AssistanceLocation info 
                    var removedAssistants = new HashSet<MateBot>(Assistant[bot].CutOffAt(position));
                    // removal happens here
                    MateScheduler.itemTable[bot.ID].ClearPickerAssignmentsAfterIndex(position);
                    removedAssistants.ExceptWith(BotsInProcessOfAborting);
                    BotsInProcessOfAborting.UnionWith(removedAssistants);
                    foreach (var assistant in removedAssistants)
                    {
                        AbortAssistance(assistant);
                        BotsInProcessOfAborting.Remove(assistant);
                    }

                    //remove AssistanceLocaitons[bot] that come after position+1 
                    //and notify MateScheduler which locations were removed
                    MateScheduler.NotifyLocationsRemoved(bot, AssistanceLocations[bot].CutOffAt(position + 1)); //needs to be +1 so that location at position remains visible
                    // only the last unassigned location remains open
                    if (MateScheduler.itemTable[bot.ID].addresses.Count > position + 1)
                        MateScheduler.itemTable[bot.ID].RemoveAfterIndex(position + 1);

                    bot.AssistanceAborted = true; //bot will update it's arrival time

                    List<string> remainingLocations = AssistanceLocations[bot].Select(t => MateScheduler.GetBotCurrentItemAddress(bot, t.Item1) ).ToList();
                }
                else//assistance was ended regularly, adjust variables accordingly
                {
                    if (position != 0) throw new Exception("only assist at position 0 can be ended regularly!");
                    Assistant[bot].RemoveFirst();
                    AssistanceLocations[bot].RemoveFirst();
                    Instance.Controller.MateScheduler.itemTable[bot.ID].CompleteLocationAtIndex(0);
                    Instance.Controller.MateScheduler.itemTable[bot.ID].RemoveAtIndex(0);
                }

                //if bot no longer needs assistance on some location, remove it from AssistanceLocations
                if (AssistanceLocations[bot].Count == 0)
                    AssistanceLocations.Remove(bot);
            }
            /// <summary>
            /// Helper method for removing info about assistance
            /// </summary>
            /// <param name="assistant">Assistant whose assistance is being aborted</param>
            private void AbortAssistance(MateBot assistant)
            {
                assistant.AbortAssist();
            }
            /// <summary>
            /// Helper method which increments all the nodes following and excluding <paramref name="startNode"/> by <paramref name="valueDiff"/> 
            /// </summary>
            /// <param name="startNode">Node after which all the nodes will be incremented</param>
            /// <param name="valueDiff">Value by which nodes will be incremented</param>
            private void IncrementNextNodes(LinkedListNode<Tuple<Waypoint, double>> startNode, double valueDiff)
            {
                if (valueDiff < 1) return; //increment to small, reduce the number of objects created
                for (var node = startNode.Next; node != null; node = node.Next)
                    node.Value = new Tuple<Waypoint, double>(node.Value.Item1, node.Value.Item2 + valueDiff);
            }
            #endregion

            #region Properties
            /// <summary>
            /// Gets the number of available locations on which the assistance was requested
            /// </summary>
            internal int AvailableLocationsCount => AvailableRequests.Count(); //available locations are unique
            /// <summary>
            /// Locations are valid if arrival time is less then MaxValue
            /// </summary>
            internal virtual IEnumerable<Waypoint> ValidLocations
            {
                get
                {   //go through every registered location
                    foreach (var BWDtuple in this)
                        if (BWDtuple.Item3 < double.MaxValue)
                            yield return BWDtuple.Item2;
                }
            }
            /// <summary>
            /// Gets a subset of AvailableRequests where duplicate locations are removed
            /// </summary>
            internal IEnumerable<Waypoint> AvailableLocations
            {
                get
                {
                    return new HashSet<Waypoint>(AvailableRequests.Select(req => req.Item1)); //hashset removes duplicates
                }
            }
            /// <summary>
            /// Gets all valid (locations,time) pairs where no assistance have yet been assigned
            /// </summary>
            protected virtual IEnumerable<Tuple<Waypoint, double>> AvailableRequests
            {
                get
                {
                    //List<Bot> botList = AssistanceLocations.Keys.ToList();
                    foreach (var bot in Bots)
                    //foreach (Bot bot in botList)
                    {
                        // check if the collection was modified, i.e. a bot was removed
                        // this may not be necessary
                        if (!AssistanceLocations.Keys.Contains(bot)) continue;
                        var requestList = AssistanceLocations[bot];
                        int assistantsListCount = Assistant[bot].Count;
                        if (requestList.Count == assistantsListCount) continue;
                        int index = requestList.Count - 1;

                        for (var requestNode = requestList.Last; requestNode != null && assistantsListCount <= index; requestNode = requestNode.Previous, index--)
                        {
                            if (requestNode.Value.Item2 < double.MaxValue)
                                yield return requestNode.Value;
                        }
                    }
                }
            }
            /// <summary>
            /// Gets all the bots that have requested assistance
            /// </summary>
            protected IEnumerable<Bot> Bots => AssistanceLocations.Keys;
            /// <summary>
            /// internal data structure used to store asssitance location info
            /// </summary>
            public Dictionary<Bot, LinkedList<Tuple<Waypoint, double>>> AssistanceLocations { get; set; }
            /// <summary>
            /// Stores all the bots that are in the process of aborting (will have AbortAssistance() called upon them) 
            /// </summary>
            protected HashSet<MateBot> BotsInProcessOfAborting { get; set; }
            /// <summary>
            /// stores data about which MateBot is assigned to which Bot as an assistant
            /// </summary>
            protected Dictionary<Bot, LinkedList<MateBot>> Assistant { get; set; }
            /// <summary>
            /// Reference to this instance
            /// </summary>
            protected Instance Instance { get; set; }
            /// <summary>
            /// Reference to MateScheduler that is using this AssistLocations object
            /// </summary>
            protected MateScheduler MateScheduler { get; set; }
            #endregion
        }

        /// <summary>
        /// private singleton class used to store info about assist locations in SeeOff mate scheduling strategy
        /// </summary>
        public class SeeOffAssistLocations : AssistLocations
        {
            #region Constructor and Indexers
            public SeeOffAssistLocations(Instance instance, SeeOffMateScheduler mateScheduler) : base(instance, mateScheduler) { }

            /// <summary>
            /// Indexer which returns the first <see cref="Bot"/> that requested the assistance on <paramref name="wp"/> but has no assistance assigned
            /// </summary>
            /// <param name="wp"><see cref="Waypoint"/> on which asssit was requested</param>
            /// <returns><see cref="Bot"/> which requested the assistance on <see cref="Waypoint"/> <paramref name="wp"/></returns>
            internal override Bot this[Waypoint wp]
            {
                get
                {
                    if (wp == null)
                        throw new ArgumentNullException(nameof(wp), "parameter was null");
                    Bot returnBot = null;
                    double bestTime = double.PositiveInfinity;
                    //for each bot search all assist locations to find which bot arrives quickest
                    foreach (var bot in Bots)
                    {
                        int pos = 0;
                        int assistantCount = Assistant[bot].Count;
                        for (var node = AssistanceLocations[bot].First; node != null; node = node.Next, pos++)
                        {
                            bool doesntHaveAssistanceAtLocation = (pos >= assistantCount);
                            if (doesntHaveAssistanceAtLocation && node.Value.Item1 == wp && node.Value.Item2 < bestTime)
                            {
                                returnBot = bot;
                                bestTime = node.Value.Item2;
                            }
                        }
                    }
                    return returnBot;
                }
            }
            #endregion

            #region Methods
            /// <summary>
            /// If possible, registers location of <paramref name="bot"/> that comes after <paramref name="waypoint"/> 
            /// </summary>
            /// <param name="bot"><see cref="Bot"/> whose location will be registered</param>
            /// <param name="waypoint">last <see cref="Waypoint"/> that was registered</param>
            protected override void AddFutureLocation(Bot bot, Waypoint waypoint, int locationOffset = 0)
            {
                //ignore future locations in SeeOff scheduling
            }
            #endregion

            #region Properties
            /// <summary>
            /// Gets all valid (locations,time) pairs where no assistance have yet been assigned
            /// </summary>
            protected override IEnumerable<Tuple<Waypoint, double>> AvailableRequests
            {
                get
                {
                    foreach (var bot in Bots)
                    {
                        if (AssistanceLocations[bot].Count > Assistant[bot].Count)
                            yield return AssistanceLocations[bot].First.Value;
                    }
                }
            }
            #endregion
        }

        public class WaveAssistLocations : AssistLocations
        {
            public WaveAssistLocations (Instance instance, WaveMateScheduler mateScheduler, WideWave wave) : base(instance, mateScheduler)
            {
                Wave = wave;
            }

            #region Properties
            internal override IEnumerable<Waypoint> ValidLocations
            {
                get
                {   //go through every registered location
                    foreach (var BWDtuple in this)
                        if (Wave.Contains(BWDtuple.Item2) &&
                            BWDtuple.Item3 < double.MaxValue)
                            yield return BWDtuple.Item2;
                }
            }

            /// <summary>
            /// Gets all valid (locations,time) pairs where no assistance have yet been assigned
            /// </summary>
            protected override IEnumerable<Tuple<Waypoint, double>> AvailableRequests
            {
                get
                {
                    foreach (var bot in Bots)
                    {
                        var requestList = AssistanceLocations[bot];
                        int assistantsListCount = Assistant[bot].Count;
                        if (requestList.Count == assistantsListCount) continue;
                        int index = requestList.Count - 1;

                        for (var requestNode = requestList.Last; requestNode != null && assistantsListCount <= index; requestNode = requestNode.Previous, index--)
                        {
                            if (requestNode.Value.Item2 < double.MaxValue && Wave.Contains(requestNode.Value.Item1))
                                yield return requestNode.Value;
                        }
                    }

                }
            }

            public WideWave Wave { get; set; }
            #endregion
        }
    }
}