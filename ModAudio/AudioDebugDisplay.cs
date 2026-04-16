using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using BepInEx.Logging;
using UnityEngine;

namespace Marioalexsan.ModAudio;

public enum DebugMessageCategories
{
    All = -1,
    Pack,
    Audio,
    Script,
    Engine
}

// General notes about this:
// The debug display deals with tons of log data, and as a result needs to be implemented efficiently
// Don't shy away from caching data if needed
public class AudioDebugDisplay : MonoBehaviour
{
    [Flags]
    public enum AudioLogFlags
    {
        None = 0,
        Routed = 1 << 0,
        Overlay = 1 << 1,
        Is2DSound = 1 << 2,
    }

    private struct MessageLog
    {
        public LogLevel Level;
        public DebugMessageCategories Category;
        public string Message;
        public AudioLogFlags Flags;
        public string? AudioGroup;
        public float AudioDistance; // Custom data
        public bool ShouldDisplayCached; // Cached information about whenever this should render during this frame


        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void SetFlag(AudioLogFlags flag) => Flags |= flag;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void ClearFlag(AudioLogFlags flag) => Flags &= ~flag;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool HasFlag(AudioLogFlags flag) => (Flags & flag) == flag; // Do not use Enum.HasFlag, it's a boxing operation and allocates junk

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void AssignFlag(AudioLogFlags flag, bool shouldBeSet)
        {
            if (shouldBeSet)
            {
                SetFlag(flag);
            }
            else
            {
                ClearFlag(flag);
            }
        }
    }

    // The actual capacity will be 9999 due to how the range is implemented; fixing this would overcomplicate the logic
    // since we'd have to differentiate between a full and empty buffer
    private static readonly MessageLog[] _circularBuffer = new MessageLog[10000];
    private static int _bufferStart; // Inclusive
    private static int _bufferEnd; // Exclusive

    public static void LogAudio(LogLevel level, string message, AudioLogFlags logFlags, string? audioGroup, float distance)
    {
        AudioDebugDisplay.Log(new MessageLog()
        {
            Category = DebugMessageCategories.Audio,
            Level = level,
            Message = message,
            Flags = logFlags,
            AudioGroup = audioGroup,
            AudioDistance = distance,
        });

        if (ModAudio.WriteAudioLogsToBepinexLog.Value)
            Logging.Log($"[Audio] {message}", level);
    }

    public static void LogPack(LogLevel level, AudioPack? pack, string message)
    {
        var extendedMessage = $"[{pack?.Config.DisplayName ?? "ModAudio"}] {message}";

        AudioDebugDisplay.Log(new MessageLog()
        {
            Category = DebugMessageCategories.Pack,
            Level = level,
            Message = extendedMessage,
        });

        if (ModAudio.WritePackLogsToBepinexLog.Value)
            Logging.Log($"[Pack] {extendedMessage}", level);
    }

    public static void LogScript(LogLevel level, AudioPack? script, string message)
    {
        var extendedMessage = $"[{script?.Config.DisplayName ?? "ModAudio"}] {message}";

        AudioDebugDisplay.Log(new MessageLog()
        {
            Category = DebugMessageCategories.Script,
            Level = level,
            Message = extendedMessage,
        });

        if (ModAudio.WriteScriptLogsToBepinexLog.Value)
            Logging.Log($"[Script] {extendedMessage}", level);
    }

    public static void LogEngine(LogLevel level, string message)
    {
        AudioDebugDisplay.Log(new MessageLog()
        {
            Category = DebugMessageCategories.Engine,
            Level = level,
            Message = message,
        });

        if (ModAudio.WriteEngineLogsToBepinexLog.Value)
            Logging.Log($"[Engine] {message}", level);
    }

    private static void Log(MessageLog entry)
    {
        entry.ShouldDisplayCached = ShouldDisplayLog(entry);

        _circularBuffer[_bufferEnd] = entry;

        _bufferEnd = (_bufferEnd + 1) % _circularBuffer.Length;
        if (_bufferEnd == _bufferStart)
            _bufferStart = (_bufferStart + 1) % _circularBuffer.Length;
    }

