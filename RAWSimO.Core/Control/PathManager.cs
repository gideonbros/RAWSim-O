// If defined, activates assertion of tractable requests to the path planners
//#define DEBUGINTRACTABLEREQUESTS

using RAWSimO.Core.Bots;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Helper;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.Waypoints;
using RAWSimO.MultiAgentPathFinding;
using RAWSimO.MultiAgentPathFinding.Algorithms.AStar;
using RAWSimO.MultiAgentPathFinding.DataStructures;
using RAWSimO.MultiAgentPathFinding.Elements;
using RAWSimO.MultiAgentPathFinding.Methods;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Diagnostics;
using RAWSimO.Core.Statistics;
using RAWSimO.Core.IO;
using RAWSimO.Core.Geometrics;
using RAWSimO.Toolbox;
using RAWSimO.MultiAgentPathFinding.Physic;

namespace RAWSimO.Core.Control
{
    /// <summary>
    /// The path manager implementation.
    /// </summary>
    public abstract class PathManager : IUpdateable, IStatTracker
    {
        #region Attributes

        /// <summary>
        /// path finding optimizer
        /// </summary>
        public bool Log;

        /// <summary>
        /// Gets or sets the data points.
        /// </summary>
        /// <value>
        /// The data points.
        /// </value>
        public List<PathFindingDatapoint> StatDataPoints { get; set; }

        /// <summary>
        /// path finding optimizer
        /// </summary>
        public PathFinder PathFinder;

        /// <summary>
        /// current instance
        /// </summary>
        public Instance Instance;

        /// <summary>
        /// ids of the way points
        /// </summary>
        protected BiDictionary<Waypoint, int> _waypointIds;

        /// <summary>
        /// Waypoint - graph node ID pairs 
        /// </summary>
        public BiDictionary<Waypoint, int> WaypointIDs { get { return _waypointIds; } } 

        /// <summary>
        /// ids of the elevators
        /// </summary>
        protected Dictionary<int, Elevator> _elevatorIds;

        /// <summary>
        /// Contains meta information about the nodes that is passed to the path planning engine.
        /// </summary>
        private Dictionary<Waypoint, NodeInfo> _nodeMetaInfo;

        /// <summary>
        /// The queue managers
        /// </summary>
        private Dictionary<Waypoint, QueueManager> _queueManagers;

        /// <summary>
        /// Location manager controlling queuing behaviour
        /// </summary>
        public LocationManager _locationManager;

        /// <summary>
        /// The reservation table
        /// </summary>
        protected ReservationTable _reservationTable;

        /// <summary>
        /// The reservations
        /// </summary>
        protected Dictionary<BotNormal, List<ReservationTable.Interval>> _reservations;

        /// <summary>
        /// The stop watch
        /// </summary>
        protected Stopwatch _stopWatch;

        /// <summary>
        /// The time stamp of the last optimization call
        /// </summary>
        protected double _lastCallTimeStamp = double.MinValue / 10.0;

        #endregion

        #region core

        /// <summary>
        /// Initializes a new instance of the <see cref="PathManager"/> class.
        /// </summary>
        protected PathManager(Instance instance)
        {
            //instance
            this.Instance = instance;
            this.Log = true; //log => high memory consumption
        }

        /// <summary>
        /// convert way point graph -> graph for multi agent path finding
        /// </summary>
        /// <returns>The generated graph.</returns>
        protected Graph GenerateGraph()
        {
            var waypointGraph = Instance.WaypointGraph;

            //Give every way point an unique id
            _waypointIds = new BiDictionary<Waypoint, int>();
            int id = 0;
            foreach (var tier in waypointGraph.GetWayPoints())
                foreach (var waypoint in tier.Value)
                    _waypointIds.Add(waypoint, id++);

            //initiate queue managers
            _queueManagers = new Dictionary<Waypoint, QueueManager>();
            foreach (IQueuesOwner queueOwner in Instance.InputStations.Cast<IQueuesOwner>().Union(Instance.OutputStations.Cast<IQueuesOwner>().Union(Instance.Elevators.Cast<IQueuesOwner>())))
                foreach (var queue in queueOwner.Queues)
                    _queueManagers.Add(queue.Key, new QueueManager(queue.Key, queue.Value, this));

            _locationManager = new LocationManager(Instance.MapRowCount, Instance.MapColumnCount, Instance);

            //create the lightweight graph
            var graph = new Graph(_waypointIds.Count);

            // Create collection of all node meta information
            _nodeMetaInfo = Instance.Waypoints.ToDictionary(k => k, v => new NodeInfo()
            {
                ID = _waypointIds[v],
                IsQueue = v.QueueManager != null,
                IsInputPalletStand = v.InputStation != null || v.InputPalletStand != null,
                IsOutputPalletStand = v.OutputStation != null || v.OutputPalletStand != null,
                QueueTerminal = v.QueueManager != null ? _waypointIds[v.QueueManager.QueueWaypoint] : -1,
            });
            // Submit node meta info to graph
            foreach (var waypoint in Instance.Waypoints)
                graph.NodeInfo[_waypointIds[waypoint]] = _nodeMetaInfo[waypoint];

            //create edges
            foreach (var tier in waypointGraph.GetWayPoints())
            {
                foreach (var waypointFrom in tier.Value)
                {

                    //create Array
                    Edge[] outgoingEdges = new Edge[waypointFrom.Paths.Count(w => w.Tier == tier.Key)];
                    ElevatorEdge[] outgoingElevatorEdges = new ElevatorEdge[waypointFrom.Paths.Count(w => w.Tier != tier.Key)];

                    //fill Array
                    int edgeId = 0;
                    int elevatorEdgeId = 0;
                    int row;
                    int col;
                    foreach (var waypointTo in waypointFrom.Paths)
                    {
                        //elevator edge
                        if (waypointTo.Tier != tier.Key)
                        {
                            var elevator = Instance.Elevators.First(e => e.ConnectedPoints.Contains(waypointFrom) && e.ConnectedPoints.Contains(waypointTo));

                            outgoingElevatorEdges[elevatorEdgeId++] = new ElevatorEdge
                            {
                                From = _waypointIds[waypointFrom],
                                To = _waypointIds[waypointTo],
                                Distance = 0,
                                TimeTravel = elevator.GetTiming(waypointFrom, waypointTo),
                                Reference = elevator
                            };
                        }
                        else
                        {
                            //normal edge
                            int angle = Graph.RadToDegree(Math.Atan2(waypointTo.Y - waypointFrom.Y, waypointTo.X - waypointFrom.X));
                            //costs mapping from CSV
                            Instance.XYToRowCol(waypointFrom.X, waypointFrom.Y, out row, out col);
                            char[] costs = Instance.MapArray[row][col].ToCharArray();
                            char cost_letter = waypointTo.X > waypointFrom.X ? costs[0] : waypointTo.Y > waypointFrom.Y ? costs[1] : waypointTo.Y < waypointFrom.Y ? costs[2] : waypointTo.X < waypointFrom.X ? costs[3] : '!';
                            double cost = cost_letter == 'n' ? Instance.SettingConfig.NeutralCost : cost_letter == 'p' ? Instance.SettingConfig.PreferredCost : cost_letter == 'u' ? Instance.SettingConfig.UnpreferredCost : double.PositiveInfinity;
                            Edge edge = new Edge
                            {
                                From = _waypointIds[waypointFrom],
                                To = _waypointIds[waypointTo],
                                Distance = waypointFrom.GetDistance(waypointTo),
                                Angle = (short)angle,
                                FromNodeInfo = _nodeMetaInfo[waypointFrom],
                                ToNodeInfo = _nodeMetaInfo[waypointTo],
                                Cost = cost,
                            };
                            outgoingEdges[edgeId++] = edge;
                        }
                    }

                    //set Array
                    graph.Edges[_waypointIds[waypointFrom]] = outgoingEdges;
                    graph.PositionX[_waypointIds[waypointFrom]] = waypointFrom.X;
                    graph.PositionY[_waypointIds[waypointFrom]] = waypointFrom.Y;
                    if (outgoingElevatorEdges.Length > 0)
                        graph.ElevatorEdges[_waypointIds[waypointFrom]] = outgoingElevatorEdges;
                }
            }

            return graph;
        }

