using RAWSimO.Core.Control;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Geometrics;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.Metrics;
using RAWSimO.Core.Waypoints;
using RAWSimO.MultiAgentPathFinding;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Core.Bots
{
    #region Move state

    /// <summary>
    /// The state defining the operation of moving.
    /// </summary>
    internal class BotMove : IBotState
    {
        /// <summary>
        /// next node to reach
        /// </summary>
        public Waypoint DestinationWaypoint { get; set; }

        /// <summary>
        /// bool indicating that bot is breaking
        /// </summary>
        public bool IsBreaking { get; set; }

        /// <summary>
        /// constructor
        /// </summary>
        /// <param name="w">way point</param>
        public BotMove(Waypoint w, bool isBreaking = false) { DestinationWaypoint = w; IsBreaking = isBreaking; }

        /// <summary>
        /// Indicates whether we just entered the state.
        /// </summary>
        private bool _initialized;

        /// <summary>
        /// Logs an unfinished trip.
        /// </summary>
        /// <param name="bot">The bot that is logging the trip.</param>
        internal void LogUnfinishedTrip(BotNormal bot)
        {
            // Manage connectivity statistics
            if (bot.DestinationWaypoint != null)
                bot.DestinationWaypoint.StatLogUnfinishedTrip(bot);
        }

        /// <summary>
        /// act
        /// </summary>
        /// <param name="self">driver</param>
        /// <param name="lastTime">The time before this update.</param>
        /// <param name="currentTime">The current time.</param>
        public void Act(Bot self, double lastTime, double currentTime)
        {
            var bot = self as BotNormal;
            var rm = bot.Instance.ResourceManager;

            // If it's the first time executing this, log the start time of the trip
            if (!_initialized)
            {
                bot.isStartingMove = true;
                bot.Path = new Path();
                // Track path statistics
                self.StatLastTripStartTime = currentTime;
                self.StatTotalStateCounts[Type]++;
                self.StatDistanceRequestedOptimal += self.Pod != null ?
                    Distances.CalculateShortestPathPodSafe(bot.CurrentWaypoint, DestinationWaypoint, bot.Instance) :
                    Distances.CalculateShortestPath(bot.CurrentWaypoint, DestinationWaypoint, bot.Instance);
                // Track last mile statistics
                if (DestinationWaypoint.OutputStation != null)
                {
                    if (!bot.IsInStationQueueZone(DestinationWaypoint.OutputStation))
                        // Start the trip now
                        bot._queueTripStartTime = bot.Instance.Controller.CurrentTime;
                    else
                        // Already at the location - no trip to do
                        bot._queueTripStartTime = double.NaN;
                }
                else if (DestinationWaypoint.InputStation != null)
                {
                    if (!bot.IsInStationQueueZone(DestinationWaypoint.InputStation))
                        // Start the trip now
                        bot._queueTripStartTime = bot.Instance.Controller.CurrentTime;
                    else
                        // Already at the location - no trip to do
                        bot._queueTripStartTime = double.NaN;
                }
                else
                {
                    // No station trip - do not track
                    bot._queueTripStartTime = double.NaN;
                }
                // Mark initialized
                _initialized = true;
            }

            //if bot is driving, do nothing, _updateDrive() will move the bot between key waypoints
            if (bot.GetSpeed() > 0)
            {
                return;
            }

            //set destination way point
            bot.DestinationWaypoint = DestinationWaypoint;

            //we are at the destination && RealWorldIntegrationEventDriven
            if (bot.CurrentWaypoint == bot.DestinationWaypoint)
            {
                // Manage connectivity statistics
                if (bot.DestinationWaypoint != null)
                    bot.CurrentWaypoint.StatReachedDestination(bot);

                // Remove this task
                bot.NextWaypoint = null;
                bot.DestinationWaypoint = null;
                // Notify location manager that bot reached the destination waypoint
                if (bot.Instance.SettingConfig.UsingLocationManager)
                    bot.Instance.Controller.PathManager._locationManager.BotReachedDestination(bot);
                bot.DequeueState(lastTime, currentTime);
                return;
            }

            // Only mark the move state if we weren't already at the destination waypoint
            bot._lastExteriorState = Type;

            //the bot has already something to do
            if (bot.NextWaypoint != null)
                return;

            //Has the bot a path?
            if (bot.Path == null || bot.Path.Count == 0)
            {
                bot.RequestReoptimization = true;
                return;
            }

            //Bot reached the next way point?
            if (bot.Instance.Controller.PathManager.GetWaypointByNodeId(bot.Path.NextAction.Node) != bot.CurrentWaypoint)
            {
                // --> Not reached yet
                bool successfulRegistration = bot.setNextWaypoint(bot.Instance.Controller.PathManager.GetWaypointByNodeId(bot.Path.NextAction.Node), currentTime);
                if (successfulRegistration)
                    bot._eventReachedNextWaypoint = false;
                else
                    bot._waitUntil = currentTime + 1;
            }
            else
            {
                // --> Bot reached the next way point
                // See whether turning to prepare for next move is necessary
                if (bot.Path.NextAction == bot.Path.LastAction && bot.Path.NextNodeToPrepareFor >= 0)
                {
                    // Calculate turn times
                    bot._startOrientation = bot.Orientation;
                    Waypoint turnTowards = bot.Instance.Controller.PathManager.GetWaypointByNodeId(bot.Path.NextNodeToPrepareFor);
                    bot._endOrientation = Circle.GetOrientation(bot.X, bot.Y, turnTowards.X, turnTowards.Y);
                    double rotateDuration;
                    // use the real turn speed only at the beginning and the end of the path, otherwise use a lower speed
                    if (bot is MovableStation && (bot.isInPlace || bot.isStartingMove))
                        rotateDuration = bot.Physics.getTimeNeededToTurn(bot._startOrientation, bot._endOrientation, bot.Instance.layoutConfiguration.StartStopTurnSpeed);
                    else
                        rotateDuration = bot.Physics.getTimeNeededToTurn(bot._startOrientation, bot._endOrientation);
                    
                    // Forget about the node
                    bot.Path.NextNodeToPrepareFor = -1;
                    // See whether we need to turn at all
                    if (rotateDuration > 0)
                    {
                        // Proceed with turn then come back here to get rid of the action
                        bot._waitUntil = Math.Max(bot._waitUntil, currentTime);
                        bot._rotateDuration = rotateDuration;
                        return;
                    }
                }

                // Set wait until time
                if (bot.Path.NextAction.StopAtNode && bot.Path.NextAction.WaitTimeAfterStop > 0)
                    bot._waitUntil = currentTime + bot.Path.NextAction.WaitTimeAfterStop;

                //pop the node
                bot.Path.RemoveFirstAction();

                //skip all non-stopping nodes
                while (bot.Path.Count > 0 && bot.Path.NextAction.StopAtNode == false)
                    bot.Path.RemoveFirstAction();

                if (bot.Path == null || bot.Path.Count == 0)
                {
                    if (bot._waitUntil <= currentTime)
                        bot.RequestReoptimization = true;
                    return;
                }

                //special case intervention
                Waypoint nextWaypoint = bot.Instance.Controller.PathManager.GetWaypointByNodeId(bot.Path.NextAction.Node);
                if (nextWaypoint.X == bot.X && nextWaypoint.Y == bot.Y && bot.Path.Count == 1)
                {
                    bot.CurrentWaypoint = nextWaypoint;
                    bot.Path.Clear();
                    bot.requestReoptimization = true;
                    bot._waitUntil = currentTime + 1;
                    bot._rotateDuration = 0;
                    bot._driveDuration = 0;
                    return;
                }

                //set next destination
                if (bot.setNextWaypoint(nextWaypoint, currentTime))
                    bot._eventReachedNextWaypoint = false;
                else
                    bot._waitUntil = currentTime + 1;
            }
        }

        /// <summary>
        /// Notifies the move state, that a collision occurred.
        /// </summary>
        /// <param name="bot">The bot.</param>
        /// <param name="currentTime">The current simulation time.</param>
        internal void NotifyCollision(BotNormal bot, double currentTime)
        {
            if (bot.Path == null)
                bot.Path = new Path();

            //drive back to passed way point
            bot.setNextWaypoint(bot.CurrentWaypoint, currentTime);

            bot.Path.AddFirst(bot.Instance.Controller.PathManager.GetNodeIdByWaypoint(bot.CurrentWaypoint), true, 0);
        }
        /// <summary>
        /// state name
        /// </summary>
        /// <returns>name</returns>
        public override string ToString() { return "Move"; }

        /// <summary>
        /// State type.
        /// </summary>
        public virtual BotStateType Type { get { return BotStateType.Move; } }

    }
    #endregion

    #region Pickup and Set down states

    /// <summary>
    /// The state defining the operation of picking up a pod at the current location.
    /// </summary>
    internal class BotPickupPod : IBotState
    {
        private Pod _pod;
        private Waypoint _waypoint;
        private bool _initialized = false;
        private bool _executed = false;
        public BotPickupPod(Pod b) { _pod = b; _waypoint = _pod.Waypoint; }
        public Waypoint DestinationWaypoint { get { return _waypoint; } set { _waypoint = value; } }
        public void Act(Bot self, double lastTime, double currentTime)
        {
            var bot = self as BotNormal;

            // Remember the last state we were in
            bot._lastExteriorState = Type;

            // Initialize
            if (!_initialized) { self.StatTotalStateCounts[Type]++; _initialized = true; }

            // Dequeue the state as soon as it is finished
            if (_executed)
            {
                bot.DequeueState(lastTime, currentTime);
                return;
            }
            // Act based on whether pod was picked up
            if (bot.PickupPod(_pod, currentTime))
            {
                _executed = true;
                bot.WaitUntil(bot.BlockedUntil);
                bot.Instance.WaypointGraph.PodPickup(_pod);
                bot.Instance.Controller.BotManager.PodPickedUp(bot, _pod, _waypoint);

                //#RealWorldIntegraton.Start
                //Trigger comes from outside => stay blocked
                if (bot.Instance.SettingConfig.RealWorldIntegrationEventDriven)
                    bot.BlockedUntil = bot._waitUntil = double.PositiveInfinity;
                //#RealWorldIntegraton.End
            }
            else
            {
                // Failed to pick up pod
                bot.StateQueueClear();
                bot.Instance.Controller.BotManager.TaskAborted(bot, bot.CurrentTask);
            }
        }

        /// <summary>
        /// state name
        /// </summary>
        /// <returns>name</returns>
        public override string ToString() { return "PickupPod"; }

        /// <summary>
        /// State type.
        /// </summary>
        public BotStateType Type { get { return BotStateType.PickupPod; } }
    }

    /// <summary>
    /// The state defining the operation of setting down a pod at the current location.
    /// </summary>
    internal class BotSetdownPod : IBotState
    {
        private Waypoint _waypoint;
        private bool _initialized = false;
        private bool _executed = false;
        public BotSetdownPod(Waypoint w) { _waypoint = w; }
        public Waypoint DestinationWaypoint { get { return _waypoint; } set { _waypoint = value; } }
        public void Act(Bot self, double lastTime, double currentTime)
        {
            var bot = self as BotNormal;

            // Remember the last state we were in
            bot._lastExteriorState = Type;

            // Initialize
            if (!_initialized) { self.StatTotalStateCounts[Type]++; _initialized = true; }

            // Dequeue the state as soon as it is finished
            if (_executed)
            {
                bot.DequeueState(lastTime, currentTime);
                return;
            }

            //remember Pod
            Pod pod = bot.Pod;

            // Act based on whether pod was set down
            if (bot.SetdownPod(currentTime))
            {
                _executed = true;
                bot.WaitUntil(bot.BlockedUntil);
                bot.Instance.WaypointGraph.PodSetdown(pod, _waypoint);
                bot.Instance.Controller.BotManager.PodSetDown(bot, pod, _waypoint);

                //#RealWorldIntegraton.Start
                //Trigger comes from outside => stay blocked
                if (bot.Instance.SettingConfig.RealWorldIntegrationEventDriven)
                    bot.BlockedUntil = bot._waitUntil = double.PositiveInfinity;
                //#RealWorldIntegraton.End
            }
            else
            {
                // Failed to set down pod
                bot.StateQueueClear();
                bot.Instance.Controller.BotManager.TaskAborted(bot, bot.CurrentTask);
            }
        }

        /// <summary>
        /// state name
        /// </summary>
        /// <returns>name</returns>
        public override string ToString() { return "SetdownPod"; }

        /// <summary>
        /// State type.
        /// </summary>
        public BotStateType Type { get { return BotStateType.SetdownPod; } }
    }

    #endregion

    #region Get and Put states

    /// <summary>
    /// The state defining the operation of storing an item-bundle in the pod at an input-station.
    /// </summary>
    internal class BotGetItems : IBotState
    {
        private InsertTask _storeTask;
        private Waypoint _waypoint;
        private bool _initialized = false;
        private bool alreadyRequested = false;
        public BotGetItems(InsertTask storeTask) { _storeTask = storeTask; _waypoint = _storeTask.InputStation.Waypoint; }
        public Waypoint DestinationWaypoint { get { return _waypoint; } set { _waypoint = value; } }
        public void Act(Bot self, double lastTime, double currentTime)
        {
            var bot = self as BotNormal;

            // Initialize
            if (!_initialized) { self.StatTotalStateCounts[Type]++; _initialized = true; }

            //#RealWorldIntegration.start
            if (bot.Instance.SettingConfig.RealWorldIntegrationCommandOutput && bot._lastExteriorState != Type)
            {
                // Log the pickup command
                var sb = new StringBuilder();
                sb.Append("#RealWorldIntegration => Bot ").Append(bot.ID).Append(" Get");
                bot.Instance.SettingConfig.LogAction(sb.ToString());
                // Issue the pickup command
                bot.Instance.RemoteController.RobotSubmitGetItemCommand(bot.ID);
            }
            //#RealWorldIntegration.end

            // If this is the first put action at a station, register - we need to notify it
            if (bot._lastExteriorState != Type)
                _storeTask.InputStation.RegisterBot(bot);

            // Remember the last state we were in
            bot._lastExteriorState = Type;

            // If it's the first time, request the bundles
            if (!alreadyRequested)
            {
                _storeTask.InputStation.RequestBundle(bot, _storeTask.Requests.First());
                alreadyRequested = true;
            }

            if (bot.Pod == null)
            {
                // Something wrong happened... don't have a pod!
                bot.Instance.Controller.BotManager.TaskAborted(bot, bot.CurrentTask);
                bot.DequeueState(lastTime, currentTime);
                return;
            }

            // See if bundle has been deposited in the pod
            switch (_storeTask.Requests.First().State)
            {
                case Management.RequestState.Unfinished: /* Ignore */ break;
                case Management.RequestState.Aborted: // Request was aborted for some reason - give it back to the manager for re-insertion
                    {
                        // Remove the request that was just aborted
                        _storeTask.FirstAborted();
                        // See whether there are more bundles to store
                        if (_storeTask.Requests.Any())
                        {
                            // Store another one
                            alreadyRequested = false;
                        }
                        else
                        {
                            // We are done here
                            bot.DequeueState(lastTime, currentTime);
                            return;
                        }
                    }
                    break;
                case Management.RequestState.Finished: // Request was finished - we can go on
                    {
                        // Remove the request that was just completed
                        _storeTask.FirstStored();
                        // See whether there are more bundles to store
                        if (_storeTask.Requests.Any())
                        {
                            // Store another one
                            alreadyRequested = false;
                        }
                        else
                        {
                            // We are done here
                            bot.DequeueState(lastTime, currentTime);
                            return;
                        }
                    }
                    break;
                default: throw new ArgumentException("Unknown request state: " + _storeTask.Requests.First().State);
            }
        }

        /// <summary>
        /// state name
        /// </summary>
        /// <returns>name</returns>
        public override string ToString() { return "GetItems"; }

        /// <summary>
        /// State type.
        /// </summary>
        public BotStateType Type { get { return BotStateType.GetItems; } }
    }

    /// <summary>
    /// The state defining the operation of picking an item from the pod at an output-station.
    /// </summary>
    internal class BotPutItems : IBotState
    {
        ExtractTask _extractTask;
        Waypoint _waypoint;
        private bool _initialized = false;
        bool alreadyRequested = false;
        public BotPutItems(ExtractTask extractTask)
        { _extractTask = extractTask; _waypoint = extractTask.OutputStation.Waypoint; }
        public Waypoint DestinationWaypoint { get { return _waypoint; } set { _waypoint = value; } }
        public void Act(Bot self, double lastTime, double currentTime)
        {
            var bot = self as BotNormal;

            // Initialize
            if (!_initialized) { self.StatTotalStateCounts[Type]++; _initialized = true; }

            //#RealWorldIntegration.start
            if (bot.Instance.SettingConfig.RealWorldIntegrationCommandOutput && bot._lastExteriorState != Type)
            {
                // Log the pickup command
                var sb = new StringBuilder();
                sb.Append("#RealWorldIntegration => Bot ").Append(bot.ID).Append(" Put");
                bot.Instance.SettingConfig.LogAction(sb.ToString());
                // Issue the pickup command
                bot.Instance.RemoteController.RobotSubmitPutItemCommand(bot.ID);
            }
            //#RealWorldIntegration.end

            // If this is the first put action at a station, register - we need to notify it
            if (bot._lastExteriorState != Type)
                _extractTask.OutputStation.RegisterBot(bot);

            // Remember the last state we were in
            bot._lastExteriorState = Type;

            // If it's the first time, request the items be taken
            if (!alreadyRequested)
            {
                _extractTask.OutputStation.RequestItemTake(bot, _extractTask.Requests.First());
                alreadyRequested = true;
            }

            if (bot.Pod == null)
            {
                // Something wrong happened... don't have a pod!
                bot.Instance.Controller.BotManager.TaskAborted(bot, bot.CurrentTask);
                bot.StateQueueClear();
                return;
            }

            // See if item has been picked from the pod
            switch (_extractTask.Requests.First().State)
            {
                case Management.RequestState.Unfinished: /* Ignore */ break;
                case Management.RequestState.Aborted: // Request was aborted for some reason - give it back to the manager for re-insertion
                    {
                        // Remove the request that was just aborted
                        _extractTask.FirstAborted();
                        // See whether there are more items to pick
                        if (_extractTask.Requests.Any())
                        {
                            // Pick another one
                            alreadyRequested = false;
                        }
                        else
                        {
                            // We are done here
                            bot.DequeueState(lastTime, currentTime);
                            return;
                        }
                    }
                    break;
                case Management.RequestState.Finished: // Request was finished - we can go on
                    {
                        // Remove the request that was just completed
                        _extractTask.FirstPicked();
                        // See whether there are more items to pick
                        if (_extractTask.Requests.Any())
                        {
                            // Pick another one
                            alreadyRequested = false;
                        }
                        else
                        {
                            // We are done here
                            bot.DequeueState(lastTime, currentTime);
                            return;
                        }
                    }
                    break;
                default: throw new ArgumentException("Unknown request state: " + _extractTask.Requests.First().State);
            }
        }

        /// <summary>
        /// state name
        /// </summary>
        /// <returns>name</returns>
        public override string ToString() { return "PutItems"; }

        /// <summary>
        /// State type.
        /// </summary>
        public BotStateType Type { get { return BotStateType.PutItems; } }
    }

    /// <summary>
    /// The state defining the picking of pallet from the input pallet stand
    /// </summary>
    internal class BotGetPallet : IBotState
    {
        /// <summary>
        /// Constructs new <see cref="BotGetPallet"/> state
        /// </summary>
        /// <param name="waypoint">Waypoint of the input pallet stand</param>
        /// <param name="timeDuration">Time it takes to get a pallet</param>
        public BotGetPallet(Waypoint waypoint, double timeDuration = 54.37)
        {
            DestinationWaypoint = waypoint;
            actionDuration = timeDuration;
            actionStarted = false;
            startTime = double.NaN;
        }
        /// <summary>
        /// Location of the input pallet stand
        /// </summary>
        public Waypoint DestinationWaypoint { get; set; }
        /// <summary>
        /// Type of BotState
        /// </summary>
        public BotStateType Type => BotStateType.GetPallet;
        /// <summary>
        /// Gets string representing this state
        /// </summary>
        /// <returns>String which represents this state</returns>
        public override string ToString() { return "GetPallet"; }
        /// <summary>
        /// Defines how <see cref="Bot"/> will act in this state
        /// </summary>
        /// <param name="self"><see cref="Bot"/> which is in this state</param>
        /// <param name="lastTime">time of the last simulation tick</param>
        /// <param name="currentTime">time of the current simulation tick</param>
        public void Act(Bot self, double lastTime, double currentTime)
        {
            //check if action already started
            if (!actionStarted)
            {
                startTime = currentTime;
                actionStarted = true;
            }
            //check if action has been going for the required duration
            if (currentTime > startTime + actionDuration)
            {
                (self as BotNormal).StateQueueDequeue();
                self.IgnoreInputPalletStandQueue = true; //reset the flag once the pallet is on
                --DestinationWaypoint.InputPalletStand.IncomingBots;
            }
        }
        /// <summary>
        /// Time at which <see cref="Bot"/> started to get the pallet
        /// </summary>
        private double startTime;
        /// <summary>
        /// Bool indicating if the <see cref="Bot"/> already started the action
        /// </summary>
        private bool actionStarted;
        /// <summary>
        /// Parameter of how much the operation lasts
        /// </summary>
        private double actionDuration;
    }

    /// <summary>
    /// The state defining the drop of pallet on the output pallet stand
    /// </summary>
    internal class BotPutPallet : IBotState
    {
        /// <summary>
        /// Constructs new <see cref="BotPutPallet"/> state
        /// </summary>
        /// <param name="waypoint">Waypoint of the output pallet stand</param>
        /// <param name="timeDuration">Time it takes to put a pallet</param>
        public BotPutPallet(Waypoint waypoint, double timeDuration = 54.37)
        {
            DestinationWaypoint = waypoint;
            actionDuration = timeDuration;
            actionStarted = false;
            startTime = double.NaN;
        }
        /// <summary>
        /// Location of the output pallet stand
        /// </summary>
        public Waypoint DestinationWaypoint { get; set; }
        /// <summary>
        /// Type of BotState
        /// </summary>
        public BotStateType Type => BotStateType.PutPallet;
        /// <summary>
        /// Gets string representing this state
        /// </summary>
        /// <returns>String which represents this state</returns>
        public override string ToString() { return "PutPallet"; }
        /// <summary>
        /// Defines how <see cref="Bot"/> will act in this state
        /// </summary>
        /// <param name="self"><see cref="Bot"/> which is in this state</param>
        /// <param name="lastTime">time of the last simulation tick</param>
        /// <param name="currentTime">time of the current simulation tick</param>
        public void Act(Bot self, double lastTime, double currentTime)
        {

            //check if action already started
            if (!actionStarted)
            {
                startTime = currentTime;
                actionStarted = true;
            }
            //check if action has been going for the required duration
            if (currentTime > startTime + actionDuration)
            {
                (self as BotNormal).StateQueueDequeue();
                self.IgnoreOutputPalletStandQueue = true; //reset the flag once the pallet is off
                --DestinationWaypoint.OutputPalletStand.IncomingBots;
            }
        }
        /// <summary>
        /// Time at which <see cref="Bot"/> started to drop the pallet
        /// </summary>
        private double startTime;
        /// <summary>
        /// Bool indicating if the <see cref="Bot"/> already started the action
        /// </summary>
        private bool actionStarted;
        /// <summary>
        /// Parameter of how much the operation lasts
        /// </summary>
        private double actionDuration;
    }

    #endregion

    #region Use elevator state

    /// <summary>
    /// State: Bot uses an elevator to get to a different tier
    /// </summary>
    internal class UseElevator : IBotState
    {
        private Elevator _elevator;
        private Waypoint _waypointFrom;
        private Waypoint _waypointTo;
        private bool _initialized = false;
        private bool inUse;
        private double travelUntil;
        public Waypoint DestinationWaypoint { get { return _waypointTo; } set { _waypointTo = value; } }
        public UseElevator(Elevator elevator, Waypoint waypointFrom, Waypoint waypointTo) { _elevator = elevator; _waypointFrom = waypointFrom; _waypointTo = waypointTo; inUse = false; }
        public void Act(Bot self, double lastTime, double currentTime)
        {
            var bot = self as BotNormal;

            // Initialize
            if (!_initialized) { self.StatTotalStateCounts[Type]++; _initialized = true; }

            // Remember the last state we were in
            bot._lastExteriorState = Type;

            // Check if i already using the elevator
            if (!inUse)
            {
                //consistency
                if (!_elevator.ConnectedPoints.Contains(_waypointFrom) || !_elevator.ConnectedPoints.Contains(_waypointTo))
                    throw new NotSupportedException("Way point is not managed by Elevator!");

                inUse = true;
                travelUntil = currentTime + _elevator.GetTiming(_waypointFrom, _waypointTo);
                bot._waitUntil = travelUntil;
            }


            if (currentTime >= travelUntil)
            {
                //do the transportation
                _elevator.Transport(bot, _waypointFrom, _waypointTo);
                bot.CurrentWaypoint = _waypointTo;
                bot.DequeueState(lastTime, currentTime);
                return;
            }

        }

        /// <summary>
        /// state name
        /// </summary>
        /// <returns>name</returns>
        public override string ToString() { return "UseElevator"; }

        /// <summary>
        /// State type.
        /// </summary>
        public BotStateType Type { get { return BotStateType.UseElevator; } }
    }
    #endregion

    #region Rest state

    internal class BotRest : IBotState
    {
        // TODO make rest time randomized and parameterized
        private const double dEFAULT_REST_TIME = 5;
        private double _timeSpan;
        private bool _initialized = false;
        private bool alreadyRested = false;
        public BotRest(Waypoint waypoint, double timeSpan) { DestinationWaypoint = waypoint; _timeSpan = timeSpan; }
        public Waypoint DestinationWaypoint { get; set; }

        public void Act(Bot self, double lastTime, double currentTime)
        {
            var bot = self as BotNormal;

            // Initialize
            if (!_initialized) { self.StatTotalStateCounts[Type]++; _initialized = true; }

            //#RealWorldIntegration.start
            if (bot.Instance.SettingConfig.RealWorldIntegrationCommandOutput && bot._lastExteriorState != Type)
            {
                // Log the pickup command
                var sb = new StringBuilder();
                sb.Append("#RealWorldIntegration => Bot ").Append(bot.ID).Append(" Rest");
                bot.Instance.SettingConfig.LogAction(sb.ToString());
                // Issue the pickup command
                bot.Instance.RemoteController.RobotSubmitRestCommand(bot.ID);
            }
            //#RealWorldIntegration.end

            // Remember the last state we were in
            bot._lastExteriorState = Type;

            // Randomly rest or exit resting
            if (!alreadyRested)
            {
                // Rest for a predefined period
                bot.BlockedUntil = currentTime + _timeSpan;
                bot.WaitUntil(bot.BlockedUntil);
                alreadyRested = true;
                return;
            }
            else
            {
                // exit the resting
                bot.DequeueState(lastTime, currentTime);
                bot.LastRestLocation = DestinationWaypoint;
            }
        }

        /// <summary>
        /// state name
        /// </summary>
        /// <returns>name</returns>
        public override string ToString() { return "Rest"; }

        /// <summary>
        /// State type.
        /// </summary>
        public BotStateType Type { get { return BotStateType.Rest; } }

        public static double DEFAULT_REST_TIME => dEFAULT_REST_TIME;
    }
    #endregion

    #region Assistance-related states

    internal class RequestAssistance : IBotState
    {
        public RequestAssistance(Waypoint waypoint)
        {
            DestinationWaypoint = waypoint;
        }
        /// <summary>
        /// waypoint where assistence is required
        /// </summary>
        public Waypoint DestinationWaypoint { get; set; }
        /// <summary>
        /// type of BotState
        /// </summary>
        public BotStateType Type => BotStateType.RequestAssistance;
        /// <summary>
        /// does actual stuff
        /// </summary>
        /// <param name="self"> bot that will act</param>
        /// <param name="lastTime"></param>
        /// <param name="currentTime"></param>
        public void Act(Bot self, double lastTime, double currentTime)
        {
            BotNormal bot = self as BotNormal;
            bot.Instance.Controller.MateScheduler.RequestAssistance(self, DestinationWaypoint, false);
            bot.DequeueState(lastTime, currentTime);
        }
        /// <summary>
        /// state name, used in drawing
        /// </summary>
        /// <returns>name</returns>
        public override string ToString() { return "RequestAssistance"; }
    }

    internal class MoveToAssist : BotMove
    {
        public MoveToAssist(Waypoint waypoint, bool isBreaking) : base(waypoint) { IsBreaking = isBreaking; }

        /// <summary>
        /// state name, used in drawing
        /// </summary>
        /// <returns>name</returns>
        public override string ToString() { return "MoveToAssist"; }
        /// <summary>
        /// Gets state type
        /// </summary>
        public override BotStateType Type => BotStateType.MoveToAssist;
        /// <summary>
        /// indicates if bot is only in this state to break from full speed to zero and then change destination 
        /// </summary>
        new public bool IsBreaking { get; set; }
    }

    /// <summary>
    /// State that aborts current item collection mission
    /// </summary>
    internal class AbortMoveToAndWait: IBotState
    {
        public AbortMoveToAndWait(Waypoint abortedDestination)
        {
            DestinationWaypoint = abortedDestination;
        }
        public BotStateType Type => BotStateType.AbortMoveToAndWait;
        public Waypoint DestinationWaypoint { get; set; }

        public override string ToString()
        {
            return "AbortMoveToAndWait";
        }

        public void Act(Bot _self, double lastTime, double currentTime)
        {
            BotNormal bot = _self as BotNormal;
            // dequeue this state immediately, Peek() now refers to the next state;
            bot.DequeueState(lastTime, currentTime);
            // if destination is changing, robot is automatically not in place
            bot.isInPlace = false;
            // if moving
            if (bot.StateQueuePeek().Type == BotStateType.Move)
            {
                // remove the current move state
                bot.DequeueState(lastTime, currentTime);
                // remove the next wait state
                bot.DequeueState(lastTime, currentTime);
                // add PPT
                bot.StateQueueEnqueueFront(new PreparePartialTask(bot,
                    DestinationWaypoint, bot.SwarmState.currentAddress, bot.SwarmState.currentPalletGroup));
            }
            // if waiting
            else if (bot.StateQueuePeek().Type == BotStateType.WaitingForMate)
            {
                // remove the current wait state
                bot.DequeueState(lastTime, currentTime);
                // add PPT
                bot.StateQueueEnqueueFront(new PreparePartialTask(bot, 
                    DestinationWaypoint, bot.SwarmState.currentAddress, bot.SwarmState.currentPalletGroup));
            }
            else return;
        }
    }

    /// <summary>
    /// State that changes the item collection destination
    /// </summary>
    internal class ChangeDestination : IBotState
    {
        public ChangeDestination(Waypoint newDestination)
        {
            DestinationWaypoint = newDestination;
        }
        public BotStateType Type => BotStateType.ChangeDestination;
        public Waypoint DestinationWaypoint { get; set; }

        public override string ToString()
        {
            return "ChangeDestination";
        }

        public void Act(Bot _self, double lastTime, double currentTime)
        {
            BotNormal bot = _self as BotNormal;
            // dequeue this state immediately, Peek() now refers to the next state;
            bot.DequeueState(lastTime, currentTime);
            // if destination is changing, robot is automatically not in place
            bot.isInPlace = false;
            // if moving
            if (bot.StateQueuePeek().Type == BotStateType.Move)
            {
                // remove the current move state
                bot.DequeueState(lastTime, currentTime);

                // in case of BotAssist, save the GoalWp
                Waypoint BotAssistGoalWaypoint = null;
                if (bot.StateQueuePeek().Type == BotStateType.BotAssist)
                    BotAssistGoalWaypoint = (bot.StateQueuePeek() as BotAssist).GoalWaypoint;

                // remove the next wait state
                bot.DequeueState(lastTime, currentTime);
                // request assistance was already called for the end goal
                // or destination queue wp and this wait for mate will execute
                // at the arbitrary position in the queue
                // 2. add new wait state
                if(!bot.Instance.SettingConfig.BotsSelfAssist)
                    bot.StateQueueEnqueueFront(new WaitForMate(DestinationWaypoint, bot.Instance));
                else
                    bot.StateQueueEnqueueFront(new BotAssist(DestinationWaypoint, BotAssistGoalWaypoint));
                // 1. add new move state
                bot.PrependMoveStates(bot.CurrentWaypoint, DestinationWaypoint);
            }
            // if waiting
            else if (bot.StateQueuePeek().Type == BotStateType.WaitingForMate)
            {
                // remove the current wait state
                bot.DequeueState(lastTime, currentTime);
                // 2. add new wait state
                bot.StateQueueEnqueueFront(new WaitForMate(DestinationWaypoint, bot.Instance));
                // 1. add new move state
                bot.PrependMoveStates(bot.CurrentWaypoint, DestinationWaypoint);
            }
            else if (bot.StateQueuePeek().Type == BotStateType.BotAssist)
            {
                // create continued bot assist state
                BotAssist continuedState = (bot.StateQueuePeek() as BotAssist).CreateContinuedAssistState();
                // remove the current wait state
                bot.DequeueState(lastTime, currentTime);
                // 2. add new wait state
                bot.StateQueueEnqueueFront(continuedState);
                // 1. add new move state
                bot.PrependMoveStates(bot.CurrentWaypoint, DestinationWaypoint);
            }
            else return;
        }
    }

    internal class WaitForMate : IBotState
    {
        public WaitForMate(Waypoint waypoint, Instance instance)
        {
            DestinationWaypoint = waypoint;
            Instance = instance;
        }

        public Waypoint DestinationWaypoint { get; set; } //not used but needed because of interface
        public Instance Instance { get; set; }
        public BotStateType Type => BotStateType.WaitingForMate;
        /// <summary>
        /// Checks if MateBot arrived at his location
        /// </summary>
        /// <param name="self"></param>
        /// <param name="lastTime"></param>
        /// <param name="currentTime"></param>
        public void Act(Bot _self, double lastTime, double currentTime)
        {
            if (!Initilized)
            {
                var bot = (_self as BotNormal);
                Initilized = true;
                bot.isInPlace = true;
                Instance.Controller.MateScheduler.UpdateArrivalTime(bot, bot.AssistantDestination, currentTime);
            }
            //wait for mate to unlock
        }

        /// <summary>
        /// state name, used in drawing
        /// </summary>
        /// <returns>name</returns>
        public override string ToString() { return "WaitingForMate"; }
        /// <summary>
        /// bool indicating if this state has already been initialized
        /// </summary>
        private bool Initilized { get; set; } = false;
    }

    internal class WaitForStation : IBotState
    {
        public WaitForStation(Waypoint waypoint)
        {
            DestinationWaypoint = waypoint;
            AssistStarted = false;
        }

        public Waypoint DestinationWaypoint { get; set; }

        public BotStateType Type => BotStateType.WaitingForStation;
        /// <summary>
        /// Bot waits for Station, simulates assistance when both arrive and unlocks both of them
        /// </summary>
        /// <param name="_self">Bot doing the asssitance</param>
        /// <param name="lastTime">last time</param>
        /// <param name="currentTime">current time</param>
        public void Act(Bot _self, double lastTime, double currentTime)
        {
            //set variables for this state
            if (!Initilized)
            {
                Self = _self as MateBot;
                Ms = (Self.CurrentTask as AssistTask).BotToAssist as MovableStation;
                Self.Instance.Controller.MateScheduler.UpdateMateArrivalTime(Ms, Self, DestinationWaypoint, currentTime);
                Initilized = true;
            }
            //if ms is in place and it's assistant is Self, then both at the needed location
            if (Ms.isInPlace && Ms.Assistant == Self && Ms.AssistantDestination == Self.CurrentWaypoint)
            {
                //if assist process didn't start, start it
                if (!AssistStarted)
                {
                    AssistStarted = true;
                    AssistStartTime = currentTime;
                    if (Ms.CurrentTask is MultiPointGatherTask &&
                      ((Ms.CurrentTask as MultiPointGatherTask).Times?.Count ?? 0) > 0)
                    {
                        var task = Ms.CurrentTask as MultiPointGatherTask;
                        double duration = Ms.Instance.SettingConfig.AssistDuration;
                        if (!Ms.Instance.SettingConfig.UseConstantAssistDuration)
                          duration = task.Times[task.Locations.IndexOf(Self.CurrentWaypoint)];
                        Self.SetAssistDuration(duration);
                    }
                    else
                    {
                        throw new Exception("assist time for a task is not defined");
                    }
                    Ms.NotifyAssistStarted(Self.AssistDuration, Self);
                }
                // NOTE the program will only arrive at this 'if' statement
                // if the robot (MovableStation) is in place and it is waiting
                // for the picker (MateBot)
                // if MateBot's AssistTime has passed since the start, finish assistance
                if (AssistStarted && !double.IsNaN(AssistStartTime) &&
                   AssistStartTime + Self.AssistDuration < currentTime)
                {
                    //notify assist end
                    Ms.OnAssistEnded(Self);
                    Self.OnAssistEnded();

                    //dequeue states and nullify task related variables
                    Self.DequeueState(lastTime, currentTime);
                    Self.isInPlace = false;
                    Self.DestinationWaypoint = null;
                    Ms.DequeueState(lastTime, currentTime);
                    if (Self.Instance.SettingConfig.UsingLocationManager)
                        Self.Instance.Controller.PathManager._locationManager.NotifyBotCompletedQueueing(Ms);
                    // robot ended queueing
                    // station is not in place anymore
                    Ms.isInPlace = false;
                }
            }
        }
        /// <summary>
        /// state name, used in drawing
        /// </summary>
        /// <returns>name</returns>
        public override string ToString() { return "WaitingForStation"; }
        /// <summary>
        /// flag indicating if the assist has already started 
        /// </summary>
        private bool AssistStarted { get; set; }
        /// <summary>
        /// Start time of the assist process
        /// </summary>
        private double AssistStartTime { get; set; }
        /// <summary>
        /// bool indicating if this state has already been initialized
        /// </summary>
        private bool Initilized { get; set; } = false;
        /// <summary>
        /// 
        /// </summary>
        private MateBot Self { get; set; }
        /// <summary>
        /// 
        /// </summary>
        private MovableStation Ms { get; set; }
    }

    internal class PreparePartialTask : IBotState
    {
        public PreparePartialTask(BotNormal bot, Waypoint destination, string address, int palletGroup = -1, double maxWaitTime = 10)
        {
            DestinationWaypoint = destination;
            NoFreePosition = false;
            TimeoutDuration = maxWaitTime;
            WaitStartTime = double.NaN;
            HasClaimedRestingLocation = false;
            ClaimedRestingLocation = null;
            CurrentPalletGroup = palletGroup;
            Address = address;
        }
        /// <summary>
        /// Current waypoint of a bot in PreparePartialTask state
        /// </summary>
        private Waypoint CurrentWaypoint { get; set; }
        /// <summary>
        /// Destination of partial task
        /// </summary>
        public Waypoint DestinationWaypoint { get; set; }
        /// <summary>
        /// Destination on which bot needing assistance will go
        /// </summary>
        public Waypoint BotDestination = null;
        /// <summary>
        /// Destination on which assistent will go
        /// </summary>
        public Waypoint AssistantDestination = null;
        /// <summary>
        /// Amount of time bot will wait before it enters the waiting period
        /// </summary>
        public double TimeoutDuration { get; set; }
        /// <summary>
        /// Current palletID to stack on
        /// </summary>
        public int CurrentPalletGroup = -1;
        /// <summary>
        /// Pod location address related to this partial task
        /// </summary>
        public string Address { get; set; } 
        /// <summary>
        /// Type of BotState
        /// </summary>
        public BotStateType Type => BotStateType.PreparePartialTask;
        /// <summary>
        /// Tries to prepare everything needed for a task
        /// </summary>
        /// <param name="self">Bot that has to execute the task</param>
        /// <param name="lastTime">last time</param>
        /// <param name="currentTime">current time</param>
        public void Act(Bot self, double lastTime, double currentTime)
        {
            CurrentWaypoint = self.CurrentWaypoint;
            BotNormal bot = self as BotNormal;
            var rm = bot.Instance.ResourceManager;
            var lm = bot.Instance.Controller.PathManager._locationManager;

            //define state filter which will be used
            Func<IBotState, bool> stateFilter;
            if(bot.Instance.Controller.MateScheduler is WaveMateScheduler)
            {
                var waveFilter = (bot.Instance.Controller.MateScheduler as WaveMateScheduler).Wave;
                stateFilter = state => state.Type == BotStateType.PreparePartialTask && waveFilter.Contains(state.DestinationWaypoint);
            }
            else
            {
                if (bot.SwarmState.currentPalletGroup == -1)
                {
                    stateFilter = state => state.Type == BotStateType.PreparePartialTask;
                }
                else
                {
                    stateFilter = state => state.Type == BotStateType.PreparePartialTask && (state as PreparePartialTask).CurrentPalletGroup == bot.SwarmState.currentPalletGroup;
                }
            }
            List<IBotState> states = bot.GetStatesWhere(stateFilter).ToList();
            if (bot.SwarmState.currentPalletGroup != -1)
            {
                // change current pallet group if neccessary
                if (states.Count == 0)
                {
                    bot.SwarmState.isSwitchingPallets = true;
                    bot.SwarmState.currentPalletGroup = -1;
                    stateFilter = state => state.Type == BotStateType.PreparePartialTask;
                    states = bot.GetStatesWhere(stateFilter).ToList();
                }
            }

            //go through all the states that are PreparePartialTask
            foreach (var state in states)
            {
                if (bot.Instance.SettingConfig.UsingLocationManager)
                {

                    Waypoint botDestionation = lm.CreateBotQueue(bot, state.DestinationWaypoint);

                    // if this position is occupied and no queuing is available
                    if (botDestionation == null)
                        continue;

                    // remove this state, since it has been executed
                    bot.StateQueueRemove(state);
                    // set current pallet group
                    bot.SwarmState.currentPalletGroup = (state as PreparePartialTask).CurrentPalletGroup;

                    // and current item address
                    bot.SwarmState.currentAddress = (state as PreparePartialTask).Address;

                    // set the internal bot assistant destination
                    bot.AssistantDestination = state.DestinationWaypoint;

                    // this will return previously set item address
                    string adr = bot.Instance.Controller.MateScheduler.GetBotCurrentItemAddress(bot, state.DestinationWaypoint);

                    if (MateScheduler.BotOrderInfo.GetPodLocked(adr) == -1)
                        bot.Instance.Controller.MateScheduler.itemTable[bot.ID].UpdatePodLock(adr, bot.ID);

                    if (!bot.Instance.SettingConfig.BotsSelfAssist)
                    {
                        // 3. When arrived, start waiting
                        bot.StateQueueEnqueueFront(new WaitForMate(botDestionation, bot.Instance));
                        // 2. Start moving to the destination
                        bot.PrependMoveStates(bot.CurrentWaypoint, botDestionation);
                        // 1. Request assistance immediately
                        bot.StateQueueEnqueueFront(new RequestAssistance(bot.AssistantDestination));
                    }
                    else
                    {
                        // 2. When arrived, start assisting 
                        bot.StateQueueEnqueueFront(new BotAssist(botDestionation, state.DestinationWaypoint));
                        bot.Instance.Controller.MateScheduler.itemTable[bot.ID].AddPickingAddress(adr);
                        bot.Instance.Controller.MateScheduler.itemTable[bot.ID].UpdateAssignedPicker(adr, bot.ID);
                        // 1. Start moving to the destination
                        bot.PrependMoveStates(bot.CurrentWaypoint, botDestionation);
                    }

                    // parking setup as before
                    NoFreePosition = false;
                    WaitStartTime = double.NaN;
                    if (HasClaimedRestingLocation)
                    {
                        bot.Instance.ResourceManager.ReleaseRestingLocation(ClaimedRestingLocation);
                        ClaimedRestingLocation = null;
                        HasClaimedRestingLocation = false;
                        bot.LastRestLocation = null;
                    }

                    // if the location was successful exit
                    return;
                }
                else if (rm.TryToLockPosition(state.DestinationWaypoint, out BotDestination, out AssistantDestination))
                {
                    if (bot.Instance.SettingConfig.SameAssistLocation)
                        // remove the location from the other tasks (if robots have it as a future location already)
                        bot.Instance.Controller.MateScheduler.AssistInfo.RemoveFutureLocation(AssistantDestination, bot);

                    // remove the state whose destinations can be locked
                    bot.StateQueueRemove(state);
                    // set current pallet group
                    bot.SwarmState.currentPalletGroup = (state as PreparePartialTask).CurrentPalletGroup;
                    // and current item address
                    bot.SwarmState.currentAddress = (state as PreparePartialTask).Address;
                    string adr = "";
                    // lock the position in the status table
                    if (bot.Instance.SettingConfig.BotsSelfAssist)
                        adr = bot.Instance.Controller.MateScheduler.GetBotCurrentItemAddress(bot, BotDestination);
                    else
                        adr = bot.Instance.Controller.MateScheduler.GetBotCurrentItemAddress(bot, AssistantDestination);
                    bot.Instance.Controller.MateScheduler.itemTable[bot.ID].UpdatePodLock(adr, bot.ID);

                    NoFreePosition = false;
                    WaitStartTime = double.NaN;
                    if (HasClaimedRestingLocation)
                    {
                        bot.Instance.ResourceManager.ReleaseRestingLocation(ClaimedRestingLocation);
                        ClaimedRestingLocation = null;
                        HasClaimedRestingLocation = false;
                        bot.LastRestLocation = null;
                    }
                    //enter waiting for assistant state
                    if (!bot.Instance.SettingConfig.BotsSelfAssist)
                        bot.StateQueueEnqueueFront(new WaitForMate(BotDestination, bot.Instance));
                    else
                    {
                        bot.StateQueueEnqueueFront(new BotAssist(BotDestination, AssistantDestination));
                        bot.Instance.Controller.MateScheduler.itemTable[bot.ID].AddPickingAddress(adr);
                        bot.Instance.Controller.MateScheduler.itemTable[bot.ID].UpdateAssignedPicker(adr, bot.ID);
                    }
                    //request move from current location to the first location in a task
                    bot.PrependMoveStates(CurrentWaypoint, BotDestination);
                    //request assistant at location
                    if (!bot.Instance.SettingConfig.BotsSelfAssist)
                        bot.StateQueueEnqueueFront(new RequestAssistance(AssistantDestination));
                    bot.AssistantDestination = AssistantDestination;
                    return;
                }
            }
            //if bot has to wait for position to free up  
            if (NoFreePosition)
            {
                //check if bot waited enough
                if (!double.IsNaN(WaitStartTime) &&
                    (WaitStartTime + TimeoutDuration < currentTime) &&
                    (CurrentWaypoint != bot.LastRestLocation))
                {
                    //find the closest resting location for bot on parking so that it does not block space
                    ClaimedRestingLocation = bot.Instance.findClosestLocation(bot.Instance.ResourceManager.UnusedRestingLocations.ToList(), bot.CurrentWaypoint);
                    if (ClaimedRestingLocation != null)
                    {
                        bot.Instance.ResourceManager.ClaimRestingLocation(ClaimedRestingLocation);
                        HasClaimedRestingLocation = true;
                        bot.PrependMoveStates(CurrentWaypoint, ClaimedRestingLocation);
                        //prepare parametars for PreparePartialTask which will be used after the bot reaches restLocation
                        bot.LastRestLocation = ClaimedRestingLocation;
                        //notify MateScheduler
                        if (!bot.Instance.SettingConfig.BotsSelfAssist)
                            bot.Instance.Controller.MateScheduler.NotifyBotGoingToRestingLocation(bot);
                        return;
                    }
                }
                else if (CurrentWaypoint == bot.LastRestLocation &&
                        LastCheckTime + TimeoutDuration < currentTime)
                {
                    if (!bot.Instance.SettingConfig.SameAssistLocation)
                        rm.CheckLockedPosition((bot.StateQueuePeek() as PreparePartialTask).DestinationWaypoint);
                    LastCheckTime = currentTime;
                }
            }
            else
            {//Start of waiting for some position to open
                NoFreePosition = true;
                WaitStartTime = currentTime;
            }
        }
        /// <summary>
        /// state name, used in drawing
        /// </summary>
        /// <returns>name</returns>
        public override string ToString() { return "PreparePartialTask"; }
        /// <summary>
        /// Waypoint of claimed resting location
        /// </summary>
        public Waypoint ClaimedRestingLocation { get; set; }
        /// <summary>
        /// Bool indicating if there is no free destinations left in stateQueue
        /// </summary>
        private bool NoFreePosition { get; set; }
        /// <summary>
        /// Holds the time when Bot entered period of waiting for open destination
        /// </summary>
        private double WaitStartTime { get; set; }
        /// <summary>
        /// Time of last position check
        /// </summary>
        private double LastCheckTime { get; set; }
        /// <summary>
        /// Bool indicating if bot has claimed resting location
        /// </summary>
        private bool HasClaimedRestingLocation { get; set; }
    }

    #endregion

    #region Prepare states
    /// <summary>
    /// The state for preparing the moving to input pallet stand
    /// </summary>
    internal class BotPrepareMoveToInputPalletStand : IBotState
    {
        /// <summary>
        /// Constructs new <see cref="BotPrepareMoveToInputPalletStand"/> state
        /// </summary>
        /// <param name="waypoint">Current bot waypoint</param>
        public BotPrepareMoveToInputPalletStand() { }
        /// <summary>
        /// Unused, needed for interface
        /// </summary>
        public Waypoint DestinationWaypoint { get; set; }
        /// <summary>
        /// Type of BotState
        /// </summary>
        public BotStateType Type => BotStateType.PrepareMoveToInputPalletStand;
        /// <summary>
        /// Gets string representing this state
        /// </summary>
        /// <returns>String which represents this state</returns>
        public override string ToString() { return "PrepareMoveToInputPalletStand"; }
        /// <summary>
        /// Defines how <see cref="Bot"/> will act in this state
        /// </summary>
        /// <param name="self"><see cref="Bot"/> which is in this state</param>
        /// <param name="lastTime">time of the last simulation tick</param>
        /// <param name="currentTime">time of the current simulation tick</param>
        public void Act(Bot self, double lastTime, double currentTime)
        {
            //do not ignore the input pallet stand queue - go get the pallet
            self.IgnoreInputPalletStandQueue = false;
            (self as BotNormal).StateQueueDequeue();
        }
    }

    /// <summary>
    /// The state for preparing the moving to output pallet stand
    /// </summary>
    internal class BotPrepareMoveToOutputPalletStand : IBotState
    {
        /// <summary>
        /// Constructs new <see cref="BotPrepareMoveToOutputPalletStand"/> state
        /// </summary>
        public BotPrepareMoveToOutputPalletStand() { }
        /// <summary>
        /// Unused, needed for interface
        /// </summary>
        public Waypoint DestinationWaypoint { get; set; }
        /// <summary>
        /// Type of BotState
        /// </summary>
        public BotStateType Type => BotStateType.PrepareMoveToOutputPalletStand;
        /// <summary>
        /// Gets string representing this state
        /// </summary>
        /// <returns>String which represents this state</returns>
        public override string ToString() { return "PrepareMoveToOutputPalletStand"; }
        /// <summary>
        /// Defines how <see cref="Bot"/> will act in this state
        /// </summary>
        /// <param name="self"><see cref="Bot"/> which is in this state</param>
        /// <param name="lastTime">time of the last simulation tick</param>
        /// <param name="currentTime">time of the current simulation tick</param>
        public void Act(Bot self, double lastTime, double currentTime)
        {

            // Update the current item destination for the bot in the status table (going to ouput stand
            //self.Instance.Controller.MateScheduler.UpdateCurrentItem(self.ID, "#####", -1);

            //do not ignore the output pallet stand queue - go put the pallet
            self.IgnoreOutputPalletStandQueue = false;
            (self as BotNormal).StateQueueDequeue();

            Waypoint outputPalletStandWaypoint = (self as MovableStation).GetOutputPalletStandWaypoint();
            //if outputPalletStandWaypoint is null, use last location of the task if it has any
            //if all fails, use current waypoint
            outputPalletStandWaypoint ??= self.CurrentWaypoint;
            (self as BotNormal).AppendMoveStates(self.CurrentWaypoint, outputPalletStandWaypoint);
            (self as BotNormal).StateQueueEnqueue(new BotPutPallet(outputPalletStandWaypoint));

            if (!self.Instance.SettingConfig.BotsSelfAssist)
                self.Instance.Controller.MateScheduler.NotifyBotGoingToOutputPalletStand(self);
        }
    }
    /// <summary>
    /// The state in which bot is in until it receives see-off assistance
    /// </summary>
    internal class WaitForSeeOffAssistance : IBotState
    {
        /// <summary>
        /// Constructs new state object
        /// </summary>
        public WaitForSeeOffAssistance()
        {

        }
        /// <summary>
        /// Destination waypoint of <see cref="WaitForSeeOffAssistance"/> is claimed resting location
        /// </summary>
        public Waypoint DestinationWaypoint { get; set; }
        /// <summary>
        /// Type of this state
        /// </summary>
        public BotStateType Type => BotStateType.WaitingForSeeOffAssistance;
        /// <summary>
        /// Name of this state
        /// </summary>
        /// <returns>String representation of this state</returns>
        public override string ToString() { return "WaitingForSeeOffAssistance"; }
        /// <summary>
        /// Defines how <paramref name="self"/> will act in this state
        /// </summary>
        /// <param name="self"><see cref="Bot"/> that is in <see cref="WaitForSeeOffAssistance"/> state</param>
        /// <param name="lastTime">last time</param>
        /// <param name="currentTime">current time</param>
        public void Act(Bot self, double lastTime, double currentTime)
        {
            BotNormal bot = self as BotNormal;
            if (!claimedRestingLocation)
            {
                //Find available parking location
                Waypoint closestParkingLocation = bot.Instance.findClosestLocation(
                            bot.Instance.ResourceManager.UnusedRestingLocations, bot.NextPPTLocation);

                if (closestParkingLocation != null)
                {
                    //Send Bot to found parking location
                    bot.PrependMoveStates(bot.CurrentWaypoint, closestParkingLocation);
                    //set closest parking location as DestinationWaypoint of this state
                    DestinationWaypoint = closestParkingLocation;
                    //claim location in resource manager
                    bot.Instance.ResourceManager.ClaimRestingLocation(closestParkingLocation);
                    claimedRestingLocation = true;
                }

                //Publish first location to MateScheduler so that Mates could be assigned
                bot.Instance.Controller.MateScheduler.RequestAssistance(self, bot.NextPPTLocation, true);
            }

        }

        /// <summary>
        /// Bool indicating whether resting location has been claimed for a Bot in this state
        /// </summary>
        private bool claimedRestingLocation = false;
    }

    internal class BotAssist : IBotState
    {
        /// <summary>
        /// Constructs new state object
        /// </summary>
        public BotAssist(Waypoint waypoint, Waypoint goalWaypoint)
        {
            DestinationWaypoint = waypoint;
            GoalWaypoint = goalWaypoint;
        }
        /// <summary>
        /// Not used
        /// </summary>
        public Waypoint DestinationWaypoint { get; set; }

        /// <summary>
        /// The end destination waypoint with queue index 0.
        /// The DestinationWaypoint is where robot will arrive.
        /// Queue index greater or equal 0.
        /// </summary>
        public Waypoint GoalWaypoint { get; set; }
        /// <summary>
        /// Type of this state
        /// </summary>
        public BotStateType Type => BotStateType.BotAssist;
        /// <summary>
        /// Name of this state
        /// </summary>
        /// <returns>String representation of this state</returns>
        public override string ToString() { return "BotAssist"; }
        /// <summary>
        /// flag indicating if the assist has already started 
        /// </summary>
        private bool AssistStarted { get; set; }
        /// <summary>
        /// Start time of the assist process
        /// </summary>
        private double AssistStartTime { get; set; }
        /// <summary>
        /// bool indicating if this state has already been initialized
        /// </summary>
        private bool Initilized { get; set; } = false;
        /// <summary>
        /// Bot as MovableStation
        /// </summary>
        private MovableStation Self { get; set; }

        public BotAssist CreateContinuedAssistState()
        {
            BotAssist continuedBotAssist = new BotAssist(DestinationWaypoint, GoalWaypoint);
            if (AssistStarted)
            {
                continuedBotAssist.AssistStarted = true;
                continuedBotAssist.AssistStartTime = AssistStartTime;
            }
            return continuedBotAssist;
        }
        /// <summary>
        /// Defines how <paramref name="self"/> will act in this state
        /// </summary>
        /// <param name="self"><see cref="Bot"/> that is in <see cref="BotAssist"/> state</param>
        /// <param name="lastTime"></param>
        /// <param name="currentTime"></param>
        public void Act(Bot self, double lastTime, double currentTime)
        {
            //set variables for this state
            if (!Initilized)
            {
                Self = self as MovableStation;
                Initilized = true;
                Self.isInPlace = true;
            }

            //if assist process didn't start, start it
            if (!AssistStarted)
            {
                AssistStarted = true;
                AssistStartTime = currentTime;
                if (Self.CurrentTask is MultiPointGatherTask && ((Self.CurrentTask as MultiPointGatherTask).Times?.Count ?? 0) > 0)
                {
                    MultiPointGatherTask task = Self.CurrentTask as MultiPointGatherTask;
                    double duration = Self.Instance.SettingConfig.AssistDuration;
                    if (!Self.Instance.SettingConfig.UseConstantAssistDuration)
                        duration = task.Times[task.Locations.IndexOf(Self.AssistantDestination)];
                    Self.SetAssistDuration(duration);
                }
                else
                {
                    throw new Exception("assist time for a task is not defined");
                }
            }

            //if AssistTime has passed since the start, finish assist
            if (AssistStarted && !double.IsNaN(AssistStartTime))
            {
                // if bot is currently switching pallets add time it takes for bot to switch pallets
                if ((Self.SwarmState.isSwitchingPallets && AssistStartTime + Self.AssistDuration + Self.Instance.SettingConfig.SwitchPalletDuration < currentTime) ||
                    (!Self.SwarmState.isSwitchingPallets && AssistStartTime + Self.AssistDuration < currentTime))
                {
                    var task = (MultiPointGatherTask) Self.CurrentTask;
                    // reach the pod location of the current item (task)
                    Waypoint podLocation = 
                        task.PodLocations[task.Locations.FindIndex(l => l.ID == GoalWaypoint.ID)];
                    // get the current item and updated the status in the order
                    var item = task.LocationItemDictionary[podLocation].First();
                    task.Order.CompleteLocation(item);
                    // NOTE: the following commented lines are done in OnAssistEnded()
                    //       when bot self assist is not used, but they don't work well
                    //       when used here
                    //task.Locations.Remove(GoalWaypoint);
                    //// to keep the indexing correct, we need to remove also the pod location
                    //task.PodLocations.Remove(podLocation);
                    //Items.SimpleItemDescription simpleItem = task.PodItems[task.Locations.FindIndex(l => l.ID == GoalWaypoint.ID)];
                    //task.PodItems.Remove(simpleItem);

                    // bot is not switching pallets anymore
                    Self.SwarmState.isSwitchingPallets = false;
                    //notify assist end
                    Self.OnAssistEnded();
                    // first complete queuing, then update the table
                    if (Self.Instance.SettingConfig.UsingLocationManager)
                        Self.Instance.Controller.PathManager._locationManager.NotifyBotCompletedQueueing(Self);
                    // The picking address will always be first
                    Self.Instance.Controller.MateScheduler.itemTable[self.ID].CompleteLocationAtIndex(0);
                    Self.Instance.Controller.MateScheduler.itemTable[self.ID].RemoveAtIndex(0);
                    //dequeue states and nullify task related variables
                    Self.DequeueState(lastTime, currentTime);
                    Self.isInPlace = false;
                }

            }
        }
    }

    #endregion

    /// <summary>
    /// Enumerates all states a bot can be in.
    /// </summary>
    public enum BotStateType
    {
        /// <summary>
        /// Indicates that the bot is picking up a pod.
        /// </summary>
        PickupPod,
        /// <summary>
        /// Indicates that the bot is setting down a pod.
        /// </summary>
        SetdownPod,
        /// <summary>
        /// Indicates that the bot is getting a bundle for its pod.
        /// </summary>
        GetItems,
        /// <summary>
        /// Indicates that the bot is putting an item for an order.
        /// </summary>
        PutItems,
        /// <summary>
        /// Indicates that the bot is idling.
        /// </summary>
        Rest,
        /// <summary>
        /// Indicates that the bot is moving.
        /// </summary>
        Move,
        /// <summary>
        /// Indicates that the bot is evading another bot.
        /// </summary>
        Evade,
        /// <summary>
        /// Indicates that the bot is using an elevator.
        /// </summary>
        UseElevator,
        /// <summary>
        /// indicates that the bot is requesting assistance
        /// </summary>
        RequestAssistance,
        /// <summary>
        /// indicates that the bot will move to the waypoint to assist
        /// </summary>
        MoveToAssist,
        /// <summary>
        /// indicates that the bot is waiting for MateBot
        /// </summary>
        WaitingForMate,
        /// <summary>
        /// indicates that the bot is waiting for station
        /// </summary>
        WaitingForStation,
        /// <summary>
        /// indicates that the bot will signal sucessfull end of current task
        /// </summary>
        FinishTask,
        /// <summary>
        /// indicates that the bot will try to prepare partial task
        /// </summary>
        PreparePartialTask,
        /// <summary>
        /// indicates that the bot will try to get the pallet
        /// </summary>
        GetPallet,
        /// <summary>
        /// indicates that the bot will try to put the pallet
        /// </summary>
        PutPallet,
        /// <summary>
        /// indicates that the bot is preparing to move towards input pallet stand
        /// </summary>
        PrepareMoveToInputPalletStand,
        /// <summary>
        /// indicates that the bot is preparing to move towards input pallet stand
        /// </summary>
        PrepareMoveToOutputPalletStand,
        /// <summary>
        /// indicates that the bot is waiting for see-off scheduling assistance
        /// </summary>
        WaitingForSeeOffAssistance,
        /// <summary>
        /// indicates that the bot is performing the assisting by itself
        /// </summary>
        BotAssist,
        /// <summary>
        /// indicates that the bot is not in any state
        /// </summary>
        NullState,
        /// <summary>
        /// Robot changes the destination according to location manager
        /// </summary>
        ChangeDestination,
        /// <summary>
        /// Aborts the current move to and wait for mate mission
        /// </summary>
        AbortMoveToAndWait
    }
}
