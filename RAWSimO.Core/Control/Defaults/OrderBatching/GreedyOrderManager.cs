using RAWSimO.Core.Configurations;
using RAWSimO.Core.Elements;
using RAWSimO.Core.IO;
using RAWSimO.Core.Items;
using RAWSimO.Core.Metrics;
using RAWSimO.Toolbox;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;

namespace RAWSimO.Core.Control.Defaults.OrderBatching
{

    /// <summary>
    /// Implements a manager that assigns first available order to first available station
    /// </summary>
    public class GreedyOrderManager : OrderManager
    {
        private StreamWriter _writer;

        public GreedyOrderManager(Instance instance) : base(instance)
        {
            _writer = new StreamWriter($"{instance.CreatedAtString}.greedy");
        }
        public override void SignalCurrentTime(double currentTime)
        {
          /* Ignore since this simple manager is always ready. */
        }
        /// <summary>
        /// Method that assigns pending orders to stations if any station is available
        /// </summary>
        protected override void DecideAboutPendingOrders()
        {  
            //get all stations which are currently not doing anything
            List<MovableStation> availableStations = Instance.MovableStations.Where(s => s.CapacityInUse == 0).ToList();
            //assign pending orders to stations respectively
            int pendingOrdersCount = PendingOrdersCount;
            for (int i = 0; i < Math.Min(availableStations.Count, pendingOrdersCount); i++)
            {
                /*
                Order closest = Instance.findClosestOrder(_pendingOrders.ToList().
                                GetRange(0,Math.Min(20, _pendingOrders.Count)), availableStations[i]);
                                */
                Order closest = _pendingOrders.First();
                //assign closest order, closest will be removed from pending orders in AllocateOrder()
                AllocateOrder(closest, availableStations[i]);
                _writer.WriteLine($"{closest.ID} {availableStations[i].ID} {availableStations[i].CurrentWaypoint.X} {availableStations[i].CurrentWaypoint.Y}");
            }
        }

    }

}