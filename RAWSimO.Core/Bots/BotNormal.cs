using RAWSimO.Core.Control;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Waypoints;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.Info;
using RAWSimO.Core.Geometrics;
using RAWSimO.MultiAgentPathFinding;
using RAWSimO.MultiAgentPathFinding.Physic;
using RAWSimO.Toolbox;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;



namespace RAWSimO.Core.Bots
{
    /// <summary>
    /// Bot Driver
    /// Optimization is complete delegated to the controller, because he knows all the bots
    /// </summary>
    public partial class BotNormal : Bot
    {
        #region Attributes

        /// <summary>
        /// The bots request a re-optimization after failing of next way point reservation
        /// </summary>
        private static bool requestReoptimizationAfterFailingOfNextWaypointReservation = false;

        /// <summary>
        /// Gets BotType
        /// </summary>
        public override BotType Type => BotType.BotNormal; 

        /// <summary>
        /// The current destination of the bot as useful information for other mechanisms. If not available, the current waypoint will be provided.
        /// </summary>
        internal override Waypoint TargetWaypoint { get { return _destinationWaypoint ?? _currentWaypoint; } }

        /// <summary>
        /// destination way point
        /// </summary>
        public Waypoint DestinationWaypoint
        {
            get { return _destinationWaypoint; }
            set
            {
                if (_destinationWaypoint != value)
                {
                    _destinationWaypoint = value;
                    Instance.Controller.PathManager.notifyBotNewDestination(this);
                }
            }
        }
        private Waypoint _destinationWaypoint;

        /// <summary>
        /// next way point
        /// </summary>
        public Waypoint NextWaypoint
        {
            get
            {
                return _nextWaypoint;
            }
            internal set
            {
                if (value != null) { _nextWaypointID = value.ID; }
                _nextWaypoint = value;
            }
        }
        private Waypoint _nextWaypoint;
        private int _nextWaypointID;
        /// <summary>
        /// true if bot is starting the BotMove state
        /// </summary>
        public bool isStartingMove = false;
        /// <summary>
        /// true if bot is currently standing on destination waypoint
        /// </summary>
        public bool isInPlace = false;
        /// <summary>
        /// indicates if bot is breaking
        /// </summary>
        public bool IsBreaking { get
            {
                switch (CurrentBotStateType)
                {
                    case BotStateType.Move:
                        return (StateQueuePeek() as BotMove).IsBreaking;
                    case BotStateType.MoveToAssist:
                        return (StateQueuePeek() as MoveToAssist).IsBreaking;
                    default:
                        return false;
                }
                    
                
            }
        }
        public SwarmMissionState SwarmState { get; set; } 
        /// <summary>
        /// Class containing swarm mission state.
        /// </summary>
        public class SwarmMissionState
        {
            public SwarmMissionState()
            {
                currentPalletGroup = -1;
                isSwitchingPallets = false;
                currentAddress = null;
            }
            /// <summary>
            /// Current palletID to stack on
            /// </summary>
            public int currentPalletGroup {get; set;}
            /// <summary>
            /// Is bot switching pallets
            /// </summary>
            public bool isSwitchingPallets {get; set;}
            /// <summary>
            /// Current that bot is processing
            /// </summary>
            public string currentAddress {get; set;}
        }
        /// <summary>
        /// next way point
        /// </summary>
        internal bool requestReoptimization;

        #region State handling
        /// <summary>
        /// Gets all the states in this bot state queue that are true on the <paramref name="predicate"/>
        /// </summary>
        /// <param name="predicate">Function which return true or false for a given state based on some condition</param>
        /// <returns>Enumerable of states that satisfy the condition</returns>
        public IEnumerable<IBotState> GetStatesWhere(Func<IBotState, bool> predicate) => _stateQueue.Where(predicate);
        /// <summary>
        /// The state queue
        /// </summary>
        protected LinkedList<IBotState> _stateQueue = new LinkedList<IBotState>();
        /// <summary>
        /// Returns the next state in the state queue without removing it.
        /// </summary>
        /// <returns>The next state in the state queue.</returns>
        internal IBotState StateQueuePeek() { return _stateQueue.First?.Value; }
        /// <summary>
        /// Returns the second state in the state queue without modification of state queue
        /// </summary>
        /// <returns>The second state of the state queue</returns>
        protected IBotState StateQueuePeekSecond() { return _stateQueue.First.Next.Value; }
        /// <summary>
        /// Enqueues a state at the end of the queue.
        /// </summary>
        /// <param name="state">The state to enqueue.</param>
        internal void StateQueueEnqueue(IBotState state) => _stateQueue.AddLast(state); 
        /// <summary>
        /// Enqueues a state at the front of the queue.
        /// </summary>
        /// <param name="state"></param>
        internal void StateQueueEnqueueFront(IBotState state) => _stateQueue.AddFirst(state);
        /// <summary>
        /// Removes first occurence of a given IBotState from _StateQueue 
        /// </summary>
        /// <param name="state">IBotState to remove</param>
        internal void StateQueueRemove(IBotState state) => _stateQueue.Remove(state);
        /// <summary>
        /// Dequeues the next state from the state queue.
        /// </summary>
        /// <returns>The state that was just dequeued.</returns>
        internal IBotState StateQueueDequeue() 
        {
            IBotState state = _stateQueue.First.Value;
            _stateQueue.RemoveFirst(); 
            return state; 
        }
        /// <summary>
        /// Clears the complete state queue.
        /// </summary>
        internal void StateQueueClear() => _stateQueue.Clear();
        /// <summary>
        /// The number of states currently in the queue.
        /// </summary>
        protected int StateQueueCount => _stateQueue.Count;
        /// <summary>
        /// The type of state this bot is currently in
        /// </summary>
        internal BotStateType CurrentBotStateType
        {
            get
            {
                IBotState state = StateQueuePeek();
                return state != null ? state.Type : BotStateType.NullState;
            }
        }
        #endregion

        /// <summary>
        /// Flag determining whether to calculate time needed to move
        /// </summary>
        internal bool _calculateTimeNeededToMove;

        /// <summary>
        /// Stores the previous call time for calculating the traveled distance and new velocity
        /// </summary>
        internal double _previousCallTime;

        /// <summary>
        /// drive until
        /// </summary>
        internal double _driveDuration = -1.0;

        /// <summary>
        /// rotate until
        /// </summary>
        internal double _rotateDuration = -1.0;

        /// <summary>
        /// rotate until
        /// </summary>
        internal double _waitUntil = -1.0;

        /// <summary>
        /// rotate until
        /// </summary>
        internal double _startOrientation = 0;

        /// <summary>
        /// rotate until
        /// </summary>
        internal double _endOrientation = 0;

        /// <summary>
        /// The agent reached the next way point
        /// </summary>
        internal bool _eventReachedNextWaypoint = false;

