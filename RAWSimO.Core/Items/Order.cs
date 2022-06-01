using RAWSimO.Core.Elements;
using RAWSimO.Core.Helper;
using RAWSimO.Core.Info;
using RAWSimO.Core.Management;
using RAWSimO.Core.Waypoints;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Core.Items
{
    /// <summary>
    /// Defines one order.
    /// </summary>
    public class Order : IOrderInfo
    {
        private static int _idCounter = 0;

        public static void ResetIDCounter() { _idCounter = 0; }

        #region Constructors

        /// <summary>
        /// Creates a new instance of the order.
        /// </summary>
        internal Order(Waypoint dropWaypoint = null)
        {
            DropWaypoint = dropWaypoint;
            ID = _idCounter++;
        }

        #endregion

        #region Core
        /// <summary>
        /// The overall demand quantity.
        /// </summary>
        private int _overallQuantity = 0;
        /// <summary>
        /// The quantities needed per order line.
        /// </summary>
        private Dictionary<ItemDescription, int> _quantities = new Dictionary<ItemDescription, int>();
        /// <summary>
        /// Pallet IDs on which item is put on.
        /// </summary>
        private Dictionary<Waypoint, int> _palletIDs = new Dictionary<Waypoint, int>();

        /// <summary>
        /// The requests belonging to the order lines.
        /// </summary>
        private Dictionary<ItemDescription, HashSet<ExtractRequest>> _requests = new Dictionary<ItemDescription, HashSet<ExtractRequest>>();
        /// <summary>
        /// The overall served demand quantity.
        /// </summary>
        private int _overallServedQuantity = 0;
        /// <summary>
        /// The quantities already fulfilled per order line.
        /// </summary>
        private Dictionary<ItemDescription, int> _servedQuantities = new Dictionary<ItemDescription, int>();

        /// <summary>
        /// Used when robots need to visit specific locations to collect items,
        /// as opposed to choosing one or multiple locations to collect the desired
        /// queanity of an item.
        /// The latter is tracked in `_servedQuantities` and relates to original problem.
        /// -1 -> location is still not considered
        /// 0 -> location is open (multiple)
        /// 1 -> location is locked (only one, determined by the robot)
        /// 2 -> picking in process
        /// 3 -> location is completed
        /// </summary>
        private Dictionary<ItemDescription, int> _locationStatus = new Dictionary<ItemDescription, int>();
        
        /// <summary>
        /// The number of finished order lines.
        /// </summary>
        private int _servedPositions;
        /// <summary>
        /// Assist times for each Item description
        /// </summary>
        public Dictionary<ItemDescription, double> _assistTimes = new Dictionary<ItemDescription, double>();
        /// <summary>
        /// Order ID.
        /// </summary>
        public int ID { get; private set; }

        public int movableStationID = -1;
        public double movableStationHue = 180;
        /// <summary>
        /// The time this order is placed.
        /// </summary>
        public double TimeStamp { get; set; }
        /// <summary>
        /// The time stamp this order was submitted to an output-station.
        /// </summary>
        public double TimeStampSubmit { get; set; } = double.PositiveInfinity;
        /// <summary>
        /// The time this order was submitted to a queue of a station.
        /// </summary>
        public double TimeStampQueued { get; set; }
        /// <summary>
        /// List of times needed for each item
        /// </summary>
        public Dictionary<ItemDescription, double> Times { get { return _assistTimes; } }

        /// <summary>
        /// The time stamp at which the order should be completed.
        /// </summary>
        public double DueTime { get; set; } = double.PositiveInfinity;
        /// <summary>
        /// Item drop waypoint
        /// </summary>
        public Waypoint DropWaypoint { get; private set; } = null;
        /// <summary>
        /// boolean indicating if this order is completed
        /// </summary>
        public bool Completed { get; set; }
        /// <summary>
        /// Enumerates all lines of the order.
        /// </summary>
        public Dictionary<ItemDescription, int> Positions { get { return _quantities; } }
        /// <summary>
        /// Gets palletID on which item is put on - stacked pallets version
        /// </summary>
        public Dictionary<Waypoint, int> PalletIDs { get { return _palletIDs; } }
        /// <summary>
        /// Ordered list of items according to some initial sorting.
        /// This won't necessarily be the final order.
        /// Used only in swarm.
        /// </summary>
        public List<ItemDescription> OrderedItemList = new List<ItemDescription>();

        /// <summary>
        /// Gets the overall demand count of all lines of the order.
        /// </summary>
        /// <returns>The overall quantity necessary to fulfill this order.</returns>
        public int GetDemandCount() { return _overallQuantity; }
        /// <summary>
        /// Gets the overall open demand count of all lines of the order.
        /// </summary>
        /// <returns>The overall open demand of the order.</returns>
        public int GetOpenDemandCount() { return _overallQuantity - _overallServedQuantity; }
        /// <summary>
        /// Gets the given lines's demand count.
        /// </summary>
        /// <param name="item">The order line.</param>
        /// <returns>The demand of the line.</returns> 
        public int GetDemandCount(ItemDescription item) { return _quantities.ContainsKey(item) ? _quantities[item] : 0; }
        /// <summary>
        /// Enumerates all requests for picking the different items.
        /// </summary>
        public IEnumerable<ExtractRequest> Requests { get { return _requests.SelectMany(i => i.Value); } }
        /// <summary>
        /// Adds a new line to the order.
        /// </summary>
        /// <param name="itemDescription">The item description of the line.</param>
        /// <param name="count">The quantity.</param>
        public void AddPosition(ItemDescription itemDescription, int count, double times = -1, int palletID = 0)
        {
            if (!_quantities.ContainsKey(itemDescription))
            {
                _quantities[itemDescription] = 0;
                _servedQuantities[itemDescription] = 0;
                _locationStatus[itemDescription] = -1;
            }
            _quantities[itemDescription] += count;
            _overallQuantity += count;

            if (times == -1)
            {
                double distrProb = 0.912;
                if (Instance.CurrentInstance.Randomizer.NextDouble() < distrProb)
                {
                    times = 2.0 + Instance.CurrentInstance.Randomizer.NextGammaDouble(MateBot.kParam, MateBot.thetaParam, 0, 80);
                }
                else
                {
                    times = 80.0 + Instance.CurrentInstance.Randomizer.NextGammaDouble(MateBot.kParamBig, MateBot.thetaParamBig);
                }
            }
            if (_assistTimes.ContainsKey(itemDescription))
                _assistTimes[itemDescription] = times;
            else
                _assistTimes.Add(itemDescription, times);
            _palletIDs.Add(itemDescription.Instance.GetWaypointFromAddress((itemDescription as SimpleItemDescription).GetLocation()), palletID);
            OrderedItemList.Add(itemDescription);
        }

        /// <summary>
        /// Used to update the item completion inside the order
        /// </summary>
        /// <param name="item"></param>
        /// <returns></returns>
        public bool CompleteLocation(ItemDescription item)
        {
            if (_servedQuantities[item] < _quantities[item])
            {
                _servedQuantities[item]++;
                _locationStatus[item] = 3;
                return true;
            }
            else
                return false;
        }
        /// <summary>
        /// Adds the request to pick the item to the order. This can be used by other components to see the particular requests' status.
        /// </summary>
        /// <param name="itemDescription">The SKU to add the request for.</param>
        /// <param name="request">The particular request to pick the item.</param>
        public void AddRequest(ItemDescription itemDescription, ExtractRequest request)
        { if (!_requests.ContainsKey(itemDescription)) _requests[itemDescription] = new HashSet<ExtractRequest>(); _requests[itemDescription].Add(request); }
        /// <summary>
        /// Returns the number of units needed to fulfill the given position.
        /// </summary>
        /// <param name="item">The SKU.</param>
        /// <returns>Number of units necessary to fulfill the position corresponding to the given SKU.</returns>
        public int PositionOverallCount(ItemDescription item) { return _quantities.ContainsKey(item) ? _quantities[item] : 0; }
        /// <summary>
        /// Returns the number of units picked so far for the given position.
        /// </summary>
        /// <param name="item">The SKU.</param>
        /// <returns>Number of units picked so far for the position corresponding to the given SKU.</returns>
        public int PositionServedCount(ItemDescription item) { return _servedQuantities.ContainsKey(item) ? _servedQuantities[item] : 0; }

        /// <summary>
        /// Gets the location status
        /// </summary>
        /// <param name="item">Item object, corresponds to the location</param>
        /// <returns>Status of the item (-1, 0, 1, 2, 3) or -2 if it doesn't exist</returns>
        public int GetLocationStatus(ItemDescription item) { return _locationStatus.ContainsKey(item) ? _locationStatus[item] : -2; }

        public List<string> GetLocationAddresses() { return _locationStatus.Keys.Select(x => (x as SimpleItemDescription).GetLocation()).ToList(); }
        /// <summary>
        /// Get all addresses that are of status -1, 0 or 1.
        /// </summary>
        /// <returns></returns>
        public List<string> GetOpenLocationAddresses() { return _locationStatus.Keys.Where(x => _locationStatus[x] < 2).Select(x => (x as SimpleItemDescription).GetLocation()).ToList(); }


        /// <summary>
        /// Fulfills one unit of this order.
        /// </summary>
        /// <param name="item">The physical unit used to fulfill a part of the order.</param>
        /// <returns>Indicates whether fulfilling one unit of the order was successful.</returns>
        public bool Serve(ItemDescription item)
        {
            _overallServedQuantity++;
            if (_servedQuantities[item] < _quantities[item])
            {
                _servedQuantities[item]++;
                if (_servedQuantities[item] >= _quantities[item])
                {
                    _servedPositions++;
                }
                return true;
            }
            else
                return false;
        }
        /// <summary>
        /// Checks whether the order is complete.
        /// </summary>
        /// <returns><code>true</code> if the order is complete, <code>false</code> otherwise.</returns>
        public virtual bool IsCompleted() => Completed;

        #endregion

        #region Inherited methods

        /// <summary>
        /// Returns a simple string giving information about the object.
        /// </summary>
        /// <returns>A simple string.</returns>
        public override string ToString() { return "Order-(" + string.Join(",", _quantities.Keys.Select(p => p.ToDescriptiveString())) + ")"; }
        #endregion

        #region Helper fields

        /// <summary>
        /// A helper tag that can be used for meta information purposes by other methods.
        /// </summary>
        internal bool HelperBoolTag;

        #endregion

        #region IOrderInfo Members

        /// <summary>
        /// Gets all positions of the order.
        /// </summary>
        /// <returns>An enumeration of all item-description in this order.</returns>
        public IEnumerable<IItemDescriptionInfo> GetInfoPositions() { return _quantities.Keys; }
        /// <summary>
        /// Gets the given position's quantity.
        /// </summary>
        /// <param name="item">The position.</param>
        /// <returns>The quantity of the position.</returns>
        public int GetInfoServedCount(IItemDescriptionInfo item) { return _servedQuantities[item as ItemDescription]; }
        /// <summary>
        /// Gets the given position's already served quantity.
        /// </summary>
        /// <param name="item">The position.</param>
        /// <returns>The already served quantity of the position.</returns>
        public int GetInfoDemandCount(IItemDescriptionInfo item) { return _quantities[item as ItemDescription]; }
        /// <summary>
        /// Indicates whether the order is already completed.
        /// </summary>
        /// <returns><code>true</code> if the order is completed, <code>false</code> otherwise.</returns>
        public bool GetInfoIsCompleted() { return IsCompleted(); }
        #endregion

        public int GetAssignedMovableStationID() { return movableStationID;  }
        public double GetAssignedMovableStationHue() { return movableStationHue; }
    }
}
