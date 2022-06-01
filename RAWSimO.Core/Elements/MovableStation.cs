using RAWSimO.Core.Bots;
using RAWSimO.Core.Control;
using RAWSimO.Core.Control.Defaults.TaskAllocation;
using RAWSimO.Core.Info;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.Items;
using RAWSimO.Core.Management;
using RAWSimO.Core.Waypoints;
using RAWSimO.MultiAgentPathFinding;
using System;
using System.Collections.Generic;
using System.Linq;


namespace RAWSimO.Core.Elements
{
    ///<summary>
    ///Class that represents movable stations  
    ///</summary>
    public class MovableStation : BotNormal, IMovableStation
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="MovableStation"/> class.
        /// </summary>
        /// <param name="id">The identifier.</param>
        /// <param name="instance">The instance.</param>
        /// <param name="radius">The radius.</param>
        /// <param name="acceleration">The maximum acceleration.</param>
        /// <param name="deceleration">The maximum deceleration.</param>
        /// <param name="maxVelocity">The maximum velocity.</param>
        /// <param name="turnSpeed">The turn speed.</param>
        /// <param name="collisionPenaltyTime">The collision penalty time.</param>
        /// <param name="x">The x.</param>
        /// <param name="y">The y.</param>
        public MovableStation(int id, Instance instance, double radius, double acceleration, double deceleration, double maxVelocity, double turnSpeed, double collisionPenaltyTime, double x = 0.0, double y = 0.0) 
        : base(id, instance, radius, 99999, acceleration, deceleration, maxVelocity, turnSpeed, collisionPenaltyTime, x, y)
        {
            StationPart = new OutputStationAux(instance);
            StationPart.movableStationPart = this;
        }
        ///<summary>
        ///implicitly cast MovableStation object to OutputStation object for compatibility with the rest of the system
        ///</summary>
        public static implicit operator OutputStation(MovableStation ms)
        {
            return ms.StationPart;
        }
        /// <summary>
        /// Station part of the movable station
        /// </summary>
        public OutputStationAux StationPart { get; set; }
        /// <summary>
        /// Gets the waypoint of the needed input pallet stand through BotManager
        /// </summary>
        /// <returns>Waypoint of the input pallet stand</returns>
        internal Waypoint GetInputPalletStandWaypoint(MultiPointGatherTask task)
        {
            return (Instance.Controller.BotManager as DummyBotManager).GetInputPalletStandLocation(task);
        }
        /// <summary>
        /// Gets the waypoint of the needed output pallet stand through BotManager
        /// </summary>
        /// <returns>Waypoint of the output pallet stand</returns>
        internal Waypoint GetOutputPalletStandWaypoint()
        {
            return (Instance.Controller.BotManager as DummyBotManager).GetOutputPalletStandLocation(this);
        }
        /// <summary>
        /// Reacts on the change of path
        /// </summary>
        public void NotifyPathChanged()
        {
            // set flags only if movable station is moving to a meeting with mate
            if(CurrentTask.Type == BotTaskType.MultiPointGatherTask)
                PathToMeetingChanged = true;
        }
        /// <summary>
        /// Get's location in state queue after <paramref name="NrRegisteredLocations"/> registered location  
        /// </summary>
        /// <param name="NrRegisteredLocations">Number of locations already registered in MateScheduler for this <see cref="MovableStation"/></param>
        /// <returns></returns>
        internal override Waypoint GetLocationAfter(int NrRegisteredLocations)
        {
            //NrRegisteredLocations should be number of state queue waypoints already registered (1-based index) 
            if (_stateQueue.Count <= NrRegisteredLocations) throw new ArgumentOutOfRangeException(nameof(NrRegisteredLocations), "There are more registered locations then PPT states");
            BotStateType firstStateType = _stateQueue.First.Value.Type;
            IBotState state = null;

            // additional cases for new state types used in queue
            if (firstStateType == BotStateType.AbortMoveToAndWait || firstStateType == BotStateType.ChangeDestination)
            {
                if (_stateQueue.First.Next.Value.Type == BotStateType.Move && _stateQueue.First.Next.Next.Value.Type == BotStateType.WaitingForMate)
                    state = _stateQueue.ElementAt(NrRegisteredLocations + 2);
                else
                    state = _stateQueue.ElementAt(NrRegisteredLocations + 1);
            }
            //get state at the correct index
            else if (firstStateType == BotStateType.Move && _stateQueue.First.Next.Value.Type == BotStateType.WaitingForMate)
                state = _stateQueue.ElementAt(NrRegisteredLocations + 1);
            else if (firstStateType == BotStateType.WaitingForMate || firstStateType == BotStateType.PreparePartialTask)
                state = _stateQueue.ElementAt(NrRegisteredLocations);

            if (state.Type == BotStateType.PreparePartialTask)
            {
                // NOTE: this is temporary fix added here because of the access
                // to the PPT and item address
                // When future assignment happens, the currentAddress parameter
                // on the robot is not set yet - it will be as soon as PPT is
                // started.
                // When assignment is added as a future assignment, GetCurrentItemAddress
                // is called based on the destination waypoint.
                // Later, based on the destination waypoint index in the Locations
                // address is found and for that address we update the picker in the
                // itemTable - now, the address information is stored in SwarmState and
                // we also read it from there so it needs to be updated here
                SwarmState.currentAddress = (state as PreparePartialTask).Address;
            }
            //if located state is of the correct type, return it's destination
            return state.Type switch
            {
                BotStateType.WaitingForMate => state.DestinationWaypoint,
                BotStateType.PreparePartialTask => state.DestinationWaypoint,
                _ => null,
            };
        }
        /// <summary>
        /// Reacts on assistant being assigned
        /// </summary>
        public override void OnAssistantAssigned()
        {   //in see-off scheduling, bots sometimes have to be woken up
            if(Instance.SettingConfig.SeeOffMateScheduling)
            {
                //if this MovableStation is currently in WaitingForSeeOffAssistance state, just dequeue this state
                if (CurrentBotStateType == BotStateType.WaitingForSeeOffAssistance)
                {
                    //release resting location and dequeue
                    var currentState = StateQueuePeek();
                    Instance.ResourceManager.ReleaseRestingLocation(currentState.DestinationWaypoint);
                    StateQueueDequeue();
                }//if this MovableStation is moving to parking to wait for see-off assistance
                else if(StateQueueCount >= 2 && CurrentBotStateType == BotStateType.Move && StateQueuePeekSecond().Type == BotStateType.WaitingForSeeOffAssistance)
                {
                    //release claimed resting location
                    var currentState = StateQueuePeek();
                    Instance.ResourceManager.ReleaseRestingLocation(currentState.DestinationWaypoint);

                    //abort move, dequeue WaitingForSeeOffAssistance, move to the the first item in order
                    Waypoint stopWP = AbortDrive(GetSpeed()); //get stop waypoint and adjust next and destination waypoints
                    StateQueueDequeue();    //dequeue old move state
                    StateQueueDequeue();    //dequeue old WaitingForSeeOffAssistance
                    PrependMoveStates(CurrentWaypoint, stopWP, false, true); //drive to the 
                }
            }
        }
        /// <summary>
        /// Bool indicating that the path has changed for this object
        /// </summary>
        private bool PathToMeetingChanged { get; set; }

