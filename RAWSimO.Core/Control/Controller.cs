﻿using RAWSimO.Core.Configurations;
using RAWSimO.Core.Management;
using RAWSimO.Core.Control.Defaults.ItemStorage;
using RAWSimO.Core.Control.Defaults.MethodManagement;
using RAWSimO.Core.Control.Defaults.OrderBatching;
using RAWSimO.Core.Control.Defaults.PathPlanning;
using RAWSimO.Core.Control.Defaults.PalletStandManagment;
using RAWSimO.Core.Control.Defaults.PodStorage;
using RAWSimO.Core.Control.Defaults.ReplenishmentBatching;
using RAWSimO.Core.Control.Defaults.Repositioning;
using RAWSimO.Core.Control.Defaults.StationActivation;
using RAWSimO.Core.Control.Defaults.TaskAllocation;
using System;
using System.Linq;
using RAWSimO.Core.Control.Filters;

using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Collections.Generic;
using System.Text;
using Newtonsoft.Json;

namespace RAWSimO.Core.Control
{
    /// <summary>
    /// The main class containing all control mechanisms for decisions conducted during simulation.
    /// </summary>
    public class Controller
    {
        /// <summary>
        /// Creates a new controller instance.
        /// </summary>
        /// <param name="instance">The instance this controller belongs to.</param>
        public Controller(Instance instance)
        {
            Instance = instance;
            // Init path manager
            switch (instance.ControllerConfig.PathPlanningConfig.GetMethodType())
            {
                case PathPlanningMethodType.Simple: PathManager = null; break;
                case PathPlanningMethodType.Dummy: PathManager = new DummyPathManager(instance); break;
                case PathPlanningMethodType.WHCAvStar: PathManager = new WHCAvStarPathManager(instance); break;
                case PathPlanningMethodType.FAR: PathManager = new FARPathManager(instance); break;
                case PathPlanningMethodType.BCP: PathManager = new BCPPathManager(instance); break;
                case PathPlanningMethodType.CBS: PathManager = new CBSPathManager(instance); break;
                case PathPlanningMethodType.OD_ID: PathManager = new ODIDPathManager(instance); break;
                case PathPlanningMethodType.WHCAnStar: PathManager = new WHCAnStarPathManager(instance); break;
                case PathPlanningMethodType.PAS: PathManager = new PASPathManager(instance); break;
                default: throw new ArgumentException("Unknown path planning engine: " + instance.ControllerConfig.PathPlanningConfig.GetMethodType());
            }
            // Init bot manager
            switch (instance.ControllerConfig.TaskAllocationConfig.GetMethodType())
            {
                case TaskAllocationMethodType.Dummy: BotManager = new DummyBotManager(instance); break;
                case TaskAllocationMethodType.BruteForce: BotManager = new BruteForceBotManager(instance); break;
                case TaskAllocationMethodType.Random: BotManager = new RandomBotManager(instance); break;
                case TaskAllocationMethodType.Balanced: BotManager = new BalancedBotManager(instance); break;
                case TaskAllocationMethodType.Swarm: BotManager = new SwarmBotManager(instance); break;
                case TaskAllocationMethodType.ConstantRatio: BotManager = new ConstantRatioBotManager(instance); break;
                case TaskAllocationMethodType.Concept: BotManager = new ConceptBotManager(instance); break;
                default: throw new ArgumentException("Unknown bot manager: " + instance.ControllerConfig.TaskAllocationConfig.GetMethodType());
            }
            // Init station manager
            switch (instance.ControllerConfig.StationActivationConfig.GetMethodType())
            {
                case StationActivationMethodType.ActivateAll: StationManager = new ActivateAllStationManager(instance); break;
                case StationActivationMethodType.BacklogThreshold: StationManager = new BacklogThresholdStationManager(instance); break;
                case StationActivationMethodType.ConstantRatio: StationManager = new ConstantRatioStationManager(instance); break;
                case StationActivationMethodType.WorkShift: StationManager = new WorkShiftStationActivationManager(instance); break;
                default: throw new ArgumentException("Unknown station manager: " + instance.ControllerConfig.StationActivationConfig.GetMethodType());
            }
            // Init item storage manager
            switch (instance.ControllerConfig.ItemStorageConfig.GetMethodType())
            {
                case ItemStorageMethodType.Dummy: StorageManager = new DummyStorageManager(instance); break;
                case ItemStorageMethodType.Random: StorageManager = new RandomStorageManager(instance); break;
                case ItemStorageMethodType.Correlative: StorageManager = new CorrelativeStorageManager(instance); break;
                case ItemStorageMethodType.Turnover: StorageManager = new TurnoverStorageManager(instance); break;
                case ItemStorageMethodType.ClosestLocation: StorageManager = new ClosestLocationStorageManager(instance); break;
                case ItemStorageMethodType.Reactive: StorageManager = new ReactiveStorageManager(instance); break;
                case ItemStorageMethodType.Emptiest: StorageManager = new EmptiestStorageManager(instance); break;
                case ItemStorageMethodType.LeastDemand: StorageManager = new LeastDemandStorageManager(instance); break;
                default: throw new ArgumentException("Unknown storage manager: " + instance.ControllerConfig.ItemStorageConfig.GetMethodType());
            }
            // Init pod storage manager
            switch (instance.ControllerConfig.PodStorageConfig.GetMethodType())
            {
                case PodStorageMethodType.Dummy: PodStorageManager = new DummyPodStorageManager(instance); break;
                case PodStorageMethodType.Fixed: PodStorageManager = new FixedPodStorageManager(instance); break;
                case PodStorageMethodType.Nearest: PodStorageManager = new NearestPodStorageManager(instance); break;
                case PodStorageMethodType.StationBased: PodStorageManager = new StationBasedPodStorageManager(instance); break;
                case PodStorageMethodType.Cache: PodStorageManager = new CachePodStorageManager(instance); break;
                case PodStorageMethodType.Utility: PodStorageManager = new UtilityPodStorageManager(instance); break;
                case PodStorageMethodType.Random: PodStorageManager = new RandomPodStorageManager(instance); break;
                case PodStorageMethodType.Turnover: PodStorageManager = new TurnoverPodStorageManager(instance); break;
                default: throw new ArgumentException("Unknown pod manager: " + instance.ControllerConfig.PodStorageConfig.GetMethodType());
            }
            // Init repositioning manager
            switch (instance.ControllerConfig.RepositioningConfig.GetMethodType())
            {
                case RepositioningMethodType.Dummy: RepositioningManager = new DummyRepositioningManager(instance); break;
                case RepositioningMethodType.Cache: RepositioningManager = new CacheRepositioningManager(instance); break;
                case RepositioningMethodType.CacheDropoff: RepositioningManager = new CacheDropoffRepositioningManager(instance); break;
                case RepositioningMethodType.Utility: RepositioningManager = new UtilityRepositioningManager(instance); break;
                case RepositioningMethodType.Concept: RepositioningManager = new ConceptRepositioningManager(instance); break;
                default: throw new ArgumentException("Unknown repositioning manager: " + instance.ControllerConfig.RepositioningConfig.GetMethodType());
            }
            // Init order batching manager
            switch (instance.ControllerConfig.OrderBatchingConfig.GetMethodType())
            {
                case OrderBatchingMethodType.Greedy: OrderManager = new GreedyOrderManager(instance); break;
                case OrderBatchingMethodType.Default: OrderManager = new DefaultOrderManager(instance); break;
                case OrderBatchingMethodType.Random: OrderManager = new RandomOrderManager(instance); break;
                case OrderBatchingMethodType.Workload: OrderManager = new WorkloadOrderManager(instance); break;
                case OrderBatchingMethodType.Related: OrderManager = new RelatedOrderManager(instance); break;
                case OrderBatchingMethodType.NearBestPod: OrderManager = new NearBestPodOrderManager(instance); break;
                case OrderBatchingMethodType.Foresight: OrderManager = new ForesightOrderManager(instance); break;
                case OrderBatchingMethodType.PodMatching: OrderManager = new PodMatchingOrderManager(instance); break;
                case OrderBatchingMethodType.LinesInCommon: OrderManager = new LinesInCommonOrderManager(instance); break;
                case OrderBatchingMethodType.Queue: OrderManager = new QueueOrderManager(instance); break;
                case OrderBatchingMethodType.Remote: OrderManager = new RemoteOrderManager(instance); break;
                default: throw new ArgumentException("Unknown order manager: " + instance.ControllerConfig.OrderBatchingConfig.GetMethodType());
            }
            // Init replenishment batching manger
            switch (instance.ControllerConfig.ReplenishmentBatchingConfig.GetMethodType())
            {
                case ReplenishmentBatchingMethodType.Random: BundleManager = new RandomBundleManager(instance); break;
                case ReplenishmentBatchingMethodType.SamePod: BundleManager = new SamePodBundleManager(instance); break;
                default: throw new ArgumentException("Unknown replenishment manager: " + instance.ControllerConfig.ReplenishmentBatchingConfig.GetMethodType());
            }
            // Init meta method manager
            switch (instance.ControllerConfig.MethodManagementConfig.GetMethodType())
            {
                case MethodManagementType.NoChange: MethodManager = new NoChangeMethodManager(instance); break;
                case MethodManagementType.Random: MethodManager = new RandomMethodManager(instance); break;
                case MethodManagementType.Scheduled: MethodManager = new ScheduleMethodManager(instance); break;
                default: throw new ArgumentException("Unknown method manager: " + instance.ControllerConfig.MethodManagementConfig.GetMethodType());
            }
            // Init pallet stand manager
            switch (instance.ControllerConfig.PalletStandManagerConfig.GetMethodType())
            {
                case PalletStandManagerType.EuclideanGreedy: PalletStandManager = new PSEuclideanGreedyManager(instance); break;
                case PalletStandManagerType.Original: PalletStandManager = new PSOriginalManager(instance); break;
                case PalletStandManagerType.Advanced: PalletStandManager = new PSAdvancedManager(instance); break;
            }
            // Init allocator
            Allocator = new Allocator(instance);

            instance.SettingConfig.MateSchedulerLoggerPath = instance.layoutConfiguration.GetLoggerPath("Mate_scheduler", "MS");
            //Init Mate scheduler
            if (Instance.SettingConfig.SeeOffMateScheduling)
            {
                MateScheduler = new SeeOffMateScheduler(instance, instance.SettingConfig.MateSchedulerLoggerPath);
            }
            else if (Instance.SettingConfig.WaveEnabled)
            {
                MateScheduler = new WaveMateScheduler(instance, instance.SettingConfig.MateSchedulerLoggerPath);
            }
            else if  (Instance.SettingConfig.HungarianMateScheduling)
            {
                MateScheduler = new HungarianMateScheduler(instance, instance.SettingConfig.MateSchedulerLoggerPath);
            }
            else
            {
                MateScheduler = new MateScheduler(instance, instance.SettingConfig.MateSchedulerLoggerPath);
            }

            string miscellaneousLoggerPath = instance.layoutConfiguration.GetLoggerPath("Miscellaneous", "Misc");
            if (Logger != null)
                Logger.Close();
            Logger = new System.IO.StreamWriter(miscellaneousLoggerPath);
            
            //Init statistics manager
            StatisticsManager = new StatisticsManager(instance);

            OptimizationClient = new OptimizationClient(instance);
        }

