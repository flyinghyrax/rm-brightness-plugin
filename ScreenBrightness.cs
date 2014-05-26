/*
 * Note: as far as I can tell, this will only work on WinVista and higher
 * TODO:
 * - Multithread updates so that repeated wmi queries don't bog down Rainmeter
 * - multi-monitor testing and support
 * credits: http://edgylogic.com/projects/display-brightness-vista-gadget/
 */
#define DLLEXPORT_EXECUTEBANG

using System;
using System.Management;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using Rainmeter;

namespace ScreenBrightnessPlugin
{
    internal class Measure
    {
        private Screen m;
        private IntPtr skinHandle;
        private string doIfSupported;
        private string doIfNotSupported;
        private bool wasSupported;

        internal Measure()
        {
            m = new Screen();
            // assume this, so if it's not true we will fire a bang
            wasSupported = true;
        }

        internal void Reload(Rainmeter.API api, ref double maxValue)
        {
            skinHandle = api.GetSkin();
            doIfSupported = api.ReadString("IfSupportedAction", "");
            doIfNotSupported = api.ReadString("IfNotSupportedAction", "");

            m.refresh();
            maxValue = m.MaxLevel;
        }

        internal double Update()
        {
            m.refresh();
            bool supported = m.Supported;
            if (supported != wasSupported)
            {
                if (!supported && !String.IsNullOrEmpty(doIfNotSupported))
                {
                    API.Execute(skinHandle, doIfNotSupported);
                }
                else if (supported && !String.IsNullOrEmpty(doIfSupported))
                {
                    API.Execute(skinHandle, doIfSupported);
                }
                wasSupported = supported;
            }
            return (double)(m.CurrentBrightness);
        }
        
#if DLLEXPORT_GETSTRING
        internal string GetString()
        {
            return "";
        }
#endif
        
#if DLLEXPORT_EXECUTEBANG
        internal void ExecuteBang(string args)
        {
            string a = args.ToLowerInvariant();
            if (a.Equals("raise"))
            {
                m.raiseBrightness();
            }
            else if (a.Equals("lower"))
            {
                 m.lowerBrightness();
            }
            else if (a.Substring(0, 3).Equals("set"))
            {
                try
                {
                    string sub = a.Substring(4);
                    byte target = byte.Parse(sub);
                    m.setBrightness(target);
                }
                catch (Exception ex)
                {
                    API.Log(API.LogType.Error, "ScreenBrightness.dll: Unable to parse brightness in \"" + a + "\"");
                    API.Log(API.LogType.Debug, ex.Message);
                }
            }
            else
            {
                API.Log(API.LogType.Error, "ScreenBrightness.dll: Invalid command \"" + args + "\"");
            }
        }
#endif
    }

    internal class Screen
    {
        private ManagementObject brightnessMethods;
        private byte[] brightnessLevels;
        private int brightnessIndex;
        private byte currentBrightness;
        private byte maxBrightness;
        private bool supported;

        private object stateLock;
        private bool queued;

        /// <summary>
        /// Indicates whether or not WMI MonitorBrightness operations are currently supported
        /// </summary>
        internal bool Supported
        {
            get
            {
                bool s;
                lock (stateLock)
                {
                    s = this.supported;
                }
                return s;
            }
        }

        /// <summary>
        /// Retrieves the maximum brightness level if supported
        /// </summary>
        internal double MaxLevel
        {
            get
            {
                double m;
                lock (stateLock)
                {
                    m = (double)(this.maxBrightness);
                }
                return m;
            }
        }

        /// <summary>
        /// Retrieves the current brightness level if supported
        /// </summary>
        internal byte CurrentBrightness
        {
            get
            {
                byte c;
                lock (stateLock)
                {
                    c = this.currentBrightness;
                }
                return c;
            }
        }

        public Screen()
        {
            this.supported = false;
            this.brightnessLevels = new byte[] { 0 };
            this.brightnessIndex = 0;
            this.currentBrightness = 0;
            this.maxBrightness = 0;

            this.queued = false;
        }

