using RAWSimO.Core.Waypoints;
using RAWSimO.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;

namespace RAWSimO.Core.Control.Filters
{
    public class WideWave : IUpdateable
    {
        /// <summary>
        /// Constructs WideWave object used in Wave scheduling
        /// </summary>
        /// <param name="instance">instance this object belongs to</param>
        /// <param name="height">Height of the wave expressed in number of rows</param>
        /// <param name="enable">Bool indicating whether to enable wave </param>
        /// <param name="maxWaveHeight"> Max height of the wave expressed in number of rows </param>
        public WideWave(Instance instance, int height, int maxWaveHeight, bool enable)
        {
            Enable = enable;
            initialHeight = height;
            Height = initialHeight;
            lastHeightChangeTime = 0;
            Instance = instance;
            stopCountingNewOrdersShift = Instance.SettingConfig.waveAuxiliaryStoppingShift;

            int x = 0;
            int y = 0;

            map[y] = new Dictionary<int, Waypoint>
            {
                [x] = Instance.Waypoints[0]
            };

            realYtoVirtualY[Instance.Waypoints[0].Y] = 0;

            for (int i = 1; i < Instance.Waypoints.Count; ++i)
            {
                if (Instance.Waypoints[i].Y != Instance.Waypoints[i - 1].Y)
                {
                    ++y;
                    x = 0;
                    map[y] = new Dictionary<int, Waypoint>();
                    realYtoVirtualY[Instance.Waypoints[i].Y] = y;
                }
                map[y][x++] = Instance.Waypoints[i];
            }

            mapWidth = map[0].Keys.Count;
            mapHeight = map.Keys.Count;
            MaxWaveHeight = maxWaveHeight;

            waveAreaMaxY = mapHeight - 4; // upper boundary for the whole wave area
            waveAreaMinY = 1; // lower boundary for the whole wave area

            UpperBound = waveAreaMaxY;
            previousUpperBound = waveAreaMaxY;
            LowerBound = Math.Max(waveAreaMinY, UpperBound - Height);
        }

        #region Private fields
        /// <summary>
        /// helper flag
        /// </summary>
        private bool waitInitialization = true;
        /// <summary>
        /// this instance
        /// </summary>
        private Instance Instance;
        /// <summary>
        /// map stored as dict of dict
        /// </summary>
        private Dictionary<int, Dictionary<int, Waypoint>> map = new Dictionary<int, Dictionary<int, Waypoint>>();
        /// <summary>
        /// map width
        /// </summary>
        private readonly int mapWidth;
        /// <summary>
        /// map height
        /// </summary>
        private readonly int mapHeight;
        /// <summary>
        /// mapping between x,y values used in simulatior and indxes
        /// </summary>
        private readonly Dictionary<double, int> realYtoVirtualY = new Dictionary<double, int>();
        #endregion

        #region Properties
        /// <summary>
        /// Wave height
        /// </summary>
        public int Height { get; set; }
        /// <summary>
        /// Flag indicating whether wave scheduling is enabled or not
        /// </summary>
        public bool Enable { get; private set; }
        /// <summary>
        /// index of Wave upper bound
        /// </summary>
        public int UpperBound { get; private set; }
        /// <summary>
        /// upper and lower bounds for wave area
        /// </summary>
        public int waveAreaMaxY, waveAreaMinY;
        #endregion

        #region Public methods
        /// <summary>
        /// Checks if given <paramref name="waypoint"/> is located inside of Wave
        /// </summary>
        /// <param name="waypoint"><see cref="Waypoint"/> to check</param>
        /// <returns><see langword="true"/> if <paramref name="waypoint"/> is contained in a wave, <see langword="false"/> otherwise</returns>
        public bool Contains(Waypoint waypoint)
        {
            double top = map[UpperBound][0].Y;
            double bottom = map[LowerBound][0].Y;

            if(isInside(waypoint.Y, top, bottom))
            {
                //List<MultiPointGatherTask> tasks = Instance.MovableStations
                //    .Where(b => b.CurrentTask is MultiPointGatherTask)
                //    .Select(b => (MultiPointGatherTask)b.CurrentTask)
                //    .Where(t => t.Locations.Select(l => l.ID).Contains(waypoint.ID))
                //    .ToList();
                //foreach (var task in tasks)
                //{
                //    foreach (var point in task.Locations)
                //    {
                //        if (point.Y > waypoint.Y && !isInside(point.Y, top, bottom) && top < bottom)
                //            return false;
                //    }
                //}
                return true;
            }
            return false;

        }
        public bool isInside(double Y, double top, double bottom)
        {
            if (
                   top > bottom &&
                   (top >= Y && Y >= bottom)
                   ||
                   top < bottom &&
                   (Y >= bottom || top >= Y)
                  )
                return true;
            return false;
        }
        /// <summary>
        /// X value of top left point contained inside of Wave
        /// </summary>
        public double TopLeftPointX => map[UpperBound][0].X;
        /// <summary>
        /// Y value of top left point contained inside of Wave
        /// </summary>
        public double TopLeftPointY => map[UpperBound][0].Y;

