namespace Storage.Infrastructure.Scanners {
    public class ClamAVConfiguration {
        public bool Enabled { get; set; } = false;
        public string ClamDaemonHost { get; set; } = "localhost";
        public int ClamDaemonPort { get; set; } = 3310;
        public bool AllowOnError { get; set; } = true;
        public int TimeoutSeconds { get; set; } = 300;
    }
}