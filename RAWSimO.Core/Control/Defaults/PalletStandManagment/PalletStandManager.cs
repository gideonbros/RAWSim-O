using RAWSimO.Core.Bots;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Waypoints;

namespace RAWSimO.Core.Control.Defaults.PalletStandManagment
{
    /// <summary>
    /// Base class for pallet stand manager.
    /// </summary>
    public abstract class PalletStandManager
    {
        /// <summary>
        /// Finds the closest input pallet stand for the given <paramref name="bot"/>
        /// </summary>
        /// <param name="bot">Bot which needs the input pallet stand</param>
        /// <param name="firstItemWP" Position of first item </param>
        /// <returns>Input pallet stand waypoint</returns>
        public abstract Waypoint GetClosestInputPalletStandWaypoint(BotNormal bot, Waypoint firstItemWP);

        /// <summary>
        /// Finds the input pallet stand for the given <paramref name="bot"/>
        /// </summary>
        /// <param name="bot">Bot which needs the input pallet stand</param>
        /// <param name="task">Task which has waypoints</param>
        /// <returns>Input pallet stand waypoint</returns>
        public abstract Waypoint GetInputPalletStandWaypoint(BotNormal bot, MultiPointGatherTask task);

        /// <summary>
        /// Finds the output pallet stand for the given <paramref name="bot"/>
        /// </summary>
        /// <param name="bot">Bot which needs the output pallet stand</param>
        /// <returns>Output pallet stand waypoint</returns>
        public abstract Waypoint GetOutputPalletStandWaypoint(BotNormal bot);
    }
}
