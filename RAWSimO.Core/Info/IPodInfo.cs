using RAWSimO.Core.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Core.Info
{
    /// <summary>
    /// The interface for supplying information about a pod object.
    /// </summary>
    public interface IPodInfo : IMovableObjectInfo
    {
        /// <summary>
        /// Returns the horizontal length of the object.
        /// </summary>
        /// <returns>The radius.</returns>
        double GetInfoHorizontalLength();
        /// <summary>
        /// Returns the vertical length of the object.
        /// </summary>
        /// <returns>The radius.</returns>
        double GetInfoVerticalLength();
        /// <summary>
        /// Returns the address of the object.
        /// </summary>
        /// <returns>Returns the address of the object.</returns>
        string GetPodAddress();

        /// <summary>
        /// Returns the waypoint ID or -1 of the waypoint where pod is currently at.
        /// </summary>
        /// <returns>-1 or ID</returns>
        int GetPodWaypointID();
        /// <summary>
        /// Returns the zone of the object.
        /// </summary>
        /// <returns></returns>
        string GetPodZone();
        int GetRowFromY(double y);
        int GetColFromX(double x);
        // return integer depending on assigned mate or if the item is opened
        double GetStatusColorKey();
        double GetPodLocked();
        /// <summary>
        /// Gets the current heat associated with this pod as a value between 0 (low heat) and 100 (high heat).
        /// </summary>
        /// <returns>The heat of this pod.</returns>
        double GetInfoHeatValue();
        /// <summary>
        /// Gets the capacity this pod offers.
        /// </summary>
        /// <returns>The capacity of the pod.</returns>
        double GetInfoCapacity();
        /// <summary>
        /// Gets the absolute capacity currently in use.
        /// </summary>
        /// <returns>The capacity in use.</returns>
        double GetInfoCapacityUsed();
        /// <summary>
        /// Gets the absolute capacity currently reserved.
        /// </summary>
        /// <returns>The capacity reserved.</returns>
        double GetInfoCapacityReserved();
        /// <summary>
        /// Gets the absolute stock capacity. Capacity of the all pallets above the main one.
        /// </summary>
        /// <returns>The stock capacity.</returns>
        double GetInfoStockCapacity();
        /// <summary>
        /// Gets information about number of items of the given kind in this pod.
        /// </summary>
        /// <returns>The number of units contained in the pod of the specified item.</returns>
        int GetInfoContent(IItemDescriptionInfo item);
        /// <summary>
        /// Indicates whether the content of the pod changed.
        /// </summary>
        /// <returns>Indicates whether the content of the pod changed.</returns>
        bool GetInfoContentChanged();
        /// <summary>
        /// Indicates whether the pod is ready for refill.
        /// </summary>
        /// <returns>Indicates whether the pod is ready for refill.</returns>
        bool GetInfoReadyForRefill();
        /// <summary>
        /// The type of this pod as distinguished by an integer number that starts at 0. This can be set by any controller to give feedback about the mechanism.
        /// </summary>
        double InfoTagPodStorageType { get; }
        /// <summary>
        /// Some additional information about the pod.
        /// </summary>
        string InfoTagPodStorageInfo { get; }
    }
}