        /// <summary>
        /// Updates all lock and obstacle information for all connections.
        /// </summary>
        private void UpdateLocksAndObstacles()
        {
            // First reset locked / occupied by obstacle info
            foreach (var nodeInfo in _nodeMetaInfo)
            {
                nodeInfo.Value.IsLocked = false;
                nodeInfo.Value.IsObstacle = false;
            }
            // Prepare waypoints blocked within the queues
            IEnumerable<Waypoint> queueBlockedNodes = _queueManagers.SelectMany(m => m.Value.LockedWaypoints.Keys);
            // Prepare waypoints blocked by idling robots
            IEnumerable<Waypoint> idlingAgentsBlockedNodes = Instance.SettingConfig.DimensionlessBots ? new List<Waypoint>() : 
                                                             Instance.Bots.Cast<BotNormal>().Where(b => b.IsResting()).Select(b => b.CurrentWaypoint);
            // Prepare obstacles by placed pods
            IEnumerable<Waypoint> podPositions = Instance.WaypointGraph.GetPodPositions();
            // Now update with new locked / occupied by obstacle info
            foreach (var lockedWP in queueBlockedNodes.Concat(idlingAgentsBlockedNodes))
                _nodeMetaInfo[lockedWP].IsLocked = true;
            foreach (var obstacleWP in podPositions)
                _nodeMetaInfo[obstacleWP].IsObstacle = true;
            // Get blocked nodes
            // Prepare all locked nodes
            var blockedNodes = new HashSet<int>(
                // Add waypoints locked by the queue managers
                queueBlockedNodes.Select(w => _waypointIds[w])
                    // Add waypoints locked by not moving robots 
                    .Concat(idlingAgentsBlockedNodes.Select(w => _waypointIds[w])));
            // Get obstacles
            var obstacles = new HashSet<int>(podPositions.Select(wp => _waypointIds[wp]));
        }

        /// <summary>
        /// Predicts arrival time of <paramref name="bot"/> to <paramref name="goal"/>
        /// </summary>
        /// <param name="bot">Bot for which prediction is needed</param>
        /// <param name="goal"><see cref="Waypoint"/> to where <paramref name="bot"/> is headed</param>
        /// <param name="findNewPath">bool indicating if new path will be used</param>
        /// <returns>Predicted time of arrival</returns>
        public double PredictArrivalTime(BotNormal bot, Waypoint goal, bool findNewPath)
        {
            // Get way points
            Waypoint nextWaypoint = bot.NextWaypoint ?? bot.CurrentWaypoint;
            Waypoint finalDestination = goal;
            Waypoint destination = finalDestination;

            double currentTime = Instance.Controller.CurrentTime;

            // Has the destination a queue?
            if (finalDestination != null && _queueManagers.ContainsKey(finalDestination))
                // Use the place in queue as the destination
                destination = _queueManagers[finalDestination].getPlaceInQueue(bot);

            // Create reservationToNextNode
            var reservationsToNextNode = findNewPath ? new List<ReservationTable.Interval>() : 
                                                       new List<ReservationTable.Interval>(_reservations[bot]);
            if (reservationsToNextNode.Count > 0)
                reservationsToNextNode.RemoveAt(reservationsToNextNode.Count - 1); //remove last blocking node

            // Create agent
            var agent = new Agent
            {
                ID = bot.ID,
                NextNode = _waypointIds[nextWaypoint],
                ReservationsToNextNode = reservationsToNextNode,
                ArrivalTimeAtNextNode = (reservationsToNextNode.Count == 0) ? currentTime : Math.Max(currentTime, reservationsToNextNode[reservationsToNextNode.Count - 1].End),
                OrientationAtNextNode = bot.GetTargetOrientation(),
                DestinationNode = _waypointIds[destination],
                FinalDestinationNode = _waypointIds[finalDestination],
                Path = bot.Path, //path reference => will be filled
                FixedPosition = bot.hasFixedPosition(),
                Resting = bot.IsResting(),
                CanGoThroughObstacles = false,
                Physics = new Physics(bot.Physics), //copy object to avoid messing up member values
                RequestReoptimization = bot.RequestReoptimization,
                Queueing = bot.IsQueueing,
                NextNodeObject = nextWaypoint,
                DestinationNodeObject = destination,
                IsMate = bot is MateBot ? true : false,
                DimensionlessBots = Instance.SettingConfig.DimensionlessBots
            };

            Waypoint actualCurrentWP = Instance.FindWpFromXY(bot.X, bot.Y);

            //get A* for bot/agent
            ReverseResumableAStar astar = null;
            if (PathFinder is FARMethod)
            {
                astar = (PathFinder as FARMethod).GetRRAstar(agent, findNewPath);
                astar.Search(_waypointIds[actualCurrentWP]);
            }   
            //
            //Arrival prediction algorithm
            //
            double traversalTime = 0;
            double rotationTime = Instance.layoutConfiguration.TurnSpeed / 4;
            Waypoint startWP = actualCurrentWP;
            List<int> segment;

            //calculate time spent on each path segment
            while (true)
            {
                //get the next path segment
                segment = astar.NodesUntilNextTurn(startWP.ID);
                //if there are no more segments, break out
                if (!segment.Any())
                    break;
                Waypoint endWP = _waypointIds[segment.Last()];
                //add the time needed to traverse the segment
                traversalTime += agent.Physics.getTimeNeededToMove(0, startWP.GetDistance(endWP));
                //add the time needed to rotate at the end of the segment
                traversalTime += rotationTime;
                //prepare variable values for the next segment
                startWP = endWP;
            }

            return traversalTime + currentTime;
        }

        /// <summary>
        /// Removes all reservations in path manager done by <paramref name="bot"/>
        /// </summary>
        /// <param name="bot"><see cref="BotNormal"/> whose info will be deleted</param>
        public void RemoveReservations(BotNormal bot)
        {
            if (_reservations[bot].Count > 0)
            {
                _reservationTable.Remove(_reservations[bot]);
                _reservations[bot].Clear();
            }
            return;
        }

        /// <summary>
        /// optimize path
        /// </summary>
        /// <param name="currentTime">The current time.</param>
        private void _reoptimize(double currentTime)
        {

            //statistics
            _statStart(currentTime);

            // Log timing
            DateTime before = DateTime.Now;

            // Get agents
            Dictionary<BotNormal, Agent> botAgentsDict;
            getBotAgentDictionary(out botAgentsDict, currentTime);
            // Update locks and obstacles
            UpdateLocksAndObstacles();

#if DEBUGINTRACTABLEREQUESTS
            // Check for intractable requests for the path planners
            IEnumerable<KeyValuePair<BotNormal, Agent>> blockedAgents = botAgentsDict.Where(agent => agent.Key.RequestReoptimization && /*agent.Value.NextNode != agent.Value.DestinationNode &&*/ blockedNodes.Contains(agent.Value.DestinationNode));
            if (blockedAgents.Any())
                Debug.Fail("Cannot navigate to a locked node!", "Agents (agent/next/dest/currType/nextType/destType):\n" + string.Join("\n",
                    blockedAgents.Select(a =>
                    {
                        Func<Waypoint, string> getNodeType = (Waypoint w) =>
                        {
                            return (
                                w == null ? "n" :
                                w.PodStorageLocation ? "s" :
                                w.InputStation != null ? "i" :
                                w.OutputStation != null ? "o" :
                                w.Elevator != null ? "e" :
                                w.IsQueueWaypoint ? "q" :
                                "w");
                        };
                        return
                            a.Value.ID + "/" +
                            a.Value.NextNode + "/" +
                            a.Value.DestinationNode + "/" +
                            getNodeType(a.Key.CurrentWaypoint) + "/" +
                            getNodeType(a.Key.NextWaypoint) + "/" +
                            getNodeType(a.Key.DestinationWaypoint);
                    })));
#endif

            //get path => optimize!
            //first find paths of all bots that are not MateBots
            var bots = botAgentsDict.Where(bap => !(bap.Key is MateBot)).Select(bap => bap.Key as IMovableStation).ToList();
            var agents = botAgentsDict.Where(bap => !(bap.Key is MateBot)).Select(bap => bap.Value).ToList();
            if (Instance.SettingConfig.DimensionlessBots)
            {
                //find path for each bot individually
                foreach (var bot in bots)
                {
                    List<IMovableStation> tempBotList = new List<IMovableStation>();
                    tempBotList.Add(bot);
                    List<Agent> tempAgentList = new List<Agent>();
                    tempAgentList.Add(botAgentsDict[bot as BotNormal]);

                    if (PathFinder is FARMethod)
                        (PathFinder as FARMethod).FindPaths(currentTime, tempAgentList, tempBotList);
                    else
                        PathFinder.FindPaths(currentTime, tempAgentList);
                }
            }
            else
            {
                //multi-agent path planning
                if (PathFinder is FARMethod)
                    (PathFinder as FARMethod).FindPaths(currentTime, agents, bots);
                else
                    PathFinder.FindPaths(currentTime, agents);
            }

            //then find path for every MateBot individually
            var mateAgentDict = botAgentsDict.Where(bap => bap.Key is MateBot);
            foreach(var mateAgent in mateAgentDict)
            {
                List<Agent> tempList = new List<Agent>();
                tempList.Add(mateAgent.Value);
                PathFinder.FindPaths(currentTime, tempList);
            }

            //assign paths from agents to bots
            foreach (var botAgent in botAgentsDict)
                botAgent.Key.Path = botAgent.Value.Path;

            // Calculate time it took to plan the path(s)
            Instance.Observer.TimePathPlanning((DateTime.Now - before).TotalSeconds);

            _statEnd(currentTime);
        }

