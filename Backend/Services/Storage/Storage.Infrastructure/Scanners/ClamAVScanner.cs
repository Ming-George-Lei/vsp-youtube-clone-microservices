using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Net.Sockets;
using System.Text;

namespace Storage.Infrastructure.Scanners {
    public class ClamAVScanner : IAntiVirusScanner {
        private readonly ILogger<ClamAVScanner> _logger;
        private readonly ClamAVConfiguration _config;

        public ClamAVScanner(ILogger<ClamAVScanner> logger, IOptions<ClamAVConfiguration> config)
        {
            _logger = logger;
            _config = config.Value;
        }

        public async Task<bool> ScanAndClean(string filePath) {
            if (!_config.Enabled) {
                _logger.LogInformation("AntiVirus scanning is disabled, skipping scan for file: {FilePath}", filePath);
                return true;
            }

            if (!File.Exists(filePath)) {
                _logger.LogError("File not found for virus scanning: {FilePath}", filePath);
                return false;
            }

            try
            {
                _logger.LogInformation("Starting virus scan for file: {FilePath} using ClamAV daemon at {Host}:{Port}", 
                    filePath, _config.ClamDaemonHost, _config.ClamDaemonPort);

                using var tcpClient = new TcpClient();

                var connectTask = tcpClient.ConnectAsync(_config.ClamDaemonHost, _config.ClamDaemonPort);
                var timeoutTask = Task.Delay(TimeSpan.FromSeconds(_config.TimeoutSeconds));

                if (await Task.WhenAny(connectTask, timeoutTask) == timeoutTask)
                {
                    _logger.LogError("Timeout connecting to ClamAV daemon at {Host}:{Port}", _config.ClamDaemonHost, _config.ClamDaemonPort);
                    return _config.AllowOnError;
                }

                if (!tcpClient.Connected)
                {
                    _logger.LogError("Failed to connect to ClamAV daemon at {Host}:{Port}", _config.ClamDaemonHost, _config.ClamDaemonPort);
                    return _config.AllowOnError;
                }

                var stream = tcpClient.GetStream();

                // Read file into memory
                var fileBytes = await File.ReadAllBytesAsync(filePath);

                // Send INSTREAM command
                var command = Encoding.ASCII.GetBytes("zINSTREAM\0");
                await stream.WriteAsync(command);

                // Send file size (4 bytes, network byte order)
                var sizeBytes = BitConverter.GetBytes((uint)fileBytes.Length);
                if (BitConverter.IsLittleEndian)
                    Array.Reverse(sizeBytes);
                await stream.WriteAsync(sizeBytes);

                // Send file data
                await stream.WriteAsync(fileBytes);

                // Send zero-length chunk to indicate end of data
                var endBytes = new byte[4];
                await stream.WriteAsync(endBytes);

                // Read response
                var responseBuffer = new byte[1024];
                var bytesRead = await stream.ReadAsync(responseBuffer);
                var response = Encoding.ASCII.GetString(responseBuffer, 0, bytesRead).Trim();

                _logger.LogInformation("ClamAV scan response for file {FilePath}: {Response}", filePath, response);

                if (response.Contains("OK"))
                {
                    _logger.LogInformation("File is clean: {FilePath}", filePath);
                    return true;
                }
                else if (response.Contains("FOUND"))
                {
                    _logger.LogError("Virus detected in file: {FilePath}, Response: {Response}", filePath, response);
                    return false;
                }
                else
                {
                    _logger.LogWarning("Unknown ClamAV response for file {FilePath}: {Response}", filePath, response);
                    return _config.AllowOnError;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Exception occurred during virus scanning: {FilePath}", filePath);
                return _config.AllowOnError;
            }
        }
    }
}