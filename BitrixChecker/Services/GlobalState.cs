using System.Text.Json;

namespace BitrixChecker.Services
{
    public static class GlobalState
    {
        public static long TotalExpected = 0; 
        public static long TotalScanned = 0;
        private const string FILE_PATH = "scan_state.json";

        public static void Reset(long expected)
        {
            Interlocked.Exchange(ref TotalExpected, expected);
            Interlocked.Exchange(ref TotalScanned, 0);
            Save();
        }

        public static void Save()
        {
            try
            {
                var data = new { Expected = TotalExpected, Scanned = TotalScanned };
                var json = JsonSerializer.Serialize(data);
                File.WriteAllText(FILE_PATH, json);
            }
            catch { }
        }

        public static void Load()
        {
            try
            {
                if (File.Exists(FILE_PATH))
                {
                    var json = File.ReadAllText(FILE_PATH);
                    var data = JsonSerializer.Deserialize<StateData>(json);
                    if (data != null)
                    {
                        // Chỉ load nếu số liệu hợp lệ
                        if (data.Expected > 0) TotalExpected = data.Expected;
                        if (data.Scanned > 0) TotalScanned = data.Scanned;
                    }
                }
            }
            catch { }
        }

        private class StateData { public long Expected { get; set; } public long Scanned { get; set; } }
    }
}