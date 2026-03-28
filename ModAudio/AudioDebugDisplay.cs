using System.Text.RegularExpressions;
using BepInEx.Logging;
using Unity.Profiling;
using UnityEngine;

namespace Marioalexsan.ModAudio;

public enum DebugMessageCategories
{
    All = -1,
    AudioPack,
    AudioSource,
    Script,
    Engine
}

// General notes about this:
// The debug display deals with tons of log data, and as a result needs to be implemented efficiently
// Don't shy away from caching data if needed
public class AudioDebugDisplay : MonoBehaviour
{
    private struct MessageLog
    {
        public LogLevel Level;
        public DebugMessageCategories Category;
        public string Message;
        public string Tags; // Comma-separated by convention
        public float ExtraParam1; // Custom data
        public bool ShouldDisplayCached; // Cached information about whenever this should render during this frame
    }

    // The actual capacity will be 9999 due to how the range is implemented; fixing this would overcomplicate the logic
    // since we'd have to differentiate between a full and empty buffer
    private static readonly MessageLog[] _circularBuffer = new MessageLog[10000];
    private static int _bufferStart; // Inclusive
    private static int _bufferEnd; // Exclusive

    public static void LogPack(LogLevel level, string message, string tags = "audio-test.....")
        => LogMessage(DebugMessageCategories.AudioPack, level, message, tags);

    public static void LogScript(LogLevel level, string message, string tags = "")
        => LogMessage(DebugMessageCategories.Script, level, message, tags);

    public static void LogAudio(LogLevel level, string message, string tags = "", float extraParam1 = default)
        => LogMessage(DebugMessageCategories.AudioSource, level, message, tags, extraParam1);

    public static void LogEngine(LogLevel level, string message, string tags = "")
        => LogMessage(DebugMessageCategories.Engine, level, message, tags);

    private static void LogMessage(DebugMessageCategories category, LogLevel level, string message, string tags, float extraParam1 = default)
    {
        var entry = new MessageLog()
        {
            Level = level,
            Category = category,
            Message = message,
            Tags = tags,
            ExtraParam1 = extraParam1,
        };

        entry.ShouldDisplayCached = ShouldDisplayLog(entry);
        
        _circularBuffer[_bufferEnd] = entry;
        
        _bufferEnd = (_bufferEnd + 1) % _circularBuffer.Length;
        if (_bufferEnd == _bufferStart)
            _bufferStart = (_bufferStart + 1) % _circularBuffer.Length;
    }