        /// <summary>
        /// The instance to simulate.
        /// </summary>
        Instance Instance { get; set; }
        /// <summary>
        /// The method manager.
        /// </summary>
        public MethodManager MethodManager { get; private set; }
        /// <summary>
        /// The order manager.
        /// </summary>
        public OrderManager OrderManager { get; private set; }
        /// <summary>
        /// The bundle manager.
        /// </summary>
        public BundleManager BundleManager { get; private set; }
        /// <summary>
        /// The storage manager.
        /// </summary>
        public ItemStorageManager StorageManager { get; private set; }
        /// <summary>
        /// The pod storage manager.
        /// </summary>
        public PodStorageManager PodStorageManager { get; private set; }
        /// <summary>
        /// The repositioning manager.
        /// </summary>
        public RepositioningManager RepositioningManager { get; private set; }
        /// <summary>
        /// The station manager.
        /// </summary>
        public StationManager StationManager { get; private set; }
        /// <summary>
        /// The bot manager.
        /// </summary>
        public BotManager BotManager { get; private set; }
        /// <summary>
        /// The path planner.
        /// </summary>
        public PathManager PathManager { get; private set; }
        /// <summary>
        /// The pallet stand manager.
        /// </summary>
        public PalletStandManager PalletStandManager { get; private set; }
        /// <summary>
        /// The allocator.
        /// </summary>
        public Allocator Allocator { get; private set; }
        /// <summary>
        /// The mate scheduler
        /// </summary>
        public MateScheduler MateScheduler { get;  set; }
        /// <summary>
        /// The statistics manager
        /// </summary>
        public StatisticsManager StatisticsManager { get; set; }

