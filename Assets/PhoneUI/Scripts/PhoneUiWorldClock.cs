using UnityEngine;

namespace PhoneUI
{
    public static class PhoneUiWorldClock
    {
        public static string FormatGameTime()
        {
            var sun = Object.FindObjectOfType<SunSimulation>(true);
            if (sun != null)
            {
                float t = Mathf.Repeat(sun.currentTimeOfDay, 24f);
                int h = Mathf.FloorToInt(t);
                int m = Mathf.FloorToInt((t - h) * 60f);
                return $"{h:00}:{m:00}";
            }

            return System.DateTime.Now.ToString("HH:mm");
        }

        public static string FormatSignalBars()
        {
            return "5G  ▮▮▮▮";
        }

        public static string FormatBattery()
        {
            return "100%";
        }
    }
}