    public void OnGUI()
    {
        if (!_enabled)
            return;

        GUI.backgroundColor = new Color(0, 0, 0, 1);
        GUI.color = new Color(1, 1, 1, 1);

        _disabledPackButton ??= new GUIStyle(GUI.skin.button)
        {
            normal =
            {
                textColor = Color.gray
            }
        };

        _disabledWithErrorsPackButton ??= new GUIStyle(GUI.skin.button)
        {
            normal =
            {
                textColor = Color.red * 0.5f + Color.gray * 0.5f
            }
        };

        _activePackButton ??= new GUIStyle(GUI.skin.button)
        {
            normal =
            {
                textColor = Color.white
            }
        };

        _activeWithErrorsPackButton ??= new GUIStyle(GUI.skin.button)
        {
            normal =
            {
                textColor = Color.red
            }
        };

        _logInfo ??= new GUIStyle(GUI.skin.label)
        {
            normal =
            {
                textColor = Color.white
            }
        };

        _logWarn ??= new GUIStyle(GUI.skin.label)
        {
            normal =
            {
                textColor = Color.yellow
            }
        };

        _logError ??= new GUIStyle(GUI.skin.label)
        {
            normal =
            {
                textColor = Color.red
            }
        };

        if (_rowHeight < 0)
            _rowHeight = GUI.skin.label.CalcSize(new GUIContent("Text")).y;

        _windowPos = GUILayout.Window(0, new Rect(_windowPos.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), _ =>
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);

            if (_selectedTab == 0)
                RenderAudioLogs();

            else if (_selectedTab == 1)
                RenderAudioPacks();

            else if (_selectedTab == 2)
                RenderAudioSources();

        }, "ModAudio");
    }

    public void Update()
    {
        _updateLogsMarker.Begin();

        if (ModAudio.DebugMenuButton.Value != KeyCode.None)
        {
            if (Input.GetKeyDown(ModAudio.DebugMenuButton.Value))
                _enabled = !_enabled;
        }

        // No need to waste CPU cycles if we won't render this information
        if (_enabled)
        {
            if (_selectedTab == 0)
                UpdateAudioLogs();

            else if (_selectedTab == 1)
                UpdateAudioPacks();

            else if (_selectedTab == 2)
                UpdateAudioSources();
        }

        _updateLogsMarker.End();
    }

    private void UpdateAudioSources()
    {
        if (_totalSourcesLastFetchedAt + TimeSpan.FromSeconds(0.25) < DateTime.Now)
        {
            _totalSourcesLastFetchedAt = DateTime.Now;

            // Fetch this separately and directly
            var allSources = FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            _totalActiveAudioSources = 0;
            _totalInactiveAudioSources = 0;

            for (int i = 0; i < allSources.Length; i++)
            {
                if (allSources[i].isActiveAndEnabled)
                    _totalActiveAudioSources++;
                else
                    _totalInactiveAudioSources++;
            }
        }
    }

    private void RenderAudioSources()
    {
        GUILayout.Label($"[Note: this tab is not finished yet!]");
        GUILayout.Label($"Total active audio sources (Unity): {_totalActiveAudioSources}");
        GUILayout.Label($"Total inactive audio sources (Unity): {_totalInactiveAudioSources}");
        GUILayout.Label($"Total sources tracked by ModAudio: {AudioEngine.TrackedSources.Count}");
        GUILayout.Label($"Total playOnAwake sources tracked: {AudioEngine.TrackedPlayOnAwakeSourceStates.Count}");
        GUILayout.Label($"Total one shot sources tracked: {AudioEngine.TrackedOneShots.Count}");
    }

    private void UpdateAudioPacks()
    {
    }

    private void RenderAudioPacks()
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label($"Audio packs: {AudioEngine.AudioPacks.Count}");

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        _audioPackScrollView = GUILayout.BeginScrollView(_audioPackScrollView, GUILayout.Width(Screen.width * 0.2f));

        for (int i = 0; i < AudioEngine.AudioPacks.Count; i++)
        {
            var pack = AudioEngine.AudioPacks[i];

            bool clicked;

            if (pack.HasFlag(PackFlags.HasEncounteredErrors))
                clicked = GUILayout.Button(pack.Config.DisplayName, pack.HasFlag(PackFlags.Enabled) ? _activeWithErrorsPackButton : _disabledWithErrorsPackButton);

            else
                clicked = GUILayout.Button(pack.Config.DisplayName, pack.HasFlag(PackFlags.Enabled) ? _activePackButton : _disabledPackButton);

            if (clicked)
                _selectedPack = i;
        }

        GUILayout.EndScrollView();

        if (0 <= _selectedPack && _selectedPack < AudioEngine.AudioPacks.Count)
        {
            var pack = AudioEngine.AudioPacks[_selectedPack];

            _audioPackDetailsScrollView = GUILayout.BeginScrollView(_audioPackDetailsScrollView);

            if (!string.IsNullOrWhiteSpace(pack.PackPath) && GUILayout.Button("Open pack location"))
                Application.OpenURL(new Uri($"{pack.PackPath}").AbsoluteUri);   
            
            GUILayout.Label($"Display name: {pack.Config.DisplayName}");
            GUILayout.Label($"ID: {pack.Config.Id}");
            GUILayout.Label($"Path: {AudioPackLoader.AliasRootPath(pack.PackPath)}");
            GUILayout.Space(_rowHeight);
            
            GUILayout.Label($"Route files:");
            for (int i = 0; i < pack.ConfigFiles.Count; i++)
                GUILayout.Label(AudioPackLoader.AliasRootPath(pack.ConfigFiles[i]));
            
            GUILayout.Space(_rowHeight);
            
            if (pack.ScriptFiles.Count > 0)
            {
                GUILayout.Label($"Script files:");
                for (int i = 0; i < pack.ScriptFiles.Count; i++)
                    GUILayout.Label(AudioPackLoader.AliasRootPath(pack.ScriptFiles[i]));
            }
            else
            {
                GUILayout.Label($"No script files loaded");
            }
            
            GUILayout.Space(_rowHeight);
            GUILayout.Label($"Pack flags: {pack.Flags}");

            if (pack.Flags.HasFlag(PackFlags.HasEncounteredErrors))
                GUILayout.Label("This pack encountered errors!", _logError);
            else
                GUILayout.Label("Pack is working correctly");

            GUILayout.Space(_rowHeight);

            GUILayout.Label($"Total routes: {pack.Config.Routes.Count}");
            GUILayout.Label($"Total custom clips: {pack.Config.CustomClips.Count}");
            GUILayout.Label($"  Currently streaming: {pack.CurrentStreamedClips}");
            GUILayout.Label($"  Currently loaded in memory: {pack.CurrentInMemoryClips}");
            GUILayout.Label($"  Currently waiting to be loaded: {pack.CurrentlyWaitingForLoad}");
            GUILayout.Label($"Clips ready: {pack.ReadyAudio.Count}");

            GUILayout.EndScrollView();
        }

        GUILayout.EndHorizontal();
    }

    private static void UpdateCachedValue<T>(ref T cache, T value, ref bool changed)
    {
        if (!Equals(cache, value))
        {
            cache = value;
            changed = true;
        }
    }

    private void UpdateAudioLogs()
    {
        // This is to make sure we render the same number of log elements within a frame
        // Also for performance reasons; we shouldn't recompute this every time Unity wants to repaint the UI

        _visibleLogElements = int.MinValue;
        _hiddenLogElementsBefore = int.MinValue;
        _hiddenLogElementsAfter = int.MinValue;

        bool filtersChanged = false;

        UpdateCachedValue(ref _useDistanceFilterCached, _useDistanceFilter, ref filtersChanged);
        UpdateCachedValue(ref _distanceFilterCached, _distanceFilter, ref filtersChanged);
        UpdateCachedValue(ref _categoryCached, _category, ref filtersChanged);
        UpdateCachedValue(ref _subcategoryCached, _subcategory, ref filtersChanged);
        UpdateCachedValue(ref _latestMessagesOnlyCached, _latestMessagesOnly, ref filtersChanged);
        UpdateCachedValue(ref _showErrorAndWarnsOnlyCached, _showErrorAndWarnsOnly, ref filtersChanged);
        UpdateCachedValue(ref _filterModdedAudioCached, _filterModdedAudio, ref filtersChanged);
        UpdateCachedValue(ref _logIncludeFilterCached, _logIncludeFilter, ref filtersChanged);
        UpdateCachedValue(ref _logIncludeFilterMatchWordCached, _logIncludeFilterMatchWord, ref filtersChanged);

        if (_shouldClearLogs)
        {
            _shouldClearLogs = false;
            _bufferStart = _bufferEnd;
        }

        _totalMessages = _bufferStart <= _bufferEnd ? _bufferEnd - _bufferStart : _circularBuffer.Length - _bufferStart + _bufferEnd;
        _totalErrors = 0;
        _totalWarnings = 0;

        _totalDisplayedMessages = 0;
        _totalDisplayedErrors = 0;
        _totalDisplayedWarnings = 0;

        if (filtersChanged)
        {
            _logIncludeFilterRegexToUse = _logIncludeFilterMatchWordCached ? new Regex($"\\b{Regex.Escape(_logIncludeFilterCached)}\\b", RegexOptions.IgnoreCase) : null;
            
            for (int i = _bufferStart; i != _bufferEnd; i = (i + 1) % _circularBuffer.Length)
            {
                var message = _circularBuffer[i];
                _circularBuffer[i] = message with { ShouldDisplayCached = ShouldDisplayLog(message) };
            }
        }

        for (int i = _bufferStart; i != _bufferEnd; i = (i + 1) % _circularBuffer.Length)
        {
            var message = _circularBuffer[i];
            bool shouldDisplay = message.ShouldDisplayCached;

            if (shouldDisplay)
                _totalDisplayedMessages++;

            if (message.Level == LogLevel.Error)
            {
                _totalErrors++;

                if (shouldDisplay)
                    _totalDisplayedErrors++;
            }
            else if (message.Level == LogLevel.Warning)
            {
                _totalWarnings++;

                if (shouldDisplay)
                    _totalDisplayedWarnings++;
            }
        }
    }
    
    private void RenderAudioLogs()
    {
        _renderLogsMarker.Begin();

        GUILayout.BeginHorizontal();

        GUILayout.Label("Categories:");

        if (GUILayout.Toggle(_category == DebugMessageCategories.All, "All"))
            _category = DebugMessageCategories.All;

        if (GUILayout.Toggle(_category == DebugMessageCategories.AudioPack, "Audio Packs"))
            _category = DebugMessageCategories.AudioPack;

        if (GUILayout.Toggle(_category == DebugMessageCategories.AudioSource, "Audio Sources"))
            _category = DebugMessageCategories.AudioSource;

        if (GUILayout.Toggle(_category == DebugMessageCategories.Script, "Scripts"))
            _category = DebugMessageCategories.Script;

        if (GUILayout.Toggle(_category == DebugMessageCategories.Engine, "Engine"))
            _category = DebugMessageCategories.Engine;

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (_category == DebugMessageCategories.AudioSource)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("Audio categories:");

            // Note: the groups here have to match with what is added to the tags of logs *exactly*

            if (GUILayout.Toggle(_subcategory.Length == 0, "All"))
                _subcategory = "";

            if (GUILayout.Toggle(_subcategory.Equals("AudGrp ambience"), "Ambience"))
                _subcategory = "AudGrp ambience";

            if (GUILayout.Toggle(_subcategory.Equals("AudGrp game"), "Game"))
                _subcategory = "AudGrp game";

            if (GUILayout.Toggle(_subcategory.Equals("AudGrp gui"), "GUI"))
                _subcategory = "AudGrp gui";

            if (GUILayout.Toggle(_subcategory.Equals("AudGrp music"), "Music"))
                _subcategory = "AudGrp music";

            if (GUILayout.Toggle(_subcategory.Equals("AudGrp voice"), "Voice"))
                _subcategory = "AudGrp voice";

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            _useDistanceFilter = GUILayout.Toggle(_useDistanceFilter, $"Filter audio by distance from player ({_distanceFilter:F2})");
            _distanceFilter = GUILayout.HorizontalSlider(_distanceFilter, 0f, 1000f, GUILayout.MaxWidth(10000));

            GUILayout.EndHorizontal();
        }

        var totalRowHeight = _rowHeight * _totalDisplayedMessages;

        if (_latestMessagesOnlyCached)
            _logScrollView.y = totalRowHeight;

        _logScrollView = GUILayout.BeginScrollView(_logScrollView);

        var scrollRatio = Math.Clamp(_logScrollView.y / (totalRowHeight - _logScrollViewHeight), 0, 1);

        if (_visibleLogElements == int.MinValue)
        {
            _hiddenLogElementsBefore = Math.Clamp((int)(Mathf.Lerp(0, totalRowHeight - _logScrollViewHeight - _rowHeight, scrollRatio) / _rowHeight), 0, _totalDisplayedMessages);
            _hiddenLogElementsAfter = Math.Clamp((int)(Mathf.Lerp(totalRowHeight - _logScrollViewHeight - _rowHeight, 0, scrollRatio) / _rowHeight), 0, _totalDisplayedMessages);
            _visibleLogElements = _totalDisplayedMessages - _hiddenLogElementsAfter - _hiddenLogElementsBefore;
        }

        GUILayout.Space(_rowHeight * _hiddenLogElementsBefore);

        int cursor = _bufferStart;

        for (int elementCount = 0; elementCount < _hiddenLogElementsBefore + _visibleLogElements; elementCount++)
        {
            var log = _circularBuffer[cursor];

            while (!log.ShouldDisplayCached)
            {
                cursor = (cursor + 1) % _circularBuffer.Length;
                log = _circularBuffer[cursor];
            }

            if (elementCount >= _hiddenLogElementsBefore)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(MapLogLevelToLabel(log.Level), MapLogLevelToStyle(log.Level));
                GUILayout.Label(log.Message);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            cursor = (cursor + 1) % _circularBuffer.Length;
        }

        if (_hiddenLogElementsAfter >= 1)
            GUILayout.Space(_rowHeight * _hiddenLogElementsAfter);

        GUILayout.EndScrollView();

        if (Event.current.type == EventType.Repaint)
            _logScrollViewHeight = GUILayoutUtility.GetLastRect().height;
        
        GUILayout.BeginHorizontal();
        
        GUILayout.Label("Find:", GUILayout.ExpandWidth(false));
        _logIncludeFilter = GUILayout.TextField(_logIncludeFilter, 80, GUILayout.ExpandWidth(true));
        _logIncludeFilterMatchWord = GUILayout.Toggle(_logIncludeFilterMatchWord, "Match word", GUILayout.ExpandWidth(true));
        
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        GUILayout.Label($"Total: {_totalDisplayedMessages}/{_totalMessages}");
        GUILayout.Label($"Errors: {_totalDisplayedErrors}/{_totalErrors}");
        GUILayout.Label($"Warnings: {_totalDisplayedWarnings}/{_totalWarnings}");
        
        if (GUILayout.Button("Clear messages", GUILayout.ExpandWidth(false)))
            _shouldClearLogs = true;

        _latestMessagesOnly = GUILayout.Toggle(_latestMessagesOnly, "Follow log");
        _showErrorAndWarnsOnly = GUILayout.Toggle(_showErrorAndWarnsOnly, "Issues only");
        _filterModdedAudio = GUILayout.Toggle(_filterModdedAudio, "Custom audio only");

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUI.DragWindow();
        _renderLogsMarker.End();
    }

    private static bool ShouldDisplayLog(MessageLog log)
    {
        // Note: this method is in the hot path for anything related to logging
        // It's important to implement things efficiently in here - this includes mundane stuff such as string comparisons

        if (log.Level != LogLevel.Error && log.Level != LogLevel.Warning && _showErrorAndWarnsOnlyCached)
            return false;

        bool shouldShowCategory = _categoryCached == DebugMessageCategories.All || log.Category == _categoryCached;

        if (!shouldShowCategory)
            return false;

        if (log.Category == DebugMessageCategories.AudioSource && _useDistanceFilterCached && log.ExtraParam1 >= _distanceFilterCached)
            return false;

        if (log.Category == DebugMessageCategories.AudioSource)
        {
            if (_subcategoryCached != "" && !log.Tags.Contains(_subcategoryCached))
                return false;

            if (_filterModdedAudioCached && !log.Tags.Contains("Routed") && !log.Tags.Contains("Overlay"))
                return false;
        }

        if (_logIncludeFilterCached != "")
        {
            if (!_logIncludeFilterMatchWordCached)
            {
                if (!log.Message.Contains(_logIncludeFilterCached, StringComparison.InvariantCultureIgnoreCase))
                    return false;
            }
            else
            {
                if (_logIncludeFilterRegexToUse == null || !_logIncludeFilterRegexToUse.Match(log.Message).Success)
                    return false;
            }
        }

        return true;
    }

    private string MapLogLevelToLabel(LogLevel level) => level switch
    {
        LogLevel.Info => "INFO ",
        LogLevel.Debug => "DEBUG",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR ",
        LogLevel.Fatal => "FATAL ",
        _ => "OTHER"
    };

    private GUIStyle? MapLogLevelToStyle(LogLevel level) => level switch
    {
        LogLevel.Info => _logInfo,
        LogLevel.Warning => _logWarn,
        LogLevel.Error => _logError,
        _ => _logInfo
    };

    private static Rect _windowPos = new Rect(Screen.width * 0.1f, Screen.height * 0.1f, Screen.width * 0.5f, Screen.height * 0.5f);

    private readonly string[] _tabs = ["Audio logs", "Audio packs", "Audio sources"]; // TODO: Audio sources tab
    private static int _selectedTab;

    // Audio Packs start

    private static Vector2 _audioPackScrollView;
    private static Vector2 _audioPackDetailsScrollView;
    private static int _selectedPack = -1;

    // Audio Packs end

    // Audio Log Field start

    private static Vector2 _logScrollView;
    private static float _logScrollViewHeight = 100;

    private static float _rowHeight = -1;

    private static GUIStyle? _disabledPackButton;
    private static GUIStyle? _activePackButton;
    private static GUIStyle? _activeWithErrorsPackButton;
    private static GUIStyle? _disabledWithErrorsPackButton;

    private static GUIStyle? _logInfo;
    private static GUIStyle? _logWarn;
    private static GUIStyle? _logError;

    private static int _visibleLogElements = int.MinValue;
    private static int _hiddenLogElementsBefore = int.MinValue;
    private static int _hiddenLogElementsAfter = int.MinValue;

    private static bool _enabled;

    private static float _distanceFilterCached = 120;
    private static float _distanceFilter = 120;

    private static bool _useDistanceFilterCached;
    private static bool _useDistanceFilter;

    private static DebugMessageCategories _categoryCached = DebugMessageCategories.All;
    private static DebugMessageCategories _category = DebugMessageCategories.All;

    private static string _subcategoryCached = "";
    private static string _subcategory = "";

    private static bool _showErrorAndWarnsOnlyCached;
    private static bool _showErrorAndWarnsOnly;

    private static bool _latestMessagesOnlyCached = true;
    private static bool _latestMessagesOnly = true;

    private static bool _filterModdedAudioCached;
    private static bool _filterModdedAudio;

    private static string _logIncludeFilterCached = "";
    private static string _logIncludeFilter = "";

    private static Regex? _logIncludeFilterRegexToUse;

    private static bool _logIncludeFilterMatchWordCached;
    private static bool _logIncludeFilterMatchWord;

    private static bool _shouldClearLogs;

    private static int _totalErrors;
    private static int _totalWarnings;
    private static int _totalMessages;

    private static int _totalDisplayedErrors;
    private static int _totalDisplayedWarnings;
    private static int _totalDisplayedMessages;

    private static readonly ProfilerMarker _updateLogsMarker = new ProfilerMarker("ModAudio debug menu update");
    private static readonly ProfilerMarker _renderLogsMarker = new ProfilerMarker("ModAudio debug menu render");

    private static int _totalActiveAudioSources;
    private static int _totalInactiveAudioSources;
    private static DateTime _totalSourcesLastFetchedAt = DateTime.UnixEpoch;

    // Audio Log Field end
}
