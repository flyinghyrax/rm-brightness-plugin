/*
 * Note: as far as I can tell, this will only work on WinVista and higher
 * TODO:
 * - improve findLevelIndex
 * - IfNotSupportedAction measure option
 * - Multithread updates so that repeated wmi queries don't bog down Rainmeter
 * - multi-monitor testing and support
 * credits: http://edgylogic.com/projects/display-brightness-vista-gadget/
 */
#define DLLEXPORT_EXECUTEBANG

using System;
using System.Management;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Rainmeter;

namespace ScreenBrightnessPlugin
{
    internal class Measure
    {
        private Monitor m;

        internal Measure()
        {
            m = new Monitor();
        }

        internal void Reload(Rainmeter.API api, ref double maxValue)
        {
            m.reload();
            maxValue = m.MaxLevel;
            if (!m.Supported)
            {
                API.Log(API.LogType.Warning, "Screenbrightness: not supported by this system configuration");
            }
        }

        internal double Update()
        {
            m.syncBrightnessIndex();
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
            if (a.Equals("increase"))
            {
                m.raiseBrightness();
            }
            else if (a.Equals("decrease"))
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
#if DEBUG
                    API.Log(API.LogType.Debug, ex.Message);
#endif
                }
            }
            else
            {
                API.Log(API.LogType.Error, "ScreenBrightness.dll: Invalid command \"" + args + "\"");
            }
        }
#endif

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

    internal class Monitor
    {
        private ManagementObject brightnessMethods;
        private byte[] brightnessLevels;
        private int brightnessIndex = 0;

        private bool _supported;
        internal bool Supported
        {
            get
            {
                return this._supported;
            }
        }

        private byte _maxLevel;
        internal double MaxLevel
        {
            get
            {
                return (double)(_maxLevel);
            }
        }

        internal byte CurrentBrightness
        {
            get
            {
                return this.getCurrentBrightness();
            }
        }

        public Monitor()
        {
            // initialization
            this.brightnessIndex = 0;
            this._maxLevel = 0;
            this._supported = false;
            // get actual values
            this.reload();
        }

        internal void reload()
        {
            brightnessMethods = Measure.getWmiObject("WmiMonitorBrightnessMethods");
            this._supported = this.brightnessMethods != null;
            if (this.Supported)
            {
                brightnessLevels = getBrightnessLevels();
                Array.Sort(brightnessLevels);
                this._maxLevel = brightnessLevels[brightnessLevels.Length - 1];
                syncBrightnessIndex();
            }
        }

        /// <summary>
        /// Gets the current brightness level from WIM
        /// </summary>
        /// <returns>Current brightness as type 'byte'</returns>
        private byte getCurrentBrightness()
        {
            if (this.Supported)
            {
                ManagementObject monitorInfo = Measure.getWmiObject("WmiMonitorBrightness");
                byte level = (byte)(monitorInfo.GetPropertyValue("CurrentBrightness"));
                return level;
            }
            else
            {
                return 0;
            }
        }

        /// <summary>
        /// Set the current brightness to one level above the current level
        /// </summary>
        internal void raiseBrightness()
        {
            if (this.Supported)
            {
                int maxIndex = this.brightnessLevels.Length - 1;
                // assume we are already at max brightness
                byte target = brightnessLevels[maxIndex];
                // if the above is not true, update target and index
                if (brightnessIndex < maxIndex)
                {
                    brightnessIndex += 1;
                    target = brightnessLevels[brightnessIndex];
                    
                }
                setBrightness(target);
            }
        }

        /// <summary>
        /// Set the current brightness to one level below the current level
        /// </summary>
        internal void lowerBrightness()
        {
            if (this.Supported)
            {
                byte target = brightnessLevels[0];
                if (brightnessIndex > 0)
                {
                    brightnessIndex -= 1;
                    target = brightnessLevels[brightnessIndex];
                }
                setBrightness(target);
            }
        }

        /// <summary>
        /// Sets the current brightness to the supported level closest to an arbitrary specified level
        /// </summary>
        /// <param name="level">Target level, may not be a supported brightness level</param>
        internal void setBrightness(byte level)
        {
            if (this.Supported)
            {
                int i = findLevelIndex(level, brightnessLevels);
                brightnessMethods.InvokeMethod("WmiSetBrightness", new Object[] { 10, brightnessLevels[i] });
            }
        }

        /// <summary>
        /// Synchronize the index in the supported levels array to the current actual brightness
        /// </summary>
        internal void syncBrightnessIndex()
        {
            byte currentBrightness = getCurrentBrightness();
            brightnessIndex = findLevelIndex(currentBrightness, brightnessLevels);
        }

        /// <summary>
        /// Finds the index of supported level closest to but still greater than specified level.
        /// TODO: Actual closest supported level, not closest-but-greater
        /// </summary>
        /// <param name="level">Brightness level to look for</param>
        /// <param name="levels">Array of levels to look in</param>
        /// <returns>integer index of closest supported level to the input level (default 0)</returns>
        private int findLevelIndex(byte level, byte[] levels)
        {
            int li = 0;
            for (int i = 0; i < levels.Length; i += 1)
            {
                if (levels[i] > level)
                {
                    break;
                }
                else
                {
                    li += 1;
                }
            }
            return li;
        }

        /// <summary>
        /// Retreives an array of supported brightness levels from WMI
        /// </summary>
        /// <returns>byte[] of supported brightness levels</returns>
        private byte[] getBrightnessLevels()
        {
            if (this.Supported)
            {
                ManagementObject monitorInfo = Measure.getWmiObject("WmiMonitorBrightness");
                return (byte[])(monitorInfo.GetPropertyValue("Level"));
            }
            else
            {
                return new byte[0];
            }
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