        /// <summary>
        /// start the statistical output.
        /// </summary>
        /// <param name="currentTime">The current time.</param>
        private void _statStart(double currentTime)
        {
            if (!Log)
                return;

            //create data list
            if (StatDataPoints == null)
                StatDataPoints = new List<PathFindingDatapoint>();

            StatDataPoints.Add(new PathFindingDatapoint(currentTime, 0, Instance.Bots.Count(b => ((BotNormal)b).RequestReoptimization)));

            if (_stopWatch == null)
                _stopWatch = new Stopwatch();

            _stopWatch.Restart();
        }

        /// <summary>
        /// end the statistical output.
        /// </summary>
        /// <param name="currentTime">The current time.</param>
        private void _statEnd(double currentTime)
        {
            if (!Log)
                return;

            _stopWatch.Stop();

            //add elapsed seconds
            StatDataPoints[StatDataPoints.Count - 1].Runtime = _stopWatch.ElapsedMilliseconds / 1000.0;
        }

        /// <summary>
        /// create Agents from bot positions and destinations
        /// </summary>
        /// <returns>agents</returns>
        private void getBotAgentDictionary(out Dictionary<BotNormal, Agent> botAgentDictionary, double currentTime)
        {
            botAgentDictionary = new Dictionary<BotNormal, Agent>();

            foreach (var abot in Instance.Bots)
            {
                var bot = abot as BotNormal;

                // Get way points
                Waypoint nextWaypoint = (bot.NextWaypoint != null) ? bot.NextWaypoint : bot.CurrentWaypoint;
                Waypoint finalDestination = (bot.DestinationWaypoint == null) ? bot.CurrentWaypoint : bot.DestinationWaypoint;
                Waypoint destination = finalDestination;

                // Has the destination a queue?
                if ( !(bot is MateBot) && bot.DestinationWaypoint != null && _queueManagers.ContainsKey(bot.DestinationWaypoint))
                    // Use the place in queue as the destination
                    destination = _queueManagers[bot.DestinationWaypoint].getPlaceInQueue(bot);
                // If already within a queue, there is no path finding needed => Queue manager does it for us
                if (bot.IsQueueing)
                    continue;
                // Ignore bots for which origin matches destination
                if (nextWaypoint == destination)
                    continue;

                // Create reservationToNextNode
                var reservationsToNextNode = new List<ReservationTable.Interval>(_reservations[bot]);
                if (reservationsToNextNode.Count > 0)
                    reservationsToNextNode.RemoveAt(reservationsToNextNode.Count - 1); //remove last blocking node

                if (bot.NextWaypoint == null)
                    reservationsToNextNode.Clear();

                // Create agent
                var agent = new Agent
                {
                    ID = bot.ID,
                    NextNode = _waypointIds[nextWaypoint],
                    ReservationsToNextNode = reservationsToNextNode,
                    ArrivalTimeAtNextNode = (reservationsToNextNode.Count == 0) ? currentTime : Math.Max(currentTime, reservationsToNextNode[reservationsToNextNode.Count - 1].End),
                    OrientationAtNextNode = bot.GetTargetOrientation(),
                    DestinationNode = _waypointIds[destination],
                    FinalDestinationNode = _waypointIds[finalDestination],
                    Path = bot.Path, //path reference => will be filled
                    FixedPosition = bot.hasFixedPosition(),
                    Resting = bot.IsResting(),
                    CanGoThroughObstacles = false,
                    Physics = new Physics(bot.Physics),
                    RequestReoptimization = bot.RequestReoptimization,
                    Queueing = bot.IsQueueing,
                    NextNodeObject = nextWaypoint,
                    DestinationNodeObject = destination,
                    IsMate = bot is MateBot ? true : false,
                    IgnoreInputPalletStandQueue = bot.IgnoreInputPalletStandQueue,
                    IgnoreOutputPalletStandQueue = bot.IgnoreOutputPalletStandQueue,
                    DimensionlessBots = Instance.SettingConfig.DimensionlessBots
                };
                // Add agent
                botAgentDictionary.Add(bot, agent);

                Debug.Assert(nextWaypoint.Tier.ID == destination.Tier.ID);
            }
        }

        /// <summary>
        /// Gets the way point by node identifier.
        /// </summary>
        /// <param name="node">The node.</param>
        /// <returns></returns>
        public Waypoint GetWaypointByNodeId(int node)
        {
            return _waypointIds[node];
        }

        /// <summary>
        /// Gets the way point by node identifier.
        /// </summary>
        /// <param name="waypoint">The node.</param>
        /// <returns>The id of the node.</returns>
        public int GetNodeIdByWaypoint(Waypoint waypoint)
        {
            return _waypointIds[waypoint];
        }
        #endregion

        #region IUpdateable Members

        /// <summary>
        /// The next event when this element has to be updated.
        /// </summary>
        /// <param name="currentTime">The current time of the simulation.</param>
        /// <returns>The next time this element has to be updated.</returns>
        public double GetNextEventTime(double currentTime)
        {
            return double.PositiveInfinity;
        }

        /// <summary>
        /// Updates the specified last time.
        /// </summary>
        /// <param name="lastTime">The last time.</param>
        /// <param name="currentTime">The current time.</param>
        public virtual void Update(double lastTime, double currentTime)
        {
            //manage queues
            foreach (var queueManager in _queueManagers.Values)
                queueManager.Update();

            if (Instance.SettingConfig.UsingLocationManager)
                _locationManager.Update();

            //reorganize table
            if (_reservationTable == null)
                _initReservationTable();
            _reservationTable.Reorganize(currentTime);

            //check if minimum time span was exceeded
            if (_lastCallTimeStamp + Instance.ControllerConfig.PathPlanningConfig.Clocking > currentTime)
                return;

            //check if any bot request a re-optimization and reset the flag
            var reoptimize = Instance.Bots.Any(b => ((BotNormal)b).RequestReoptimization);

            //NextReoptimization < currentTime with < instead of <=, because we want to update in the next call.
            //So we wait until all bots have updated from lastTime to currentTime
            if (reoptimize)
            {
                //call re optimization
                _lastCallTimeStamp = currentTime;
                _reoptimize(currentTime);

                //reset flag
                foreach (var bot in Instance.Bots)
                    ((BotNormal)bot).RequestReoptimization = false;
            }

        }

        /// <summary>
        /// Notifies the path manager when the bot has a new destination.
        /// </summary>
        /// <param name="bot">The bot.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        internal void notifyBotNewDestination(BotNormal bot)
        {
            if (bot.DestinationWaypoint == null)
                return;

            if (_queueManagers.ContainsKey(bot.DestinationWaypoint))
                _queueManagers[bot.DestinationWaypoint].onBotJoinQueue(bot);
        }

        /// <summary>
        /// Checks weather the bot can go to the next way point without collisions.
        /// </summary>
        /// <param name="botNormal">The bot.</param>
        /// <param name="currentTime">The current time.</param>
        /// <param name="waypointStart">The way point start.</param>
        /// <param name="waypointEnd">The way point end.</param>
        /// <param name="blockCurrentWaypointUntil">Block duration.</param>
        /// <param name="rotationDuration">Rotation duration.</param>
        /// <returns></returns>
        public bool RegisterNextWaypoint(BotNormal botNormal, double currentTime, double blockCurrentWaypointUntil, double rotationDuration, Waypoint waypointStart, Waypoint waypointEnd, out List<int> blockedAgentsIdxs)
        {
            blockedAgentsIdxs = new List<int>();
            // MateBots can always register the next waypoint
            if (botNormal is MateBot)
            {
                if (_reservations[botNormal].Count > 0)
                {
                    _reservationTable.Remove(_reservations[botNormal]);
                    _reservations[botNormal].Clear();
                }
                return true;
            }

            //get checkpoints
            var tmpReservations = _reservationTable.CreateIntervals(currentTime, blockCurrentWaypointUntil + rotationDuration, botNormal.GetSpeed(), botNormal.Physics, _waypointIds[waypointStart], _waypointIds[waypointEnd], true);
            if (tmpReservations == null)
                return false; //no valid way point

            //remove current reservations for the bot
            _reservationTable.Remove(_reservations[botNormal]);

            // Bots can register the next waypoint if DimensionlessBots is set, but collisions in pallet stand queues are forbidden
            if (Instance.SettingConfig.DimensionlessBots && botNormal is MovableStation)
                tmpReservations.RemoveAll(r => !Instance.GetWaypointByID(r.Node).IsQueueWaypoint);

            //check if free
            var free = _reservationTable.IntersectionFree(tmpReservations, out blockedAgentsIdxs);

            if (free) 
                _reservations[botNormal] = tmpReservations;

            //(re)add intervals
            _reservationTable.Add(_reservations[botNormal], botNormal.ID);

            return free;
        }

