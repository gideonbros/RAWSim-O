using System;
using System.Collections.Generic;
using System.Linq;
using DocumentFormat.OpenXml.Drawing;
using DocumentFormat.OpenXml.Office2013.Drawing.ChartStyle;
using RAWSimO.Core.Bots;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.Items;
using RAWSimO.Core.Waypoints;

namespace RAWSimO.Core.Control
{
    #region OrderManager

    /// <summary>
    /// Implements the core order manager functionality.
    /// </summary>
    public abstract class OrderManager : IUpdateable, IOptimize, IStatTracker
    {
        #region Constructor

        /// <summary>
        /// Creates a new order manager.
        /// </summary>
        /// <param name="instance">The instance this order manager belongs to.</param>
        protected OrderManager(Instance instance)
        {
            Instance = instance;

            // Subscribe to events
            Instance.BundleStored += SignalBundleStored;
            Instance.NewOrder += SignalNewOrderAvailable;
            Instance.OrderCompleted += SignalOrderFinished;
        }

        #endregion Constructor

        #region Fields
        /// <summary>
        /// Number of pending orders
        /// </summary>
        public int PendingOrdersCount => _pendingOrders.Count;

        /// <summary>
        /// The instance this manager is assigned to.
        /// </summary>
        protected Instance Instance { get; set; }

        /// <summary>
        /// All not yet decided orders.
        /// </summary>
        protected HashSet<Order> _pendingOrders = new HashSet<Order>();

        /// <summary>
        /// List of not yet decided orders, used for easier indexing
        /// Contains all the same elements as HashSet
        /// </summary>
        protected List<Order> _orders = new List<Order>();

        /// <summary>
        /// Refill amount for addresses
        /// </summary>
        protected Dictionary<string, int> _refillingRequests = new Dictionary<string, int>();

        /// <summary>
        /// Refilling bots getting to specific address.
        /// </summary>
        protected Queue<string> _openRefillingAddress = new Queue<string>();

        /// <summary>
        /// Refilling bots getting to specific address to refill stock.
        /// </summary>
        protected Queue<string> _openStockRefillingAddress = new Queue<string>();

        /// <summary>
        /// Indicates that the current situation has already been investigated. So that it will be ignored.
        /// </summary>
        protected bool SituationInvestigated { get; set; }

        /// <summary>
        /// Structure used to store info to pass to remote order manager
        /// </summary>
        public struct OptimizationInfo
        {
            public int botID;
            public bool new_order;
            public bool reoptimizationFlag;
        }
        public OptimizationInfo optimizationInfo;

        public double lastReoptimizationTime = 0.0;

        #endregion Fields

        #region Methods (implemented)

        /// <summary>
        /// Exposes DecideAboutPendingOrders for usage in BotStates to trigger optimization
        /// after new order is wanted.
        /// </summary>
        public void GetNewOrder(double currentTime, int botID)
        {
            /*
            Bug hack when current time usage is wrong.
            When GetNewItem / Order functions are called from BotStates then the current time actually represents the old time.
            Alternatively, when we have calls happening at the same time, we want to execute all of them
            This is the case when different pickers finished with picking simultaneously.
            */
            double time_interval = currentTime - lastReoptimizationTime;
            bool same_time_call = Math.Abs(time_interval) < 0.001;
            bool illegal_use_of_current_time = time_interval < 0 && !same_time_call;
            if (illegal_use_of_current_time)
                throw new Exception("Illegal use of current time!");
            bool reoptimization_available = time_interval > Instance.SettingConfig.reoptimizationTimeInterval;
            if (same_time_call || reoptimization_available)
            {
                optimizationInfo.reoptimizationFlag = true;
                optimizationInfo.botID = botID;
                optimizationInfo.new_order = true;
                DecideAboutPendingOrders();
                lastReoptimizationTime = currentTime;
                optimizationInfo.reoptimizationFlag = false;
            }
        }
        /// <summary>
        /// Exposes DecideAboutPendingOrders for usage in BotStates to trigger optimization
        /// after new item is wanted.
        /// </summary>
        public void GetNewItem(double currentTime, int botID)
        {
            double time_interval = currentTime - lastReoptimizationTime;
            bool same_time_call = Math.Abs(time_interval) < 0.001;
            bool illegal_use_of_current_time = time_interval < 0 && !same_time_call;
            if (illegal_use_of_current_time)
                throw new Exception("Illegal use of current time!");
            bool reoptimization_available = time_interval > Instance.SettingConfig.reoptimizationTimeInterval;
            if (same_time_call || reoptimization_available)
            {
                optimizationInfo.reoptimizationFlag = true;
                optimizationInfo.botID = botID;
                optimizationInfo.new_order = false;
                DecideAboutPendingOrders();
                lastReoptimizationTime = currentTime;
                optimizationInfo.reoptimizationFlag = false;
            }
        }

        #region Refilling

