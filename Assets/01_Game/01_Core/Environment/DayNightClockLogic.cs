#region

using UnityEngine;

#endregion

namespace Zombera.Environment
{
    public readonly struct DayNightAdvanceResult
    {
        public DayNightAdvanceResult(float hour, int dayNumber)
        {
            Hour = hour;
            DayNumber = dayNumber;
        }

        public float Hour { get; }
        public int DayNumber { get; }
    }

    public readonly struct DayNightTickResult
    {
        public DayNightTickResult(int lastWholeHour, TimeOfDayPhase phase, bool hourChanged, bool phaseChanged)
        {
            LastWholeHour = lastWholeHour;
            Phase = phase;
            HourChanged = hourChanged;
            PhaseChanged = phaseChanged;
        }

        public int LastWholeHour { get; }
        public TimeOfDayPhase Phase { get; }
        public bool HourChanged { get; }
        public bool PhaseChanged { get; }
    }

    public static class DayNightClockLogic
    {
        public static float NormalizeHour(float hour)
        {
            return Mathf.Repeat(hour, 24f);
        }

        public static DayNightAdvanceResult Advance(float currentHour, int dayNumber, float unscaledDeltaTime, float timeScale,
            float realSecondsPerGameDay)
        {
            var safeSecondsPerGameDay = Mathf.Max(1f, realSecondsPerGameDay);
            var nextHour = currentHour + unscaledDeltaTime * timeScale * 24f / safeSecondsPerGameDay;
            var nextDayNumber = dayNumber;

            if (nextHour >= 24f) nextDayNumber++;

            return new DayNightAdvanceResult(nextHour % 24f, nextDayNumber);
        }

        public static TimeOfDayPhase GetPhase(float hour)
        {
            return hour switch
            {
                >= 5f and < 7f => TimeOfDayPhase.Dawn,
                >= 7f and < 18f => TimeOfDayPhase.Day,
                >= 18f and < 21f => TimeOfDayPhase.Dusk,
                _ => TimeOfDayPhase.Night
            };
        }

        public static DayNightTickResult Tick(float currentHour, TimeOfDayPhase currentPhase, int lastWholeHour)
        {
            var wholeHour = Mathf.FloorToInt(currentHour);
            var nextLastWholeHour = lastWholeHour;
            var hourChanged = false;
            if (wholeHour != lastWholeHour)
            {
                nextLastWholeHour = wholeHour;
                hourChanged = true;
            }

            var phase = GetPhase(currentHour);
            var phaseChanged = phase != currentPhase;
            return new DayNightTickResult(nextLastWholeHour, phase, hourChanged, phaseChanged);
        }

        public static string FormatHour(float hour)
        {
            var wrappedHour = Mathf.Repeat(hour, 24f);
            var wholeHour = Mathf.FloorToInt(wrappedHour);
            var minute = Mathf.FloorToInt((wrappedHour - wholeHour) * 60f);
            return $"{wholeHour:00}:{minute:00}";
        }
    }
}