        /// <summary>
        /// REST client that is an interface to outside optimization module.
        /// </summary>
        public OptimizationClient OptimizationClient { get; set; }

        /// <summary>
        /// Logger for miscellaneous output
        /// </summary>
        public System.IO.StreamWriter Logger { get; set; }

        /// <summary>
        /// The time the simulation step is completed.
        /// </summary>
        private double _updateFinishTime = 0.0;

        /// <summary>
        /// The current time.
        /// </summary>
        public double CurrentTime { get; private set; } = 0.0;

        /// <summary>
        /// The last time statistics summary was outputed.
        /// </summary>
        public double LastStatisticsWriteTime { get; private set; } = 0.0;

        /// <summary>
        /// The last time statistics summary was outputed.
        /// </summary>
        public StatisticsSummaryEntry TotalSummary;

        /// <summary>
        /// The last time statistics summary was outputed.
        /// </summary>
        public int StatisticsOutputsCount= 0;

        /// <summary>
        /// The progress of the simulation.
        /// </summary>
        public double Progress { get { return CurrentTime / (Instance.SettingConfig.SimulationWarmupTime + Instance.SettingConfig.SimulationDuration); } }

        /// <summary>
        /// Used to wait for workers that are still busy. (In case we simulated faster than real-time)
        /// </summary>
        /// <param name="currentTime">The current simulation time.</param>
        protected void WaitForUnfinishedWorker(double currentTime)
        {
            BotManager.SignalCurrentTime(currentTime);
            StationManager.SignalCurrentTime(currentTime);
            StorageManager.SignalCurrentTime(currentTime);
            PodStorageManager.SignalCurrentTime(currentTime);
            RepositioningManager.SignalCurrentTime(currentTime);
            OrderManager.SignalCurrentTime(currentTime);
            BundleManager.SignalCurrentTime(currentTime);
        }