        /// <summary>
        /// Indicates whether the first position info was received from the remote server.
        /// </summary>
        private bool _initialEventReceived = false;

        /// <summary>
        /// Indicates the last state of the robot. This is used to lower the communication with the robot.
        /// </summary>
        internal BotStateType _lastExteriorState = BotStateType.Rest;

        /// <summary>
        /// The current path.
        /// </summary>
        private Path _path;
        /// <summary>
        /// The current path.
        /// </summary>
        public Path Path
        {
            get
            {
                // Just return it
                return _path;
            }
            internal set
            {
                // Set it
                _path = value;
                // Make path public if visualization is present
                if (Instance.SettingConfig.VisualizationAttached && _path != null)
                    _currentPath = _path.Actions.Select(a => Instance.Controller.PathManager.GetWaypointByNodeId(a.Node)).Cast<IWaypointInfo>().ToList();
            }
        }
        /// <summary>
        /// Counter of all the PassBy events that happened in this instance
        /// </summary>
        public static int TotalPassByEvents { get { return PassByEvent.TotalPassByEvents;  }
                                              set { PassByEvent.TotalPassByEvents = value; }}
        /// <summary>
        /// Counter of total number of pass by events by hour. Keys are hours, values are number of events in a given hour
        /// </summary>
        public static Dictionary<int, int> PassByEvents { get { return PassByEvent.PassByEvents; }
                                                          set { PassByEvent.PassByEvents = value; }}

        #endregion

        #region core

        /// <summary>
        /// Initializes a new instance of the <see cref="BotNormal"/> class.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="instance">The instance.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="podTransferTime">The pod transfer time.</param>
        /// <param name="acceleration">The maximum acceleration.</param>
        /// <param name="deceleration">The maximum deceleration.</param>
        /// <param name="maxVelocity">The maximum velocity.</param>
        /// <param name="turnSpeed">The turn speed.</param>
        /// <param name="collisionPenaltyTime">The collision penalty time.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public BotNormal(int id, Instance instance, double radius, double podTransferTime, double acceleration, double deceleration, double maxVelocity, double turnSpeed, double collisionPenaltyTime, double x = 0.0, double y = 0.0) : base(instance)
        {
            ID = id;
            Instance = instance;
            Radius = radius;
            X = x;
            Y = y;
            PodTransferTime = podTransferTime;
            MaxAcceleration = acceleration;
            MaxDeceleration = deceleration;
            MaxVelocity = maxVelocity;
            TurnSpeed = turnSpeed;
            CollisionPenaltyTime = collisionPenaltyTime;
            Physics = new Physics(acceleration, deceleration, maxVelocity, turnSpeed);
            SwarmState = new SwarmMissionState();
        }

        /// <summary>
        /// Copy Constructor
        /// </summary>
        /// <param name="other"><see cref="BotNormal"/> to be copied</param>
        public BotNormal(BotNormal other):base(other.Instance)
        {
            ID = other.ID;
            Instance = other.Instance;
            Radius = other.Radius;
            X = other.X;
            Y = other.Y;
            PodTransferTime = other.PodTransferTime;
            MaxAcceleration = other.MaxAcceleration;
            MaxDeceleration = other.MaxDeceleration;
            MaxVelocity = other.MaxVelocity;
            TurnSpeed = other.TurnSpeed;
            CollisionPenaltyTime = other.CollisionPenaltyTime;
            Physics = new Physics(other.MaxAcceleration, other.MaxDeceleration, other.MaxVelocity, other.TurnSpeed);
            
        }

        /// <summary>
        /// orientation the bot should look at
        /// </summary>
        /// <returns>orientation</returns>
        public double GetTargetOrientation()
        {
            return _endOrientation;
        }

        #endregion

        #region Bot Members

        /// <summary>
        /// last way point
        /// </summary>
        public override Waypoint CurrentWaypoint
        {
            get
            {
                // Get it
                return _currentWaypoint;
            }
            set
            {
                // Set it
                _currentWaypoint = value;
                // If the current waypoint belongs to a queue, notify the corresponding manager
                if (_currentWaypoint.QueueManager != null)
                {
                    if ((_currentWaypoint.QueueManager.QueueWaypoint.InputStation != null && !this.IgnoreInputPalletStandQueue) ||
                        (_currentWaypoint.QueueManager.QueueWaypoint.OutputStation != null && !this.IgnoreOutputPalletStandQueue))
                        // Notify the manager about this bot joining the pallet stand queue
                        _currentWaypoint.QueueManager.onBotJoinQueue(this);
                }
            }
        }

        public static bool RequestReoptimizationAfterFailingOfNextWaypointReservation { get => requestReoptimizationAfterFailingOfNextWaypointReservation; set => requestReoptimizationAfterFailingOfNextWaypointReservation = value; }
        public bool RequestReoptimization { get => requestReoptimization; set => requestReoptimization = value; }
        public Physics Physics { get; set; }
        public string CurrentInfoStateName
        {
            get
            {
                try
                {
                    return _stateQueue.Count != 0 ? StateQueuePeek().ToString() : "";
                }
                catch (NullReferenceException) //unknown crash
                {
                    return "";
                }
            }
        }
        /// <summary>
        /// The last waypoint.
        /// </summary>
        private Waypoint _currentWaypoint;

