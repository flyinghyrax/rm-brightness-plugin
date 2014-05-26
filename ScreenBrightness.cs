/*
 * Note: as far as I can tell, this will only work on WinVista and higher
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
        private Screen m;

        internal Measure()
        {
            m = new Screen();
        }

        internal void Reload(Rainmeter.API api, ref double maxValue)
        {
            m.reload();
            maxValue = m.MaxLevel;
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

        /// <summary>
        /// Retrieves the maximum brightness level if supported
        /// </summary>
        internal double MaxLevel
        {
            get
            {
                return (double)(brightnessLevels[brightnessLevels.Length - 1]);
            }
        }

        /// <summary>
        /// Retrieves the current brightness level if supported
        /// </summary>
        internal byte CurrentBrightness
        {
            get
            {
                ManagementObject monitorInfo = getWmiObject("WmiMonitorBrightness");
                return (monitorInfo == null ?
                    (byte)(0) :
                    (byte)(monitorInfo.GetPropertyValue("CurrentBrightness")));
            }
        }

        public Screen()
        {
            this.brightnessLevels = new byte[] { 0 };
            this.brightnessIndex = 0;   
        }

        /// <summary>
        /// Refresh attributes
        /// </summary>
        internal void reload()
        {
            brightnessMethods = getWmiObject("WmiMonitorBrightnessMethods");
            brightnessLevels = getBrightnessLevels();
            Array.Sort(brightnessLevels);
            syncBrightnessIndex();
        }

        /// <summary>
        /// Set the current brightness to one level above the current level
        /// </summary>
        internal void raiseBrightness()
        {
            int maxIndex = this.brightnessLevels.Length - 1;
            // assume we are already at max brightness
            byte target = brightnessLevels[maxIndex];
            // if the above is not true, update target and index
            if (brightnessIndex < maxIndex)
            {
                brightnessIndex += 1;
                target = brightnessLevels[brightnessIndex];
                setBrightness(target);
            }
        }

        /// <summary>
        /// Set the current brightness to one level below the current level
        /// </summary>
        internal void lowerBrightness()
        {
            byte target = brightnessLevels[0];
            if (brightnessIndex > 0)
            {
                brightnessIndex -= 1;
                target = brightnessLevels[brightnessIndex];
                setBrightness(target);
            }
        }

        /// <summary>
        /// Sets the current brightness to the supported level closest to an arbitrary specified level
        /// </summary>
        /// <param name="level">Target level, may not be a supported brightness level</param>
        internal void setBrightness(byte level)
        {
            int i = findLevelIndex(level, brightnessLevels);
            if (brightnessMethods != null)
            {
                brightnessMethods.InvokeMethod("WmiSetBrightness", new Object[] { 10, brightnessLevels[i] });
            }
        }

        /// <summary>
        /// Synchronize the index in the supported levels array to the current actual brightness
        /// </summary>
        internal void syncBrightnessIndex()
        {
            this.brightnessIndex = findLevelIndex(this.CurrentBrightness, this.brightnessLevels);
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
            
            if (levels != null && levels.Length > 0)
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

        /// <summary>
        /// Retreives an array of supported brightness levels from WMI
        /// </summary>
        /// <returns>byte[] of supported brightness levels</returns>
        private byte[] getBrightnessLevels()
        {
            ManagementObject monitorInfo = getWmiObject("WmiMonitorBrightness");
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
