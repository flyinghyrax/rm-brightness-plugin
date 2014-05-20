/*
 * Current state: returns correct value initially but does not update. Need to re-run WMI object query every update?
 * TODO:
 * - IfNotSupportedAction measure option
 * - Requery every update cycle
 * - Multithread updates so that repeated wmi queries don't bog down Rainmeter
 * - increase/decrease bangs
 * - set <level> bang (dangerous, not all devices support all levels?)
 * - revert bang (WmiRevertTopolicyBrightness)
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
            return m.getCurrentBrightness();
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
            // increase
            // decrease
            // set <level> (?)
            // revert
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
        private int levelIndex = 0;
        
        // maximum brightness level, determined from levels array
        private byte _maxLevel;
        internal double MaxLevel
        {
            get
            {
                return (double)(_maxLevel);
            }
        }

        // indicates whether or not the plugin is actually supported
        private bool _supported;
        internal bool Supported
        {
            get
            {
                return this._supported;
            }
        }

        public Monitor()
        {
            // initialization
            this.levelIndex = 0;
            this._maxLevel = 0;
            this._supported = false;
            // get actual values
            this.reload();
        }

        internal void reload()
        {
            brightnessMethods = Measure.getWmiObject("WmiMonitorBrightnessMethods");
            this._supported = (this.brightnessMethods == null ? false : true);
            if (this.Supported)
            {
                brightnessLevels = getBrightnessLevels();
                Array.Sort(brightnessLevels);
                this._maxLevel = brightnessLevels[brightnessLevels.Length - 1];
            }
        }

        internal double getCurrentBrightness()
        {
            if (this.Supported)
            {
                ManagementObject monitorInfo = Measure.getWmiObject("WmiMonitorBrightness");
                byte level = (byte)(monitorInfo.GetPropertyValue("CurrentBrightness"));
                return (double)(level);
            }
            else
            {
                return 0.0;
            }
        }

        internal void raiseBrightness()
        {
            // TODO
        }

        internal void lowerBrightness()
        {
            // TODO
        }

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
