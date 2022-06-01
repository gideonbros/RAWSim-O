using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Core.Info
{
    /// <summary>
    /// The interface for supplying information about a waypoint object.
    /// </summary>
    public interface IWaypointInfo : IImmovableObjectInfo
    {
        /// <summary>
        /// Returns the row and column of the bo
        /// </summary>
        /// <returns></returns>
        string GetInfoRowColumn();
        /// <summary>
        /// Returns the row of the object.
        /// </summary>
        /// <returns>Returns the row of the object.</returns>
        int GetInfoRow();
        /// <summary>
        /// Returns the column of the object.
        /// </summary>
        /// <returns>Returns the column of the object.</returns>
        int GetInfoColumn();
        /// <summary>
        /// Indicates whether the waypoint is a storage location.
        /// </summary>
        /// <returns><code>true</code> if it is a storage location, <code>false</code> otherwise.</returns>
        bool GetInfoStorageLocation();

        // functions used to view unavailable storage locations
        public bool GetInfoUnavailableStorage();
        public double GetInfoHorizontalLength();
        public double GetInfoVerticalLength();
        /// <summary>
        /// Gets all outgoing connections of the waypoint.
        /// </summary>
        /// <returns>An enumeration of waypoints this waypoint has a directed edge to.</returns>
        IEnumerable<IWaypointInfo> GetInfoConnectedWaypoints();
    }
}