        /// <summary>
        /// Processes the item and if needed asks for refilling.
        /// </summary>
        /// <param name="task">Multipoint gather task that is processing.</param>
        /// <param name="Ms">Bot which collects items.</param>
        /// <param name="quantityNeeded">Quantity needed to be put on the bot.</param>
        /// <param name="triesOfItemCollecting">Tries of collecting the item.</param>
        /// <param name="time">Time needed for whole item.</param>
        /// <param name="address">Address of the item.</param>
        /// <param name="destinationWaypoint">Current destination waypoint.</param>
        public void CheckForReplenishment(MultiPointGatherTask task, MovableStation Ms, int quantityNeeded, int triesOfItemCollecting, double time, string address, Waypoint destinationWaypoint)
        {
            var pod = Instance.GetWaypointFromAddress(address).Pod;
            Items.SimpleItemDescription item = task.Order.GetItemByAddress(address);
            if (pod.CapacityInUse <= quantityNeeded)
            {
                var ppt = new PreparePartialTask(
                    Ms, 
                    Instance.GetWaypointByID(Instance.addressToAccessPoint[item.GetAddressWithoutSufix()]), 
                    address, 
                    Ms.SwarmState.currentPalletGroup);

                Ms.Instance.Controller.OrderManager.RequestRefillingAtLocation(address, (int)(pod.Capacity));
                // split order
                if (triesOfItemCollecting == 0)
                {
                    Ms.StateQueueEnqueueSecondToLast(ppt);
                    Waypoint point = Ms.Instance.GetWaypointByID(item.ID);
                    // locations where robot will come
                    task.Locations.Add(destinationWaypoint);
                    // true locations of the pod so that the mapping of the
                    // bot location and the pod location is tracked
                    task.PodLocations.Add(point);
                    task.PodItems.Add(item);
                    Ms.Instance.Controller.MateScheduler.itemTable[Ms.ID].AddAddress(item.GetAddress());
                    task.NeededQuantities.Add(address, (int)(quantityNeeded - pod.CapacityInUse));
                    task.TriesOfItemCollecting.Add(address, triesOfItemCollecting + 1);
                    task.Times.Add(address, time);
                    if (!task.LocationItemDictionary.ContainsKey(point))
                    {
                        task.LocationItemDictionary.Add(point, new List<Items.ItemDescription>());
                    }
                    task.LocationItemDictionary[point].Add(item);
                }
                Ms.ProcessedQuantity += pod.CapacityInUse;
                pod.CapacityInUse = 0;
            } else
            {
                pod.CapacityInUse -= quantityNeeded;
                Ms.ProcessedQuantity += quantityNeeded;
            }
        }

        /// <summary>
        /// Return true if there is an address where there are no items available
        /// </summary>
        public bool IsRefillingNeeded()
        {
            return _openRefillingAddress.Count != 0;
        }

        /// <summary>
        /// Return true if there is an address where stock refilling is needed.
        /// </summary>
        public bool IsStockRefillingNeeded()
        {
            return _openStockRefillingAddress.Count != 0;
        }

        /// <summary>
        /// Returns all refilling addresses
        /// </summary>
        /// <returns></returns>
        public List<string> GetRefillingAddresses()
        {
            return _refillingRequests.Keys.ToList();
        }
        /// <summary>
        /// Returns refilling amount needed
        /// </summary>
        /// <returns></returns>
        public int GetRefillingAmount(string address)
        {
            return _refillingRequests.ContainsKey(address) ? _refillingRequests[address]: 0;
        }
        /// <summary>
        /// Updated needed refilling
        /// </summary>
        public void RequestRefillingAtLocation(string address, int quantity)
        {
            if (_refillingRequests.ContainsKey(address))
            {
                _refillingRequests[address] += quantity;
            } else
            {
                _refillingRequests.Add(address, quantity);
                _openRefillingAddress.Enqueue(address);
            }
        }

        /// <summary>
        /// Enqueues addresses that need the stock refill.
        /// </summary>
        /// <param name="addresses">List of addresses that need the stock refill.</param>
        public void RequestStockRefillingAtLocation(List<string> addresses)
        {
            foreach(string address in addresses)
            {
                if(address != "")
                {
                    _openStockRefillingAddress.Enqueue(address);
                }
            }
        }

        /// <summary>
        /// Refills location
        /// </summary>
        public void OnRefillingEnded(string address, int quantity)
        {
            if (!_refillingRequests.ContainsKey(address)) return;
            _refillingRequests[address] -= quantity;
            if (_refillingRequests[address] <= 0)
            {
                _refillingRequests.Remove(address);
            }
        }

        /// <summary>
        /// Gives next address which is needed for refilling.
        /// </summary>
        /// <returns></returns>
        public string NextRefillingAddress()
        {
            if(_openRefillingAddress.Count == 0)
            {
                return "";
            }
            return _openRefillingAddress.Dequeue();
        }

        /// <summary>
        /// Gives next address which is needed for stock refill.
        /// </summary>
        /// <returns>Address of the item that needs stock refill.</returns>
        public string NextStockRefillingAddress()
        {
            if (_openStockRefillingAddress.Count == 0)
            {
                return "";
            }
            return _openStockRefillingAddress.Dequeue();
        }

        #endregion

