using RAWSimO.Core.Waypoints;
using System;
using System.Collections.Generic;


namespace RAWSimO.Core.Items
{
    public class DummyOrder : Order
    {
        public DummyOrder()
        {
            Locations = new List<int>();
            Times = new List<double>();
            Type = ItemType.LocationsList;
            Completed = false;
            TimeStamp = 0;//every order is automatically placed
            DueTime = double.MaxValue;//complete whenever
            Instance = null;
        }
        /// <summary>
        /// Constructs DummyOrder from locations list 
        /// </summary>
        /// <param name="list">List from which Order will be constructed</param>
        public DummyOrder(List<int> list, Instance instance = null)
        {
            Locations = list;
            Type = ItemType.LocationsList;
            Completed = false;
            TimeStamp = 0;
            DueTime = double.MaxValue;
            Instance = instance;
            Locations = new List<int>();
            Times = new List<double>();
            DropWaypoint = Instance.GetDropWaypointFromAddress(null);
        }
        /// <summary>
        /// Constructs DummyOrder from address info list
        /// </summary>
        /// <param name="addressInfo">List from which Order will be constructed</param>
        public DummyOrder(List<Tuple<string, double>> addressInfo, Instance instance = null, string dropaddress = null)
        {
            Type = ItemType.AddressList;
            Completed = false;
            TimeStamp = 0;
            DueTime = double.MaxValue;
            Instance = instance;
            Locations = new List<int>();
            Times = new List<double>();

            //if instance was not passed, return since locations have to be calculated from the layout
            if (instance == null) return; 
            //decipher addresses into Waypoint
            foreach (var tuple in addressInfo)
            {
                Waypoint wp = Instance.GetWaypointfromAddress(tuple.Item1);
                Locations.Add(wp.ID);
                Times.Add(tuple.Item2);
            }

            DropWaypoint = Instance.GetDropWaypointFromAddress(dropaddress);
        }
        /// <summary>
        /// boolean indicating if this order is completed
        /// </summary>
        public bool Completed{ get; set; }
        /// <summary>
        /// instance this order belongs to
        /// </summary>
        private Instance Instance { get; set; }
        public override bool IsCompleted(){ return Completed; }
        /// <summary>
        /// Represents a list of Waypoint ID's which should be visited in an order
        /// </summary>
        public List<int> Locations{ get; set; }
        /// <summary>
        /// Represents a list of times needed for each item
        /// </summary>
        public List<double> Times { get; set; }
        /// <summary>
        /// Types of items in this order
        /// </summary>
        public ItemType Type{get; set;}
        /// <summary>
        /// Item drop waypoint
        /// </summary>
        public Waypoint DropWaypoint { get; set; }
        /// <summary>
        /// For printing
        /// </summary>
        public override string ToString()
        {
            return "Order - " + Locations.ToString();
        }
    }

}