        /// <summary>
        /// Moves the simulation forward by the specified amount of time.
        /// </summary>
        /// <param name="elapsedTime">The relative amount of time by which the simulation is forwarded.</param>
        public void Update(double elapsedTime, double updateRate = 1)
        {
            // Don't want to update less than the time required for something to move past 1/3 of the tolerance in a given time interval
            // TODO this probably results in inaccurate timing statistics - is it necessary to change this? (minimum updatetime influences constant times of tasks - they are not constant anymore, because their finish event might be skipped)
            double minimumUpdateTime = Instance.SettingConfig.Tolerance / 3.0 / Instance.Bots.Max(b => b.MaxVelocity);

            for (var k = 0; k < updateRate; ++k)
            {
                _updateFinishTime = CurrentTime + elapsedTime;
                while (CurrentTime < _updateFinishTime)
                {
                    //if order mode is fixed or fill and all the order have been completed, stop when all movable stations enter rest
                    //or if we have manually set order count, stop at the reached count of completed orders
                    if (Instance.SettingConfig.StatisticsSummaryOutputFrequency > 0 &&
                        CurrentTime > 1 &&
                        Instance.MovableStations.All(ms => ms.IsResting()) &&
                        Instance.MovableStations.Count(ms => ms.AssignedOrders.Count() > 0) == 0 &&
                        OrderManager.PendingOrdersCount == 0)
                    {
                        if (Instance.SettingConfig.PartialStatisticsSummaryFile != null)
                        {
                            Instance.LogDefault(">>> Writing final partial statistics summary...");
                            StatisticsManager.StatisticsSummaryTotal.Update(new Management.StatisticsSummaryEntry(Instance));
                            StatisticsManager.WriteStatisticsSummary(
                                Instance.SettingConfig.PartialStatisticsSummaryFile,
                                true
                            );
                        }

                        Instance.SettingConfig.StopCondition = true;
                        return;
                    }
                    // If all of the MS are resting or some of them is refill and all orders are done, then we are done with the simulation.
                    if ((Instance.MovableStations.All(ms => (ms.IsResting() || ms.IsRefill)) || Instance.SettingConfig.ManualOrderCountStopCondition) &&
                        Instance.SettingConfig.OrderCountStopCondition == Instance.ItemManager.CompletedOrdersCount)
                    {
                        Instance.SettingConfig.StopCondition = true;
                        return;
                    }
                    //if simulation time progress reaches 100%, return 
                    if(Progress >= 1)
                    {
                        Instance.SettingConfig.StopCondition = true;
                        return;
                    }

                    // --> Get the next event time
                    double nextTime =
                        Math.Min(_updateFinishTime, // Stop after all time is elapsed
                        Math.Min(MethodManager.GetNextEventTime(CurrentTime), // Check the meta manager
                        Instance.Updateables.Min(u => u.GetNextEventTime(CurrentTime)))); // Jump to next event of all agents

                    // See if a potential collision will happen before the next event
                    double minTimeDelta = Math.Min(Instance.Compound.GetShortestTimeWithoutCollision(), nextTime - CurrentTime);
                    minTimeDelta = Math.Max(minTimeDelta, minimumUpdateTime);	// Make sure update rate never gets too slow

                    // Update by at least the minimum, but don't go past the next time
                    nextTime = Math.Min(_updateFinishTime, CurrentTime + minTimeDelta);

                    // Wait for unfinished optimization workers
                    WaitForUnfinishedWorker(nextTime);

                    // --> Run up til the next event
                    // Update method manager (needs to be updated first, because it might change the update-list)
                    MethodManager.Update(CurrentTime, nextTime);
                    // Update all agents in the list
                    foreach (var updateable in Instance.Updateables)
                        updateable.Update(CurrentTime, nextTime);

                    // Set new time
                    CurrentTime = nextTime;

                    if (Instance.SettingConfig.PartialStatisticsSummaryFile != null &&
                        Instance.SettingConfig.StatisticsSummaryOutputFrequency > 0 &&
                        LastStatisticsWriteTime + Instance.SettingConfig.StatisticsSummaryOutputFrequency * 60 < CurrentTime)
                    {
                        Instance.LogDefault(">>> Writing partial results ...");
                        // Write statistics
                        StatisticsManager.StatisticsSummaryTotal.Update(new StatisticsSummaryEntry(Instance));
                        StatisticsManager.WriteStatisticsSummary(
                            Instance.SettingConfig.PartialStatisticsSummaryFile,
                            true
                        );
                        // Clear statistics
                        Instance.StatReset();
                        Instance.LogDefault(">>> Finished writing partial results ...");
                        LastStatisticsWriteTime = CurrentTime;
                        StatisticsOutputsCount++;
                        if (Instance.layoutConfiguration.PickersPerPeriod.Count > 0)
                        {
                            for (int i = 0; i < Instance.MateBots.Count; i++)
                            {
                                if (StatisticsOutputsCount < Instance.layoutConfiguration.PickersPerPeriod.Count)
                                {
                                    if (i < Instance.layoutConfiguration.PickersPerPeriod[StatisticsOutputsCount])
                                        Instance.MateBots[i].IsActive = true;
                                    else
                                        Instance.MateBots[i].IsActive = false;

                                }
                                else
                                {
                                    if (i < Instance.layoutConfiguration.PickersPerPeriod.Last())
                                        Instance.MateBots[i].IsActive = true;
                                    else
                                        Instance.MateBots[i].IsActive = false;
                                }
                            }
                        }
                        if (Instance.layoutConfiguration.BotsPerPeriod.Count > 0)
                        {
                            for (int i = 0; i < Instance.MovableStations.Count - Instance.layoutConfiguration.RefillingStationCount; i++)
                            {
                                if (StatisticsOutputsCount < Instance.layoutConfiguration.BotsPerPeriod.Count)
                                {
                                    if (i < Instance.layoutConfiguration.BotsPerPeriod[StatisticsOutputsCount])
                                        Instance.MovableStations[i].IsActive = true;
                                    else
                                        Instance.MovableStations[i].IsActive = false;

                                }
                                else
                                {
                                    if (i < Instance.layoutConfiguration.BotsPerPeriod.Last())
                                        Instance.MovableStations[i].IsActive = true;
                                    else
                                        Instance.MovableStations[i].IsActive = false;
                                }
                            }
                        }
                        
                        if (Instance.layoutConfiguration.RefillingPerPeriod.Count > 0)
                        {
                            for (int i = Instance.layoutConfiguration.MovableStationCount; i < Instance.MovableStations.Count; i++)
                            {
                                if (StatisticsOutputsCount < Instance.layoutConfiguration.RefillingPerPeriod.Count)
                                {
                                    if (i < Instance.layoutConfiguration.RefillingPerPeriod[StatisticsOutputsCount] + Instance.layoutConfiguration.MovableStationCount)
                                        Instance.MovableStations[i].IsActive = true;
                                    else
                                        Instance.MovableStations[i].IsActive = false;

                                }
                                else
                                {
                                    if (i < Instance.layoutConfiguration.BotsPerPeriod.Last() + Instance.layoutConfiguration.MovableStationCount)
                                        Instance.MovableStations[i].IsActive = true;
                                    else
                                        Instance.MovableStations[i].IsActive = false;
                                }
                            }
                        }
                    }
                }
            }
        }

