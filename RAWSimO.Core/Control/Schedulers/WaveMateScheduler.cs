using RAWSimO.Core.Control.Filters;
using System.Linq;
using System.Collections.Generic;

namespace RAWSimO.Core.Control
{
    /// <summary>
    /// Singleton class which represents mate scheduler which has an additional Wave filter
    /// </summary>
    public class WaveMateScheduler : MateScheduler
    {
        /// <summary>
        /// Constructs new Wave based MateScheduler
        /// </summary>
        /// <param name="instance">Instance this scheduler belongs to</param>
        /// <param name="loggerPath">path to .txt file where decisions of this scheduler will be logged</param>
        public WaveMateScheduler(Instance instance, string loggerPath) : base(instance, loggerPath)
        {
            Wave = new WideWave(instance, instance.SettingConfig.WaveHeight, instance.SettingConfig.MaxWaveHeight, instance.SettingConfig.WaveEnabled);
            AssistInfo = new WaveAssistLocations(instance, this, Wave);
        }
        /// <summary>
        /// Updates this object
        /// </summary>
        /// <param name="lastTime">last time</param>
        /// <param name="currentTime">current time</param>
        public override void Update(double lastTime, double currentTime)
        {
            Wave.Update(lastTime, currentTime);
            base.Update(lastTime, currentTime);
        }
        /// <summary>
        /// Wave filter used by this MateScheduler
        /// </summary>
        public WideWave Wave { get; set; }
    }
}