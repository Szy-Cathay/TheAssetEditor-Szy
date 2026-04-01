using System;
using System.IO;
using Shared.Core.ErrorHandling;
using Shared.Core.PackFiles;
using Shared.Core.Settings;

namespace Shared.Core.Services
{
    /// <summary>
    /// Periodically auto-saves the current editable pack file.
    /// Backup is handled automatically by SavePackContainer.
    /// </summary>
    public class PackAutoSaveService
    {
        private System.Timers.Timer _timer;
        private readonly IPackFileService _packFileService;
        private readonly ApplicationSettingsService _settingsService;
        private readonly ILogger _logger = Logging.Create<PackAutoSaveService>();

        public PackAutoSaveService(IPackFileService packFileService, ApplicationSettingsService settingsService)
        {
            _packFileService = packFileService;
            _settingsService = settingsService;
        }

        public void Start()
        {
            Stop();

            var settings = _settingsService.CurrentSettings;
            if (!settings.EnableAutoSave)
                return;

            var intervalMs = Math.Max(settings.AutoSaveIntervalMinutes, 1) * 60 * 1000;
            _timer = new System.Timers.Timer(intervalMs);
            _timer.Elapsed += OnTimerElapsed;
            _timer.AutoReset = true;
            _timer.Start();

            _logger.Here().Information($"Auto-save started, interval: {settings.AutoSaveIntervalMinutes} minutes");
        }

        public void Stop()
        {
            if (_timer != null)
            {
                _timer.Stop();
                _timer.Elapsed -= OnTimerElapsed;
                _timer.Dispose();
                _timer = null;
            }
        }

        /// <summary>
        /// Restart the timer (call after settings change).
        /// </summary>
        public void Restart()
        {
            Start();
        }

        private void OnTimerElapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            try
            {
                var editablePack = _packFileService.GetEditablePack();
                if (editablePack == null || editablePack.IsCaPackFile)
                    return;

                var systemPath = editablePack.SystemFilePath;
                if (string.IsNullOrWhiteSpace(systemPath) || !File.Exists(systemPath))
                    return;

                var gameInfo = GameInformationDatabase.GetGameById(_settingsService.CurrentSettings.CurrentGame);
                _packFileService.SavePackContainer(editablePack, systemPath, false, gameInfo);
                _logger.Here().Information($"Auto-save completed: {systemPath}");
            }
            catch (Exception ex)
            {
                _logger.Here().Error(ex, "Auto-save failed");
            }
        }
    }
}