        #region Manager exchange handling

        /// <summary>
        /// Exchanges the active pod storage manager with the given one.
        /// </summary>
        /// <param name="newManager">The new manager.</param>
        public void ExchangePodStorageManager(PodStorageManager newManager)
        {
            Instance.RemoveUpdateable(PodStorageManager);
            PodStorageManager = newManager;
            Instance.AddUpdateable(newManager);
        }

        #endregion

    }

    public class PickerInfo
    {
        public PickerInfo(int picker_id_, double x_, double y_, int bot_id_, string item_, double item_status_)
        {
            picker_id = picker_id_;
            x = x_; y = y_;
            bot_id = bot_id_;
            item = item_;
            item_status = item_status_;
        }
        public int picker_id { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public int bot_id { get; set; }
        public string item { get; set; }
        public double item_status { get; set; }
    }

    public class BotInfo
    {
        public BotInfo(int bot_id_, double x_, double y_, string item_, double item_status_, double _predicted_time)
        {
            bot_id = bot_id_;
            x = x_;
            y = y_;
            item = item_;
            item_status = item_status_;
            predicted_time = _predicted_time;
        }

        public int bot_id { get; set; }
        public double x { get; set; }
        public double y { get; set; }
        public string item { get; set; }
        public double item_status { get; set; }
        public double predicted_time { get; set; }
    }

