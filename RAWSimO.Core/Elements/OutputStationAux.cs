using RAWSimO.Core.Control;
using RAWSimO.Core.Geometrics;
using RAWSimO.Core.Info;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.Items;
using RAWSimO.Core.Management;
using RAWSimO.Core.Statistics;
using RAWSimO.Core.Waypoints;
using RAWSimO.Toolbox;
using System;
using System.Collections.Generic;
using System.Linq;

namespace RAWSimO.Core.Elements
{
    /// <summary>
    /// The auxiliary class that redefines OutputStation class
    /// </summary>
    public class OutputStationAux : OutputStation
    {
        ///<summary>
        ///reference to the whole MovableStation class of which this class is part
        ///</summary>
        internal MovableStation movableStationPart;
        ///<summary>
        ///constructs in the same way as OutputStation class
        ///</summary>
        internal OutputStationAux(Instance instance) : base(instance)
        {
        }
        ///<summary>
        ///implicit cast to MovableStation class 
        ///</summary>
        public static implicit operator MovableStation(OutputStationAux os)
        {
            return os.movableStationPart;
        }
        /// <summary>
        /// removes completed order from the parent movable station
        /// </summary>
        /// <param name="currentTime"></param>
        /// <returns></returns>
        protected override Order RemoveAnyCompletedOrder(double currentTime)
        {
            return movableStationPart.RemoveAnyCompletedOrder(currentTime);
        }
    }

}