        /// <summary>
        /// assign a task to a bot -&gt; delegate to controller
        /// </summary>
        /// <param name="t">task</param>
        /// <exception cref="System.ArgumentException">Unknown task-type:  + t.Type</exception>
        public override void AssignTask(BotTask t)
        {
            
            // Warn when clearing incomplete tasks
            if (StateQueueCount > 0)
            {
                // Abort drive and get stop waypoint
                Waypoint stopWP = AbortDrive(GetSpeed());
                StateQueueClear();
                AppendMoveStates(CurrentWaypoint, stopWP, true, true);
                CurrentTask.Cancel();
            }
            CurrentTask = t;


            switch (t.Type)
            {
                case BotTaskType.None:
                    return;
                case BotTaskType.ParkPod:

                    //re-optimize
                    RequestReoptimization = true;

                    ParkPodTask storePodTask = t as ParkPodTask;
                    // If we have another pod we cannot store the given one
                    if (storePodTask.Pod != Pod)
                    {
                        Instance.LogDefault("WARNING! Cannot park a pod that the bot is not carrying!");
                        Instance.Controller.BotManager.TaskAborted(this, storePodTask);
                        return;
                    }
                    // Add the move states for parking the pod
                    AppendMoveStates(CurrentWaypoint, storePodTask.StorageLocation);
                    // After bringing the pod to the storage location set it down
                    StateQueueEnqueue(new BotSetdownPod(storePodTask.StorageLocation));

                    break;
                case BotTaskType.RepositionPod:

                    //re-optimize
                    RequestReoptimization = true;

                    RepositionPodTask repositionPodTask = t as RepositionPodTask;
                    // If don't have pod requested to store, then go get it 
                    if (Pod == null)
                    {
                        // Add states for getting the pod
                        AppendMoveStates(CurrentWaypoint, repositionPodTask.Pod.Waypoint);
                        // Add state for picking up pod
                        StateQueueEnqueue(new BotPickupPod(repositionPodTask.Pod));
                        // Add states for repositioning the pod
                        AppendMoveStates(repositionPodTask.Pod.Waypoint, repositionPodTask.StorageLocation);
                        // After bringing the pod to the storage location set it down
                        StateQueueEnqueue(new BotSetdownPod(repositionPodTask.StorageLocation));
                        // Log a repositioning move
                        Instance.NotifyRepositioningStarted(this, repositionPodTask.Pod.Waypoint, repositionPodTask.StorageLocation, repositionPodTask.Pod);
                    }
                    // We are already carrying a pod: we cannot execute the task
                    else
                    {
                        Instance.LogDefault("WARNING! Cannot reposition a pod when the robot already is carrying one!");
                        Instance.Controller.BotManager.TaskAborted(this, repositionPodTask);
                        return;
                    }

                    break;
                case BotTaskType.Insert:

                    //re-optimize
                    RequestReoptimization = true;

                    InsertTask storeTask = t as InsertTask;
                    if (storeTask.ReservedPod != Pod)
                    {
                        var podWaypoint = storeTask.ReservedPod.Waypoint;
                        AppendMoveStates(CurrentWaypoint, podWaypoint);
                        StateQueueEnqueue(new BotPickupPod(storeTask.ReservedPod));
                        AppendMoveStates(podWaypoint, storeTask.InputStation.Waypoint);
                    }
                    else
                    {
                        AppendMoveStates(CurrentWaypoint, storeTask.InputStation.Waypoint);
                    }
                    StateQueueEnqueue(new BotGetItems(storeTask));

                    break;
                case BotTaskType.Extract:

                    //re-optimize
                    RequestReoptimization = true;

                    ExtractTask extractTask = t as ExtractTask;
                    if (extractTask.ReservedPod != Pod)
                    {
                        var podWaypoint = extractTask.ReservedPod.Waypoint;
                        AppendMoveStates(CurrentWaypoint, podWaypoint);
                        StateQueueEnqueue(new BotPickupPod(extractTask.ReservedPod));
                        AppendMoveStates(podWaypoint, extractTask.OutputStation.Waypoint);
                    }
                    else
                    {
                        AppendMoveStates(CurrentWaypoint, extractTask.OutputStation.Waypoint);
                    }
                    StateQueueEnqueue(new BotPutItems(extractTask));

                    break;
                case BotTaskType.Rest:
                    var restTask = t as RestTask;
                    // Only append move task to get to resting location, if we are not at it yet
                    if ((restTask.RestingLocation != null) && (CurrentWaypoint != restTask.RestingLocation || Moving))
                        AppendMoveStates(CurrentWaypoint, restTask.RestingLocation);
                    StateQueueEnqueue(new BotRest(restTask.RestingLocation, BotRest.DEFAULT_REST_TIME)); // TODO set paramterized wait time and adhere to it
                    break;
                case BotTaskType.MultiPointGatherTask:
                    var MPGatherTask = t as MultiPointGatherTask;
                    var inputPalletStand = (this as MovableStation).GetInputPalletStandWaypoint(MPGatherTask);

                    //if inputPalletStandWaypoint is null, use CurrentWaypoint
                    inputPalletStand ??= CurrentWaypoint;

                    //first visit input pallet stand and get the pallet
                    StateQueueEnqueue(new BotPrepareMoveToInputPalletStand());
                    AppendMoveStates(CurrentWaypoint, inputPalletStand);
                    StateQueueEnqueue(new BotGetPallet(inputPalletStand));

                    //if see-off mate scheduling is turned on, enqueue specific state
                    if (Instance.SettingConfig.SeeOffMateScheduling == true)
                        StateQueueEnqueue(new WaitForSeeOffAssistance());

                    //iterate over the locations of a list and mark them to be visited in that order
                    for (int i = 0; i < MPGatherTask.Locations.Count; ++i)
                    {
                        StateQueueEnqueue(new PreparePartialTask(
                            this,
                            MPGatherTask.Locations[i], 
                            MPGatherTask.PodItems[i].location,
                            MPGatherTask.Order.PalletIDs[MPGatherTask.PodLocations[i]]));
                    }

                    //at the end, visit output pallet stand 
                    StateQueueEnqueue(new BotPrepareMoveToOutputPalletStand());
                    break;
                case BotTaskType.AssistTask:
                    RequestReoptimization = true;

                    var assistTask = t as AssistTask;
                    //move to needed waypoint
                    AppendMoveStates(CurrentWaypoint, assistTask.Waypoint, true);

                    // mate gets a task to assist, and this is queued
                    // TODO: assistTask.Waypoints inside a single order should not be the same
                    // enable this after refactoring
                    var currentTask = (MultiPointGatherTask)assistTask.BotToAssist.CurrentTask;

                    // NOTE: another fix connected to GetLocationAfter and GetBotCurrentItemAddress
                    // now this will always be related to the last added address on the robot
                    //string address = currentTask.PodItems[currentTask.Locations.FindIndex(l => l.ID == assistTask.Waypoint.ID)].location;
                    string address = Instance.Controller.MateScheduler.GetBotCurrentItemAddress(assistTask.BotToAssist, assistTask.Waypoint);
                    Instance.Controller.MateScheduler.itemTable[assistTask.BotToAssist.ID].UpdateAssignedPicker(address, ID);
                    //enter the state of waiting for the other bot
                    StateQueueEnqueue(new WaitForStation(assistTask.Waypoint));
                    break;
                case BotTaskType.AbortingTask:
                    OnAbortingTaskAsigned();
                    break;
                default:
                    throw new ArgumentException("Unknown task-type: " + t.Type);
            }

            // Track task count
            StatAssignedTasks++;
            StatTotalTaskCounts[t.Type]++;
        }

