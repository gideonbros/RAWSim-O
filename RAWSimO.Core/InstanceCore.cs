using RAWSimO.Core.Bots;
using RAWSimO.Core.Configurations;
using RAWSimO.Core.Control;
using RAWSimO.Core.Control.Shared;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Geometrics;
using RAWSimO.Core.Helper;
using RAWSimO.Core.Items;
using RAWSimO.Core.Management;
using RAWSimO.Core.Statistics;
using RAWSimO.Core.Waypoints;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace RAWSimO.Core
{
    /// THIS PARTIAL CLASS CONTAINS THE CORE FIELDS OF AN INSTANCE
    /// <summary>
    /// The core element of each simulation instance.
    /// </summary>
    public partial class Instance
    {
        #region Constructors

        internal Instance()
        {
            Observer = new SimulationObserver(this);
            StockInfo = new StockInformation(this);
            MetaInfoManager = new MetaInformationManager(this);
            FrequencyTracker = new FrequencyTracker(this);
            ElementMetaInfoTracker = new ElementMetaInfoTracker(this);
            BotCrashHandler = new BotCrashHandler(this);
            SharedControlElements = new SharedControlElementsContainer(this);
            CurrentInstance = this;
        }

        #endregion

        #region Core

        /// <summary>
        /// Reference to the current instance.
        /// </summary>
        public static Instance CurrentInstance { get; private set; }

        /// <summary>
        /// The name of the instance.
        /// </summary>
        public string Name;

        /// <summary>
        /// Date and time when this instance was created.
        /// </summary>
        public DateTime CreatedAt { get; } = DateTime.Now;

        /// <summary>
        /// Default string representation of date and time when this instance was created.
        /// </summary>
        public string CreatedAtString { get { return CreatedAt.ToString("yyyy-MM-dd-HH-mm-ss"); } }

        /// <summary>
        /// The configuration to use while executing the instance.
        /// </summary>
        public SettingConfiguration SettingConfig { get; set; }
        /// <summary>
        /// The configuration for all controlling mechanisms.
        /// </summary>
        public ControlConfiguration ControllerConfig { get; set; }
        /// <summary>
        /// All SKUs available in this instance.
        /// </summary>
        public List<ItemDescription> ItemDescriptions = new List<ItemDescription>();
        /// <summary>
        /// All item bundles known so far.
        /// </summary>
        public List<ItemBundle> ItemBundles = new List<ItemBundle>();
        /// <summary>
        /// A list of given orders that will be passed to the item manager.
        /// </summary>
        public OrderList OrderList;

        public Dictionary<string, Dictionary<int, int>> OrderLocationInfo;

        /// <summary>
        /// List of refills used for replenishment of items.
        /// <remarks>
        /// Dictionary key is the completed order count before which the replenishment is triggered.
        /// In list are all necessary addresses.
        /// </remarks>
        /// </summary>
        public Dictionary<int, List<string>> RefillsList = new Dictionary<int, List<string>>();
        /// <summary>
        /// The compound declaring all physical attributes of the instance.
        /// </summary>
        public Compound Compound;
        /// <summary>
        /// All robots of this instance.
        /// </summary>
        public List<Bot> Bots = new List<Bot>();
        /// <summary>
        /// All movable stations of this instance.
        /// </summary>
        public List<MovableStation> MovableStations = new List<MovableStation>();
        /// <summary>
        /// All MateBots of this instace
        /// </summary>
        public List<MateBot> MateBots = new List<MateBot>();
        /// <summary>
        /// All pods of this instance.
        /// </summary>
        public List<Pod> Pods = new List<Pod>();
        /// <summary>
        /// All elevators of this instance.
        /// </summary>
        public List<Elevator> Elevators = new List<Elevator>();
        /// <summary>
        /// All input-stations of this instance.
        /// </summary>
        public List<InputStation> InputStations = new List<InputStation>();
        /// <summary>
        /// All input pallet stands of this instance
        /// </summary>
        public List<InputPalletStand> InputPalletStands = new List<InputPalletStand>();
        /// <summary>
        /// All output pallet stands of this instance
        /// </summary>
        public List<OutputPalletStand> OutputPalletStands = new List<OutputPalletStand>();
        /// <summary>
        /// All label stands of this instance with their IDs
        /// </summary>
        public Dictionary<string, Waypoint> LabelStands = new Dictionary<string, Waypoint>();
        /// <summary>
        /// All output-stations of this instance.
        /// </summary>
        public List<OutputStation> OutputStations = new List<OutputStation>();
        /// <summary>
        /// All waypoints of this instance.
        /// </summary>
        public List<Waypoint> Waypoints = new List<Waypoint>();
        /// <summary>
        /// All semaphors of this instance.
        /// </summary>
        public List<QueueSemaphore> Semaphores = new List<QueueSemaphore>();
        /// <summary>
        /// Set of all available bot parking space 
        /// </summary>
        public HashSet<Waypoint> ParkingLot = new HashSet<Waypoint>();
        /// <summary>
        /// String array containing map layout and direction costs
        /// </summary>
        public List<List<string>> MapArray = new List<List<string>>();
        /// <summary>
        /// String array containing item addresses
        /// </summary>
        public List<List<string>> ItemAddressArray = new List<List<string>>();
        /// <summary>
        /// Contains visiting order for all picking locations (pods/items)
        /// </summary>
        public List<List<string>> ItemAddressSortOrder = new List<List<string>>();
        /// <summary>
        /// Dictionary mapping address of the picking location/item to the sort order
        /// </summary>
        public Dictionary<string, int> addressToSortOrder = new Dictionary<string, int>();
        /// <summary>
        /// Data from file containing access points
        /// </summary>
        public List<List<string>> AccessPointsArray = new List<List<string>>();
        /// <summary>
        /// Data from file containing mapping from addresses to access points
        /// </summary>
        public List<List<string>> AddressesAccessPointsArray = new List<List<string>>();
        /// <summary>
        /// Data from file containing quantites of each location
        /// </summary>
        public List<List<int>> PodsQuantitiesArray = new List<List<int>>();
        /// <summary>
        /// Mapping each item address to an access point waypoint ID
        /// </summary>
        public Dictionary<string, int> addressToAccessPoint = new Dictionary<string, int>(); 
        /// <summary>
        /// Mapping each item address to an access point
        /// </summary>
        public Dictionary<string, Tuple<int, int>> accessPointToLocation = new Dictionary<string, Tuple<int, int>>();
        /// <summary>
        /// Int array containing mateBot zones where they operate
        /// </summary>
        public List<List<string>> ZoneLocationsOnMap = new List<List<string>>();
        /// <summary>
        /// Number of storage pods
        /// </summary>
        public int PodCount { get; set; }
        /// <summary>
        /// Number of waypoint columns in the map
        /// </summary>
        public int MapColumnCount { get; set; }
        /// <summary>
        /// Number of waypoint rows in the map
        /// </summary>
        public int MapRowCount { get; set; }
        /// <summary>
        /// Horizontal length of the map layout
        /// </summary>
        public double MapHorizontalLength { get; set; }
        /// <summary>
        /// Vertical length of the map layout
        /// </summary>
        public double MapVerticalLength { get; set; }
        /// <summary>
        /// Number of input pallet stands
        /// </summary>
        public int NInputPalletStands { get; set; }
        /// <summary>
        /// Number of output pallet stands
        /// </summary>
        public int NOutputPalletStands { get; set; }
        /// <summary>
        /// Number of waypoints for Input pallet stand queue/buffer
        /// </summary>
        public int InputQueueSize { get; set; }
        /// <summary>
        /// Number of waypoints for Output pallet stand queue/buffer
        /// </summary>
        public int OutputQueueSize { get; set; }
        /// <summary>
        /// All distinct x values of a Waypoint, sorted
        /// </summary>
        public List<double> WaypointXs { get; set; }
        /// <summary>
        /// All distinct y values of a Waypoint, sorted
        /// </summary>
        public List<double> WaypointYs { get; set; }
        /// <summary>
        /// Dictinary which holds all waypoints as values and x,y as keys
        /// </summary>
        public Dictionary<double, Dictionary<double, Waypoint>> Waypoints_dict { get; set; } = new Dictionary<double, Dictionary<double, Waypoint>>();
        
        /// <summary>
        /// Returns waypoint for a given address.
        /// </summary>
        /// <param name="address"></param>
        /// <returns></returns>
        public Waypoint GetWaypointFromAddress(string address)
        {
            var pod = Pods.FirstOrDefault(a => a.Address.Equals(Order.RemoveSufixFromAddress(address)));
            if (pod == null) throw new Exception("Address " + address + " doesn't exists!");
            return pod.Waypoint;
        }
       
        /// <summary>
        /// Returns address for a given waypoint.
        /// </summary>
        /// <param name="waypoint"></param>
        /// <returns></returns>
        public string GetAddressFromWayoint(Waypoint waypoint)
        {
            var pod = Pods.FirstOrDefault(a => a.Waypoint == waypoint);
            if (pod == null) throw new Exception("Waypoint " + waypoint + " doesn't exists!");
            return pod.Address;
        }

        public double GetHueForSector(char S)
        {
            return (double)(S - 'A') / ('K' - 'A') * 360;
        }

        /// <summary>
        /// Finds adequate drop waypoint from output_id
        /// </summary>
        /// <param name="output_stand_id"></param>
        /// <returns></returns>
        public Waypoint GetDropWaypointFromAddress(int output_stand_id = -1)
        {
            for(int i = 0; i < OutputPalletStands.Count; i++)
            {
                if (OutputPalletStands[i].ID == output_stand_id)
                {
                    lastDropSite = i;
                    return OutputPalletStands[i].Waypoint;
                }
            }
            if (OutputPalletStands.Count > 0 )
                return OutputPalletStands[lastDropSite++ % OutputPalletStands.Count].Waypoint;
            return null;
        }

        /// <summary>
        /// Refills some quantity of items at a specific address
        /// </summary>
        /// <param name="address">Where to refill capacity.</param>
        public void RefillCapacityInUseAtAddress(string address)
        {
            var pod = GetWaypointFromAddress(address).Pod;
            pod.CapacityInUse += pod.Capacity;
            pod.StockCapacity -= pod.Capacity;

            Controller.OrderManager.OnRefillingEnded(address, (int)pod.Capacity);
        }

        /// <summary>
        /// Refills some quantity of items at a specific address
        /// </summary>
        /// <param name="address">Where to refill stock capacity.</param>
        public void RefillStockCapacityAtAddress(string address)
        {
            var pod = GetWaypointFromAddress(address).Pod;
            pod.StockCapacity += pod.Capacity;
        }

        /// <summary>
        /// Finds element of the <see cref="List{T}"/> which is closest to destination
        /// </summary>
        /// <typeparam name="T">Class which is inherited from <see cref="Bot"/></typeparam>
        /// <typeparam name="S">Class which is inherited from <see cref="Circle"/></typeparam>
        /// <param name="list">List of elements which will be searched</param>
        /// <param name="destination"></param>
        /// <returns>Closest element of the list</returns>
        public T findClosestBot<T, S>(List<T> list, S destination) where T : Bot
                                                                   where S : Circle
        {
            T closest = null;
            double minDistance = double.MaxValue;
            foreach (var bot in list)
            {
                double distance = bot.CurrentWaypoint.GetDistance(destination);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = bot;
                }
            }
            return closest;
        }
        /// <summary>
        /// Deletes item duplicates and adds up the time 
        /// </summary>
        internal void MergeTheSameItems(List<string> allAddresses, List<double> allTimes, List<int> allQuantities, List<int> allPalletIDs, List<string> addresses, List<double> times, List<int> quantities, List<int> palletIDs)
        {
            for (int i = 0; i < allAddresses.Count; i++)
            {
                int index = -1;
                index = addresses.FindIndex(a => a == allAddresses[i]);
                if (index != -1)
                {
                    times[index] += allTimes[i];
                    quantities[index] += allQuantities[i];
                }
                else
                {
                    addresses.Add(allAddresses[i]);
                    times.Add(allTimes[i]);
                    quantities.Add(allQuantities[i]);
                    palletIDs.Add(allPalletIDs[i]);
                }
            }
        }

        /// <summary>
        /// Find waypoint in a list which is closest the focusWp
        /// </summary>
        /// <typeparam name="T">Location type derived from <see cref="Waypoint"/></typeparam>
        /// <param name="container">List of waypoints from which the closest will be found</param>
        /// <param name="focusWp">waypoint for which location is being searched</param>
        /// <returns>Location in <paramref name="container"/> which is closest to <paramref name="focusWp"/></returns>
        public T findClosestLocation<T>(IEnumerable<T> container, T focusWp) where T : Waypoint
        {
            T closest = null;
            double minDistance = double.MaxValue;
            foreach (var wp in container)
            {
                double distance = focusWp.GetDistance(wp);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = wp;
                }
            }
            return closest;
        }
        /// <summary>
        /// Find order which is closest to the given Bot
        /// </summary>
        /// <typeparam name="T">Order type derived from <see cref="Order"/></typeparam>
        /// <typeparam name="S">>Bot type derived from <see cref="Bot"/></typeparam>
        /// <param name="list">List of orders from which the closest will be found</param>
        /// <param name="bot">Bot for which order is being searched</param>
        /// <returns>Order in <paramref name="list"/> which is closest to <paramref name="bot"/></returns>
        public T findClosestOrder<T, S>(List<T> list, S bot) where T : Order
                                                             where S : Bot
        {
            T closest = null;
            double minDistance = double.MaxValue;
            foreach (var order in list)
            {
                double distance = GetWaypointByID(order.Positions.First().Key.ID).GetDistance(bot.CurrentWaypoint);
                if (distance < minDistance)
                {
                    minDistance = distance;
                    closest = order;
                }
            }
            return closest;
        }

        private static int lastDropSite = 0;
        #endregion
    }
}
