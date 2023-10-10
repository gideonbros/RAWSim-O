using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Core.Configurations
{
    /// <summary>
    /// Configuration for PalletStand manager with euclidean distance greedy algorithm.
    /// </summary>
    public class EuclideanGreedyPalletStandManagerConfiguration : PalletStandManagerConfiguration
    {
        /// <summary>
        /// Returns the type of the corresponding method this configuration belongs to.
        /// </summary>
        /// <returns>The type of the method.</returns>
        public override PalletStandManagerType GetMethodType() { return PalletStandManagerType.EuclideanGreedy; }

        /// <summary>
        /// Returns a name identifying the method.
        /// </summary>
        /// <returns>The name of the method.</returns>
        public override string GetMethodName() { if (!string.IsNullOrWhiteSpace(Name)) return Name; return "psmEG"; }

    }

    /// <summary>
    /// Configuration for PalletStand manager with original algorithm.
    /// </summary>
    public class OriginalPalletStandManagerConfiguration : PalletStandManagerConfiguration
    {
        /// <summary>
        /// Returns the type of the corresponding method this configuration belongs to.
        /// </summary>
        /// <returns>The type of the method.</returns>
        public override PalletStandManagerType GetMethodType() { return PalletStandManagerType.Original; }

        /// <summary>
        /// Returns a name identifying the method.
        /// </summary>
        /// <returns>The name of the method.</returns>
        public override string GetMethodName() { if (!string.IsNullOrWhiteSpace(Name)) return Name; return "psmO"; }
        
    }

    /// <summary>
    /// Configuration for PalletStand manager with advanced algorithm which uses A* for computing arrival times.
    /// </summary>
    public class AdvancedPalletStandManagerConfiguration : PalletStandManagerConfiguration
    {
        /// <summary>
        /// Returns the type of the corresponding method this configuration belongs to.
        /// </summary>
        /// <returns>The type of the method.</returns>
        public override PalletStandManagerType GetMethodType() { return PalletStandManagerType.Advanced; }

        /// <summary>
        /// Returns a name identifying the method.
        /// </summary>
        /// <returns>The name of the method.</returns>
        public override string GetMethodName() { if (!string.IsNullOrWhiteSpace(Name)) return Name; return "psmA"; }
    }
}