        /// <summary>
        /// appends the move states with respect to tiers and elevators.
        /// </summary>
        /// <param name="waypointFrom">The from waypoint.</param>
        /// <param name="waypointTo">The destination waypoint.</param>
        internal void AppendMoveStates(Waypoint waypointFrom, Waypoint waypointTo, bool isMovingToAssist = false, bool isBreaking = false)
        {
            //double distance;
            //next line crashes, currently no elevators are used

            //var checkPoints = Instance.Controller.PathManager.FindElevatorSequence(this, waypointFrom, waypointTo, out distance);
            //StatDistanceEstimated += distance;
            /*
            foreach (var point in checkPoints)
            {
                IBotState newState = isMovingToAssist ? new MoveToAssist(point.Item2) : new BotMove(point.Item2);
                StateQueueEnqueue(newState);
                StateQueueEnqueue(new UseElevator(point.Item1, point.Item2, point.Item3));
            }
            */
            var state = isMovingToAssist ? new MoveToAssist(waypointTo, isBreaking) : new BotMove(waypointTo, isBreaking);
            StateQueueEnqueue(state);
        }
        /// <summary>
        /// prepends the move state
        /// </summary>
        /// <param name="waypointFrom">The from waypoint</param>
        /// <param name="waypointTo">The destination waypoint</param>
        internal void PrependMoveStates(Waypoint waypointFrom, Waypoint waypointTo,  bool isMovingToAssist = false, bool isBreaking = false)
        {
            var state = isMovingToAssist ? new MoveToAssist(waypointTo, isBreaking) : new BotMove(waypointTo, isBreaking);
            StateQueueEnqueueFront(state);
        }

        /// <summary>
        /// Dequeues the state.
        /// </summary>
        /// <param name="lastTime">The last time.</param>
        /// <param name="currentTime">The current time.</param>
        internal void DequeueState(double lastTime, double currentTime)
        {
            IBotState dequeuedState = StateQueueDequeue();
            Debug.Assert(StateQueueCount == 0 || !(StateQueuePeek() is BotMove) || CurrentWaypoint.Tier.ID == ((BotMove)StateQueuePeek()).DestinationWaypoint.Tier.ID);
            StatLastState = dequeuedState.Type;
        }

        /// <summary>
        /// Sets the next way point.
        /// </summary>
        /// <param name="waypoint">The way point.</param>
        /// <param name="currentTime">The current time.</param>
        /// <returns>A boolean value indicating whether the reservation was successful.</returns>
        public bool setNextWaypoint(Waypoint waypoint, double currentTime)
        {
            if (GetSpeed() > 0)
                return false;
            if (X == waypoint.X && Y == waypoint.Y)
            {
                NextWaypoint = null;
                if (RequestReoptimizationAfterFailingOfNextWaypointReservation)
                    RequestReoptimization = true;
                return false;
            }
                
            _startOrientation = Orientation;
            _endOrientation = Circle.GetOrientation(X, Y, waypoint.X, waypoint.Y);

            double rotateDuration;
            // use the real turn speed only at the beginning and the end of the path, otherwise use a lower speed
            if (this is MovableStation && (isInPlace || isStartingMove))
                rotateDuration = Physics.getTimeNeededToTurn(_startOrientation, _endOrientation, Instance.layoutConfiguration.StartStopTurnSpeed);
            else
                rotateDuration = Physics.getTimeNeededToTurn(_startOrientation, _endOrientation);

            var waitUntil = Math.Max(_waitUntil, currentTime);

            if (Instance.Controller.PathManager.RegisterNextWaypoint(this, currentTime, waitUntil, rotateDuration, CurrentWaypoint, waypoint, out _))
            {
                //set way point
                NextWaypoint = waypoint;

                //set move times
                _waitUntil = waitUntil;
                _rotateDuration = rotateDuration;
                _driveDuration = Physics.getTimeNeededToMove(0, CurrentWaypoint.GetDistance(NextWaypoint));

                //set the flag and reset the traveled distance
                _calculateTimeNeededToMove = true;

                return true;
            }
            else
            {
                NextWaypoint = null;
                if (RequestReoptimizationAfterFailingOfNextWaypointReservation)
                    RequestReoptimization = true;

                // Log failed reservation
                Instance.StatOverallFailedReservations++;

                return false;
            }
        }

        /// <summary>
        /// Blocks the robot for the specified time.
        /// </summary>
        /// <param name="time">The time to be blocked for.</param>
        public override void WaitUntil(double time)
        {
            if (this.GetSpeed() > 0)
                throw new Exception("Can not wait while driving!");

            _waitUntil = time;
        }

        /// <summary>
        /// Determines whether this bot is fixed to a position.
        /// </summary>
        /// <returns>true, if it is fixed</returns>
        public bool hasFixedPosition()
        {
            return StateQueueCount == 0 || !(StateQueuePeek() is BotMove);
        }

        /// <summary>
        /// Determines whether this bot is currently resting.
        /// </summary>
        /// <returns><code>true</code> if the bot is resting, <code>false</code> otherwise.</returns>
        public bool IsResting()
        {
            if(this is MateBot) return false;
            var type = StateQueueCount == 0 ? BotStateType.Rest : StateQueuePeek().Type;
            return type == BotStateType.Rest || type == BotStateType.WaitingForMate || 
                   type == BotStateType.WaitingForStation || type == BotStateType.PreparePartialTask ||
                   type == BotStateType.WaitingForSeeOffAssistance || type == BotStateType.BotAssist;
        }

        /// <summary>
        /// Logs the data of an unfinished trip.
        /// </summary>
        internal override void LogIncompleteTrip()
        {
            if (StateQueueCount > 0 && StateQueuePeek() is BotMove)
                (StateQueuePeek() as BotMove).LogUnfinishedTrip(this);
        }
        internal override Waypoint GetLocationAfter(int NrRegisteredLocations)
        {
            throw new NotImplementedException();
        }
        public override void OnAssistantAssigned()
        {
            throw new NotImplementedException();
        }
        /// <summary>
        /// Gets destination waypoint of next <see cref="PreparePartialTask"/> 
        /// </summary>
        /// <returns>Waypoint of next <see cref="PreparePartialTask"/> if such exist or null if does not</returns>
        public Waypoint NextPPTLocation => _stateQueue.FirstOrDefault(state => state.Type == BotStateType.PreparePartialTask)?.DestinationWaypoint;
        /// <summary>
        /// Property holding destination waypoint of this bots assistant
        /// </summary>
        public Waypoint AssistantDestination { get;  set; }
        #endregion

        #region Queueing zone tracking