        /// <summary>
        /// Initializes the reservation table.
        /// </summary>
        private void _initReservationTable()
        {
            _reservationTable = new ReservationTable(PathFinder.Graph, true, true);

            //lasy initialization reservations
            _reservations = new Dictionary<BotNormal, List<ReservationTable.Interval>>();
            foreach (BotNormal bot in Instance.Bots)
            {
                if (bot.CurrentWaypoint == null)
                    bot.CurrentWaypoint = bot.Instance.WaypointGraph.GetClosestWaypoint(bot.Instance.Compound.BotCurrentTier[bot], bot.X, bot.Y);

                _reservations.Add(bot, new List<ReservationTable.Interval>());
                if (bot is MateBot || Instance.SettingConfig.DimensionlessBots)
                    _reservations[bot].Add(new ReservationTable.Interval(_waypointIds[bot.CurrentWaypoint], 0, 0));
                else
                    _reservations[bot].Add(new ReservationTable.Interval(_waypointIds[bot.CurrentWaypoint], 0, double.PositiveInfinity));
                _reservationTable.Add(_reservations[bot][0], bot.ID);
            }
        }

        #endregion

        #region IStatTracker Members

        /// <summary>
        /// The callback that indicates that the simulation is finished and statistics have to submitted to the instance.
        /// </summary>
        public virtual void StatFinish() { /* Default case: do not flush any statistics */ }

        /// <summary>
        /// The callback indicates a reset of the statistics.
        /// </summary>
        public virtual void StatReset() { /* Default case: nothing to reset */ }

        #endregion

        #region Elevator Sequence
        /// <summary>
        /// Finds a sequence of elevators with must be visit to reach the end node.
        /// </summary>
        /// <param name="start">The start node.</param>
        /// <param name="end">The end node.</param>
        /// <param name="bot">The bot.</param>
        /// <param name="distance">The distance.</param>
        /// <returns>
        /// elevator sequence
        /// </returns>
        public List<Tuple<Elevator, Waypoint, Waypoint>> FindElevatorSequence(BotNormal bot, Waypoint start, Waypoint end, out double distance)
        {
            //find sequence in one code line
            return Instance.Controller.PathManager.PathFinder.FindElevatorSequence(_waypointIds[start], _waypointIds[end], bot.Physics, out distance).Select(e => Tuple.Create((Elevator)e.Item1, _waypointIds[e.Item2], _waypointIds[e.Item3])).ToList();
        }
        #endregion


        #region Location Manager

        public class LocationManager
        {

            /// <summary>
            /// Tables tracking the occupation and reservation on the map.
            /// Supports thread safe updates.
            /// </summary>
            public class TableStatus
            {
                private int[,] occupationTable, reservationTable;
                private Instance _instance;
                private System.Threading.Mutex tableMutex;

                public TableStatus(int rowCount, int colCount, Instance instance)
                {
                    _instance = instance;
                    occupationTable = new int[rowCount, colCount]; // initialized to 0 by default
                    reservationTable = new int[rowCount, colCount];
                    for (int i = 0; i < rowCount; ++i)
                    {
                        for (int j = 0; j < colCount; ++j)
                        {
                            occupationTable[i, j] = -1;
                            reservationTable[i, j] = -1;
                        }
                    }
                    tableMutex = new System.Threading.Mutex();
                }

                private void SetTable(int[,] table, int botID, Waypoint wp)
                {
                    tableMutex.WaitOne();
                    table[wp.Row, wp.Column] = botID;
                    tableMutex.ReleaseMutex();
                }

                private int GetTableEntry(int[,] table, Waypoint wp)
                {
                    int botID;
                    tableMutex.WaitOne();
                    botID = table[wp.Row, wp.Column];
                    tableMutex.ReleaseMutex();
                    return botID;
                }
                public void SetReservation(int botID, Waypoint wp) { SetTable(reservationTable, botID, wp); }
                public void SetOccupation(int botID, Waypoint wp) { SetTable(occupationTable, botID, wp); }
                public int GetReservation(Waypoint wp) { return GetTableEntry(reservationTable, wp); }
                public BotNormal GetBotReservation(Waypoint wp)
                {
                    int botID = GetReservation(wp);
                    if (botID == -1) return null;
                    return _instance.GetBotByID(botID) as BotNormal;
                }
                public int GetOccupation(Waypoint wp) { return GetTableEntry(occupationTable, wp); }
                public BotNormal GetBotOccupation(Waypoint wp)
                {
                    int botID = GetOccupation(wp);
                    if (botID == -1) return null;
                    return _instance.GetBotByID(botID) as BotNormal;
                }
            }

            /// <summary>
            /// Handles all the actions and information regarding a queue for a bot.
            /// Each queue is uniquely defined with the destination waypoint and the robot.
            /// Queue is first initialized with robot and destination, then follows the possible queue
            /// creation or update.
            /// </summary>
            public class BotQueueClass
            {
                public int _botID;
                public Waypoint _destWP;
                /// <summary>
                /// List of all queueing waypoints, including entry and last waypoint.
                /// </summary>
                public LinkedList<Waypoint> _queueWps;
                public TableStatus _tables;

                /// <summary>
                /// Creata a queue for the bot as a list of queueing waypoints.
                /// </summary>
                /// <param name="botID"> Id of the boot owner of the queue </param>
                /// <param name="destWp"> Final destination for which the queue is built </param>
                /// <param name="tables"> Class with reservation and occupation tables </param>
                public BotQueueClass(int botID, Waypoint destWp, TableStatus tables)
                {
                    _botID = botID;
                    _destWP = destWp;
                    _tables = tables;
                    _queueWps = new LinkedList<Waypoint>();
                }

                public string GetQueueInfo()
                {
                    string istr = String.Format("Bot {0}: ", _botID);
                    foreach (var wp in _queueWps)
                    {
                        istr += String.Format("{0} ({1}, {2}), ", wp.ID, _tables.GetReservation(wp), _tables.GetOccupation(wp));
                    }
                    return istr;
                }

                public bool TryCreatingQueue()
                {
                    bool created = false;
                    for (Waypoint wp = _destWP; wp != null; wp = wp.nextQueueWaypoint)
                    {
                        _queueWps.AddLast(wp);
                        // if there is no reservation and it is free, or if robot is already in position
                        if (_tables.GetOccupation(wp) == -1 && _tables.GetReservation(wp) == -1 
                            || _tables.GetOccupation(wp) == _botID)
                        {
                            created = true;
                            break;
                        }
                    }
                    if (created)
                    {
                        _tables.SetReservation(_botID, lastWp);
                        return true;
                    }
                    else
                    {
                        _queueWps.Clear();
                        return false;
                    }
                }

                /// <summary>
                /// Tries to add additional waypoints to the queue (in case when robot is overtaken)
                /// </summary>
                /// <returns> True if entry was shifted </returns>
                public bool TryUpdatingQueue()
                {
                    bool updated = false;
                    for (Waypoint wp = lastWp.nextQueueWaypoint; wp != null; wp = wp.nextQueueWaypoint)
                    {
                        _queueWps.AddLast(wp);
                        if (_tables.GetOccupation(wp) == -1 && _tables.GetReservation(wp) == -1)
                        {
                            updated = true;
                            break;
                        }
                    }
                    if (updated)
                    {
                        _tables.SetReservation(_botID, lastWp);
                        return true;
                    }
                    else
                    {
                        _queueWps.Clear();
                        return false;
                    }
                }

                /// <summary>
                /// Tries to update the entry waypoint of the queue by moving it closer
                /// </summary>
                /// <returns> True if entry was moved </returns>
                public bool TryMovingEntryCloser()
                {
                    Waypoint previousEntryWp = lastWp;
                    int movedCount = 0;
                    while (nextWp != null && _tables.GetOccupation(nextWp) == -1 && _tables.GetReservation(nextWp) == -1)
                    {
                        ++movedCount;
                        // shorten the queue
                        _queueWps.RemoveLast();
                    }
                    if (movedCount > 0)
                    {
                        // cancel previous entry wp reservation
                        _tables.SetReservation(-1, previousEntryWp);
                        // new entry wp is reserved
                        _tables.SetReservation(_botID, lastWp);
                    }
                    return movedCount > 0;
                }

                /// <summary>
                /// Internal update of tables when bot reaches the first position in the queue.
                /// </summary>
                public void UpdateBotReachedQueue()
                {
                    // remove reservation
                    _tables.SetReservation(-1, lastWp);
                    // set as occupied
                    _tables.SetOccupation(_botID, lastWp);
                }

                #region Queue waypoints
                public Waypoint lastWp
                {
                    get
                    {
                        if (_queueWps.Count == 0) throw new NullReferenceException("No last queue waypoint defined, queueing list is empty!");
                        else return _queueWps.Last.Value;
                    }
                }
                public Waypoint nextWp
                {
                    get
                    {
                        if (_queueWps.Count == 0) throw new NullReferenceException("No next waypoint defined, queueing list is empty!");
                        if (_queueWps.Count == 1) return null;
                        else return _queueWps.Last.Previous.Value;
                    }
                }
                #endregion
            }