    public class OrderInfo
    {
        public int order_id { get; set; }
        public int bot_id { get; set; }
        public int deadline { get; set; }
        public List<string> items { get; set; }
        public List<double> times { get; set; }
    }

    public class PickerAssignment
    {
        public PickerAssignment(int bot_id_, string item_)
        {
            bot_id = bot_id_;
            item = item_;
        }
        public int bot_id;
        public string item;
    }
    public class PickerSchedule
    {
        public PickerSchedule(int picker_id_)
        {
            picker_id = picker_id_;
            schedule = new List<PickerAssignment>();
        }
        public int picker_id { get; set; }
        public List<PickerAssignment> schedule;
    }

    public class BotOrder
    {
        public string bot { get; set; }
        public List<string> order { get; set; }

        public override string ToString()
        {
            string botOrder = bot + " : [";
            foreach (var item in order)
            {
                botOrder += item + ", ";
            }
            botOrder = botOrder.Remove(botOrder.Length - 2, 2) + "]";
            return botOrder;
        }
    }

    public class OptimizationClient
    {
        private HttpClient client;
        private Instance Instance { get; set; }

        public ItemSchedule schedule { get; set; }

        public double lastCallTime = 0;
        
        public bool isRecentlyUpdated = false;
        public class OrderInfo2
        {
            public OrderInfo2(int botid, List<string> itemlist)
            {
                botID = botid;
                items = itemlist;
            }
            public int botID { get; set; }
            public List<string> items { get; set; }
            public override string ToString()
            {
                string botOrder = botID + " : [";
                foreach (var item in items)
                {
                    botOrder += item + ", ";
                }
                botOrder = botOrder.Remove(botOrder.Length - 2, 2) + "]";
                return botOrder;
            }
        }