        #region OutputStation members
        /// <summary>
        /// Time it takes for this particular MovableStation to perform assisting
        /// </summary>
        public double AssistDuration { get; set; }
        /// <summary>
        /// Indicates whether this station is active
        /// </summary>
        public bool Active {get => StationPart.Active; } 
        /// <summary>
        /// Activates this station.
        /// </summary>
        public void Activate()
        {
            StationPart.Activate();
        }
        /// <summary>
        /// Deactivates this station.
        /// </summary>
        public void Deactivate()
        {
            StationPart.Deactivate();
        }
        /// <summary>
        /// The capacity currently in use at this station.
        /// </summary>
        public int CapacityInUse { get => StationPart.CapacityInUse; }
        /// <summary>
        /// The amount of capacity reserved by a controller.
        /// </summary>
        internal int CapacityReserverd { get => StationPart.CapacityReserved; }
        /// <summary>
        /// The orders currently assigned to this station.
        /// </summary>
        public IEnumerable<Order> AssignedOrders { get => StationPart.AssignedOrders; }
        /// <summary>
        /// The orders currently queued to this station.
        /// </summary>
        public IEnumerable<Order> QueuedOrders { get => StationPart.QueuedOrders; } 
        /// <summary>
        /// Checks whether the specified order can be added for reservation to this station.
        /// </summary>
        /// <param name="order">The order that has to be checked.</param>
        /// <returns><code>true</code> if the bundle fits, <code>false</code> otherwise.</returns>
        public bool FitsForReservation(Order order) { return StationPart.FitsForReservation(order) ;}
         /// <summary>
        /// Reserves capacity of this station for the given order. The reserved capacity will be maintained when the order is allocated.
        /// </summary>
        /// <param name="order">The order for which capacity shall be reserved.</param>
        internal void RegisterOrder(Order order){ StationPart.RegisterOrder(order); }
        /// <summary>
        /// The order to queue in for this station.
        /// </summary>
        /// <param name="order">The order to queue in.</param>
        internal void QueueOrder(Order order){ StationPart.QueueOrder(order); }
        /// <summary>
        /// Assigns a new order to this station.
        /// </summary>
        /// <param name="order">The order to assign to this station.</param>
        /// <returns><code>true</code> if the order was successfully assigned, <code>false</code> otherwise.</returns>
        public bool AssignOrder(Order order){ return StationPart.AssignOrder(order); }
        /// <summary>
        /// Requests the station to pick the given item for the given order.
        /// </summary>
        /// <param name="bot">The bot that requests the pick.</param>
        /// <param name="request">The request to handle.</param>
        public void RequestItemTake(Bot bot, ExtractRequest request){ StationPart.RequestItemTake(bot, request); }
        /// <summary>
        /// Marks a pod as inbound for a station.
        /// </summary>
        /// <param name="pod">The pod that being brought to the station.</param>
        internal void RegisterInboundPod(Pod pod){ StationPart.RegisterInboundPod(pod); }
        /// <summary>
        /// Removes a pod from the list of inbound pods.
        /// </summary>
        /// <param name="pod">The pod that is not inbound anymore.</param>
        internal void UnregisterInboundPod(Pod pod){ StationPart.UnregisterInboundPod(pod); }
        /// <summary>
        /// All pods currently approaching the station.
        /// </summary>
        internal IEnumerable<Pod> InboundPods { get => StationPart.InboundPods; }
        /// <summary>
        /// Register an extract task with this station.
        /// </summary>
        /// <param name="task">The task that shall be done at this station.</param>
        internal void RegisterExtractTask(ExtractTask task){ StationPart.RegisterExtractTask(task); }
    	/// <summary>
        /// Unregister an extract task with this station.
        /// </summary>
        /// <param name="task">The task that was done or cancelled for this station.</param>
        internal void UnregisterExtractTask(ExtractTask task){ StationPart.UnregisterExtractTask(task); } 
        /// <summary>
        /// Register a newly approached bot before picking begins for statistical purposes.
        /// </summary>
        /// <param name="bot">The bot that just approached the station.</param>
        public void RegisterBot(Bot bot){ StationPart.RegisterBot(bot); }
        /// <summary>
        /// The number of requests currently open (not assigned to a bot) for this station.
        /// </summary>
        internal int StatCurrentlyOpenRequests { get => StationPart.StatCurrentlyOpenRequests ; set => StationPart.StatCurrentlyOpenRequests = value;}
        /// <summary>
        /// The number of requests currently open (not assigned to a bot) for this station.
        /// </summary>
        internal int StatCurrentlyOpenQueuedRequests { get => StationPart.StatCurrentlyOpenQueuedRequests ; set => StationPart.StatCurrentlyOpenQueuedRequests = value;}
        /// <summary>
        /// The number of items currently open (not picked yet) for this station.
        /// </summary>
        internal int StatCurrentlyOpenItems { get => StationPart.StatCurrentlyOpenItems; }
        /// <summary>
        /// The number of items currently open (not picked yet) and queued for this station.
        /// </summary>
        internal int StatCurrentlyOpenQueuedItems { get => StationPart.StatCurrentlyOpenQueuedItems; }
        /// <summary>
        /// The (sequential) number of pods handled at this station.
        /// </summary>
        public int StatPodsHandled { get => StationPart.StatPodsHandled; }
        /// <summary>
        /// The time it took to handle one pod in average.
        /// </summary>
        public double  StatPodHandlingTimeAvg { get => StationPart.StatPodHandlingTimeAvg; }
        /// <summary>
        /// The variance in the handling times of the pods.
        /// </summary>
        public double StatPodHandlingTimeVar { get =>StationPart.StatPodHandlingTimeVar; }
        /// <summary>
        /// The minimal handling time of a pod.
        /// </summary>
        public double StatPodHandlingTimeMin { get =>StationPart.StatPodHandlingTimeMin; }
        /// <summary>
        /// The maximal handling time of a pod.
        /// </summary>
        public double StatPodHandlingTimeMax { get =>StationPart.StatPodHandlingTimeMax; }
        /// <summary>
        /// The item pile-on of this station, i.e. the relative number of items picked from the same pod in one 'transaction'.
        /// </summary>
        public double StatItemPileOn { get =>StationPart.StatItemPileOn; }
        /// <summary>
        /// The injected item pile-on of this station, i.e. the relative number of injected items picked from the same pod in one 'transaction'.
        /// </summary>
        public double StatInjectedItemPileOn { get =>StationPart.StatInjectedItemPileOn; }
        /// <summary>
        /// The order pile-on of this station, i.e. the relative number of orders finished from the same pod in one 'transaction'.
        /// </summary>
        public double StatOrderPileOn { get =>StationPart.StatOrderPileOn; }
        /// <summary>
        /// The time this station was active.
        /// </summary>
        public double StatActiveTime { get => StationPart.StatActiveTime; }
        /// <summary>
        /// Returns a simple string identifying this object in its instance.
        /// </summary>
        /// <returns>A simple name identifying the instance element.</returns>
        public override string GetIdentfierString() { return "MovableStation" + this.ID; }
        /// <summary>
        /// Returns a simple string giving information about the object.
        /// </summary>
        /// <returns>A simple string.</returns>
        public override string ToString() { return "MovableStation" + this.ID; }
        /// <summary>
        /// Gets the number of assigned orders.
        /// </summary>
        /// <returns>The number of assigned orders.</returns>
        public int GetInfoAssignedOrders(){ return StationPart.GetInfoAssignedOrders(); }
         /// <summary>
        /// Gets all order currently open.
        /// </summary>
        /// <returns>The enumeration of open orders.</returns>
        public IEnumerable<IOrderInfo> GetInfoOpenOrders(){ return StationPart.GetInfoOpenOrders(); } 
         /// <summary>
        /// Gets all orders already completed.
        /// </summary>
        /// <returns>The enumeration of completed orders.</returns>
        public IEnumerable<IOrderInfo> GetInfoCompletedOrders() { return StationPart.GetInfoCompletedOrders(); }
        /// <summary>
        /// Gets the capacity this station offers.
        /// </summary>
        /// <returns>The capacity of the station.</returns>
        public double GetInfoCapacity() { return StationPart.GetInfoCapacity(); }
        /// <summary>
        /// Gets the absolute capacity currently in use.
        /// </summary>
        /// <returns>The capacity in use.</returns>
        public double GetInfoCapacityUsed() { return StationPart.GetInfoCapacityUsed(); }
         /// <summary>
        /// Indicates the number that determines the overall sequence in which stations get activated.
        /// </summary>
        /// <returns>The order ID of the station.</returns>
        public int GetInfoActivationOrderID() { return StationPart.GetInfoActivationOrderID(); }
        /// <summary>
        /// Gets the information queue.
        /// </summary>
        /// <returns>Queue</returns>
        public string GetInfoQueue() { return StationPart.GetInfoQueue(); }
        /// <summary>
        /// Indicates whether the station is currently activated (available for new assignments).
        /// </summary>
        /// <returns><code>true</code> if the station is active, <code>false</code> otherwise.</returns>
        public bool GetInfoActive(){ return StationPart.GetInfoActive(); }
        /// <summary>
        /// Gets the of requests currently open (not assigned to a bot) for this station.
        /// </summary>
        /// <returns>The number of active requests.</returns>
        public int GetInfoOpenRequests(){ return StationPart.GetInfoOpenRequests(); }
        /// <summary>
        /// Gets the number of queued requests currently open (not assigned to a bot) for this station.
        /// </summary>
        /// <returns>The number of active queued requests.</returns>
        public int GetInfoOpenQueuedRequests(){ return StationPart.GetInfoOpenQueuedRequests(); }
        /// <summary>
        /// Gets the number of currently open items (not yet picked) for this station.
        /// </summary>
        /// <returns>The number of open items.</returns>
        public int GetInfoOpenItems(){ return StationPart.GetInfoOpenItems(); }
        /// <summary>
        /// Gets the number of currently queued and open items (not yet picked) for this station.
        /// </summary>
        /// <returns>The number of queued open items.</returns>
        public int GetInfoOpenQueuedItems(){ return StationPart.GetInfoOpenQueuedItems(); }
        /// <summary>
        /// Gets the number of pods currently incoming to this station.
        /// </summary>
        /// <returns>The number of pods currently incoming to this station.</returns>
        public int GetInfoInboundPods(){ return StationPart.GetInfoInboundPods(); }
        /// <summary>
        /// The Queue starting with the nearest way point ending with the most far away one.
        /// </summary>
        /// <value>
        /// The queue.
        /// </value>
        public Dictionary<Waypoint, List<Waypoint>> Queues { get => StationPart.Queues; set => StationPart.Queues = value; }
        /// <summary>
        /// Completes an order that is ready, if there is one.
        /// </summary>
        /// <param name="currentTime">The current simulation time.</param>
        /// <returns>The completed order if there was one, <code>null</code> otherwise.</returns>
        public Order RemoveAnyCompletedOrder(double currentTime)
        {
            // Remove any orders that are finished
            Order finishedOrder = null;
            foreach (var order in AssignedOrders)
                if (order.IsCompleted())
                {
                    finishedOrder = order;
                    StatNumOrdersFinished++;
                    // Notify the item manager about this
                    Instance.ItemManager.CompleteOrder(finishedOrder);
                    // Notify completed order
                    Instance.NotifyOrderCompleted(finishedOrder, this);
                    // Break early and block action
                    BlockedUntil = currentTime + OrderCompletionTime;
                    break;
                }
            // Remove the finished order from the todo-list
            StationPart._assignedOrders.Remove(finishedOrder);
            if (finishedOrder != null)
            {
                // Add order to the completed order list
                StationPart._completedOrders.Add(finishedOrder);
                // Remove it from the open list
                StationPart._openOrders.Remove(finishedOrder);
            }
            // Return either the completed order or null to signal no order could be completed
            return finishedOrder;
        }
        public double ItemTransferTime { get => StationPart.ItemTransferTime; set => StationPart.ItemTransferTime = value; }
        public double ItemPickTime { get => StationPart.ItemPickTime; set => StationPart.ItemPickTime = value; }
        public double OrderCompletionTime { get => StationPart.OrderCompletionTime; set => StationPart.OrderCompletionTime = value; }
        public Waypoint Waypoint { get => StationPart.Waypoint; set => StationPart.Waypoint = value; }
        public int ActivationOrderID { get => StationPart.ActivationOrderID; set => StationPart.ActivationOrderID = value; }
        public int Capacity { get => StationPart.Capacity; set => StationPart.Capacity = value; }
        public int StatNumItemsPicked { get => StationPart.StatNumItemsPicked; set => StationPart.StatNumItemsPicked = value; }
        public int StatNumInjectedItemsPicked{ get => StationPart.StatNumInjectedItemsPicked; set => StationPart.StatNumInjectedItemsPicked = value; }
        public int StatNumOrdersFinished{ get => StationPart.StatNumOrdersFinished; set => StationPart.StatNumOrdersFinished = value; }
        public double StatIdleTime{ get => StationPart.StatIdleTime; set => StationPart.StatIdleTime = value; }
        public double StatDownTime{ get => StationPart.StatDownTime; set => StationPart.StatDownTime = value; }

