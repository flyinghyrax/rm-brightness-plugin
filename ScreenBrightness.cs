/*
 * Current state: returns correct value initially but does not update. Need to re-run WMI object query every update?
 * TODO:
 * - Detect support of WmiMonitorBrightness* (a.k.a. actual error handling)
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
        private ManagementObject brightnessInfo;
        private ManagementObject brightnessMethods;
        private byte[] levels;

        internal Measure()
        {   
            brightnessInfo = getWmiRootObject("WmiMonitorBrightness");
            brightnessMethods = getWmiRootObject("WmiMonitorBrightnessMethods");
        }

        internal void Reload(Rainmeter.API api, ref double maxValue)
        {
            if (brightnessInfo != null)
            {
                levels = getBrightnessLevels();
                maxValue = levels[levels.Length - 1];
            }
            else
            {
                API.Log(API.LogType.Error, "ScreenBrightness.dll: (In reload) Could not get WmiMonitorBrightness");
            }
            
        }

        internal double Update()
        {
            if (brightnessInfo != null)
            {
                return getBrightness();
            }
            else
            {
                API.Log(API.LogType.Error, "ScreenBrightness.dll: (In update) Could not get WmiMonitorBrightness");
                return 0.0;
            }
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

        private byte getBrightness()
        {
            return (byte)brightnessInfo.GetPropertyValue("CurrentBrightness");
        }

        private byte[] getBrightnessLevels()
        {
            return (byte[])brightnessInfo.GetPropertyValue("Level");
        }

        private void setBrightness(byte targetBrightness)
        {
            brightnessMethods.InvokeMethod("WmiSetBrightness", new Object[] { UInt32.MaxValue, targetBrightness });
        }

        /* There really must be a better way.  I know there is. */
        private static ManagementObject getWmiRootObject(string which)
        {
            ManagementObject res = null;
            ManagementObjectSearcher mos = null;
            ManagementObjectCollection moc = null;
            try
            {
                ManagementScope s = new ManagementScope("\\\\.\\root\\WMI");
                SelectQuery q = new SelectQuery(which);
                mos = new ManagementObjectSearcher(s, q);
                moc = mos.Get();

                foreach (ManagementObject o in moc)
                {
                    res = o;
                    break;
                }
            }
            catch (ManagementException mex)
            {
                API.Log(API.LogType.Error, mex.Message);
            }
            finally
            {
                if (mos != null)
                    mos.Dispose();
                if (moc != null)
                    moc.Dispose();
            }
            return res;
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
