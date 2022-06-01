using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace RAWSimO.Core.Elements
{
    /// <summary>
    /// Class which represents output pallet stands
    /// </summary>
    public class OutputPalletStand : OutputStation
    {
        /// <summary>
        /// Constructs one input pallet stand
        /// </summary>
        /// <param name="instance"><see cref="Instance"/> to which this object belongs to</param>
        internal OutputPalletStand(Instance instance) : base(instance)
        {
            IncomingBots = 0;
        }
        /// <summary>
        /// The number of Bots about to come to this station
        /// </summary>
        public int IncomingBots { get; set; }
        /// <summary>
        /// Gets the string which identifies this object
        /// </summary>
        /// <returns>String which identifies this object</returns>
        public override string GetIdentfierString() { return "OutputPalletStand" + ID.ToString(); }
        /// <summary>
        /// Gets string representing this object
        /// </summary>
        /// <returns>String representing this object</returns>
        public override string ToString() { return "OutputPalletStand" + ID.ToString(); }
        /// <summary>
        /// Get the full name of the object
        /// </summary>
        /// <returns>String representing full name of the object</returns>
        public override string GetInfoFullName() { return ToString(); }
    }
}