        internal void refresh()
        {
            if (!queued)
            {
                queued = ThreadPool.QueueUserWorkItem(new WaitCallback(concurrentRefresh));
            }
        }        

        private void concurrentRefresh(object state)
        {
            ManagementObject bm, bi;
            bool support;
            byte[] levels;
            byte maxLevel, currentLevel;
            int currentIndex;
            // get wmi objects
            bm = getWmiObject("WmiMonitorBrightnessMethods");
            bi = getWmiObject("WmiMonitorBrightness");
            // check support
            support = bm != null && bi != null;
            if (support)
            {
                // get possible levels
                levels = getBrightnessLevels(bi);
                Array.Sort(levels);
                // get max level
                maxLevel = (levels[levels.Length - 1]);
                // get current level
                currentLevel = getCurrentBrightness(bi);
                // get current index
                currentIndex = findLevelIndex(currentLevel, levels);
            }
            else
            {
                // defaults
                levels = new byte[] { 0 };
                currentIndex = maxLevel = currentLevel = 0;
            }
            // lock and set
            lock (stateLock)
            {
                this.brightnessMethods = bm;
                this.brightnessLevels = levels;
                this.brightnessIndex = currentIndex;
                this.currentBrightness = currentLevel;
                this.maxBrightness = maxLevel;
                this.supported = support;
            }
            queued = false;
        }

        /// <summary>
        /// Set the current brightness to one level above the current level
        /// </summary>
        internal void raiseBrightness()
        {
            byte[] levels;
            int currentIndex;
            lock (stateLock)
            {
                levels = new byte[this.brightnessLevels.Length];
                this.brightnessLevels.CopyTo(levels, 0);
                currentIndex = this.brightnessIndex;
            }

            int maxIndex = levels.Length - 1;
            byte target = levels[maxIndex];
            if (currentIndex < maxIndex)
            {
                currentIndex += 1;
                target = levels[currentIndex];
                setBrightness(target);
                lock (stateLock)
                {
                    this.brightnessIndex = currentIndex;
                }
            }
        }

        /// <summary>
        /// Set the current brightness to one level below the current level
        /// </summary>
        internal void lowerBrightness()
        {
            byte[] levels;
            int currentIndex;
            lock (stateLock)
            {
                levels = new byte[this.brightnessLevels.Length];
                this.brightnessLevels.CopyTo(levels, 0);
                currentIndex = this.brightnessIndex;
            }

            byte target = levels[0];
            if (currentIndex > 0)
            {
                currentIndex -= 1;
                target = levels[currentIndex];
                setBrightness(target);
                lock (stateLock)
                {
                    this.brightnessIndex = currentIndex;
                }
            }
        }

        /// <summary>
        /// Sets the current brightness to the supported level closest to an arbitrary specified level
        /// </summary>
        /// <param name="level">Target level, may not be a supported brightness level</param>
        internal void setBrightness(byte level)
        {
            ManagementObject bm;
            byte[] levels;
            lock (stateLock)
            {
                levels = new byte[this.brightnessLevels.Length];
                this.brightnessLevels.CopyTo(levels, 0);
                bm = this.brightnessMethods;
            }
            if (bm != null) 
            {
                int i = findLevelIndex(level, levels);
                bm.InvokeMethod("WmiSetBrightness", new Object[] { 10, levels[i] });
            }
        }

