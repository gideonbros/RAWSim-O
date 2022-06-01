using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RAWSimO.Core.Control.Filters;

namespace RAWSimO.Core.Info
{
    /// <summary>
    /// The interface for getting information about an instance object.
    /// </summary>
    public interface IInstanceInfo
    {
        /// <summary>
        /// Returns an enumeration of all pods of this instance.
        /// </summary>
        /// <returns>All pods of this instance.</returns>
        IEnumerable<IPodInfo> GetInfoPods();
        /// <summary>
        /// Returns an enumeration of all bots of this instance.
        /// </summary>
        /// <returns>All bots of this instance.</returns>
        IEnumerable<IBotInfo> GetInfoBots();

        IEnumerable<IBotInfo> GetInfoMovableStations();

        void AddInfoLocationAddress(int location, string address);
        string GetInfoAddressesForLocation(int locationID);

        int GetStatusTableOrderID(int botID);
        List<string> GetStatusTableOrderAddresses(int botID);

        /// <summary>
        /// Returns the status of the item inside the current order for the given robot.
        /// </summary>
        /// <param name="botID"></param>
        /// <param name="itemIndex"></param>
        /// <returns>Tuple consisting of:<br/> 
        ///     Item1 = (bool) item opened<br/>
        ///     Item2 = (int) assigned picker ID if there is one, -1 otherwise<br/>
        ///     Item3 = (bool) item completed<br/>
        ///     Item4 = (bool) item locked (robot is sure to come to this item)<br/>
        /// </returns>///
        Tuple<bool, int, bool, bool> GetStatusTableInfoOnItem(int botID, int itemIndex);

        /// <summary>
        /// Get the hue of the robot.
        /// </summary>
        /// <param name="botID">Robot ID</param>
        /// <returns>a double from 0 to 200 representing hue</returns>
        double GetInfoBotHue(int botID);

        /// <summary>
        /// Get the picker assigned to this address
        /// </summary>
        /// <param name="address"></param>
        /// <returns>
        /// Address can be either locked or free.<br/>
        ///     0  -> locked, no picker<br/>
        ///     -1 -> free, no picker<br/>
        ///     >0 -> assigned picker<br/>
        ///     IF locked, return the mate ID that is assigned to the robot
        ///     that locks the address OR 0 if no mate is assigned<br/>
        ///     ELSE, return the mate ID of the last assigned mate to this
        ///     free address
        /// </returns>
        int GetColorKeyForPodAddress(string address);

        /// <summary>
        /// Get robot ID that currently locks the address
        /// </summary>
        /// <param name="address"></param>
        /// <returns>-1 if address is not locked, otherwise robot ID (0,1,2,...)</returns>
        int GetPodLockedForAddress(string address);

        IEnumerable<IBotInfo> GetInfoMates();
        // TODO: pending for removal...
        List<IBotInfo> GetInfoMatesNeedingAssignment();

        /// <summary>
        /// Returns an enumeration of all tiers of this instance.
        /// </summary>
        /// <returns>All tiers of this instance.</returns>
        IEnumerable<ITierInfo> GetInfoTiers();
        /// <summary>
        /// Returns the elevators connected to this tier.
        /// </summary>
        /// <returns>All elevators connected to this tier.</returns>
        IEnumerable<IElevatorInfo> GetInfoElevators();
        /// <summary>
        /// Indicates whether anything has changed in the instance.
        /// </summary>
        /// <returns><code>false</code> if nothing changed since the last query, <code>true</code> otherwise.</returns>
        bool GetInfoChanged();
        /// <summary>
        /// Returns all item descriptions used in the instance.
        /// </summary>
        /// <returns>All item descriptions used by the instance.</returns>
        IEnumerable<IItemDescriptionInfo> GetInfoItemDescriptions();
        /// <summary>
        /// Returns the item manager of this instance.
        /// </summary>
        /// <returns>The item manager of this instance.</returns>
        IItemManagerInfo GetInfoItemManager();
        /// <summary>
        /// Returns the count of items handled by the system.
        /// </summary>
        /// <returns>The number of items handled.</returns>
        int GetInfoStatItemsHandled();
        /// <summary>
        /// Returns the count of bundles handled by the system.
        /// </summary>
        /// <returns>The number of bundles handled.</returns>
        int GetInfoStatBundlesHandled();
        /// <summary>
        /// Returns the count of orders handled by the system.
        /// </summary>
        /// <returns>The number of orders handled.</returns>
        int GetInfoStatOrdersHandled();
        /// <summary>
        /// Returns the count of orders handled that were not completed in time.
        /// </summary>
        /// <returns>The number of orders not completed in time.</returns>
        int GetInfoStatOrdersLate();
        /// <summary>
        /// Returns the count of repositioning moves started so far.
        /// </summary>
        /// <returns>The number of repositioning moves started.</returns>
        int GetInfoStatRepositioningMoves();
        /// <summary>
        /// Returns the count of occurred collisions.
        /// </summary>
        /// <returns>The number of occurred collisions.</returns>
        int GetInfoStatCollisions();
        /// <summary>
        /// Returns the storage fill level.
        /// </summary>
        /// <returns>The storage fill level.</returns>
        double GetInfoStatStorageFillLevel();
        /// <summary>
        /// Returns the storage fill level including the already present reservations.
        /// </summary>
        /// <returns>The storage fill level.</returns>
        double GetInfoStatStorageFillAndReservedLevel();
        /// <summary>
        /// Returns the storage fill level including the already present reservations and the capacity consumed by backlog bundles.
        /// </summary>
        /// <returns>The storage fill level.</returns>
        double GetInfoStatStorageFillAndReservedAndBacklogLevel();
        /// <summary>
        /// Returns the current name of the instance.
        /// </summary>
        /// <returns>The name of the instance.</returns>
        string GetInfoName();
        /// <summary>
        /// Return the current wave used
        /// </summary>
        /// <returns></returns>
        WideWave GetInfoWave();

        /// <summary>
        /// Returns width of cells
        /// </summary>
        /// <returns></returns>
        double GetInfoCellWidth();
        /// <summary>
        /// Returns height of cells
        /// </summary>
        /// <returns></returns>
        double GetInfoCellHeight();



        int GetRowFromY(double y);
        int GetColFromX(double x);
    }
}