            /// <summary>
            /// Bots and corresponding queues
            /// </summary>
            public Dictionary<BotNormal, BotQueueClass> botQueues;
            /// <summary>
            /// Table with reservations and occupations on the map
            /// </summary>
            public TableStatus tables;

            /// <summary>
            /// List of bots that are arriving to their queue
            /// </summary>
            public LinkedList<BotNormal> arrivingBots;
            /// <summary>
            /// List of bots that are currently in their queue
            /// </summary>
            public LinkedList<BotNormal> queueingBots;
            /// <summary>
            /// List of bots that are currently leaving their queue with
            /// the waypoint from which they are leaving
            /// </summary>
            public LinkedList<Tuple<BotNormal,Waypoint>> leavingBots;

            /// <summary>
            /// Bot queue creation mutex
            /// </summary>
            public System.Threading.Mutex botQueuesMutex;

            /// <summary>
            /// Instance variable used to access the logger path for Location Manager
            /// </summary>
            public Instance Instance;

            /// <summary>
            /// Main LM logger path
            /// </summary>
            public string loggerPath;
            /// <summary>
            /// Temp logger path used for limiting the logged lines
            /// and overwriting the logger when the limit is exceeded
            /// </summary>
            public string tempLoggerPath;
            /// <summary>
            /// Logger object defined by loggerPath.
            /// </summary>
            public static System.IO.StreamWriter Logger = null;
            /// <summary>
            /// Initialize Location Manager with dimensions of the map
            /// </summary>
            /// <param name="rowCount"> Number of rows in the map </param>
            /// <param name="colCount"> Number of columns in the map </param>
            public LocationManager(int rowCount, int colCount, Instance instance)
            {
                // Logging
                // NOTE: instance used to access MateScheduler and the print function for loggger
                Instance = instance;
                //loggerPath = instance.SettingConfig.LocationManagerLoggerPath;
                loggerPath = "log.txt";
                try
                {
                    if (Logger != null)
                        Logger.Close();
                }
                catch (System.NullReferenceException ex)
                {
                   Console.WriteLine("Problems"); 
                }
                Logger = new System.IO.StreamWriter(loggerPath);
                //tempLoggerPath = instance.SettingConfig.TempLoggerPath;
                tempLoggerPath = "temp_log.txt";
                logged = false;
                loggerLineCount = 0;
                outputCount = 0;

                botQueues = new Dictionary<BotNormal, BotQueueClass>();
                botQueuesMutex = new System.Threading.Mutex();
                arrivingBots = new LinkedList<BotNormal>();
                queueingBots = new LinkedList<BotNormal>();
                leavingBots = new LinkedList<Tuple<BotNormal,Waypoint>>();
                // instace is only used to convert botID to BotNormal
                tables = new TableStatus(rowCount, colCount, instance);
            }

            /// <summary>
            /// Create the initial queue and add the bot to the arriving list.
            /// </summary>
            /// <param name="bot"></param>
            /// <param name="destWp"> Final destination in the queue </param>
            public Waypoint CreateBotQueue(BotNormal bot, Waypoint destWp)
            {
                botQueuesMutex.WaitOne();
                botQueues[bot] = new BotQueueClass(bot.ID, destWp, tables);
                bool created = botQueues[bot].TryCreatingQueue();

                if (!created)
                {
                    botQueues.Remove(bot);
                    return null;
                }

                Waypoint entryWp = botQueues[bot].lastWp;
                arrivingBots.AddLast(bot);
                log(String.Format("Bot {0}: [{1}] -> .. -> [{2}] created queue", bot.ID, entryWp.ID, botQueues[bot]._destWP.ID));
                Logger.Flush();
                botQueuesMutex.ReleaseMutex();
                return entryWp;
            }

            public string BotQueuesString()
            {
                string info_str = "";
                botQueuesMutex.WaitOne();
                foreach (var k in botQueues.Keys)
                {
                    info_str += botQueues[k].GetQueueInfo();
                    info_str += "\n";
                }
                botQueuesMutex.ReleaseMutex();
                return info_str;
            }
            /// <summary>
            /// Updates the arriving, queuing and leaving robots.
            /// CreateBotQueue - Update - NotifyBotCompletedQueueing are mutually exclusive
            /// </summary>
            public void Update()
            {
                botQueuesMutex.WaitOne();

                // TODO: what about the bots that accidentally enter the queue?
                //       it should not happen because the queues are determined by bots
                //       probably results in bots just waiting for the position to clear

                // leaving
                for (var node = leavingBots.First; node != null; )
                {
                    BotNormal bot = node.Value.Item1;
                    Waypoint lastWp = node.Value.Item2;
                    var nodeNext = node.Next;
                    if (BotLeftQueue(bot, lastWp))
                    {
                        leavingBots.Remove(node);
                        tables.SetOccupation(-1, lastWp);
                        log(String.Format("Bot {0}: [O] <- [{1}] left queue", bot.ID, lastWp.ID));
                    }
                    node = nodeNext;
                }

                // queueing
                for (var node = queueingBots.First; node != null; node = node.Next)
                {
                    BotNormal bot = node.Value;
                    Waypoint nextWp = botQueues[bot].nextWp;
                    Waypoint lastWp = botQueues[bot].lastWp;
                    if (nextWp != null)
                    {
                        // if the next wp is free and robot didn't start moving yet
                        if (tables.GetOccupation(nextWp) == -1 && tables.GetReservation(nextWp) != bot.ID)
                        {
                            int prevID = -1;
                            try
                            {
                                BotNormal prevBot = tables.GetBotReservation(nextWp);
                                // set to new reservation
                                tables.SetReservation(bot.ID, nextWp);
                                if (prevBot != null)
                                {
                                    prevID = prevBot.ID;
                                    // update queue of prevBot now that reserved position has been changed
                                    bool updated = botQueues[prevBot].TryUpdatingQueue();
                                    if (updated)
                                    {
                                        // if update successful, just change the location
                                        prevBot.StateQueueEnqueueFront(new ChangeDestination(botQueues[prevBot].lastWp));
                                        log(String.Format("Bot {0}: [{2}] <- [{1}] extended queue", prevBot.ID, nextWp.ID, botQueues[prevBot].lastWp.ID));
                                    }
                                    else
                                    {
                                        // otherwise reset the partial task and remove bot (goes to parking or some other item)
                                        // cancel current move to and wait, and return same PPT
                                        // legacy that we need to return PPT
                                        prevBot.StateQueueEnqueueFront(new AbortMoveToAndWait(botQueues[prevBot]._destWP));
                                        // Important to also clear the assignments!
                                        if (prevBot.Instance.SettingConfig.BotsSelfAssist)
                                            prevBot.Instance.Controller.MateScheduler.itemTable[prevBot.ID].ClearCurrentLocation();

                                        // bot was in the arriving list so we have to remove it
                                        arrivingBots.Remove(prevBot);
                                        // removed also from queueing bots (PPT returned)
                                        botQueues.Remove(prevBot);
                                        log(String.Format("Bot {0}: [{1}] -> [X] canceled queue", prevBot.ID, nextWp.ID));
                                    }
                                }
                                bot.StateQueueEnqueueFront(new ChangeDestination(nextWp));
                                log(String.Format("Bot {0}: [{1}] -> [{2}] move to next ", bot.ID, lastWp.ID, nextWp.ID));
                            }
                            catch (KeyNotFoundException ex)
                            {
                                string info_str = String.Format("Bot {0} start moving to next wp {1} (reservation {2}) failed\n", bot.ID, nextWp.ID, prevID);
                                botQueuesMutex.WaitOne();
                                info_str += BotQueuesString();
                                info_str += String.Format("A: " + String.Join(", ", arrivingBots.Select(bn => bn.ID).ToList()) + "\n");
                                info_str += String.Format("Q: " + String.Join(", ", queueingBots.Select(bn => bn.ID).ToList()) + "\n");
                                info_str += String.Format("L: " + String.Join(", ", leavingBots.Select(bn => bn.Item1.ID).ToList()) + "\n");
                                ex.Data["UserMessage"] += info_str;
                                Console.WriteLine(ex.Data["UserMessage"].ToString());
                                PrintCompleteStatus();
                                Logger.Flush();
                                throw ex;
                            }
                        }
                    }
                }

                // arriving
                for (var node = arrivingBots.First; node != null; )
                {
                    BotNormal bot = node.Value;
                    var nodeNext = node.Next;
                    bool moved = false;
                    try
                    {
                        int lastWpID = botQueues[bot].lastWp.ID;
                        moved = botQueues[bot].TryMovingEntryCloser();
                        if (moved)
                        {
                            bot.StateQueueEnqueueFront(new ChangeDestination(botQueues[bot].lastWp));
                            log(String.Format("Bot {0}: [{1}] -> [{2}] move entry", bot.ID, lastWpID, botQueues[bot].lastWp.ID));
                        }
                    }
                    catch (KeyNotFoundException ex)
                    {
                        string info_str = String.Format("Try moving entry closer ({0}) for {1}\n", moved, bot.ID);
                        info_str += "\nList status:";
                        info_str += "A: " + String.Join(", ", arrivingBots.Select(bn => bn.ID).ToList()) + "\n";
                        info_str += "Q: " + String.Join(", ", queueingBots.Select(bn => bn.ID).ToList()) + "\n";
                        info_str += "L: ";
                        foreach (var bw in leavingBots)
                        {
                            info_str += String.Format("({0}-{1}) ", bw.Item1.ID, bw.Item2.ID);
                        }
                        info_str += "\n";
                        string botQueuesString = BotQueuesString();
                        info_str += "\n" + botQueuesString + "\n\n";
                        string order_str = Instance.Controller.MateScheduler.PrintOrderStatus() + "\n";
                        info_str += order_str + "\n";
                        ex.Data["UserMessage"] += info_str;
                        Console.WriteLine(ex.Data["UserMessage"].ToString());
                        log("Last error printout\n");
                        PrintCompleteStatus();
                        Logger.Flush();
                        throw ex;
                    }
                    node = nodeNext;
                }

                if (logged)
                {
                    PrintCompleteStatus();
                    Logger.Flush();
                }
                botQueuesMutex.ReleaseMutex();
            }