        #endregion

        #region IUpdateable methods
        /// <summary>
        /// The next event when this element has to be updated.
        /// </summary>
        /// <param name="currentTime">The current time of the simulation.</param>
        /// <returns>The next time this element has to be updated.</returns>
        public override double GetNextEventTime(double currentTime)
        {
            var botTime = base.GetNextEventTime(currentTime);
            var stationTIme = StationPart.GetNextEventTime(currentTime);

            return Math.Min(botTime,stationTIme);
        }
        /// <summary>
        /// Updates the element to the specified time.
        /// </summary>
        /// <param name="lastTime">The time before the update.</param>
        /// <param name="currentTime">The time to update to.</param>
        public override void Update(double lastTime, double currentTime)
        {
            //first do everything a station has to do
            StationPart.Update(lastTime, currentTime);
            //if station has something to do, force wake the bot 
            if(AssignedOrders.Count() > 0 && StateQueueCount > 0 &&
               (base.CurrentTask.Type == BotTaskType.Rest || base.CurrentTask.Type == BotTaskType.None))
            {
                StateQueueClear(); 
            }
            //then do what robot base has to
            base.Update(lastTime, currentTime);

            if(PathToMeetingChanged || AssistanceAborted)
            {
                //if you are in PreparePartialTask, don't do anything
                if (CurrentBotStateType == BotStateType.PreparePartialTask)
                    return;

                bool isGoingToRest = (StateQueueCount >= 2 && StateQueuePeekSecond().Type == BotStateType.PreparePartialTask &&
                                      (StateQueuePeekSecond() as PreparePartialTask).ClaimedRestingLocation == DestinationWaypoint
                                      && DestinationWaypoint != null);
                bool isGoingToGetPallet = (StateQueueCount >= 2 && StateQueuePeekSecond().Type == BotStateType.GetPallet);
                bool isGoingToPutPallet = (StateQueueCount >= 2 && StateQueuePeekSecond().Type == BotStateType.PutPallet);
                bool isWaitingForSeeOffAssistance = (Instance.SettingConfig.SeeOffMateScheduling == true &&
                                                        ((StateQueueCount >= 2 && CurrentBotStateType == BotStateType.Move && StateQueuePeekSecond().Type == BotStateType.WaitingForSeeOffAssistance) ||
                                                        (StateQueueCount >= 1 && CurrentBotStateType == BotStateType.WaitingForSeeOffAssistance)));
                if (!(IsBreaking || isGoingToRest || isGoingToGetPallet || isGoingToPutPallet || isWaitingForSeeOffAssistance))
                {
                    //calculate the aproximated time of arrival 
                    double predictedArrivalTime = DestinationWaypoint != null ? 
                       Instance.Controller.PathManager.PredictArrivalTime(this, DestinationWaypoint, false) : currentTime;
                    //Notify Mate scheduler
                    if (!Instance.SettingConfig.BotsSelfAssist)
                        Instance.Controller.MateScheduler.UpdateArrivalTime(this, AssistantDestination, predictedArrivalTime);
                }

                if (PathToMeetingChanged) PathToMeetingChanged = false;
                if (AssistanceAborted) AssistanceAborted = false;
            }
        }