        /// <summary>
        /// Upper left corner of the wave, x-coordinate
        /// </summary>
        public double waveUpperLeftX => map[UpperBound][0].X;
        /// <summary>
        /// Upper left corner of the wave, y-coordinate
        /// </summary>
        public double waveUpperLeftY => map[UpperBound][0].Y;
        /// <summary>
        /// Lower right corner of the wave, x-coordinate
        /// </summary>
        public double waveLowerRightX => map[LowerBound][mapWidth - 1].X;
        /// <summary>
        /// Lower right corner of the wave, y-coordinate
        /// </summary>
        public double waveLowerRightY => map[LowerBound][mapWidth - 1].Y;
        /// <summary>
        /// Upper left corner of admissible wave area, x-coordinate
        /// </summary>
        public double areaUpperLeftX => map[waveAreaMaxY][0].X;
        /// <summary>
        /// Upper left corner of admissible wave area, y-coordinate
        /// </summary>
        public double areaUpperLeftY => map[waveAreaMaxY][0].Y;
        /// <summary>
        /// Lower right corner of admissible wave area, x-coordinate
        /// </summary>
        public double areaLowerRightX => map[waveAreaMinY][mapWidth - 1].X;
        /// <summary>
        /// Lower right corner of admissible wave area, y-coordinate
        /// </summary>
        public double areaLowerRightY => map[waveAreaMinY][mapWidth - 1].Y;
        /// <summary>
        /// Wave's lower bound
        /// </summary>
        public int LowerBound;
        /// <summary>
        /// Width of a wave
        /// </summary>
        public int Width => mapWidth;
        #endregion

        #region IUpdateable implementation
        /// <summary>
        /// Gets next time this object expects an event
        /// </summary>
        /// <param name="currentTime">current time</param>
        /// <returns>double.MaxValue</returns>
        public double GetNextEventTime(double currentTime)
        {
            return double.MaxValue;
        }

        private double lastHeightChangeTime;
        private int initialHeight;
        private int MaxWaveHeight;
        private int stopCountingNewOrdersShift = 0;
        private bool decreaseNext = false;
        private int previousUpperBound;
        private List<MultiPointGatherTask> upperBoundTasks = new List<MultiPointGatherTask>();