            public void BotReachedDestination(BotNormal bot)
            {
                botQueuesMutex.WaitOne();
                if (botQueues.ContainsKey(bot))
                {
                    if (bot.CurrentWaypoint == botQueues[bot].lastWp)
                    {
                        arrivingBots.Remove(bot);
                        queueingBots.AddLast(bot);
                        botQueues[bot].UpdateBotReachedQueue();
                        log(String.Format("Bot {0}: [O] -> [{1}] reached queue", bot.ID, botQueues[bot].lastWp.ID));
                    }
                    else if (botQueues[bot].nextWp != null)
                    {
                        Waypoint nextWp = botQueues[bot].nextWp;
                        // if the next wp is free and robot is moving towards it
                        if (tables.GetOccupation(nextWp) == -1 && tables.GetReservation(nextWp) == bot.ID
                            && bot.CurrentWaypoint == botQueues[bot].nextWp)
                        {
                            Waypoint lastWp = botQueues[bot].lastWp;
                            tables.SetOccupation(bot.ID, nextWp);
                            tables.SetReservation(-1, nextWp);
                            tables.SetOccupation(-1, lastWp);
                            botQueues[bot]._queueWps.RemoveLast();
                            log(String.Format("Bot {0}: [{1}] -> [{2}] reached next", bot.ID, lastWp.ID, nextWp.ID));
                        }
                    }
                    else
                    {
                        log(String.Format("Bot {0}: reached some waypoint [{1}]", bot.ID, bot.CurrentWaypoint.ID));
                    }
                    Logger.Flush();
                    // otherwise, bot is not queuing
                }
                botQueuesMutex.ReleaseMutex();
            }

            public bool BotLeftQueue(BotNormal bot, Waypoint lastWp)
            {
                return bot.CurrentWaypoint != lastWp ? true : false;
            }

            public void NotifyBotCompletedQueueing(BotNormal bot)
            {
                botQueuesMutex.WaitOne();
                try
                {
                    queueingBots.Remove(bot);
                    leavingBots.AddLast(new Tuple<BotNormal, Waypoint>(bot, botQueues[bot].lastWp));
                    int lastWpID = botQueues[bot].lastWp.ID;
                    int wpID = botQueues[bot]._destWP.ID;
                    // NOTE: fix for a bug causing leftover reservations 
                    // after robot is removed from the queue
                    Waypoint nextWp = botQueues[bot].nextWp;
                    if (nextWp != null && tables.GetReservation(nextWp) == bot.ID)
                        tables.SetReservation(-1, nextWp);
                    botQueues.Remove(bot);
                    log(String.Format("Bot {0}: [O] <- [{1}] ({2}) leaving queue", bot.ID, lastWpID, wpID));
                }
                catch (KeyNotFoundException ex)
                {
                    string info_str = String.Format("Robot {0} not present in queue.\n", bot.ID);
                    info_str += "\nList status:";
                    info_str += "A: " + String.Join(", ", arrivingBots.Select(bn => bn.ID).ToList()) + "\n";
                    info_str += "Q: " + String.Join(", ", queueingBots.Select(bn => bn.ID).ToList()) + "\n";
                    info_str += "L: ";
                    foreach (var bw in leavingBots)
                    {
                        info_str += String.Format("({0}-{1}) ", bw.Item1.ID, bw.Item2.ID);
                    }
                    info_str += "\n";
                    string botQueuesString = BotQueuesString();
                    info_str += "\n" + botQueuesString + "\n\n";
                    string order_str = Instance.Controller.MateScheduler.PrintOrderStatus() + "\n";
                    info_str += order_str + "\n";
                    ex.Data["UserMessage"] += info_str;
                    Console.WriteLine(ex.Data["UserMessage"].ToString());
                    log("Last error printout\n");
                    PrintCompleteStatus();
                    Logger.Flush();
                    throw ex;
                }
                botQueuesMutex.ReleaseMutex();
            }

            public int loggerLineCount;
            public int outputCount;
            public bool logged;

            public void log(string message)
            {
                Logger.WriteLine(message);
                loggerLineCount += 1;
                logged = true;
            }

            public void PrintCompleteStatus()
            {
                Logger.WriteLine("\nList status:");
                Logger.WriteLine("A: " + String.Join(", ", arrivingBots.Select(bn => bn.ID).ToList()));
                Logger.WriteLine("Q: " + String.Join(", ", queueingBots.Select(bn => bn.ID).ToList()));
                //Logger.WriteLine("L: " + String.Join(", ", leavingBots.Select(bn => bn.Item1.ID).ToList()));
                string info_str = "L: ";
                foreach (var bw in leavingBots)
                {
                    info_str += String.Format("({0}-{1}) ", bw.Item1.ID, bw.Item2.ID);
                }
                info_str += "\n";
                loggerLineCount += 1 + 4; // 4 lines for List A Q L
                string botQueuesString = BotQueuesString();
                info_str += "\n" + botQueuesString + "\n";
                loggerLineCount += 1 + botQueuesString.Count(c => c == '\n') + 1;
                Logger.Write(info_str);
                string order_str = Instance.Controller.MateScheduler.PrintOrderStatus() + "\n";
                Logger.Write(order_str);
                loggerLineCount += order_str.Count(c => c == '\n') + 1;
                outputCount += 1;
                Logger.WriteLine(String.Format("Log lines: {0} (output {1})", loggerLineCount, outputCount));
                Logger.WriteLine("--------------------------------------------------------------------------------");
                loggerLineCount += 1;
                if (loggerLineCount > 25000) ClearLogger();
                logged = false;
            }

            public void PrintListStatue(int botID, int wpID, string listDescr)
            {
                Logger.WriteLine("List status: {0:D} [{1:D}] -> {2}", botID, wpID, listDescr);
                Logger.WriteLine("A: " + String.Join(", ", arrivingBots.Select(bn => bn.ID).ToList()));
                Logger.WriteLine("Q: " + String.Join(", ", queueingBots.Select(bn => bn.ID).ToList()));
                //Logger.WriteLine("L: " + String.Join(", ", leavingBots.Select(bn => bn.Item1.ID).ToList()));
                string info_str = "L: ";
                foreach (var bw in leavingBots)
                {
                    info_str += String.Format("({0}-{1}) ", bw.Item1.ID, bw.Item2.ID);
                }
                info_str += "\n";
                Logger.WriteLine(info_str);
            }

            public void ClearLogger()
            {
                Logger.Close();
                int count = 20000;
                var openStream = new System.IO.StreamReader(loggerPath);
                var saveStream = new System.IO.StreamWriter(tempLoggerPath);
                for (int i = 0; i < count; ++i)
                {
                    openStream.ReadLine();
                }
                for (string line = openStream.ReadLine(); line != null; line = openStream.ReadLine())
                {
                    saveStream.WriteLine(line);
                }
                loggerLineCount -= count;
                openStream.Close();
                saveStream.Close();
                System.IO.File.Copy(tempLoggerPath, loggerPath, true);
                Logger = new System.IO.StreamWriter(loggerPath, append: true);
            }

        }

        #endregion

        #region Queue Manager

        /// <summary>
        /// Manages the path finding for bots in the queue
        /// </summary>
        internal class QueueManager
        {
            /// <summary>
            /// The queue way point
            /// </summary>
            public Waypoint QueueWaypoint;

