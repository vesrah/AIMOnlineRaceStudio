using System.Runtime.InteropServices;

namespace XrkApi
{
    public class MatLabXrkWrapper : IXrkFileReader
    {
        private const string DllName = "MatLabXRK-2022-64-ReleaseU.dll";
        private const int MaxChannels = 10_000;
        private const int MaxGpsChannels = 1_000;
        private const int MaxLaps = 100_000;
        private const int MaxSamplesPerChannel = 20_000_000;
        private int _fileHandle = -1;

        private static class Native
        {
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr get_library_date();

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr get_library_time();

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern int open_file(string full_path_name);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl, CharSet = CharSet.Ansi)]
            public static extern IntPtr get_last_open_error();

            /// <summary>Close by internal file index (returned by open_file). Use this, not close_file_n (which takes a path string).</summary>
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int close_file_i(int idxf);

            // All get_*_name/units return char const* (IntPtr); marshal with PtrToStringAnsi
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr get_vehicle_name(int idxf);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr get_track_name(int idxf);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr get_racer_name(int idxf);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr get_championship_name(int idxf);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr get_session_type_name(int idxf);

            /// <summary>Session date and time for the opened XRK file (idxf). Returns pointer to struct tm, or null.</summary>
            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr get_date_and_time(int idxf);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern uint get_logger_id(int idxf);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int get_laps_count(int idxf);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int get_session_duration(int idxf, out double pduration);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int get_lap_info(int idxf, int idxl, out double pstart, out double pduration);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int get_channels_count(int idxf);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr get_channel_name(int idxf, int idxc);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr get_channel_units(int idxf, int idxc);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int get_channel_samples_count(int idxf, int idxc);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int get_channel_samples(int idxf, int idxc, double[] ptimes, double[] pvalues, int cnt);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int set_GPS_sample_freq(double freq);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern int get_GPS_channels_count(int idxf);

            [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
            public static extern IntPtr get_GPS_channel_name(int idxf, int idxc);
        }

        /// <summary>C struct tm layout (Windows) for get_date_and_time.</summary>
        [StructLayout(LayoutKind.Sequential)]
        private struct Tm
        {
            public int tm_sec;
            public int tm_min;
            public int tm_hour;
            public int tm_mday;
            public int tm_mon;
            public int tm_year;
            public int tm_wday;
            public int tm_yday;
            public int tm_isdst;
        }

        /// <summary>Session date (YYYY-MM-DD) from get_date_and_time(idxf) when file is open; otherwise DLL compile date.</summary>
        public string LibraryDate => TryGetSessionTm(out var tm) ? $"{tm.tm_year + 1900:D4}-{tm.tm_mon + 1:D2}-{tm.tm_mday:D2}" : (Marshal.PtrToStringAnsi(Native.get_library_date()) ?? "");
        /// <summary>Session time (HH:mm:ss) from get_date_and_time(idxf) when file is open; otherwise DLL compile time.</summary>
        public string LibraryTime => TryGetSessionTm(out var tm) ? $"{tm.tm_hour:D2}:{tm.tm_min:D2}:{tm.tm_sec:D2}" : (Marshal.PtrToStringAnsi(Native.get_library_time()) ?? "");

        private bool TryGetSessionTm(out Tm tm)
        {
            tm = default;
            if (_fileHandle <= 0) return false;
            var ptr = Native.get_date_and_time(_fileHandle);
            if (ptr == IntPtr.Zero) return false;
            try
            {
                tm = Marshal.PtrToStructure<Tm>(ptr);
                return true;
            }
            catch { return false; }
        }

        public int Open(string path)
        {
            _fileHandle = Native.open_file(System.IO.Path.GetFullPath(path));
            return _fileHandle;
        }

        public string GetVehicleName() => PtrToString(Native.get_vehicle_name(_fileHandle));
        public string GetTrackName() => PtrToString(Native.get_track_name(_fileHandle));
        public string GetRacerName() => PtrToString(Native.get_racer_name(_fileHandle));
        public string GetChampionshipName() => PtrToString(Native.get_championship_name(_fileHandle));
        public string GetSessionTypeName() => PtrToString(Native.get_session_type_name(_fileHandle));
        public double? GetSessionDurationSeconds() => _fileHandle <= 0 ? null : Native.get_session_duration(_fileHandle, out var d) == 1 ? d : null;
        public uint? GetLoggerId() => _fileHandle <= 0 ? null : Native.get_logger_id(_fileHandle);

        /// <summary>Last error from the DLL when open_file failed. Call after Open() returns &lt;= 0.</summary>
        public static string GetLastOpenError() => PtrToString(Native.get_last_open_error());

        public int LapCount => _fileHandle <= 0 ? 0 : Math.Min(Math.Max(0, Native.get_laps_count(_fileHandle)), MaxLaps);

        public record LapInfo(int Index, double Start, double Duration);

        public IReadOnlyList<LapInfo> GetLaps()
        {
            var laps = new List<LapInfo>();
            if (_fileHandle <= 0) return laps;

            var count = LapCount;
            for (var i = 0; i < count; i++)
            {
                var result = Native.get_lap_info(_fileHandle, i, out var start, out var duration);
                if (result <= 0) continue;

                laps.Add(new LapInfo(i + 1, start, duration));
            }

            return laps;
        }

        public List<string> GetChannelNames()
        {
            var names = new List<string>();
            if (_fileHandle <= 0) return names;

            var count = Native.get_channels_count(_fileHandle);
            if (count <= 0) return names;
            count = Math.Min(count, MaxChannels);
            for (var i = 0; i < count; i++)
                names.Add(PtrToString(Native.get_channel_name(_fileHandle, i)));

            return names;
        }

        public IReadOnlyList<string> GetGpsChannelNames()
        {
            var names = new List<string>();
            if (_fileHandle <= 0) return names;

            // DLL requires set_GPS_sample_freq before any GPS channel access (1–100 Hz).
            _ = Native.set_GPS_sample_freq(10.0);

            var count = Native.get_GPS_channels_count(_fileHandle);
            if (count <= 0) return names;
            count = Math.Min(count, MaxGpsChannels);
            for (var i = 0; i < count; i++)
                names.Add(PtrToString(Native.get_GPS_channel_name(_fileHandle, i)));

            return names;
        }

        public (double[] Times, double[] Values) GetChannelData(int channelIndex)
        {
            var count = Native.get_channel_samples_count(_fileHandle, channelIndex);
            if (count <= 0) return (Array.Empty<double>(), Array.Empty<double>());
            count = Math.Min(count, MaxSamplesPerChannel);

            var times = new double[count];
            var values = new double[count];
            Native.get_channel_samples(_fileHandle, channelIndex, times, values, count);
            return (times, values);
        }

        public string GetChannelUnits(int channelIndex)
        {
            if (_fileHandle <= 0) return "";
            return PtrToString(Native.get_channel_units(_fileHandle, channelIndex));
        }

        private static string PtrToString(IntPtr ptr) => Marshal.PtrToStringAnsi(ptr) ?? "";

        public void Dispose()
        {
            if (_fileHandle > 0)
            {
                try { Native.close_file_i(_fileHandle); } catch { /* ignore */ }
                _fileHandle = -1;
            }
        }
    }
}