        /// <summary>
        /// Finds the index of supported level closest to the specified level.
        /// Assumes that array of levels is sorted in ascending order
        /// </summary>
        /// <param name="level">Brightness level to look for</param>
        /// <param name="levels">Array of levels to look in</param>
        /// <returns>integer index of closest supported level to the input level (default 0)</returns>
        private int findLevelIndex(byte targetLevel, byte[] levels)
        {
            int closestIndex = 0;
            
            if (this.Supported)
            {
                int maxIndex = levels.Length - 1;
                byte minVal = levels[0];
                byte maxVal = levels[maxIndex];

                if (targetLevel <= minVal)
                {
                    // target is below or equal to our lowest level
                    closestIndex = 0;
                }
                else if (targetLevel >= maxVal)
                {
                    // target is above or equal to our highest value
                    closestIndex = maxIndex;
                }
                else
                {
                    // target is somewhere in between
                    int currentIndex = 0;
                    while (currentIndex < levels.Length && levels[currentIndex] < targetLevel)
                    {
                        currentIndex += 1;
                    }
                    // currentIndex now point to the closest level greater than or equal to the target
                    byte valBelow = levels[currentIndex - 1];
                    byte valAbove = levels[currentIndex];
                    if (valAbove == targetLevel)
                    {
                        // if target is exactly equal to one of the intermediate levels
                        closestIndex = currentIndex;
                    }
                    else
                    {
                        // target is between two levels; do a comparison
                        int diffBelow = targetLevel - valBelow;
                        int diffAbove = valAbove - targetLevel;
                        // if equal diff, prefer lower
                        closestIndex = (diffBelow <= diffAbove ? currentIndex - 1 : currentIndex);
                    }
                } 
            }

            return closestIndex;
        }

        private byte getCurrentBrightness()
        {
            ManagementObject monitorInfo = getWmiObject("WmiMonitorBrightness");
            return getCurrentBrightness(monitorInfo);
        }

        private byte getCurrentBrightness(ManagementObject monitorInfo)
        {
            return (monitorInfo == null ? 
                (byte)(0) : 
                (byte)(monitorInfo.GetPropertyValue("CurrentBrightness")));
        }

        private byte[] getBrightnessLevels()
        {
            ManagementObject monitorInfo = getWmiObject("WmiMonitorBrightness");
            return getBrightnessLevels(monitorInfo);
        }

        private byte[] getBrightnessLevels(ManagementObject monitorInfo)
        {
            return (monitorInfo == null ? 
                new byte[] { 0 } : 
                (byte[])(monitorInfo.GetPropertyValue("Level")));
        }

        /// <summary>
        /// Helper method for retrieving WMI instances
        /// </summary>
        /// <param name="which">Name of class to get instances of</param>
        /// <returns>First instance of specified class or null if unsupported/not found</returns>
        internal static ManagementObject getWmiObject(string which)
        {
            ManagementObject mobj = null;
            ManagementObjectCollection moc = null;
            try
            {
                ManagementClass mc = new ManagementClass("root\\WMI", which, null);
                moc = mc.GetInstances();
                foreach (ManagementObject m in moc)
                {
                    mobj = m;
                    break;
                }
            }
            catch (ManagementException)
            {
                mobj = null;
            }
            finally
            {
                if (moc != null)
                {
                    moc.Dispose();
                }
            }
            return mobj;
        }
    }

    public static class Plugin
    {
#if DLLEXPORT_GETSTRING
        static IntPtr StringBuffer = IntPtr.Zero;
#endif

        [DllExport]
        public static void Initialize(ref IntPtr data, IntPtr rm)
        {
            data = GCHandle.ToIntPtr(GCHandle.Alloc(new Measure()));
        }

        [DllExport]
        public static void Finalize(IntPtr data)
        {
            GCHandle.FromIntPtr(data).Free();
            
#if DLLEXPORT_GETSTRING
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }
#endif
        }

        [DllExport]
        public static void Reload(IntPtr data, IntPtr rm, ref double maxValue)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.Reload(new Rainmeter.API(rm), ref maxValue);
        }

        [DllExport]
        public static double Update(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            return measure.Update();
        }
        
#if DLLEXPORT_GETSTRING
        [DllExport]
        public static IntPtr GetString(IntPtr data)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            if (StringBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(StringBuffer);
                StringBuffer = IntPtr.Zero;
            }

            string stringValue = measure.GetString();
            if (stringValue != null)
            {
                StringBuffer = Marshal.StringToHGlobalUni(stringValue);
            }

            return StringBuffer;
        }
#endif

#if DLLEXPORT_EXECUTEBANG
        [DllExport]
        public static void ExecuteBang(IntPtr data, IntPtr args)
        {
            Measure measure = (Measure)GCHandle.FromIntPtr(data).Target;
            measure.ExecuteBang(Marshal.PtrToStringUni(args));
        }
#endif
    }
}