        public class ItemSchedule
        {
            public Dictionary<int, List<string>> botOrder;
            public Dictionary<int, int> botOrderID;
            public Dictionary<int, PickerInfo> pickers;
            public Dictionary<int, BotInfo> bots;
            public Dictionary<int, List<PickerAssignment>> pickersSchedule;
            public List<Tuple<int, int, string>> assistanceRequests;
            public ItemSchedule()
            {
                botOrder = new Dictionary<int, List<string>>();
                botOrderID = new Dictionary<int, int>();
                pickers = new Dictionary<int, PickerInfo>();
                bots = new Dictionary<int, BotInfo>();
                pickersSchedule = new Dictionary<int, List<PickerAssignment>>();
                assistanceRequests = new List<Tuple<int, int, string>>();
            }

            public void Update(List<OrderInfo> assignedOrders)
            {
                foreach (var orderInfo in assignedOrders)
                {
                    // if an order is already in the schedule
                    // just update the order of items
                    if (botOrder.ContainsKey(orderInfo.bot_id) &&
                        botOrderID[orderInfo.bot_id] == orderInfo.order_id)
                    {
                        botOrder[orderInfo.bot_id].Sort(
                            (string a, string b) =>
                            {
                                int ia = orderInfo.items.FindIndex(adr => adr == a);
                                int ib = orderInfo.items.FindIndex(adr => adr == b);
                                return ia > ib ? 1 : ia < ib ? -1 : 0;
                            }
                            );
                    }
                    else
                    {
                        botOrder[orderInfo.bot_id] = orderInfo.items;
                        botOrderID[orderInfo.bot_id] = orderInfo.order_id;
                    }
                }
            }

            public void InitPickers(List<int> pickerIDs)
            {
                foreach (var id in pickerIDs)
                {
                    PickerInfo pickerInfo = new PickerInfo(
                        id, 0.0, 0.0, -1, "", -1);
                    pickers.Add(id, pickerInfo);
                }
            }

            public void InitBots(List<int> botIDs)
            {
                foreach (var id in botIDs)
                {
                    BotInfo botInfo = new BotInfo(
                        id, 0.0, 0.0, "", -1, 0.0);
                    bots.Add(id, botInfo);
                }
            }

            public void UpdatePicker(int ID, double x, double y, Waypoints.Waypoint currentWp, int botID, string item, double item_status)
            {
                UpdatePickerXY(ID, x, y);
                pickers[ID].bot_id = botID;
                UpdatePickerAddress(ID, item);
                UpdatePickerItemStatus(ID, item_status);
            }

            public void UpdatePickerWp(int ID, Waypoints.Waypoint currentWp)
            {
                pickers[ID].x = currentWp.X;
                pickers[ID].y = currentWp.Y;
            }
            public void UpdatePickerXY(int ID, double x, double y)
            {
                pickers[ID].x = x;
                pickers[ID].y = y;
            }
            public void UpdateBotWp(int ID, Waypoints.Waypoint currentWp)
            {
                bots[ID].x = currentWp.X;
                bots[ID].y = currentWp.Y;
            }
            public void UpdateBotXY(int ID, double x, double y)
            {
                bots[ID].x = x;
                bots[ID].y = y;
            }
            public void UpdateBotPredictedTime(int ID, double predicted_time)
            {
                bots[ID].predicted_time = predicted_time;
            }
            public void UpdatePickerItemStatus(int ID, double item_status)
            {
                pickers[ID].item_status = item_status;
            }

