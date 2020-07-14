using System;
using System.Diagnostics;

namespace XDS.SDK.Cryptography.E2E
{
    public class RatchetTimer
    {
        long previousSecondTicks;

        /// <summary>
        /// Generates the Dynamic Key Ids. A Hybrid
        /// of a Clock and a Counter.
        /// </summary>
        /// <remarks>Not thread safe.</remarks>
        public long GetNextTicks(long persistedMaxKeyId)
        {
            if (persistedMaxKeyId >= this.previousSecondTicks)
                this.previousSecondTicks = persistedMaxKeyId;

            // The ids should be similar to timestamps, but we don't want to
            // be too exact here to prevent misuse in timing attacks. A resolution of
            // seconds should be fuzzy enough. 
            const long resolution = TimeSpan.TicksPerSecond;
            var date = DateTime.UtcNow;
            var currentTicks = date.Ticks - date.Ticks % resolution;

            // If the clock ticks forward, this should be the normal case.
            if (currentTicks > this.previousSecondTicks)
            {
                this.previousSecondTicks = currentTicks;
                return currentTicks;
            }
            // If our clock has gone backwards, or if persistedMaxKeyId was 'in 
            // the future' for unknown reasons, we just increment till time catches
            // up, if that ever happens.
            // We do not simply increment persistedMaxKeyId, because if persistence
            // is broken, we'll generate the same number over and over again.
            this.previousSecondTicks++;

            Debug.Assert(this.previousSecondTicks > persistedMaxKeyId);
            return this.previousSecondTicks;
        }
    }
}