    public void Awake()
    {
        _nextFilter = _currentFilter = new FilterData(
            ShowErrorsAndWarnings: true,
            ShowInfo: true,
            ShowDebug: false,
            Show2DAudio: true,
            Show3DAudio: true,
            FilterByDistance: false,
            FilterByDistanceValue: 120,
            TextFilter: "",
            MatchWord: false,
            Category: DebugMessageCategories.All,
            Subcategory: "",
            LatestMessagesOnly: true,
            ShowModdedAudioOnly: false
        );
    }

    public void OnGUI()
    {
        if (!_enabled)
            return;

        GUI.backgroundColor = new Color(0, 0, 0, 1);
        GUI.color = new Color(1, 1, 1, 1);

        if (_background == null)
        {
            _background = new Texture2D(1, 1);
            _background.SetPixel(0, 0, new Color(0, 0, 0, ModAudio.DebugDisplayOpacity.Value / 100f));
            _background.Apply();
        }

        if (_windowStyle == null)
        {
            // Either I'm stupid, or Unity is
            _windowStyle = new GUIStyle(GUI.skin.window);
            _windowStyle.normal.background = _background;
            _windowStyle.normal.textColor = Color.white;
            _windowStyle.hover.background = _background;
            _windowStyle.hover.textColor = Color.white;
            _windowStyle.active.background = _background;
            _windowStyle.active.textColor = Color.white;
            _windowStyle.focused.background = _background;
            _windowStyle.focused.textColor = Color.white;
            _windowStyle.onNormal.background = _background;
            _windowStyle.onNormal.textColor = Color.white;
            _windowStyle.onHover.background = _background;
            _windowStyle.onHover.textColor = Color.white;
            _windowStyle.onActive.background = _background;
            _windowStyle.onActive.textColor = Color.white;
            _windowStyle.onFocused.background = _background;
            _windowStyle.onFocused.textColor = Color.white;
        }

        if (_disabledPackButton == null)
        {
            _disabledPackButton = new GUIStyle(GUI.skin.button);
            _disabledPackButton.normal.textColor = Color.gray;
        }

        if (_disabledWithErrorsPackButton == null)
        {
            _disabledWithErrorsPackButton = new GUIStyle(GUI.skin.button);
            _disabledWithErrorsPackButton.normal.textColor = Color.red * 0.5f + Color.gray * 0.5f;
        }

        if (_activePackButton == null)
        {
            _activePackButton = new GUIStyle(GUI.skin.button);
            _activePackButton.normal.textColor = Color.white;
        }

        if (_alignTextLeftButton == null)
        {
            _alignTextLeftButton = new GUIStyle(GUI.skin.button);
            _alignTextLeftButton.normal.textColor = Color.white;
            _alignTextLeftButton.alignment = TextAnchor.MiddleLeft;
        }

        if (_activeWithErrorsPackButton == null)
        {
            _activeWithErrorsPackButton = new GUIStyle(GUI.skin.button);
            _activeWithErrorsPackButton.normal.textColor = Color.red;
        }

        if (_logDebug == null)
        {
            _logDebug = new GUIStyle(GUI.skin.label);
            _logDebug.normal.textColor = Color.gray;
        }

        if (_logInfo == null)
        {
            _logInfo = new GUIStyle(GUI.skin.label);
            _logInfo.normal.textColor = Color.white;
        }

        if (_logWarn == null)
        {
            _logWarn = new GUIStyle(GUI.skin.label);
            _logWarn.normal.textColor = Color.yellow;
        }

        if (_logError == null)
        {
            _logError = new GUIStyle(GUI.skin.label);
            _logError.normal.textColor = Color.red;
        }

        if (_unbreakingText == null)
        {
            _unbreakingText = new GUIStyle(GUI.skin.label);
            _unbreakingText.wordWrap = false;
        }

        if (_rowHeight < 0)
            _rowHeight = GUI.skin.label.CalcSize(new GUIContent("Text")).y;

        _windowPos = GUILayout.Window(0, new Rect(_windowPos.position, new Vector2(WindowWidth, WindowHeight)), _ =>
        {
            _selectedTab = GUILayout.Toolbar(_selectedTab, _tabs);

            if (_selectedTab == 0)
                RenderAudioLogs();

            else if (_selectedTab == 1)
                RenderAudioPacks();

            else if (_selectedTab == 2)
                RenderAudioSourceTab();

            else if (_selectedTab == 3)
                RenderModAudioTab();

            GUI.DragWindow();
        }, "ModAudio", _windowStyle);
    }