        /// <summary>
        /// Stores the last trip start time.
        /// </summary>
        internal double _queueTripStartTime = double.NaN;
        /// <summary>
        /// Contains all output station queueing areas.
        /// </summary>
        private VolatileIDDictionary<OutputStation, SimpleRectangle> _queueZonesOStations;
        /// <summary>
        /// Contains all input station queueing areas.
        /// </summary>
        private VolatileIDDictionary<InputStation, SimpleRectangle> _queueZonesIStations;
        /// <summary>
        /// Checks whether the bot is currently within the stations queueing area.
        /// </summary>
        /// <param name="station">The station to check.</param>
        /// <returns><code>true</code> if the bot is within the stations queueing area, <code>false</code> otherwise.</returns>
        internal bool IsInStationQueueZone(OutputStation station)
        {
            if (_queueZonesOStations == null)
                _queueZonesOStations = new VolatileIDDictionary<OutputStation, SimpleRectangle>(Instance.OutputStations.Select(s =>
                {
                    double lowX = s.Queues != null && s.Queues.Any() && s.Queues.First().Value.Any() ? s.Queues.Min(q => q.Value.Min(w => w.X)) : s.X - Instance.layoutConfiguration.HorizontalWaypointDistance / 2;
                    double highX = s.Queues != null && s.Queues.Any() && s.Queues.First().Value.Any() ? s.Queues.Max(q => q.Value.Max(w => w.X)) : s.X + Instance.layoutConfiguration.HorizontalWaypointDistance / 2;
                    double lowY = s.Queues != null && s.Queues.Any() && s.Queues.First().Value.Any() ? s.Queues.Min(q => q.Value.Min(w => w.Y)) : s.Y - Instance.layoutConfiguration.VerticalWaypointDistance / 2;
                    double highY = s.Queues != null && s.Queues.Any() && s.Queues.First().Value.Any() ? s.Queues.Max(q => q.Value.Max(w => w.Y)) : s.Y + Instance.layoutConfiguration.VerticalWaypointDistance / 2;
                    return new VolatileKeyValuePair<OutputStation, SimpleRectangle>(s, new SimpleRectangle(s.Tier, lowX, lowY, highX - lowX, highY - lowY));
                }).ToList());
            return _queueZonesOStations[station].IsContained(Tier, X, Y);
        }
        /// <summary>
        /// Checks whether the bot is currently within the stations queueing area.
        /// </summary>
        /// <param name="station">The station to check.</param>
        /// <returns><code>true</code> if the bot is within the stations queueing area, <code>false</code> otherwise.</returns>
        internal bool IsInStationQueueZone(InputStation station)
        {
            if (_queueZonesIStations == null)
                _queueZonesIStations = new VolatileIDDictionary<InputStation, SimpleRectangle>(Instance.InputStations.Select(s =>
                {
                    double lowX = s.Queues != null && s.Queues.Any() && s.Queues.First().Value.Any() ? s.Queues.Min(q => q.Value.Min(w => w.X)) : s.X - Instance.layoutConfiguration.HorizontalWaypointDistance / 2;
                    double highX = s.Queues != null && s.Queues.Any() && s.Queues.First().Value.Any() ? s.Queues.Max(q => q.Value.Max(w => w.X)) : s.X + Instance.layoutConfiguration.HorizontalWaypointDistance / 2;
                    double lowY = s.Queues != null && s.Queues.Any() && s.Queues.First().Value.Any() ? s.Queues.Min(q => q.Value.Min(w => w.Y)) : s.Y - Instance.layoutConfiguration.VerticalWaypointDistance / 2;
                    double highY = s.Queues != null && s.Queues.Any() && s.Queues.First().Value.Any() ? s.Queues.Max(q => q.Value.Max(w => w.Y)) : s.Y + Instance.layoutConfiguration.VerticalWaypointDistance / 2;
                    return new VolatileKeyValuePair<InputStation, SimpleRectangle>(s, new SimpleRectangle(s.Tier, lowX, lowY, highX - lowX, highY - lowY));
                }).ToList());
            return _queueZonesIStations[station].IsContained(Tier, X, Y);
        }

        #endregion

        #region IUpdateable Members

        /// <summary>
        /// The next event when this element has to be updated.
        /// </summary>
        /// <param name="currentTime">The current time of the simulation.</param>
        /// <returns>The next time this element has to be updated.</returns>
        public override double GetNextEventTime(double currentTime)
        {
            // Return soonest event that has not happened yet
            var minUntil = Double.PositiveInfinity;
            if (_waitUntil >= currentTime) minUntil = Math.Min(_waitUntil, minUntil);
            if (_waitUntil + _rotateDuration >= currentTime) minUntil = Math.Min(_waitUntil + _rotateDuration, minUntil);
            if (_waitUntil + _rotateDuration + _driveDuration >= currentTime) minUntil = Math.Min(_waitUntil + _rotateDuration + _driveDuration, minUntil);
            return minUntil;
        }

        /// <summary>
        /// update bot
        /// </summary>
        /// <param name="lastTime">time stamp: last update</param>
        /// <param name="currentTime">time stamp: now</param>
        public override void Update(double lastTime, double currentTime)
        {
            //wait short start time

            if (currentTime < 0.2)
                return;
            //bot is blocked
            if (_waitUntil >= currentTime)
            {
                // We still want to update the statistics of the bot
                _updateStatistics(currentTime - lastTime, X, Y);
                return;
            }

            var delta = currentTime - lastTime;
            var xOld = X;
            var yOld = Y;

            //get a task
            if (StateQueueCount == 0)
            {
                if (CurrentTask != null)
                    Instance.Controller.BotManager.TaskComplete(this, CurrentTask);
                Instance.Controller.BotManager.RequestNewTask(this);
            }

            //do state dependent action
            if (StateQueueCount > 0)
                StateQueuePeek().Act(this, lastTime, currentTime);

            //bot is blocked
            if (this._waitUntil >= currentTime)
                return;

            // Indicate change
            _changed = true;
            Instance.Changed = true;

            //get target orientation
            _updateDrive(lastTime, currentTime);

            //do state dependent action
            if (StateQueueCount > 0)
                StateQueuePeek().Act(this, lastTime, currentTime);

            //save statistics
            _updateStatistics(delta, xOld, yOld);
        }

        /// <summary>
        /// Drive the bot.
        /// </summary>
        /// <param name="lastTime">The last time.</param>
        /// <param name="currentTime">The current time.</param>
        private void _updateDrive(double lastTime, double currentTime)
        {
            // Is there a rotation still going on?
            if (_waitUntil + _rotateDuration >= currentTime)
            {
                // --> First rotate
                _updateRotation(currentTime);
            }
            else
            {
                // Complete any started rotation
                if (_endOrientation != Orientation)
                {
                    //_rotateDuration = 0;
                    Orientation = _endOrientation;
                    _startOrientation = _endOrientation;
                    if (this.Pod != null && Instance.SettingConfig.RotatePods)
                        this.Pod.Orientation = _endOrientation;
                }

                // --> Then move (if we have a target)
                if (NextWaypoint != null)
                {
                    _updateMove(currentTime);
                }
            }
        }