            /// <summary>
            /// The queue
            /// </summary>
            public List<Waypoint> Queue;

            /// <summary>
            /// The locked way points
            /// </summary>
            public Dictionary<Waypoint, Bot> LockedWaypoints;

            /// <summary>
            /// The indices regarding this queue of the managed waypoints.
            /// </summary>
            private Dictionary<Waypoint, int> _queueWaypointIndices;

            /// <summary>
            /// The bots that are want to reach the way point at the beginning of the queue.
            /// </summary>
            private List<BotNormal> _managedBots;

            /// <summary>
            /// The bots that are want to reach the queue way point ansd already reached the queue.
            /// </summary>
            private HashSet<BotNormal> _botsInQueue;

            /// <summary>
            /// The assigned place in queue
            /// </summary>
            private Dictionary<BotNormal, Waypoint> _placeInQueue;

            /// <summary>
            /// Contains the destination of robots that are cruising along the queue (no new paths will be planned for them until they arrive).
            /// </summary>
            private Dictionary<BotNormal, List<Waypoint>> _queueCruisePaths;

            /// <summary>
            /// Contains all queue nodes acessible in a fast way.
            /// </summary>
            private QuadTree<Waypoint> _queueNodeTree;

            /// <summary>
            /// A loosely defined left border of the managed area.
            /// </summary>
            private double _queueAreaXMin;
            /// <summary>
            /// A loosely defined right border of the managed area.
            /// </summary>
            private double _queueAreaXMax;
            /// <summary>
            /// A loosely defined lower border of the managed area.
            /// </summary>
            private double _queueAreaYMin;
            /// <summary>
            /// A loosely defined upper border of the managed area.
            /// </summary>
            private double _queueAreaYMax;

            /// <summary>
            /// The maximum length of alle edges that have to be respected by the queue manager for identifying locking bots.
            /// </summary>
            private Dictionary<Waypoint, double> _maxEdgeLength;

            /// <summary>
            /// The path manager
            /// </summary>
            private PathManager _pathManager;

            /// <summary>
            /// Initializes a new instance of the <see cref="QueueManager"/> class.
            /// </summary>
            /// <param name="queueWaypoint">The queue way point.</param>
            /// <param name="queue">The queue.</param>
            /// <param name="pathManager">The path manager.</param>
            public QueueManager(Waypoint queueWaypoint, List<Waypoint> queue, PathManager pathManager)
            {
                QueueWaypoint = queueWaypoint;
                Queue = queue;
                _queueWaypointIndices = new Dictionary<Waypoint, int>();
                for (int i = 0; i < Queue.Count; i++)
                {
                    _queueWaypointIndices[Queue[i]] = i;
                    Queue[i].QueueManager = this;
                }
                Queue.RemoveAll(w => w == null);
                LockedWaypoints = new Dictionary<Waypoint, Bot>();

                _pathManager = pathManager;
                // Init storage
                _managedBots = new List<BotNormal>();
                _botsInQueue = new HashSet<BotNormal>();
                _placeInQueue = new Dictionary<BotNormal, Waypoint>();
                _queueCruisePaths = new Dictionary<BotNormal, List<Waypoint>>();
                // Define a loos queue area for some asserting
                double minArcLength = Queue.Min(firstWP => Queue.Where(wp => wp != firstWP).Min(secondWP => firstWP.GetDistance(secondWP)));
                _queueAreaXMin = Queue.Min(w => w.X) - minArcLength;
                _queueAreaXMax = Queue.Max(w => w.X) + minArcLength;
                _queueAreaYMin = Queue.Min(w => w.Y) - minArcLength;
                _queueAreaYMax = Queue.Max(w => w.Y) + minArcLength;
                // Get a quad tree to lookup real waypoints faster
                _queueNodeTree = new QuadTree<Waypoint>(2, 1, _queueAreaXMin, _queueAreaXMax, _queueAreaYMin, _queueAreaYMax);
                foreach (var wp in Queue)
                    _queueNodeTree.Add(wp);
                // Obtain maximal edge length
                _maxEdgeLength = Queue.ToDictionary(wp => wp, wp => wp.Paths.Max(otherWP => wp.GetDistance(otherWP)));
            }

            /// <summary>
            /// Returns the waypoint nearest to the given coordinate.
            /// </summary>
            /// <param name="x">The x-value of the coordinate.</param>
            /// <param name="y">The y-value of the coordinate.</param>
            /// <returns>The nearest waypoint of the queue.</returns>
            private Waypoint GetNearestQueueWaypoint(double x, double y)
            {
                double nearestDistance;
                return _queueNodeTree.GetNearestObject(x, y, out nearestDistance);
            }