    public void Update()
    {
        using var updateLogsMarker = Profiling.UpdateLogsMarker.Auto();

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
                UpdateAudioSourceTab();

            else if (_selectedTab == 3)
                UpdateModAudioTab();
        }
    }

    private void UpdateModAudioTab()
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

        _totalStreamedClips = 0;
        _totalInMemoryClips = 0;
        _totalQueuedClips = 0;
        _totalInMemoryBytes = 0;

        for (int i = 0; i < AudioEngine.AudioPacks.Count; i++)
        {
            var pack = AudioEngine.AudioPacks[i];

            _totalStreamedClips += pack.CurrentStreamedClips;
            _totalInMemoryClips += pack.CurrentInMemoryClips;
            _totalQueuedClips += pack.CurrentQueuedClips;

            foreach (var clip in pack.ReadyAudio.Values)
            {
                if (clip.Stream == null)
                    _totalInMemoryBytes += clip.Clip.channels * clip.Clip.samples * 4;
            }
        }
    }

    private void RenderModAudioTab()
    {
        GUILayout.Label($"[Note: this tab is not finished yet!]");
        if (GUILayout.Button("Hard reload audio packs"))
            ModAudio.ShouldHardReloadNextFrame = true;
        GUILayout.Label($"Unity audio sources: {_totalActiveAudioSources + _totalInactiveAudioSources}");
        GUILayout.Label($" Active: {_totalActiveAudioSources}");
        GUILayout.Label($" Inactive: {_totalInactiveAudioSources}");
        GUILayout.Space(_rowHeight);
        GUILayout.Label($"ModAudio tracked sources: {AudioEngine.TrackedSources.Count}");
        GUILayout.Label($" Play on awake sources: {AudioEngine.TrackedPlayOnAwakeSourceStates.Count}");
        GUILayout.Label($" One shot sources: {AudioEngine.TrackedOneShots.Count}");
        GUILayout.Space(_rowHeight);
        GUILayout.Label($"Custom clip stats:");
        GUILayout.Label($" Streamed: {_totalStreamedClips}");
        GUILayout.Label($" In-memory: {_totalInMemoryClips}");
        GUILayout.Label($"  Memory used (approx.): {_totalInMemoryBytes / 1024f / 1024f:F2} MiB");
        GUILayout.Label($" Queued: {_totalQueuedClips}");
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

        _audioPackScrollView = GUILayout.BeginScrollView(_audioPackScrollView, GUILayout.Width(WindowWidth * 0.3f));

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

        bool reloadRequired = false;

        if (0 <= _selectedPack && _selectedPack < AudioEngine.AudioPacks.Count)
        {
            var pack = AudioEngine.AudioPacks[_selectedPack];

            _audioPackDetailsScrollView = GUILayout.BeginScrollView(_audioPackDetailsScrollView);

            if (!string.IsNullOrWhiteSpace(pack.PackPath) && GUILayout.Button("Open pack location"))
                Application.OpenURL(new Uri($"{pack.PackPath}").AbsoluteUri);

            if (!pack.HasFlag(PackFlags.NotConfigurable) && GUILayout.Button("Enable / disable pack (Reloads engine!)"))
            {
                if (SoftDependencies.HasEasySettings())
                {
                    ModAudio.EasySettings_External_TogglePack(pack.Config.Id);
                }
                else
                {
                    if (ModAudio.AudioPackEnabled.TryGetValue(pack.Config.Id, out var entry))
                        entry.Value = !entry.Value;
                }

                reloadRequired = true;
            }

            GUILayout.Label($"Name: {pack.Config.DisplayName}");
            GUILayout.Label($"ID: {pack.Config.Id}");
            GUILayout.Label($"Path: {Utils.AliasRootPath(pack.PackPath)}");
            GUILayout.Space(_rowHeight);

            GUILayout.Label($"Route files: {pack.ConfigFiles.Count} loaded");
            for (int i = 0; i < pack.ConfigFiles.Count; i++)
                GUILayout.Label($"  {Utils.AliasRootPath(pack.ConfigFiles[i])}");

            GUILayout.Space(_rowHeight);

            GUILayout.Label($"Script files: {pack.ScriptFiles.Count} loaded");
            for (int i = 0; i < pack.ScriptFiles.Count; i++)
                GUILayout.Label($"  {Utils.AliasRootPath(pack.ScriptFiles[i])}");

            GUILayout.Space(_rowHeight);
            GUILayout.Label($"Pack flags: {pack.Flags}");

            if (pack.Flags.HasFlag(PackFlags.HasEncounteredErrors))
                GUILayout.Label("This pack encountered errors!", _logError);
            else
                GUILayout.Label("Pack is working correctly");

            GUILayout.Space(_rowHeight);

            GUILayout.Label($"Routes: {pack.Config.Routes.Count}");
            GUILayout.Label($"Custom clips: {pack.Config.CustomClips.Count}");
            GUILayout.Label($"  Ready: {pack.ReadyAudio.Count}");
            GUILayout.Label($"    Streaming: {pack.CurrentStreamedClips}");
            GUILayout.Label($"    In-memory: {pack.CurrentInMemoryClips}");
            GUILayout.Label($"  Queued: {pack.CurrentQueuedClips}");

            GUILayout.EndScrollView();
        }

        GUILayout.EndHorizontal();

        // TODO: This sucks
        if (reloadRequired)
            ModAudio.ApplyConfiguration();
    }

    private void UpdateAudioSourceTab()
    {
        if (DateTime.Now >= _nextCachedSourcesUpdate)
        {
            _nextCachedSourcesUpdate = DateTime.Now + TimeSpan.FromMilliseconds(1000);
            _cachedAudioSourceDisplay.Clear();
            _cachedAudioSourceDisplay.AddRange(
                AudioEngine.TrackedSources
                    .Select(x =>
                    {
                        return (GetFullPath(x.Key.gameObject), x.Key.gameObject.name, x.Value);
                    })
                    .OrderBy(x => x.name)
            );
        }
            
        _visibleAudioSourceElements = int.MinValue;
        _hiddenAudioSourceElementsBefore = int.MinValue;
        _hiddenAudioSourceElementsAfter = int.MinValue;
    }

    private void RenderAudioSourceTab()
    {
        GUILayout.BeginHorizontal();

        GUILayout.Label($"Audio sources: {_cachedAudioSourceDisplay.Count}");

        GUILayout.EndHorizontal();

        var totalButtons = _cachedAudioSourceDisplay.Count;
        var totalButtonHeight = _buttonHeight * totalButtons;
        
        GUILayout.BeginHorizontal();

        _audioSourcesScrollView = GUILayout.BeginScrollView(_audioSourcesScrollView, GUILayout.Width(WindowWidth * 0.5f));

        var scrollRatio = Math.Clamp(_audioSourcesScrollView.y / (totalButtonHeight - _audioSourcesScrollViewHeight), 0, 1);

        if (_visibleAudioSourceElements == int.MinValue)
        {
            _hiddenAudioSourceElementsBefore = Math.Clamp((int)(Mathf.Lerp(0, totalButtonHeight - _audioSourcesScrollViewHeight - _buttonHeight, scrollRatio) / _buttonHeight), 0, totalButtons);
            _hiddenAudioSourceElementsAfter = Math.Clamp((int)(Mathf.Lerp(totalButtonHeight - _audioSourcesScrollViewHeight - _buttonHeight, 0, scrollRatio) / _buttonHeight), 0, totalButtons);
            _visibleAudioSourceElements = totalButtons - _hiddenAudioSourceElementsAfter - _hiddenAudioSourceElementsBefore;
        }

        GUILayout.Space(_buttonHeight * _hiddenAudioSourceElementsBefore);

        for (int elementCount = _hiddenAudioSourceElementsBefore; elementCount < _hiddenAudioSourceElementsBefore + _visibleAudioSourceElements; elementCount++)
        {
            GUILayout.BeginHorizontal();
            
            if (GUILayout.Button(_cachedAudioSourceDisplay[elementCount].DisplayName, _alignTextLeftButton, GUILayout.Width(200)))
                _viewingThisSource = _cachedAudioSourceDisplay[elementCount].Source;

            if (elementCount == _hiddenAudioSourceElementsBefore)
            {
                if (Event.current.type == EventType.Repaint)
                    _buttonHeight = GUILayoutUtility.GetLastRect().height;
            }

            GUILayout.Label(_cachedAudioSourceDisplay[elementCount].Path, _unbreakingText);
            
            GUILayout.EndHorizontal();
        }

        if (_hiddenAudioSourceElementsAfter >= 1)
            GUILayout.Space(_buttonHeight * _hiddenAudioSourceElementsAfter);

        GUILayout.EndScrollView();

        if (Event.current.type == EventType.Repaint)
            _audioSourcesScrollViewHeight = GUILayoutUtility.GetLastRect().height;
        
        if (_viewingThisSource?.Audio != null)
        {
            var modSource = _viewingThisSource;
            var source = modSource.Audio;
            var path = GetFullPath(source.gameObject);

            _audioSourceDetailsScrollView = GUILayout.BeginScrollView(_audioSourceDetailsScrollView);

            var pos = source.transform.position;

            GUILayout.Label($"Unity stuff:");
            GUILayout.Label($"  Scene path: {path}");
            GUILayout.Label($"  Name: {source.name}");
            GUILayout.Label($"  Clip: {source.clip?.name ?? "(null)"}");
            GUILayout.Label($"  Volume: {source.volume:F2}");
            GUILayout.Label($"  Pitch: {source.pitch:F2}");
            GUILayout.Label($"  Looping: {source.loop}");
            GUILayout.Label($"  Is playing: {source.isPlaying}");
            GUILayout.Label($"  Audio group: {source.outputAudioMixerGroup?.name ?? "(null)"}");
            GUILayout.Label($"  Position in world (X, Y, Z) = ({pos.x:F2}, {pos.y:F2}, {pos.z:F2}) ");

            GUILayout.Space(_rowHeight);

            GUILayout.Label($"ModAudio specific stuff:");
            GUILayout.Label($"  Proxied volume: {modSource.ProxiedVolume:F2}");
            GUILayout.Label($"  Proxied pitch: {modSource.ProxiedPitch:F2}");
            GUILayout.Label($"  Flags: {modSource.Flags}");
            GUILayout.Label($"  Tracked instance ID: {modSource.TrackedInstanceId}");

            GUILayout.EndScrollView();
        }
        
        GUILayout.EndHorizontal();
    }

    private void UpdateAudioLogs()
    {
        // This is to make sure we render the same number of log elements within a frame
        // Also for performance reasons; we shouldn't recompute this every time Unity wants to repaint the UI

        _visibleLogElements = int.MinValue;
        _hiddenLogElementsBefore = int.MinValue;
        _hiddenLogElementsAfter = int.MinValue;

        bool filtersChanged = false;

        if (_currentFilter != _nextFilter)
        {
            _currentFilter = _nextFilter;
            filtersChanged = true;
        }

        if (_shouldClearLogs)
        {
            _shouldClearLogs = false;
            _bufferStart = _bufferEnd;
        }

        _totalMessages = _bufferStart <= _bufferEnd ? _bufferEnd - _bufferStart : _circularBuffer.Length - _bufferStart + _bufferEnd;
        _totalErrorsAndWarnings = 0;

        _totalDisplayedMessages = 0;
        _totalDisplayedErrorsAndWarnings = 0;

        if (filtersChanged)
        {
            _textFilterRegex = _currentFilter.MatchWord ? new Regex($"\\b{Regex.Escape(_currentFilter.TextFilter)}\\b", RegexOptions.IgnoreCase) : null;

            for (int i = _bufferStart; i != _bufferEnd; i = (i + 1) % _circularBuffer.Length)
            {
                var message = _circularBuffer[i];
                _circularBuffer[i] = message with { ShouldDisplayCached = ShouldDisplayLog(message) };
            }
        }

        // TODO: Likely don't need to recompute this every frame
        for (int i = _bufferStart; i != _bufferEnd; i = (i + 1) % _circularBuffer.Length)
        {
            var message = _circularBuffer[i];
            bool shouldDisplay = message.ShouldDisplayCached;

            if (shouldDisplay)
                _totalDisplayedMessages++;

            if (message.Level <= LogLevel.Warning)
            {
                _totalErrorsAndWarnings++;

                if (shouldDisplay)
                    _totalDisplayedErrorsAndWarnings++;
            }
        }
    }

    private void RenderAudioLogs()
    {
        using var renderLogsMarker = Profiling.RenderLogsMarker.Auto();

        GUILayout.BeginHorizontal();

        GUILayout.Label("Categories:");

        var category = _currentFilter.Category;
        var subcategory = _currentFilter.Subcategory;

        if (GUILayout.Toggle(category == DebugMessageCategories.All, "All"))
            category = DebugMessageCategories.All;

        if (GUILayout.Toggle(category == DebugMessageCategories.Pack, "Audio Packs"))
            category = DebugMessageCategories.Pack;

        if (GUILayout.Toggle(category == DebugMessageCategories.Audio, "Audio Sources"))
            category = DebugMessageCategories.Audio;

        if (GUILayout.Toggle(category == DebugMessageCategories.Script, "Scripts"))
            category = DebugMessageCategories.Script;

        if (GUILayout.Toggle(category == DebugMessageCategories.Engine, "Engine"))
            category = DebugMessageCategories.Engine;

        _nextFilter.Category = category;

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();

        if (category == DebugMessageCategories.Audio)
        {
            GUILayout.BeginHorizontal();

            GUILayout.Label("Audio categories:");

            // Note: the groups here have to match with what is added to the tags of logs *exactly*

            if (GUILayout.Toggle(subcategory.Length == 0, "All"))
                subcategory = "";

            if (GUILayout.Toggle(subcategory.Equals("Ambience"), "Ambience"))
                subcategory = "Ambience";

            if (GUILayout.Toggle(subcategory.Equals("Game"), "Game"))
                subcategory = "Game";

            if (GUILayout.Toggle(subcategory.Equals("GUI"), "GUI"))
                subcategory = "GUI";

            if (GUILayout.Toggle(subcategory.Equals("Music"), "Music"))
                subcategory = "Music";

            if (GUILayout.Toggle(subcategory.Equals("Voice"), "Voice"))
                subcategory = "Voice";

            _nextFilter.Subcategory = subcategory;

            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();

            _nextFilter.ShowModdedAudioOnly = GUILayout.Toggle(_currentFilter.ShowModdedAudioOnly, "Custom audio only");
            _nextFilter.Show2DAudio = GUILayout.Toggle(_currentFilter.Show2DAudio, $"2D (UI)");
            _nextFilter.Show3DAudio = GUILayout.Toggle(_currentFilter.Show3DAudio, $"3D (World)");
            _nextFilter.FilterByDistance = GUILayout.Toggle(_currentFilter.FilterByDistance, $"Filter 3D audio by distance: ({_currentFilter.FilterByDistanceValue:F2})");
            _nextFilter.FilterByDistanceValue = GUILayout.HorizontalSlider(_currentFilter.FilterByDistanceValue, 0f, 1000f, GUILayout.MaxWidth(10000));

            GUILayout.EndHorizontal();
        }

        // TODO: This doesn't take into account lines that break onto multiple lines!
        // TODO: A better solution is needed, but performance might get degraded from this.
        var totalRowHeight = _rowHeight * _totalDisplayedMessages;

        if (_currentFilter.LatestMessagesOnly)
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
                GUILayout.Label(log.Message, MapLogLevelToStyle(log.Level));
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
        _nextFilter.TextFilter = GUILayout.TextField(_currentFilter.TextFilter, 80, GUILayout.ExpandWidth(true));
        _nextFilter.MatchWord = GUILayout.Toggle(_currentFilter.MatchWord, "Match word", GUILayout.ExpandWidth(true));

        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();

        GUILayout.Label($"Total: {_totalDisplayedMessages}/{_totalMessages}");
        GUILayout.Label($"Issues: {_totalDisplayedErrorsAndWarnings}/{_totalErrorsAndWarnings}");

        if (GUILayout.Button("Clear messages", GUILayout.ExpandWidth(false)))
            _shouldClearLogs = true;

        _nextFilter.LatestMessagesOnly = GUILayout.Toggle(_currentFilter.LatestMessagesOnly, "Follow log");
        _nextFilter.ShowErrorsAndWarnings = GUILayout.Toggle(_currentFilter.ShowErrorsAndWarnings, "Errors and warnings");
        _nextFilter.ShowInfo = GUILayout.Toggle(_currentFilter.ShowInfo, "Info");
        _nextFilter.ShowDebug = GUILayout.Toggle(_currentFilter.ShowDebug, "Debug");

        GUILayout.FlexibleSpace();
        GUILayout.EndHorizontal();
    }

    private static bool ShouldDisplayLog(MessageLog log)
    {
        // Note: this method is in the hot path for anything related to logging
        // It's important to implement things efficiently in here - this includes mundane stuff such as string comparisons

        if (log.Level <= LogLevel.Warning && !_currentFilter.ShowErrorsAndWarnings)
            return false;

        if (log.Level == LogLevel.Info && !_currentFilter.ShowInfo)
            return false;

        if (log.Level == LogLevel.Debug && !_currentFilter.ShowDebug)
            return false;

        if (log.HasFlag(AudioLogFlags.Is2DSound) && !_currentFilter.Show2DAudio)
            return false;

        if (!log.HasFlag(AudioLogFlags.Is2DSound) && !_currentFilter.Show3DAudio)
            return false;

        bool shouldShowCategory = _currentFilter.Category == DebugMessageCategories.All || log.Category == _currentFilter.Category;

        if (!shouldShowCategory)
            return false;

        if (log.Category == DebugMessageCategories.Audio && _currentFilter.FilterByDistance && log.AudioDistance >= _currentFilter.FilterByDistanceValue)
            return false;

        if (log.Category == DebugMessageCategories.Audio)
        {
            if (_currentFilter.Subcategory != "" && !string.Equals(_currentFilter.Subcategory, log.AudioGroup, StringComparison.OrdinalIgnoreCase))
                return false;

            if (_currentFilter.ShowModdedAudioOnly && !log.HasFlag(AudioLogFlags.Routed) && !log.HasFlag(AudioLogFlags.Overlay))
                return false;
        }

        if (_currentFilter.TextFilter != "")
        {
            if (!_currentFilter.MatchWord)
            {
                if (!log.Message.Contains(_currentFilter.TextFilter, StringComparison.InvariantCultureIgnoreCase))
                    return false;
            }
            else
            {
                if (_textFilterRegex == null || !_textFilterRegex.Match(log.Message).Success)
                    return false;
            }
        }

        return true;
    }

    private string MapLogLevelToLabel(LogLevel level) => level switch
    {
        // Padded to 6 characters
        LogLevel.Info => "INFO  ",
        LogLevel.Debug => "DEBUG ",
        LogLevel.Warning => "WARN  ",
        LogLevel.Error => "ERROR ",
        LogLevel.Fatal => "FATAL ",
        _ => "OTHER "
    };

    private GUIStyle? MapLogLevelToStyle(LogLevel level) => level switch
    {
        LogLevel.Info => _logInfo,
        LogLevel.Debug => _logDebug,
        LogLevel.Warning => _logWarn,
        LogLevel.Error => _logError,
        LogLevel.Fatal => _logError,
        _ => _logInfo
    };

    private static float WindowWidth => Screen.width * (ModAudio.DebugDisplayWidthPct.Value / 100f);
    private static float WindowHeight => Screen.height * (ModAudio.DebugDisplayHeightPct.Value / 100f);
    private static Rect _windowPos = new Rect(
        Screen.width * 0.05f, 
        Screen.height * 0.05f, 
        WindowWidth, 
        WindowHeight
    );

    private readonly string[] _tabs = ["Audio logs", "Audio packs", "Audio sources", "ModAudio Stats"]; // TODO: Audio sources tab
    private static int _selectedTab;

    // Audio Packs start

    private static Vector2 _audioPackScrollView;
    private static Vector2 _audioPackDetailsScrollView;
    private static int _selectedPack = -1;

    // Audio Packs end

    // Audio Sources start

    private static ModAudioSource? _viewingThisSource;
    private static List<(string Path, string DisplayName, ModAudioSource Source)> _cachedAudioSourceDisplay = [];
    private DateTime _nextCachedSourcesUpdate = DateTime.Now;
    private static float _buttonHeight = 10;
    
    private static Vector2 _audioSourcesScrollView;
    private static Vector2 _audioSourceDetailsScrollView;
    private static float _audioSourcesScrollViewHeight = 100;

    private static int _visibleAudioSourceElements = int.MinValue;
    private static int _hiddenAudioSourceElementsBefore = int.MinValue;
    private static int _hiddenAudioSourceElementsAfter = int.MinValue;

    // Audio Sources end

    // Audio Log Field start

    private static Vector2 _logScrollView;
    private static float _logScrollViewHeight = 100;

    private static float _rowHeight = -1;

    private static Texture2D? _background;
    private static GUIStyle? _windowStyle;
    private static GUIStyle? _disabledPackButton;
    private static GUIStyle? _activePackButton;
    private static GUIStyle? _alignTextLeftButton;
    private static GUIStyle? _activeWithErrorsPackButton;
    private static GUIStyle? _disabledWithErrorsPackButton;

    private static GUIStyle? _logDebug;
    private static GUIStyle? _logInfo;
    private static GUIStyle? _logWarn;
    private static GUIStyle? _logError;
    
    private static GUIStyle? _unbreakingText;

    private static int _visibleLogElements = int.MinValue;
    private static int _hiddenLogElementsBefore = int.MinValue;
    private static int _hiddenLogElementsAfter = int.MinValue;

    private static bool _enabled;

    private static Regex? _textFilterRegex;

    private static bool _shouldClearLogs;

    private static FilterData _nextFilter;
    private static FilterData _currentFilter; // Do not write to properties on this

    private record struct FilterData(
        bool ShowErrorsAndWarnings,
        bool ShowInfo,
        bool ShowDebug,
        bool Show2DAudio,
        bool Show3DAudio,
        bool FilterByDistance,
        float FilterByDistanceValue,
        string TextFilter,
        bool MatchWord,
        DebugMessageCategories Category,
        string Subcategory,
        bool LatestMessagesOnly,
        bool ShowModdedAudioOnly
    );

    private static int _totalMessages;
    private static int _totalErrorsAndWarnings;

    private static int _totalDisplayedMessages;
    private static int _totalDisplayedErrorsAndWarnings;

    private static int _totalActiveAudioSources;
    private static int _totalInactiveAudioSources;
    private static int _totalStreamedClips;
    private static int _totalInMemoryClips;
    private static int _totalQueuedClips;
    private static int _totalInMemoryBytes;
    private static DateTime _totalSourcesLastFetchedAt = DateTime.UnixEpoch;

    private static StringBuilder _cachedPathBuilder = new(128);

    public static string GetFullPath(GameObject obj)
    {
        _cachedPathBuilder.Clear();

        static void BuildPath(Transform transform)
        {
            if (transform.parent != null)
            {
                BuildPath(transform.parent);
                _cachedPathBuilder.Append(" / ").Append(transform.name);
            }
            else _cachedPathBuilder.Append(transform.name);
        }

        BuildPath(obj.transform);

        return _cachedPathBuilder.ToString();
    }
    // Audio Log Field end
}