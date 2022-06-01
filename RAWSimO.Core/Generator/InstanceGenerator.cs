using RAWSimO.Core.Configurations;
using RAWSimO.Core.Bots;
using RAWSimO.Core.Elements;
using RAWSimO.Core.Interfaces;
using RAWSimO.Core.Items;
using RAWSimO.Core.Management;
using RAWSimO.Core.Randomization;
using RAWSimO.Core.Control;
using RAWSimO.Core.Waypoints;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using RAWSimO.Core.IO;

namespace RAWSimO.Core.Generator
{

    /// <summary>
    /// Supplies methods to generate new instances according to given parameters.
    /// </summary>
    public class InstanceGenerator
    {
        /// <summary>
        /// Generates an instance with the given layout and configuration attached.
        /// </summary>
        /// <param name="layoutConfiguration">The layout configuration defining all the instance characteristics.</param>
        /// <param name="rand">A randomizer that is used during generation.</param>
        /// <param name="settingConfig">The configuration for the setting to emulate that will be attached for executing the simulation afterwards.</param>
        /// <param name="controlConfig">The configuration for the controlling mechanisms that will be attached for executing the simulation afterwards.</param>
        /// <param name="logAction">An optional action for logging.</param>
        /// <returns>The generated instance.</returns>
        public static Instance GenerateLayout(
            LayoutConfiguration layoutConfiguration,
            IRandomizer rand,
            SettingConfiguration settingConfig,
            ControlConfiguration controlConfig,
            Action<string> logAction = null)
        {
            LayoutGenerator layoutGenerator = new LayoutGenerator(layoutConfiguration, rand, settingConfig, controlConfig, logAction);
            Instance instance = layoutGenerator.GenerateLayout();
            InitializeInstance(instance);
            return instance;
        }

        /// <summary>
        /// Initializes a given instance.
        /// </summary>
        /// <param name="instance">The instance to initialize.</param>
        public static void InitializeInstance(Instance instance)
        {
            // Add managers
            instance.Randomizer = new RandomizerSimple(instance.SettingConfig.Seed);
            instance.Controller = new Controller(instance);
            instance.ResourceManager = new ResourceManager(instance);
            instance.ItemManager = new ItemManager(instance);
            // Notify instance about completed initializiation (time to initialize all stuff that relies on all managers being in place)
            instance.LateInitialize();
        }
    }
}
