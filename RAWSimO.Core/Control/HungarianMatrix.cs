using RAWSimO.Core.Elements;
using RAWSimO.Core.Waypoints;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RAWSimO.Core.Control
{
    /// <summary>
    /// Class which represents hungarian matrix object
    /// </summary>
    class HungarianMatrix
    {
        public HungarianMatrix(List<MateBot> mateBots)
        {
            MateBots = new HashSet<MateBot>(mateBots);
            defaultColumn = new Dictionary<MateBot, HungarianCell>(mateBots.Count);
            foreach (var mate in mateBots)
                defaultColumn.Add(mate, new HungarianCell());
            matrix = new Dictionary<Tuple<Waypoint, Bot>, Dictionary<MateBot, HungarianCell>>();
        }

        #region API
        /// <summary>
        /// Adds column to matrix
        /// </summary>
        /// <param name="columnKey">Column key which will be used</param>
        public void Add(Tuple<Waypoint, Bot> columnKey)
        {
            if(!matrix.ContainsKey(columnKey))
                matrix.Add(columnKey, Clone(defaultColumn));
        }
        /// <summary>
        /// Removes column from matrix
        /// </summary>
        /// <param name="columnKey">Column key to remove</param>
        /// <returns><see langword="true"/> if sucessfull, <see langword="false"/> otherwise</returns>
        public bool Remove(Tuple<Waypoint, Bot> columnKey) => matrix.Remove(columnKey);
        /// <summary>
        /// Removes column from matrix whose key contains <paramref name="bot"/> and <paramref name="waypoint"/>
        /// </summary>
        /// <param name="waypoint">assist location used in column key</param>
        /// <param name="bot">bot used in column key</param>
        /// <returns><see langword="true"/> if sucessfull, <see langword="false"/> otherwise</returns>
        public bool Remove(Waypoint waypoint, Bot bot)
        {
            var columnKey = matrix.Keys.FirstOrDefault(twb => twb.Item1 == waypoint && twb.Item2 == bot);
            return columnKey != null ? Remove(columnKey) : false;
        }
        /// <summary>
        /// Removes all columns containing <paramref name="bot"/> in it's key
        /// </summary>
        /// <param name="bot">Bot whose columns will be removed</param>
        /// <returns><see langword="true"/> if every column removal was sucessfull, <see langword="false"/> otherwise</returns>
        public bool Remove(Bot bot)
        {
            var columnsToRemove = new HashSet<Tuple<Waypoint,Bot>>(matrix.Keys.Where(key => key.Item2 == bot));
            if (!columnsToRemove.Any()) return false;

            bool success = true;
            foreach (var columnKey in columnsToRemove)
                success &= Remove(columnKey);

            return success;
        }
        /// <summary>
        /// Updates arrival times of <paramref name="mate"/> based on <paramref name="arrivalMap"/> (row)
        /// </summary>
        /// <param name="mate">Mate whose arrival times will be updated</param>
        /// <param name="arrivalMap">mapping of arrival times on locations</param>
        public void UpdateArrivalTime(MateBot mate, Dictionary<Waypoint, double> arrivalMap)
        {
            if (!MateBots.Contains(mate))
                throw new ArgumentException("Argument " + nameof(mate) + " is not present as a row value in hungarian matrix!");
            if (mate == null)
                throw new ArgumentNullException(nameof(mate), " is null!");
            if (arrivalMap == null)
                throw new ArgumentNullException(nameof(arrivalMap), " is null!");

            //update all mate arrival times, either with values form the arrivalMap or with positive infinity
            foreach (var row in matrix)
            {
                var location = row.Key.Item1;
                row.Value[mate].MateArrivalTime = arrivalMap.ContainsKey(location) ? arrivalMap[location] : double.PositiveInfinity;
            }
        }
        /// <summary>
        /// Updates arrival time of <paramref name="mate"/> to <paramref name="waypoint"/> to assist <paramref name="bot"/>
        /// </summary>
        /// <param name="mate">Mate whose arrival time will be updated</param>
        /// <param name="waypoint">Location at which time will be updated</param>
        /// <param name="bot">Bot that will be helped by mate</param>
        /// <param name="value">New arrival time</param>
        public void UpdateArrivalTime(MateBot mate, Waypoint waypoint, Bot bot, double value)
        {
            if (!MateBots.Contains(mate))
                throw new ArgumentException("Argument " + nameof(mate) + " is not present as a row value in hungarian matrix!");
            if (mate == null)
                throw new ArgumentNullException(nameof(mate), " is null!");
            if (waypoint == null)
                throw new ArgumentNullException(nameof(waypoint), " is null!");
            if (bot == null)
                throw new ArgumentNullException(nameof(bot), " is null!");

            var columnKey = matrix.Keys.FirstOrDefault(twb => twb.Item1 == waypoint && twb.Item2 == bot);

            if (columnKey == null)
                throw new ArgumentException("Hungarian matrix has no column asociated with " + nameof(waypoint) + " and " + nameof(bot));

            matrix[columnKey][mate].MateArrivalTime = value;
        }
        /// <summary>
        /// Updates arrival time of bot on a given locaiton
        /// </summary>
        /// <param name="columnKey"></param>
        /// <param name="value"></param>
        public void UpdateArrivalTime(Tuple<Waypoint, Bot> columnKey, double value)
        {
            if (columnKey == null)
                throw new ArgumentNullException(nameof(columnKey) + " is null!");
            foreach (var entry in matrix[columnKey].Values)
                entry.StationArrivalTime = value;
        }
        /// <summary>
        /// Updates arrival time of <paramref name="bot"/> on <paramref name="waypoint"/> to <paramref name="value"/>
        /// </summary>
        /// <param name="waypoint">location which will be updated</param>
        /// <param name="bot">Bot whose arrival time will be updated</param>
        /// <param name="value">New arrival time</param>
        public void UpdateArrivalTime(Waypoint waypoint, Bot bot, double value) =>
                UpdateArrivalTime(matrix.Keys.FirstOrDefault(twb => twb.Item1 == waypoint && twb.Item2 == bot), value);
        /// <summary>
        /// Calculates assignment of mates 
        /// </summary>
        /// <returns>Assignment map</returns>
        public Dictionary<MateBot, Tuple<Waypoint, Bot>> CalculateAssignment()
        {
            var assignmentMap = new Dictionary<MateBot, Tuple<Waypoint, Bot>>();
            var unassignedMates= new HashSet<MateBot>(MateBots);
            var unassignedColumnKeys = new List<Tuple<Waypoint, Bot>>(matrix.Keys);

            //repeat assignment process until there are no mates left or until there are no columns left
            while(unassignedMates.Count > 0 && unassignedColumnKeys.Count > 0)
            {
                //get min values of the remaining columns and rows
                Dictionary<Tuple<Waypoint,Bot> ,double> minValues = GetMinValues(unassignedColumnKeys, unassignedMates);

                //sort unassigned column keys by min values
                unassignedColumnKeys.Sort(
                    (Tuple<Waypoint, Bot> keyA, Tuple<Waypoint, Bot> keyB) =>
                        minValues[keyA] < minValues[keyB] ? -1 : minValues[keyA] > minValues[keyB] ? 1 : 0
                );

                //find mate with best cost
                var minColumn = unassignedColumnKeys.First();
                var minValue = minValues[minColumn];

                //if minimal value is max value, then we have no viable locations left, return
                if(minValue == double.MaxValue)
                    return assignmentMap;

                //get mate with the first occurance of min value which is still unassigned
                var mate = matrix[minColumn].First(kvp => kvp.Value.Value == minValue && unassignedMates.Contains(kvp.Key)).Key;

                //add found mate and it's column to assignmentMap
                assignmentMap.Add(mate, minColumn);

                //remove mate and minColumn from unassigned mates and columnKeys
                unassignedMates.Remove(mate);
                unassignedColumnKeys.Remove(minColumn);
            }

            return assignmentMap;
        }

        #endregion

        #region Properties
        /// <summary>
        /// Collection of MateBots used in this instance
        /// </summary>
        public readonly HashSet<MateBot> MateBots;
        /// <summary>
        /// All locations currently taken into consideration by scheduler
        /// </summary>
        public IEnumerable<Waypoint> Locations => matrix.Select(kvp => kvp.Key.Item1);
        #endregion

        #region Private methods
        /// <summary>
        /// Helper method whick clones one row
        /// </summary>
        /// <param name="row">row to be cloned</param>
        /// <returns></returns>
        private Dictionary<MateBot, HungarianCell> Clone(Dictionary<MateBot, HungarianCell> row)
        {
            var ret = new Dictionary<MateBot, HungarianCell>(row.Count, row.Comparer);
            foreach (var kvp in row)
                ret.Add(kvp.Key, kvp.Value.Clone() as HungarianCell);
            return ret;
        }
        /// <summary>
        /// Helper method which gets minimal values in unasigned columns taking only unassigned mates into consideration
        /// </summary>
        /// <param name="unassignedColumnKeys">Columns which are taken into account</param>
        /// <param name="unassignedMates">Rows which are taken into account</param>
        /// <returns>Map of which min value in column corresponds to which column</returns>
        private Dictionary<Tuple<Waypoint, Bot>, double> GetMinValues(List<Tuple<Waypoint, Bot>> unassignedColumnKeys, HashSet<MateBot> unassignedMates)
        {
            var minValues = new Dictionary<Tuple<Waypoint, Bot>, double>(unassignedColumnKeys.Count);

            foreach(var columnKey in unassignedColumnKeys)
            {
                double minValue = double.MaxValue;
                foreach(var rowKey in unassignedMates)
                {
                    var value = matrix[columnKey][rowKey].Value;
                    if (value < minValue)
                        minValue = value;
                    
                }
                minValues.Add(columnKey, minValue);
            }
            return minValues;
        }
        #endregion

        #region Private fields
        /// <summary>
        /// Internal data structure used to store hungarian matrix
        /// </summary>
        private Dictionary<Tuple<Waypoint, Bot>, Dictionary<MateBot, HungarianCell>> matrix;
        /// <summary>
        /// Default column used when new columns are added
        /// </summary>
        private readonly Dictionary<MateBot, HungarianCell> defaultColumn;
        #endregion

        /// <summary>
        /// This class represents one cell in a <see cref="HungarianMatrix"/>
        /// </summary>
        class HungarianCell : ICloneable
        {
            /// <summary>
            /// Constructs a default cell. Arrival times of <see cref="MovableStation"/> and <see cref="MateBot"/> will be set to infinity
            /// </summary>
            public HungarianCell()
            {
                StationArrivalTime = double.PositiveInfinity;
                MateArrivalTime = double.PositiveInfinity;
            }
            /// <summary>
            /// Constructs a cell with given arrival times of <see cref="MovableStation"/> and <see cref="MateBot"/>
            /// </summary>
            /// <param name="stationTime">Predicted arrival time of <see cref="MovableStation"/></param>
            /// <param name="mateTime">Predicted arrival time of <see cref="MateBot"/></param>
            public HungarianCell(double stationTime, double mateTime)
            {
                StationArrivalTime = stationTime;
                MateArrivalTime = mateTime;
            }

            #region Properties
            /// <summary>
            /// Value of a cell
            /// </summary>
            public double Value => StationArrivalTime + MateArrivalTime;
            /// <summary>
            /// Time at which <see cref="MovableStation"/> is going to arrive
            /// </summary>
            public double StationArrivalTime { get; set; }
            /// <summary>
            /// Time at which <see cref="MateBot"/> is going to arrive
            /// </summary>
            public double MateArrivalTime { get; set; }
            #endregion

            #region ICloneable implementation
            /// <summary>
            /// Clones this object
            /// </summary>
            /// <returns><see cref="HungarianCell"/> that has the same values stored</returns>
            public object Clone() => new HungarianCell(StationArrivalTime, MateArrivalTime);
            #endregion
        }
    }
}