        /// <summary>
        /// update the rotation to the target orientation
        /// </summary>
        /// <param name="currentTime">time stamp: now</param>
        private void _updateRotation(double currentTime)
        {
            //stop the pod (this should already be 0)
            XVelocity = YVelocity = 0;

            // use the real turn speed only at the beginning and the end of the path, otherwise use a lower speed
            if (this is MovableStation && (isInPlace || isStartingMove))
                Orientation = Physics.getOrientationAfterTimeStep(_startOrientation, _endOrientation, currentTime - _waitUntil, Instance.layoutConfiguration.StartStopTurnSpeed);
            else 
                Orientation = Physics.getOrientationAfterTimeStep(_startOrientation, _endOrientation, currentTime - _waitUntil);

            //set the pod orientation
            if (this.Pod != null && Instance.SettingConfig.RotatePods)
                this.Pod.Orientation = Orientation;
        }

        /// <summary>
        /// move the bot towards the next way point
        /// </summary>
        /// <param name="currentTime">time stamp: now</param>
        private void _updateMove(double currentTime)
        {
            double currentSpeed = GetSpeed();
            if (currentSpeed > 0.1) isStartingMove = false; //reset flag

            getNewPosition(currentTime, currentSpeed, out var xNew, out var yNew, out _);//discard speed

            Waypoint oldWaypoint = null, newWaypoint = null;
            
            if(Instance.SettingConfig.DetectPassByEvents)
            {
                oldWaypoint = Instance.FindWpFromXY(X, Y);
                newWaypoint = Instance.FindWpFromXY(xNew, yNew);
            }

            if (currentTime >= _waitUntil + _rotateDuration + _driveDuration)
            {
                //reached goal
                XVelocity = YVelocity = 0;
                xNew = NextWaypoint.X;
                yNew = NextWaypoint.Y;
                CurrentWaypoint = NextWaypoint;
                NextWaypoint = null;
            }

            // Try to make move. If can't ask move due to a collision, then stop
            if (!Instance.Compound.BotCurrentTier[this].MoveBotOverride(this, xNew, yNew))
            {
                // Log the potential collision
                //Instance.LogInfo("Potential collision (" + GetIdentfierString() + ") - adding check for crashhandler ...");
                // Mark the bot for collision investigation
                Instance.BotCrashHandler.AddPotentialCrashBot(this);
            }

            if(Instance.SettingConfig.DetectPassByEvents)
            {
                newWaypoint.Bots.Add(this); //if this is already a part of Bots, nothing will happen
                                            //if oldWaypoint is different from newWaypoint, log that bot left old waypoint
                if (oldWaypoint != newWaypoint)
                {
                    if (oldWaypoint.Bots.Contains(this))
                        oldWaypoint.Bots.Remove(this);
                    //remove any possible pass by event of the previous waypoint
                    PassByEvent.Remove(this, oldWaypoint);
                }
            }

            // Check whether bot is now in destination's queueing area
            if (!double.IsNaN(_queueTripStartTime))
            {
                // Check whether the destination is an output-station and we reached it
                if (DestinationWaypoint.OutputStation != null)
                    if (IsInStationQueueZone(DestinationWaypoint.OutputStation))
                    {
                        Instance.NotifyTripCompleted(this, Statistics.StationTripDatapoint.StationTripType.O, Instance.Controller.CurrentTime - _queueTripStartTime);
                        _queueTripStartTime = double.NaN;
                    }
                // Check whether the destination is an input-station and we reached it
                if (DestinationWaypoint.InputStation != null)
                    if (IsInStationQueueZone(DestinationWaypoint.InputStation))
                    {
                        Instance.NotifyTripCompleted(this, Statistics.StationTripDatapoint.StationTripType.I, Instance.Controller.CurrentTime - _queueTripStartTime);
                        _queueTripStartTime = double.NaN;
                    }
            }

            //in case this is MovableStation, check for other MovableStations passing by in the opposite directions
            if(Instance.SettingConfig.DetectPassByEvents && this is MovableStation)
            { 
                //check all the neighboring waypoint
                foreach(var neighborWp in newWaypoint.Paths)
                {
                    //check all moving movable stations in the neighboring waypoint
                    foreach(var bot in neighborWp.Bots.Where(b => b is MovableStation && (b as MovableStation).CurrentBotStateType == BotStateType.Move))
                    {
                        const double tolerance = 0.25; //~15 degrees
                        double orientationDifference = Math.Abs(Orientation - bot.Orientation);
                        //if orientation difference is around PI, bots are passing by each other or they are oriented one towards another
                        if (Math.PI - tolerance <= orientationDifference && orientationDifference <= Math.PI + tolerance)
                        {
                            var numTolerance = Instance.SettingConfig.NumericalTolerance;
                            //if both bots have YVelocity zero, then their actual current waypoints need to have the same x and different y                                                                // horizontal pass   Vertical pass
    /*picture on right*/    bool HorizontalPass = Math.Abs(YVelocity - 0) < numTolerance && Math.Abs(bot.YVelocity - 0) < numTolerance && newWaypoint.X == neighborWp.X && newWaypoint.Y != neighborWp.Y;  // | |<-O| |        |   | | |
                            bool VertiacalPass  = Math.Abs(XVelocity - 0) < numTolerance && Math.Abs(bot.XVelocity - 0) < numTolerance && newWaypoint.Y == neighborWp.Y && newWaypoint.X != neighborWp.X;  // | |O->| |        | O | O |
                                                                                                                                                                                                           //                  | | |   |
                            //if bots are in Horizontal or vertical pass, log passing by event                                                                                                             //                  | V |   |
                            if (HorizontalPass || VertiacalPass)
                            {
                                 PassByEvent.Log(new PassByEvent(this, bot, newWaypoint, neighborWp, currentTime), currentTime);
                            }
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Calculate the next robot position and velocity
        /// </summary>
        /// <param name="currentTime"></param>
        /// <param name="currentSpeed"></param>
        /// <param name="xNew"></param>
        /// <param name="yNew"></param>
        /// <param name="speed"></param>
        private void getNewPosition(double currentTime, double currentSpeed, out double xNew, out double yNew, out double speed)
        {
            //calculate time durations for different driving phases (acceleration, moving with maximum speed, deceleration)
            if (_calculateTimeNeededToMove)
            {
                Physics.getTimeNeededToMove(currentSpeed, GetDistance(NextWaypoint));
                _calculateTimeNeededToMove = false;
                _previousCallTime = 0.0;
                Physics.resetTotalDistanceTraveled();
            }

            double timeSpan = (currentTime - _waitUntil - _rotateDuration) - _previousCallTime;
            _previousCallTime = (currentTime - _waitUntil - _rotateDuration);
            Physics.GetDistanceTraveledAfterTimeStep(currentSpeed, timeSpan, out double distanceTraveledInTimeSpan, out double totalDistanceTraveled, out speed);

            //set speed
            XVelocity = Math.Cos(Orientation) * speed;
            YVelocity = Math.Sin(Orientation) * speed;

            ////initiate new positions and reset it during this method
            xNew = X + Math.Cos(Orientation) * distanceTraveledInTimeSpan;
            yNew = Y + Math.Sin(Orientation) * distanceTraveledInTimeSpan;
        }

        /// <summary>
        /// Aborts current drive mission, clears Path, changes destinations, clears state queue and adds drive to closest waypoint
        /// </summary>
        /// <param name="currentSpeed">Current speed of this bot</param>
        /// <returns>Waypoint at which bot will stop</returns>
        public Waypoint AbortDrive(double currentSpeed)
        {
            //if current speed is 0, no need to look for closest stop waypoint
            if(currentSpeed == 0)
            {
                //clear path of old values
                Path?.Clear();
                DestinationWaypoint = CurrentWaypoint;
                NextWaypoint = null;
                //remove old reservation
                Instance.Controller.PathManager.RemoveReservations(this);
                //make new reservations
                Instance.Controller.PathManager.RegisterNextWaypoint(this, Instance.Controller.CurrentTime, Math.Max(_waitUntil, Instance.Controller.CurrentTime), 0.0, CurrentWaypoint, DestinationWaypoint, out _);
                //set flag to allow calling getTimeNeededToMove
                _calculateTimeNeededToMove = true;
                return CurrentWaypoint;
            }

            //get distance needed to stop
            double distToStop = Physics.getDistanceToStop(currentSpeed);

            //closest point for stopping exact
            var xStop = X + distToStop * Math.Cos(_endOrientation);
            var yStop = Y + distToStop * Math.Sin(_endOrientation);

            //set the closest waypoint on which the robot can actually stop as destination
            var closestWp = Instance.FindWpFromXY(xStop, yStop);
            xStop = (XVelocity > Instance.SettingConfig.NumericalTolerance  && xStop > closestWp.X) ? closestWp.X + Instance.layoutConfiguration.HorizontalWaypointDistance : // >
                    (XVelocity < -Instance.SettingConfig.NumericalTolerance && xStop < closestWp.X) ? closestWp.X - Instance.layoutConfiguration.HorizontalWaypointDistance : // <
                    closestWp.X;
            yStop = (YVelocity > Instance.SettingConfig.NumericalTolerance && yStop > closestWp.Y) ? closestWp.Y + Instance.layoutConfiguration.VerticalWaypointDistance : // ^
                    (YVelocity < -Instance.SettingConfig.NumericalTolerance && yStop < closestWp.Y) ? closestWp.Y - Instance.layoutConfiguration.VerticalWaypointDistance : // v
                    closestWp.Y;
            DestinationWaypoint = Instance.FindWpFromXY(xStop, yStop);

            //set the closest waypoint to the current bot position as current waypoint
            CurrentWaypoint = Instance.FindWpFromXY(X, Y);
            var waitUntil = Math.Max(_waitUntil, Instance.Controller.CurrentTime);

            // Set flag to allow calling getTimeNeededToMove
            _calculateTimeNeededToMove = true;

            //register new waypoint and set variables values accordingly
            if (Instance.Controller.PathManager.RegisterNextWaypoint(this, Instance.Controller.CurrentTime, waitUntil, 0.0, CurrentWaypoint, DestinationWaypoint, out var BlockedAgentIdxs))
            {
                //set next waypoint to be new destination waypoint
                NextWaypoint = DestinationWaypoint;

                //set move times
                _waitUntil = waitUntil;
                _rotateDuration = 0; //bot.GetDistance && currentSpeed instead of 0
                _driveDuration = Physics.getTimeNeededToMove(GetSpeed(), GetDistance(NextWaypoint));
            }
            else
            {
                //remove reservation from this bot so that other blocked bots can plan their breaking without old reservations
                Instance.Controller.PathManager.RemoveReservations(this);
                HashSet<int> blockedAgentIdxs = new HashSet<int>(BlockedAgentIdxs); //to reduce complexity of the next loop
                foreach (var blockedBot in Instance.MovableStations.Where(ms => blockedAgentIdxs.Contains(ms.ID)))
                {
                    var stopWP = blockedBot.AbortDrive(blockedBot.GetSpeed());

                    blockedBot.PrependMoveStates(blockedBot.CurrentWaypoint, stopWP, false, true);
                    blockedBot.RequestReoptimization = true;
                }

                //register new waypoint again and set variables values accordingly. Now that all BlockedAgents aborted their drive, we should be able to stop
                Instance.Controller.PathManager.RegisterNextWaypoint(this, Instance.Controller.CurrentTime, waitUntil, 0.0, CurrentWaypoint, DestinationWaypoint, out var test);
                //set next waypoint to be new destination waypoint
                NextWaypoint = DestinationWaypoint;
                //set move times
                _waitUntil = waitUntil;
                _rotateDuration = 0;
                _driveDuration = Physics.getTimeNeededToMove(GetSpeed(), GetDistance(NextWaypoint)); 
            }

            //clear path of old values
            Path.Clear();
            return DestinationWaypoint;
        }

        /// <summary>
        /// update statistical data
        /// </summary>
        /// <param name="delta">time passed since last update</param>
        /// <param name="xOld">Position x before update</param>
        /// <param name="yOld">Position y before update</param>
        private void _updateStatistics(double delta, double xOld, double yOld)
        {
            // Measure moving time
            if (Moving)
                StatTotalTimeMoving += delta;
            // Measure queueing time
            if (IsQueueing)
                StatTotalTimeQueuing += delta;
            // Measure rotating time
            if (!Moving && _startOrientation != _endOrientation &&
                CurrentBotStateType == BotStateType.Move)
                StatTotalTimeRotating += delta;
            // Measure Idle move time (no linear or angle velocity but in Move state)
            if (!Moving && _startOrientation == _endOrientation && !IsQueueing &&
                _waitUntil > Instance.Controller.CurrentTime &&
                CurrentBotStateType == BotStateType.Move)
                StatTotalTimeIdleMoving += delta;

            // Set moving flag bot
            if (XVelocity == 0.0 && YVelocity == 0.0)
                Moving = false;
            else
                Moving = true;

            // Set moving flag pod
            if (Pod != null)
                Pod.Moving = Moving;

            // Count distanceTraveled
            StatDistanceTraveled += Math.Sqrt((X - xOld) * (X - xOld) + (Y - yOld) * (Y - yOld));

            // Compute time in previous task
            StatTotalTaskTimes[StatLastTask] += delta;
            StatLastTask = CurrentTask != null ? CurrentTask.Type : BotTaskType.None;

            // Measure time spent in state
            StatTotalStateTimes[StatLastState] += delta;
            StatLastState = StateQueueCount > 0 ? StateQueuePeek().Type : BotStateType.Rest;

            bool blocked = Instance.Controller.CurrentTime < _waitUntil || Instance.Controller.CurrentTime < BlockedUntil;

            // when bot starts repeating actions: e.g. moving left-right
            double time_interval = Instance.Controller.CurrentTime - LastTimeWhenBlocked;
            if (blocked)
            {
                LastTimeWhenBlocked = Instance.Controller.CurrentTime;
                // if it it blocked-unblocked fast enough
                if (time_interval < SlowestBlockedLoopInterval)
                {
                    ++BlockedLoopSwitchesCount; // increment number of block-unblocks
                    BlockedLoopTime += time_interval; // how much time was spent in block-unblock
                }
                else // reset counters if it exited the repetative blocking
                {
                    BlockedLoopSwitchesCount = 0;
                    BlockedLoopTime = 0.0;
                    BlockedLoopFrequencey = 0;
                }
                // if enough switches, calculate the blocking frequency
                if (BlockedLoopSwitchesCount > 10)
                    BlockedLoopFrequencey = BlockedLoopSwitchesCount / BlockedLoopTime;
            }
            else
            {
                if (time_interval > SlowestBlockedLoopInterval) BlockedLoopFrequencey = 0;
            }
        }
        #endregion

        #region IBotInfo Members

        public override int GetInfoPodLocationID(int approachID)
        {
            var task = (MultiPointGatherTask)CurrentTask;
            Waypoint podLocation = task.PodLocations[task.Locations.FindIndex(l => l.ID == approachID)];
            return podLocation.ID;
        }

        public override Tuple<string, int> GetCurrentItemAddressAndMate()
        {
            Tuple<string, int> addressAndMate = Instance.Controller.MateScheduler.itemTable[ID].GetFirstAssignment();
            return addressAndMate;
        }


        public override List<Tuple<string, bool, int, bool>> GetStatus()
        {
            return Instance.Controller.MateScheduler.itemTable[ID].GetStatus();
        }

        /// <summary>
        /// x position of the goal for the info panel
        /// </summary>
        /// <returns>x position</returns>
        public override double GetInfoGoalX()
        {
            Waypoint destinationWP = DestinationWaypoint;
            if (destinationWP != null)
                return destinationWP.X;
            else
                return X;
        }
        /// <summary>
        /// y position of the goal for the info panel
        /// </summary>
        /// <returns>y position</returns>
        public override double GetInfoGoalY()
        {
            Waypoint destinationWP = DestinationWaypoint;
            if (destinationWP != null)
                return destinationWP.Y;
            else
                return Y;
        }
        /// <summary>
        /// target for the info panel
        /// </summary>
        /// <returns>orientation</returns>
        public override double GetInfoTargetOrientation() { return GetTargetOrientation(); }
        /// <summary>
        /// state for the info panel
        /// </summary>
        /// <returns>state</returns>
        public override string GetInfoState() { return CurrentInfoStateName; }
        /// <summary>
        /// Gets the current waypoint that is considered by planning.
        /// </summary>
        /// <returns>The current waypoint.</returns>
        public override IWaypointInfo GetInfoCurrentWaypoint() { return CurrentWaypoint; }
        /// <summary>
        /// Destination way point in the info panel
        /// </summary>
        /// <returns>destination</returns>
        public override IWaypointInfo GetInfoDestinationWaypoint() { return NextWaypoint; }
        /// <summary>
        /// Destination way point in the info panel
        /// </summary>
        /// <returns>destination</returns>
        public override IWaypointInfo GetInfoGoalWaypoint() { return DestinationWaypoint; }
        /// <summary>
        /// The current path the bot is following.
        /// </summary>
        private List<IWaypointInfo> _currentPath = new List<IWaypointInfo>();
        /// <summary>
        /// Gets the current path of the bot.
        /// </summary>
        /// <returns>The current path.</returns>
        public override List<IWaypointInfo> GetInfoPath() { return _currentPath; }
        /// <summary>
        /// Indicates whether the robot is currently blocked.
        /// </summary>
        /// <returns><code>true</code> if the robot is blocked, <code>false</code> otherwise.</returns>
        public override bool GetInfoBlocked() { return Instance.Controller.CurrentTime < _waitUntil || Instance.Controller.CurrentTime < BlockedUntil; }
        /// <summary>
        /// The time until the bot is blocked.
        /// </summary>
        /// <returns>The time until the bot is blocked.</returns>
        public override double GetInfoBlockedLoopFrequency() { return BlockedLoopFrequencey; }
        public override double GetInfoBlockedLeft()
        {
            double currentTime = Instance.Controller.CurrentTime; double waitUntil = _waitUntil; double blockedUntil = BlockedUntil;
            // Return next block release time that lies in the future
            return
                currentTime < waitUntil && currentTime < blockedUntil ? Math.Min(_waitUntil, blockedUntil) - currentTime :
                currentTime < waitUntil ? waitUntil - currentTime :
                currentTime < blockedUntil ? blockedUntil - currentTime :
                double.NaN;
        }
        /// <summary>
        /// Indicates whether the bot is currently queueing in a managed area.
        /// </summary>
        /// <returns><code>true</code> if the robot is within a queue area, <code>false</code> otherwise.</returns>
        public override bool GetInfoIsQueueing() { return IsQueueing; }

        #endregion

        #region Events
        /// <summary>
        /// Called when [bot reached way point].
        /// </summary>
        /// <param name="waypoint">The way point.</param>
        /// <exception cref="System.NotImplementedException"></exception>
        public override void OnReachedWaypoint(Waypoint waypoint)
        {
            //not necessary 
            if (!Instance.SettingConfig.RealWorldIntegrationEventDriven)
                return;

            // Logging info message
            Instance.LogInfo("Bot" + this.ID + " is at: " + waypoint.ID);

            //we are not interested in intermediate points
            if (waypoint.ID == _nextWaypointID || !_initialEventReceived)
                lock (this)
                {
                    _nextWaypointID = -1;
                    _eventReachedNextWaypoint = true; _initialEventReceived = true;
                    XVelocity = YVelocity = BlockedUntil = _waitUntil = _rotateDuration = _driveDuration = 0;
                }
        }

        /// <summary>
        /// Called when [bot picked up the pod].
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        public override void OnPickedUpPod()
        {
            //not necessary 
            if (!Instance.SettingConfig.RealWorldIntegrationEventDriven)
                return;

            //stop blocking
            BlockedUntil = _waitUntil = 0;
        }

        /// <summary>
        /// Called when [bot set down pod].
        /// </summary>
        /// <exception cref="System.NotImplementedException"></exception>
        public override void OnSetDownPod()
        {
            //not necessary 
            if (!Instance.SettingConfig.RealWorldIntegrationEventDriven)
                return;

            //stop blocking
            BlockedUntil = _waitUntil = 0;
        }

        /// <summary>
        /// called when AbortingTask is assigned
        /// </summary>
        public override void OnAbortingTaskAsigned()
        {
            throw new NotImplementedException();
        }
        #endregion
    }
}
