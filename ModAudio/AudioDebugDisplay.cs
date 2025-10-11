using BepInEx.Logging;
using Unity.Profiling;
using UnityEngine;

namespace Marioalexsan.ModAudio;

public enum DebugMessageCategories
{
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
        public bool FilterChecked; // Whenever the filter has been applied to this already
    }

    private static readonly List<MessageLog> _messages = new List<MessageLog>(16384);

    public static void LogPack(LogLevel level, string message, string tags = "")
    {
        _messages.Add(new()
        {
            Level = level,
            Category = DebugMessageCategories.AudioPack,
            Message = message,
            Tags = tags
        });
    }

    public static void LogScript(LogLevel level, string message, string tags = "")
    {
        _messages.Add(new()
        {
            Level = level,
            Category = DebugMessageCategories.Script,
            Message = message,
            Tags = tags
        });
    }

    public static void LogAudio(LogLevel level, string message, string tags = "", float extraParam1 = default)
    {
        _messages.Add(new()
        {
            Level = level,
            Category = DebugMessageCategories.AudioSource,
            Message = message,
            Tags = tags,
            ExtraParam1 = extraParam1
        });
    }

    public static void LogEngine(LogLevel level, string message, string tags = "")
    {
        _messages.Add(new()
        {
            Level = level,
            Category = DebugMessageCategories.Engine,
            Message = message,
            Tags = tags
        });
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

        _windowPos = GUILayout.Window(0, new Rect(_windowPos.position, new Vector2(Screen.width * 0.5f, Screen.height * 0.5f)), (int windowId) =>
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

        if (ModAudio.Plugin.DebugMenuButton.Value != KeyCode.None)
        {
            if (Input.GetKeyDown(ModAudio.Plugin.DebugMenuButton.Value))
                _enabled = !_enabled;
        }

        if (_messages.Count >= 10000)
        {
            _messages.RemoveRange(0, 5000);
            LogEngine(LogLevel.Warning, $"There are more than 10000 messages in the log! Removing oldest 5000 messages to reclaim memory.");
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

            GUILayout.Label($"Display name: {pack.Config.DisplayName}");
            GUILayout.Label($"ID: {pack.Config.Id}");
            GUILayout.Label($"Path: {pack.PackPath}");
            GUILayout.Space(_rowHeight);
            GUILayout.Label($"All flag values: {pack.Flags}");

            if (pack.Flags.HasFlag(PackFlags.HasEncounteredErrors))
                GUILayout.Label("This pack encountered errors!", _logError);
            else
                GUILayout.Label("Pack is working correctly");

            GUILayout.Space(_rowHeight);

            // TODO This is hacky; this should be reimplemented properly on the audio pack
            GUILayout.Label($"Total custom clips: {pack.Config.CustomClips.Count}");
            GUILayout.Label($"Clips streamed: {pack.OpenStreams.Count}");
            GUILayout.Label($"Clips loaded in memory: {pack.ReadyClips.Count - pack.OpenStreams.Count}");
            GUILayout.Label($"Clips waiting to be streamed: {pack.PendingClipsToStream.Count}");
            GUILayout.Label($"Clips waiting to be loaded: {pack.PendingClipsToLoad.Count}");

            GUILayout.Space(_rowHeight);

            GUILayout.Label($"Total routes: {pack.Config.Routes.Count}");

            GUILayout.Space(_rowHeight);

            GUILayout.Label($"Lua script loaded: {(pack.Script != null ? "True" : "False")}");

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
        UpdateCachedValue(ref _showAudioPackMessagesCached, _showAudioPackMessages, ref filtersChanged);
        UpdateCachedValue(ref _showAudioSourceMessagesCached, _showAudioSourceMessages, ref filtersChanged);
        UpdateCachedValue(ref _showScriptMessagesCached, _showScriptMessages, ref filtersChanged);
        UpdateCachedValue(ref _showEngineMessagesCached, _showEngineMessages, ref filtersChanged);
        UpdateCachedValue(ref _latestMessagesOnlyCached, _latestMessagesOnly, ref filtersChanged);
        UpdateCachedValue(ref _showErrorAndWarnsOnlyCached, _showErrorAndWarnsOnly, ref filtersChanged);
        UpdateCachedValue(ref _audioGroupFilterCached, _audioGroupFilter, ref filtersChanged);
        UpdateCachedValue(ref _filterModdedAudioCached, _filterModdedAudio, ref filtersChanged);
        UpdateCachedValue(ref _logIncludeFilterCached, _logIncludeFilter, ref filtersChanged);

        if (_shouldClearLogs)
        {
            _shouldClearLogs = false;
            _messages.Clear();
        }

        _totalMessages = 0;
        _totalErrors = 0;
        _totalWarnings = 0;

        _totalDisplayedMessages = 0;
        _totalDisplayedErrors = 0;
        _totalDisplayedWarnings = 0;

        for (int i = 0; i < _messages.Count; i++)
        {
            var message = _messages[i];

            bool shouldDisplay;

            if (filtersChanged || !_messages[i].FilterChecked)
            {
                shouldDisplay = ShouldDisplayLog(message);
                _messages[i] = _messages[i] with { ShouldDisplayCached = shouldDisplay, FilterChecked = true };
            }
            else
            {
                shouldDisplay = _messages[i].ShouldDisplayCached;
            }

            _totalMessages++;

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

        GUILayout.Label("Options:");

        _latestMessagesOnly = GUILayout.Toggle(_latestMessagesOnly, "Auto scroll to latest");
        _showErrorAndWarnsOnly = GUILayout.Toggle(_showErrorAndWarnsOnly, "Show only errors and warnings");
        _filterModdedAudio = GUILayout.Toggle(_filterModdedAudio, "Show only custom audio");

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        GUILayout.Label("Categories:");

        _showAudioPackMessages = GUILayout.Toggle(_showAudioPackMessages, "Audio Packs");
        _showAudioSourceMessages = GUILayout.Toggle(_showAudioSourceMessages, "Audio Sources");
        _showScriptMessages = GUILayout.Toggle(_showScriptMessages, "Scripts");
        _showEngineMessages = GUILayout.Toggle(_showEngineMessages, "Engine");

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        GUILayout.Label("Search for text:", GUILayout.ExpandWidth(false));
        _logIncludeFilter = GUILayout.TextField(_logIncludeFilter, 80, GUILayout.ExpandWidth(true));

        GUILayout.EndHorizontal();

        if (_showAudioSourceMessagesCached)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("Audio categories:");

            // Note: the groups here have to match with what is added to the tags of logs *exactly*

            if (GUILayout.Toggle(_audioGroupFilter.Length == 0, "All"))
                _audioGroupFilter = "";

            if (GUILayout.Toggle(_audioGroupFilter.Equals("AudGrp ambience"), "Ambience"))
                _audioGroupFilter = "AudGrp ambience";

            if (GUILayout.Toggle(_audioGroupFilter.Equals("AudGrp game"), "Game"))
                _audioGroupFilter = "AudGrp game";

            if (GUILayout.Toggle(_audioGroupFilter.Equals("AudGrp gui"), "GUI"))
                _audioGroupFilter = "AudGrp gui";

            if (GUILayout.Toggle(_audioGroupFilter.Equals("AudGrp music"), "Music"))
                _audioGroupFilter = "AudGrp music";

            if (GUILayout.Toggle(_audioGroupFilter.Equals("AudGrp voice"), "Voice"))
                _audioGroupFilter = "AudGrp voice";

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            _useDistanceFilter = GUILayout.Toggle(_useDistanceFilter, $"Filter audio by distance from player ({_distanceFilter:F2})");
            _distanceFilter = GUILayout.HorizontalSlider(_distanceFilter, 0f, 1000f, GUILayout.MaxWidth(10000));

            GUILayout.EndHorizontal();
        }

        if (GUILayout.Button("Clear messages", GUILayout.ExpandWidth(false)))
            _shouldClearLogs = true;

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

        int cursor = 0;

        for (int i = 0; i < _hiddenLogElementsBefore + _visibleLogElements; i++)
        {
            var log = _messages[cursor];

            while (!log.ShouldDisplayCached)
                log = _messages[++cursor];

            if (i >= _hiddenLogElementsBefore)
            {
                GUILayout.BeginHorizontal();
                GUILayout.Label(MapLogLevelToLabel(log.Level), MapLogLevelToStyle(log.Level));
                GUILayout.Label(log.Message);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
            }

            cursor++;
        }

        if (_hiddenLogElementsAfter >= 1)
            GUILayout.Space(_rowHeight * _hiddenLogElementsAfter);

        GUILayout.EndScrollView();

        if (Event.current.type == EventType.Repaint)
            _logScrollViewHeight = GUILayoutUtility.GetLastRect().height;

        GUILayout.BeginHorizontal();

        GUILayout.Label($"Total: {_totalDisplayedMessages}/{_totalMessages}");
        GUILayout.Label($"Errors: {_totalDisplayedErrors}/{_totalErrors}");
        GUILayout.Label($"Warnings: {_totalDisplayedWarnings}/{_totalWarnings}");

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        GUI.DragWindow();
        _renderLogsMarker.End();
    }

    private bool ShouldDisplayLog(MessageLog log)
    {
        // Note: this method is in the hot path for anything related to logging
        // It's important to implement things efficiently in here - this includes mundane stuff such as string comparisons

        if (log.Level != LogLevel.Error && log.Level != LogLevel.Warning && _showErrorAndWarnsOnlyCached)
            return false;

        bool shouldShowCategory = log.Category switch
        {
            DebugMessageCategories.AudioPack => _showAudioPackMessagesCached,
            DebugMessageCategories.AudioSource => _showAudioSourceMessagesCached,
            DebugMessageCategories.Script => _showScriptMessagesCached,
            DebugMessageCategories.Engine => _showEngineMessagesCached,
            _ => false
        };

        if (!shouldShowCategory)
            return false;

        if (log.Category == DebugMessageCategories.AudioSource && _useDistanceFilterCached && log.ExtraParam1 >= _distanceFilterCached)
            return false;

        if (log.Category == DebugMessageCategories.AudioSource)
        {
            if (_audioGroupFilterCached != "" && !log.Tags.Contains(_audioGroupFilterCached))
                return false;

            if (_filterModdedAudioCached && !log.Tags.Contains("Routed") && !log.Tags.Contains("Overlay"))
                return false;
        }

        if (_logIncludeFilterCached != "" && !log.Message.Contains(_logIncludeFilterCached, StringComparison.InvariantCultureIgnoreCase))
            return false;

        return true;
    }

    private string MapLogLevelToLabel(LogLevel level) => level switch
    {
        LogLevel.Info => "INFO ",
        LogLevel.Debug => "DEBUG",
        LogLevel.Warning => "WARN ",
        LogLevel.Error => "ERROR",
        LogLevel.Fatal => "FATAL",
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
    private static int _selectedTab = 0;

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

    private static bool _enabled = false;

    private static float _distanceFilterCached = 120;
    private static float _distanceFilter = 120;

    private static bool _useDistanceFilterCached = false;
    private static bool _useDistanceFilter = false;

    private static bool _showAudioPackMessagesCached = true;
    private static bool _showAudioPackMessages = true;

    private static bool _showAudioSourceMessagesCached = true;
    private static bool _showAudioSourceMessages = true;

    private static bool _showScriptMessagesCached = true;
    private static bool _showScriptMessages = true;

    private static bool _showEngineMessagesCached = true;
    private static bool _showEngineMessages = true;

    private static bool _showErrorAndWarnsOnlyCached = false;
    private static bool _showErrorAndWarnsOnly = false;

    private static bool _latestMessagesOnlyCached = false;
    private static bool _latestMessagesOnly = false;

    private static bool _filterModdedAudioCached = false;
    private static bool _filterModdedAudio = false;

    private static string _audioGroupFilterCached = "";
    private static string _audioGroupFilter = "";

    private static string _logIncludeFilter = "";
    private static string _logIncludeFilterCached = "";

    private static bool _shouldClearLogs = false;

    private static int _totalErrors = 0;
    private static int _totalWarnings = 0;
    private static int _totalMessages = 0;

    private static int _totalDisplayedErrors = 0;
    private static int _totalDisplayedWarnings = 0;
    private static int _totalDisplayedMessages = 0;

    private static readonly ProfilerMarker _updateLogsMarker = new ProfilerMarker("ModAudio debug menu update");
    private static readonly ProfilerMarker _renderLogsMarker = new ProfilerMarker("ModAudio debug menu render");

    private static int _totalActiveAudioSources;
    private static int _totalInactiveAudioSources;
    private static DateTime _totalSourcesLastFetchedAt = DateTime.UnixEpoch;

    // Audio Log Field end
}