            public void UpdatePickerAddress(int ID, string address)
            {
                pickers[ID].item = address;
            }

            public void UpdateBotItem(int ID, string item)
            {
                bots[ID].item = item;
            }

            public void UpdateBotItemStatus(int ID, double item_status)
            {
                bots[ID].item_status = item_status;
            }

            public List<string> GetItemSchedule(int botID)
            {
                return botOrder[botID];
            }

            public void Update(int botID, int orderID, List<string> items)
            {
                // Update on receiving new order
                botOrder[botID] = items;
                botOrderID[botID] = orderID;
            }

            public void Update(int botID, int orderID, string item)
            {
                if (botOrder[botID].Count == 0)
                {
                    botOrder[botID].Add(item);
                    return;
                }
                // Update on receiving new item
                if (botOrder[botID][0] == item) return;
                botOrder[botID].Remove(item);
                botOrder[botID].Insert(0, item);
            }

            public void Update(List<PickerSchedule> picker_schedules)
            {
                foreach (var pickerSchedule in picker_schedules)
                {
                    if (!pickersSchedule.ContainsKey(pickerSchedule.picker_id))
                        pickersSchedule.Add(pickerSchedule.picker_id, pickerSchedule.schedule);
                    else
                    {
                        pickersSchedule[pickerSchedule.picker_id] = pickerSchedule.schedule;
                    }
                }
            }

            public Tuple<int, int, string> GetNextAssignment(int picker_id)
            {
                if (!pickersSchedule.ContainsKey(picker_id) || pickersSchedule[picker_id].Count == 0)
                    return null;
                PickerAssignment pa = pickersSchedule[picker_id].First();
                // check if assistance was requested
                Tuple<int, int, string> ar = assistanceRequests.Find(ar => ar.Item1 == pa.bot_id && ar.Item3 == pa.item);
                return ar;
            }

            public void AddNextAssignment(int bot_id, int wp_id, string adr)
            {
                assistanceRequests.Add(new Tuple<int, int, string>(bot_id, wp_id, adr));
            }

            public void RemoveAssignment(int picker_id)
            {
                if (pickersSchedule[picker_id].Count == 0) return;
                PickerAssignment pa = pickersSchedule[picker_id].First();
                // this check is due to bug where sometimes first item in pickers schedule is not the one picker has just picked
                if (pa.bot_id != pickers[picker_id].bot_id || pa.item != pickers[picker_id].item) return;
                assistanceRequests.Remove(assistanceRequests.Find(ar => ar.Item1 == pa.bot_id && ar.Item3 == pa.item));
                pickersSchedule[picker_id].Remove(pickersSchedule[picker_id].First());
            }
        }

        public OptimizationClient(Instance instance)
        {
            client = new HttpClient();
            client.BaseAddress = new Uri("http://127.0.0.1:5000");
            client.DefaultRequestHeaders.Clear();
            client.DefaultRequestHeaders.Accept.Add(
                new MediaTypeWithQualityHeaderValue("application/json"));
            Instance = instance;
            schedule = new ItemSchedule();
            // TODO: move to some appropriate location
            schedule.InitPickers(instance.MateBots.Select(mb => mb.ID).ToList());
            schedule.InitBots(instance.MovableStations.Select(ms => ms.ID).ToList());
        }

        public void ClearItems(int ID)
        {
            if (schedule.botOrder.ContainsKey(ID))
                schedule.botOrder[ID].Clear();
        }

        public void AddNewItem(int ID, string address)
        {
            if (!schedule.botOrder.ContainsKey(ID))
                schedule.botOrder.Add(ID, new List<string>());
            schedule.botOrder[ID].Add(address);
        }

        public void RemoveItem(int ID, string address)
        {
            schedule.botOrder[ID].Remove(address);
            schedule.UpdateBotItem(ID, address);
            schedule.UpdateBotItemStatus(ID, -1);
        }
    }
}