            /// <summary>
            /// Updates this instance.
            /// </summary>
            public void Update()
            {
                //no need for managing
                if (_managedBots.Count == 0)
                    return;

                //get locked way points
                LockedWaypoints.Clear();
                _botsInQueue.Clear();

                //is elevator and elevator is in use => lock the destination waypoint
                if (QueueWaypoint.Elevator != null && QueueWaypoint.Elevator.InUse)
                    LockedWaypoints.Add(QueueWaypoint, null);

                //check bot states
                for (int i = 0; i < _managedBots.Count; i++)
                {
                    BotNormal bot = _managedBots[i];

                    var nextWaypoint = bot.NextWaypoint != null ? bot.NextWaypoint : bot.CurrentWaypoint;

                    var currentWaypointInQueue = Queue.Contains(bot.CurrentWaypoint);
                    var nextWaypointInQueue = Queue.Contains(nextWaypoint);

                    var locksCurrentWaypoint = currentWaypointInQueue && bot.CurrentWaypoint.GetDistance(bot) <= _maxEdgeLength[bot.CurrentWaypoint];

                    // Check whether bot is leaving the queue (only possible at the queue waypoint!)
                    if (// Check whether the bot has a new destination and its current waypoint is the end of the queue
                        (bot.DestinationWaypoint != QueueWaypoint && bot.CurrentWaypoint == QueueWaypoint && !locksCurrentWaypoint) ||
                        // Check whether the bot is already outside the queue area and has a different destination than the queue waypoint
                        (!currentWaypointInQueue && !nextWaypointInQueue && bot.DestinationWaypoint != QueueWaypoint) ||
                        // Check whether the bot is ignoring the queue manager
                        (bot.IgnoreInputPalletStandQueue && QueueWaypoint.InputStation != null) ||
                        (bot.IgnoreOutputPalletStandQueue && QueueWaypoint.OutputStation != null))
                    {
                        //bot leaves elevator?
                        if (QueueWaypoint.Elevator != null && bot == QueueWaypoint.Elevator.usedBy)
                        {
                            QueueWaypoint.Elevator.InUse = false;
                            QueueWaypoint.Elevator.usedBy = null;
                        }
                        // Not in queue anymore - remove it
                        _managedBots.RemoveAt(i);
                        // Update index (we removed one)
                        i--;
                        // Mark queueing inactive (this is redundant - see below)
                        bot.IsQueueing = false;
                        // Proceed to next bot
                        continue;
                    }

                    //bot is in Queue if CurrentWaypoint is in queue and if DestinationWaypoint is station/pallet stand 
                    if (currentWaypointInQueue && bot.DestinationWaypoint == QueueWaypoint)
                    {
                        _botsInQueue.Add(bot);

                        // Indicate queueing
                        bot.IsQueueing = true;

                        if (bot.CurrentWaypoint != QueueWaypoint)
                            bot.RequestReoptimization = false; //this bot will be managed by this manager

                        //add locks
                        if (locksCurrentWaypoint && !LockedWaypoints.ContainsKey(bot.CurrentWaypoint))
                            LockedWaypoints.Add(bot.CurrentWaypoint, bot);
                        if (nextWaypointInQueue && !LockedWaypoints.ContainsKey(nextWaypoint))
                            LockedWaypoints.Add(nextWaypoint, bot);
                    }

                    //bot reached end of the queue - no active queueing anymore
                    if (_queueWaypointIndices.ContainsKey(bot.CurrentWaypoint) && _queueWaypointIndices[bot.CurrentWaypoint] == 0)
                    {
                        // Mark queueing inactive
                        bot.IsQueueing = false;
                        _botsInQueue.Remove(bot);
                        // Add locking of the pallet stand when the bot is in it
                        if (!LockedWaypoints.ContainsKey(bot.CurrentWaypoint))
                            LockedWaypoints.Add(bot.CurrentWaypoint, bot);
                    }
                }

                //if this is an elevator queue, the first way point is locked, when the elevator is in use
                if (QueueWaypoint.Elevator != null && QueueWaypoint.Elevator.InUse && !LockedWaypoints.ContainsKey(QueueWaypoint))
                    LockedWaypoints[QueueWaypoint] = null;

                // Remove bots that finished their cruise
                foreach (var bot in _queueCruisePaths.Where(kvp => kvp.Key.CurrentWaypoint == kvp.Value.Last()).Select(kvp => kvp.Key).ToArray())
                    _queueCruisePaths.Remove(bot);

                // Reset places
                _placeInQueue.Clear();

                // Manage locks for cruising bots
                HashSet<BotNormal> failedCruiseBots = null;
                foreach (var bot in _queueCruisePaths.Keys)
                {
                    // Assert that the bot is in the queue
                    Debug.Assert(_queueAreaXMin <= bot.X && bot.X <= _queueAreaXMax && _queueAreaYMin <= bot.Y && bot.Y <= _queueAreaYMax);
                    // Fetch the waypoint the bot is currently at
                    var realWP = GetNearestQueueWaypoint(bot.X, bot.Y);
                    int currentQueueIndex = _queueWaypointIndices[realWP];
                    // Lock waypoints that are left for the cruise
                    foreach (var cruiseWP in _queueCruisePaths[bot].Where(wp => _queueWaypointIndices[wp] <= currentQueueIndex))
                    {
                        // If bot is not moving and next waypoint is already blocked by another bot, something went wrong - discard the cruise and lineup regularly
                        if (LockedWaypoints.ContainsKey(cruiseWP) && LockedWaypoints[cruiseWP] != bot)
                        {
                            // Cruise failed - mark for removal
                            if (failedCruiseBots == null)
                                failedCruiseBots = new HashSet<BotNormal>() { bot };
                            else
                                failedCruiseBots.Add(bot);
                        }
                        else
                        {
                            // Lock waypoint for cruise
                            LockedWaypoints[cruiseWP] = bot;
                        }
                    }
                }
                // Cancel failed cruises
                if (failedCruiseBots != null)
                    foreach (var failedBot in failedCruiseBots)
                        _queueCruisePaths.Remove(failedBot);

                // Assign already moving bots
                foreach (var bot in _botsInQueue.Where(bot => bot.CurrentWaypoint != bot.NextWaypoint && bot.NextWaypoint != null))
                    _placeInQueue.Add(bot, bot.NextWaypoint);

                //assign standing bots
                foreach (var bot in _botsInQueue.Where(bot => !_placeInQueue.ContainsKey(bot)))
                {
                    var queueIndex = _queueWaypointIndices[bot.CurrentWaypoint];
                    var newQueueIndex = -1;
                    if (queueIndex == 0 || LockedWaypoints.ContainsKey(Queue[queueIndex - 1]))
                    {
                        //locked => stay where you are
                        _placeInQueue.Add(bot, bot.CurrentWaypoint);
                        newQueueIndex = queueIndex;
                    }
                    else
                    {
                        //if almost there, just move one up
                        Path path = new Path();
                        if (queueIndex == 1)
                        {
                            LockedWaypoints.Add(Queue[queueIndex - 1], bot);
                            path.AddFirst(_pathManager.GetNodeIdByWaypoint(Queue[queueIndex - 1]), true, 0.0);
                            newQueueIndex = queueIndex - 1;
                        }
                        else
                        {
                            //go as far as you can
                            IEnumerable<Waypoint> lockedPredecessorWaypoints = LockedWaypoints.Keys.Where(q => _queueWaypointIndices[q] < queueIndex);
                            int targetQueueIndex = lockedPredecessorWaypoints.Any() ? lockedPredecessorWaypoints.Max(w => _queueWaypointIndices[w]) + 1 : QueueWaypoint.Elevator != null ? 1 : 0;
                            List<Waypoint> cruisePath = new List<Waypoint>();
                            int nextIndex = -1;
                            // Build cruise path through queue
                            for (int currentIndex = queueIndex; currentIndex >= targetQueueIndex;)
                            {
                                // Determine next waypoint in queue to go to after this one (consider shortcuts)
                                IEnumerable<Waypoint> shortCuts = Queue[currentIndex].Paths.Where(p => _queueWaypointIndices.ContainsKey(p) && _queueWaypointIndices[p] < currentIndex && targetQueueIndex <= _queueWaypointIndices[p]);
                                nextIndex = currentIndex > targetQueueIndex && shortCuts.Any() ? shortCuts.Min(p => _queueWaypointIndices[p]) : currentIndex - 1;
                                // Check whether a stop is required at the 
                                double inboundOrientation = cruisePath.Count >= 1 && currentIndex > targetQueueIndex ? Circle.GetOrientation(cruisePath[cruisePath.Count - 1].X, cruisePath[cruisePath.Count - 1].Y, Queue[currentIndex].X, Queue[currentIndex].Y) : double.NaN;
                                double outboundOrientation = cruisePath.Count >= 1 && currentIndex > targetQueueIndex ? Circle.GetOrientation(Queue[currentIndex].X, Queue[currentIndex].Y, Queue[nextIndex].X, Queue[nextIndex].Y) : double.NaN;
                                bool stopRequired = !double.IsNaN(inboundOrientation) && Math.Abs(Circle.GetOrientationDifference(inboundOrientation, outboundOrientation)) >= bot.Instance.StraightOrientationTolerance;
                                // Add connection to the overall path
                                LockedWaypoints[Queue[currentIndex]] = bot;
                                bool stopAtNode = currentIndex == queueIndex || currentIndex == targetQueueIndex || stopRequired;
                                path.AddLast(
                                    // The next waypoint to go to
                                    _pathManager.GetNodeIdByWaypoint(Queue[currentIndex]),
                                    // See whether we need to stop at the waypoint (either because it is the last one or the angles do not match - using 10 degrees in radians here, which should be in line with the Graph class of path planning)
                                    stopAtNode,
                                    // Don't wait at all
                                    0.0);
                                // Add the step to the cruise path
                                cruisePath.Add(Queue[currentIndex]);
                                // Update to next index
                                currentIndex = nextIndex;
                            }
                            // Prepare for next node in queue
                            path.NextNodeToPrepareFor = queueIndex != nextIndex && nextIndex >= 0 ? _pathManager.GetNodeIdByWaypoint(Queue[nextIndex]) : -1;
                            // The new index in queue is the targeted one
                            newQueueIndex = targetQueueIndex;
                            // Save path for overwatch
                            _queueCruisePaths[bot] = cruisePath;
                            // Check path
                            for (int i = 0; i < cruisePath.Count - 1; i++)
                            {
                                if (!cruisePath[i].ContainsPath(cruisePath[i + 1]))
                                    throw new InvalidOperationException();
                            }
                        }
                        // Set path
                        bot.Path = path;

                        _placeInQueue.Add(bot, Queue[newQueueIndex]);

                        //if the next place is an elevator set it
                        if (_placeInQueue[bot].Elevator != null)
                        {
                            QueueWaypoint.Elevator.InUse = true;
                            QueueWaypoint.Elevator.usedBy = bot;
                        }
                    }

                }

                //assign bots that are not in the queue

                //search for the first free node in the queue
                int firstFreeNode = Queue.Count - 1;
                while (firstFreeNode > 0 && !LockedWaypoints.ContainsKey(Queue[firstFreeNode - 1]))
                    firstFreeNode--;

                //if this is a queue for an elevator, then do not assign the elevator directly, because others might wait in a queue on a different tier
                if (firstFreeNode == 0 && QueueWaypoint.Elevator != null)
                    firstFreeNode = 1;

                //botsLeftToQueue
                var botsLeftToQueue = _managedBots.Where(bot => !_placeInQueue.ContainsKey(bot)).ToList();

                //while a bot exists with no place in queue
                while (botsLeftToQueue.Count > 0)
                {
                    var nearestBot = botsLeftToQueue[0];
                    var distance = Queue[firstFreeNode].GetDistance(nearestBot);
                    for (int i = 1; i < botsLeftToQueue.Count; i++)
                    {
                        if (Queue[firstFreeNode].GetDistance(botsLeftToQueue[i]) < distance)
                        {
                            nearestBot = botsLeftToQueue[i];
                            distance = Queue[firstFreeNode].GetDistance(nearestBot);
                        }
                    }

                    botsLeftToQueue.Remove(nearestBot);
                    _placeInQueue.Add(nearestBot, Queue[firstFreeNode]);

                    firstFreeNode = Math.Min(firstFreeNode + 1, Queue.Count - 1);

                }
            }

            /// <summary>
            /// Gets the place in queue.
            /// </summary>
            /// <param name="bot">The bot.</param>
            /// <returns></returns>
            public Waypoint getPlaceInQueue(BotNormal bot)
            {
                if (_botsInQueue.Contains(bot))
                    return null;
                else
                    return _placeInQueue[bot];
            }

            /// <summary>
            /// Ons the bot join queue.
            /// </summary>
            /// <param name="bot">The bot.</param>
            internal void onBotJoinQueue(BotNormal bot)
            {
                if (!_managedBots.Contains(bot))
                    _managedBots.Add(bot);
            }
        }

        #endregion
    }
}