        /// <summary>
        /// Updates this object
        /// </summary>
        /// <param name="lastTime">last time</param>
        /// <param name="currentTime">current time</param>
        public void Update(double lastTime, double currentTime)
        {
            ////////// Run at the beginning ////////////////// 
            // this is run only once in the beginning when all bots wait to pick the pallet
            // TODO: consider removing
            if (waitInitialization)
            {
                bool initializationDone = true;
                foreach (var bot in Instance.MovableStations)
                {
                    if (bot.IsQueueing == true || bot.GetInfoState().Equals("GetPallet"))
                    {
                        initializationDone = false;
                        break;
                    }
                }
                if (initializationDone) waitInitialization = false;
                else return;
            }
            ////////////////////////////////////////////////// 

            // all pending and remaining locations of the opened tasks
            var taskLocations = new List<int>();

            //////// Assistance locations ////////////////////    
            // Add all locations where robots have requested assistance, (pending locations)
            // IMPORTANT: these are not present anymore in task locations!
            var requestedAssistanceLocations = Instance.Controller.MateScheduler.AssistInfo.AssistanceLocations;
            foreach (var bot in requestedAssistanceLocations.Keys)
            {
                taskLocations.AddRange(requestedAssistanceLocations[bot].Select(v => realYtoVirtualY[v.Item1.Y]).Where(x => x <= UpperBound));
            }
            ////////////////////////////////////////////////// 

            //////// All remaining task locations //////////// 
            // Add all the remaining locations in the tasks where robots still haven't requested assistance
            // all robots
            int total_count = Instance.MovableStations.Count();
            // currently parked
            int parked_count = Instance.MovableStations
                .Where(b =>
                        b.LastRestLocation != null &&
                        b.LastRestLocation.ID == b.CurrentWaypoint.ID)
                .Count();
            int to_park_count = Instance.MovableStations
                .Where(b =>
                         b.CurrentWaypoint != null && Instance.ParkingLot.Select(p => p.ID).Contains(b.CurrentWaypoint.ID) // currently parked
                         ||
                         b.DestinationWaypoint != null && Instance.ParkingLot.Select(p => p.ID).Contains(b.DestinationWaypoint.ID)) // going to parking
                .Count();

            List<MultiPointGatherTask> currentlyOpenedTasks = new List<MultiPointGatherTask>();
            foreach (var bot in Instance.MovableStations)
            {
                // consider only MPG tasks
                if (!(bot.CurrentTask is MultiPointGatherTask)) continue;
                // typecast if it is MPG task
                var task = (MultiPointGatherTask)bot.CurrentTask;
                // add to tasks that are currently opened, but not necessarily considered for UpperBound
                currentlyOpenedTasks.Add(task);
                // if no tasks considered for UpperBound, add the current task
                if (upperBoundTasks.Count() == 0) upperBoundTasks.Add(task);
                if (!upperBoundTasks.Contains(task) && // new task
                         to_park_count / (double)total_count < 0.4 && // enough active
                         UpperBound > LowerBound // no overflow 
                    )
                    upperBoundTasks.Add(task);
                if (upperBoundTasks.Contains(task))
                {
                    // add UpperBound locations
                    foreach (var point in task.Locations)
                    {
                        var y = realYtoVirtualY[point.Y];
                        if (UpperBound >= y)
                            taskLocations.Add(y);
                    }
                }
            }
            ////////////////////////////////////////////////// 

            // remove tasks which are not currently opened, but previously were
            upperBoundTasks = upperBoundTasks.Intersect(currentlyOpenedTasks).ToList();

            ///// Update the UpperBound ////////////////////// 
            // record the previous upper bound
            previousUpperBound = UpperBound;
            // new UpperBound is the maximum of remaining and assisting locations
            UpperBound = taskLocations.Count == 0 ? waveAreaMaxY : (taskLocations.Max() + (taskLocations.Max() % 2 == 0 ? 0 : 1));
            if (previousUpperBound < UpperBound)
                stopCountingNewOrdersShift = 0;
            ////////////////////////////////////////////////// 

            ///// Update wave height ///////////////////////// 
            // update frequency
            if (currentTime - lastHeightChangeTime > 60)
            {
                // if the number of bots going to or already parked is grater than 20%
                // wave height is increased by 2 or to maximum value
                if (to_park_count / (double)total_count > 0.4) Height = Math.Min(MaxWaveHeight, Height + 2);
                // if enough robots are active, try to decrease the height next time
                // when the UpperBound changes
                else if (Height > initialHeight) decreaseNext = true;
                // update the time of last change
                lastHeightChangeTime = currentTime;
            }
            if (Height % 2 == 0) ++Height; // make height fit nicely after overflow
            ////////////////////////////////////////////////// 


            ////// Height change when UpperBound changes ///// 
            // if UpperBound changed and we are pending to decrease the wave height
            if (UpperBound != previousUpperBound && decreaseNext)
            {
                decreaseNext = false; // reset decrease
                int height_change = previousUpperBound - UpperBound; // how much did it shift
                if (previousUpperBound < UpperBound) // if the complete wave has overflown 
                    // modify the height change
                    height_change = waveAreaMaxY - UpperBound + previousUpperBound - waveAreaMinY;
                Height = Math.Max(Height - height_change, initialHeight); 
            }
            ////////////////////////////////////////////////// 

            // measure overflow, only if greater than 0
            int waveOverflow = Math.Max(Height - (UpperBound - waveAreaMinY), 0);

            ////// Update LowerBound ///////////////////////// 
            if (waveOverflow > 0) LowerBound = waveAreaMaxY - waveOverflow - 1; // -1 to make the wave fit nicely to the rows
            else LowerBound = UpperBound - Height;
            ////////////////////////////////////////////////// 
        }

        #endregion
    }
}