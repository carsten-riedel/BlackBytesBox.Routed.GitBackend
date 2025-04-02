using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;

namespace BlackBytesBox.Routed.GitBackend
{
    /// <summary>
    /// Provides dynamic settings management that supports file monitoring and change notifications.
    /// </summary>
    /// <typeparam name="T">The type of the settings.</typeparam>
    public sealed class DynamicSettingsService<T> : IDisposable where T : class, new()
    {
        private readonly string _filePath;
        private readonly object _syncRoot = new();
        private readonly FileSystemWatcher _watcher;
        private T _currentSettings;
        private DateTime _lastChange = DateTime.MinValue;

        /// <summary>
        /// Occurs when the settings have been changed.
        /// </summary>
        public event Action<T>? OnChange;

        /// <summary>
        /// Initializes a new instance of the <see cref="DynamicSettingsService{T}"/> class.
        /// </summary>
        /// <param name="filePath">The file path for the settings file. Defaults to "dynamicsettings.json".</param>
        public DynamicSettingsService(string filePath = "dynamicsettings.json")
        {
            _filePath = Path.GetFullPath(filePath);

            // Ensure directory exists
            var directory = Path.GetDirectoryName(_filePath)!;
            Directory.CreateDirectory(directory);

            // Initial load
            LoadSettings();

            // Setup file watcher
            _watcher = new FileSystemWatcher(directory)
            {
                Filter = Path.GetFileName(_filePath),
                NotifyFilter = NotifyFilters.LastWrite
            };
            _watcher.Changed += OnFileChanged;
            _watcher.EnableRaisingEvents = true;
        }

        /// <summary>
        /// Gets the current settings.
        /// </summary>
        public T CurrentSettings
        {
            get
            {
                lock (_syncRoot)
                {
                    return _currentSettings;
                }
            }
        }

        /// <summary>
        /// Updates the settings using the provided update function.
        /// </summary>
        /// <param name="updateFn">A function that takes the current settings and returns the updated settings.</param>
        public void UpdateSettings(Func<T, T> updateFn)
        {
            lock (_syncRoot)
            {
                var newSettings = updateFn(_currentSettings);
                SaveSettings(newSettings);
                NotifyChange();
            }
        }

        private void LoadSettings()
        {
            lock (_syncRoot)
            {
                try
                {
                    if (!File.Exists(_filePath))
                    {
                        _currentSettings = new T();
                        SaveSettings(_currentSettings);
                        return;
                    }

                    var json = File.ReadAllText(_filePath);
                    _currentSettings = JsonSerializer.Deserialize<T>(json) ?? new T();
                }
                catch
                {
                    _currentSettings = new T();
                }
            }
        }

        private void SaveSettings(T settings)
        {
            lock (_syncRoot)
            {
                var json = JsonSerializer.Serialize(settings, new JsonSerializerOptions
                {
                    WriteIndented = true
                });
                File.WriteAllText(_filePath, json);
                _currentSettings = settings;
            }
        }

        private void OnFileChanged(object sender, FileSystemEventArgs e)
        {
            bool shouldProcess;
            lock (_syncRoot)
            {
                shouldProcess = (DateTime.UtcNow - _lastChange).TotalMilliseconds >= 500;
                if (shouldProcess)
                {
                    _lastChange = DateTime.UtcNow;
                }
            }

            if (!shouldProcess)
                return;

            LoadSettings();
            NotifyChange();
        }


        /// <summary>
        /// Disposes the file system watcher.
        /// </summary>
        public void Dispose()
        {
            _watcher?.Dispose();
        }

        /// <summary>
        /// Notifies subscribers that the settings have changed.
        /// </summary>
        private void NotifyChange()
        {
            lock (_syncRoot)
            {
                OnChange?.Invoke(_currentSettings);
            }
        }
    }
}