        #endregion

        #region BotNormalOverrides
        /// <summary>
        /// Gets BotType
        /// </summary>
        public override BotType Type => BotType.MovableStation;

        #endregion

        #region StatsSpecific

        /// <summary>
        /// Sets the assist duration to the given value
        /// </summary>
        public void SetAssistDuration(double duration)
        {
            AssistDuration = duration;
            StatTotalAssistTime += AssistDuration;
            StatAllAssistTimes += AssistDuration;
        }

        /// <summary>
        /// Reacts on assist start pseudoevent
        /// </summary>
        /// <param name="assistDuration">Assist duration</param>
        /// <param name="assistant"><see cref="MateBot"/> giving assistance</param>
        public void NotifyAssistStarted(double assistDuration, MateBot assistant)
        {
            StatTotalAssistTime += assistDuration;
            StatAllAssistTimes += assistDuration;
            Instance.Controller.MateScheduler.NotifyAssistStarted(this, assistant);
        }

        /// <summary>
        /// Reacts on assist end pseudoevent
        /// </summary>
        /// <param name="assistant"><see cref="MateBot"/> giving assistance</param>
        public void OnAssistEnded(MateBot assistant = null)
        {
            //free locked positions
            Instance.ResourceManager.FreeLockedPosition(CurrentWaypoint);

            // unlock the address
            string address = "";

            if (!Instance.SettingConfig.BotsSelfAssist)
            {
                address = Instance.Controller.MateScheduler.GetBotCurrentItemAddress(this, assistant.CurrentWaypoint);
                if (!Instance.SettingConfig.SameAssistLocation) Instance.ResourceManager.FreeLockedPosition(assistant.CurrentWaypoint);

                Instance.Controller.MateScheduler.NotifyAssistEnded(this, assistant);

                //Instance.Controller.MateScheduler.RemovePickingLocation(this.ID, address);
                //Instance.Controller.MateScheduler.itemTable[this.ID].RemoveAddress(address);

                AssistantDestination = null;

                var task = (MultiPointGatherTask)CurrentTask;
                // reach the pod location of the current item (task)
                Waypoint podLocation = task.PodLocations[task.Locations.FindIndex(l => l.ID == assistant.CurrentWaypoint.ID)];
                // reach the current picking item
                SimpleItemDescription simpleItem = task.PodItems[task.Locations.FindIndex(l => l.ID == assistant.CurrentWaypoint.ID)];
                // get the current item and updated the status in the order
                ItemDescription item = task.LocationItemDictionary[podLocation].First();
                task.Order.CompleteLocation(item);
                task.Locations.Remove(assistant.CurrentWaypoint);
                // to keep the indexing correct, we need to remove also the pod location
                task.PodLocations.Remove(podLocation);
                task.PodItems.Remove(simpleItem);
            }
            else
               address = Instance.Controller.MateScheduler.GetBotCurrentItemAddress(this, CurrentWaypoint);

            Instance.Controller.MateScheduler.itemTable[ID].UpdatePodLock(address, -1); 
            StatNumberOfPickups++;
            // removes the locations (and subsequently also assistnace)
            // TODO: should be divided in removing assistance and then removing location
            // Removed because of BotsSelfAssist Error
            //Instance.Controller.MateScheduler.AssignemntDictionaryRemoveAssistance(this, assistant.CurrentWaypoint);
        }

        /// <summary>
        /// Statistic for total assist time given to all of the movable stations
        /// </summary>
        public static double StatAllAssistTimes = 0;

        /// <summary>
        /// Statistic for total assist time given to this movable station
        /// </summary>
        public double StatTotalAssistTime = 0;

        #endregion
    }

}