        /// <summary>
        /// Immediately submits the order to the station.
        /// </summary>
        /// <param name="order">The order that is going to be allocated.</param>
        /// <param name="station">The station the order is assigned to.</param>
        protected void AllocateOrder(Order order, OutputStation station)
        {
            // Update lists
            _pendingOrders.Remove(order);
            _orders.Remove(order);
            // Update intermediate capacity information
            station.RegisterOrder(order);
            // Submit the decision
            Instance.Controller.Allocator.Allocate(order, station);
        }

        /// <summary>
        /// Gets all pending orders that are fulfillable by their actually available stock.
        /// </summary>
        /// <returns>An array of pending orders.</returns>
        protected HashSet<Order> GetPendingAvailableStockOrders() =>
            GetPendingAvailableStockOrders(int.MaxValue);

        /// <summary>
        /// Gets all pending orders that are fulfillable by their actually available stock.
        /// </summary>
        /// <param name="maxValue">The max amount of orders to take</param>
        /// <returns>A hash-set of pending orders.</returns>
        protected HashSet<Order> GetPendingAvailableStockOrders(int maxValue) =>
            new HashSet<Order>(_pendingOrders.Where(o => o.Positions.All(p => Instance.StockInfo.GetActualStock(p.Key) >= p.Value)).Take(maxValue));

        #endregion Methods (implemented)

        #region Signals

        /// <summary>
        /// Signals the manager that the order was submitted to the system.
        /// </summary>
        /// <param name="order">The order that was allocated.</param>
        /// <param name="station">The station this order was allocated to.</param>
        public void SignalOrderAllocated(Order order, OutputStation station) { /* Not in use anymore */ }

        /// <summary>
        /// Signals the manager that the given order was completed.
        /// </summary>
        /// <param name="order">The order that was completed.</param>
        /// <param name="station">The station at which the order was completed.</param>
        public void SignalOrderFinished(Order order, OutputStation station) { SituationInvestigated = false; }

        /// <summary>
        /// Signals the manager that the order was submitted to the system.
        /// </summary>
        /// <param name="order">The order that was allocated.</param>
        public void SignalNewOrderAvailable(Order order) { SituationInvestigated = false; }

        /// <summary>
        /// Signals the manager that the bundle was placed on the pod.
        /// </summary>
        /// <param name="bundle">The bundle that was stored.</param>
        /// <param name="station">The station the bundle was assigned to.</param>
        /// <param name="pod">The pod the bundle is stored in.</param>
        /// <param name="bot">The bot that fetched the bundle.</param>
        public void SignalBundleStored(InputStation station, Bot bot, Pod pod, ItemBundle bundle) { SituationInvestigated = false; }

        /// <summary>
        /// Signals the manager that a station that was previously not in use can now be assigned orders.
        /// </summary>
        /// <param name="station">The newly activated station.</param>
        public void SignalStationActivated(OutputStation station) { SituationInvestigated = false; }

        #endregion Signals

        #region Methods (abstract)

        /// <summary>
        /// This is called to decide about potentially pending orders.
        /// This method is being timed for statistical purposes and is also ONLY called when <code>SituationInvestigated</code> is <code>false</code>.
        /// Hence, set the field accordingly to react on events not tracked by this outer skeleton.
        /// </summary>
        protected abstract void DecideAboutPendingOrders();

        #endregion Methods (abstract)

        #region IUpdateable Members

        /// <summary>
        /// The next event when this element has to be updated.
        /// </summary>
        /// <param name="currentTime">The current time of the simulation.</param>
        /// <returns>The next time this element has to be updated.</returns>
        public virtual double GetNextEventTime(double currentTime) { return double.PositiveInfinity; }
        /// <summary>
        /// Updates the element to the specified time.
        /// </summary>
        /// <param name="lastTime">The time before the update.</param>
        /// <param name="currentTime">The time to update to.</param>
        public virtual void Update(double lastTime, double currentTime)
        {
            // Retrieve the next order that we have not seen so far
            var order = Instance.ItemManager.RetrieveOrder(this);
            while (order != null)
            {
                // Add the bundle
                _pendingOrders.Add(order);
                _orders.Add(order);
                // Retrieve the next bundle that we have not seen so far
                order = Instance.ItemManager.RetrieveOrder(this);
                // Mark new situation
                SituationInvestigated = false;
            }
            // Decide about remaining orders
            if (!SituationInvestigated)
            {
                // Measure time for decision
                DateTime before = DateTime.Now;
                // Do the actual work
                DecideAboutPendingOrders();
                lastReoptimizationTime = currentTime;
                // Calculate decision time
                Instance.Observer.TimeOrderBatching((DateTime.Now - before).TotalSeconds);
                // Remember that we had a look at the situation
                SituationInvestigated = true;
            }
        }

        #endregion

        #region IOptimize Members

        /// <summary>
        /// Signals the current time to the mechanism. The mechanism can decide to block the simulation thread in order consume remaining real-time.
        /// </summary>
        /// <param name="currentTime">The current simulation time.</param>
        public abstract void SignalCurrentTime(double currentTime);

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
    }

    #endregion OrderManager
}
