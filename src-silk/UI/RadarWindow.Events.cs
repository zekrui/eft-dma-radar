using eft_dma_radar.Silk.UI.Panels;
using eft_dma_radar.Silk.UI.Widgets;
using Silk.NET.Maths;
using Silk.NET.Windowing;

namespace eft_dma_radar.Silk.UI
{
    internal static partial class RadarWindow
    {
        /// <summary>True while the radar window is in fullscreen (F11).</summary>
        internal static bool IsFullscreen => _isFullscreen;

        /// <summary>
        /// Put the radar window into fullscreen, or restore the geometry it had beforehand.
        /// Bound to F11; Escape also exits fullscreen.
        /// </summary>
        internal static void SetFullscreen(bool fullscreen)
        {
            if (_window is null || fullscreen == _isFullscreen)
                return;

            if (fullscreen)
            {
                _preFullscreenState = _window.WindowState;
                _preFullscreenSize = _window.Size;
                _preFullscreenPosition = _window.Position;
                _preFullscreenHasPosition = true;
                _window.WindowState = WindowState.Fullscreen;
                _isFullscreen = true;
            }
            else
            {
                _window.WindowState = _preFullscreenState == WindowState.Fullscreen
                    ? WindowState.Normal
                    : _preFullscreenState;

                if (_window.WindowState == WindowState.Normal)
                {
                    if (_preFullscreenSize.X > 0 && _preFullscreenSize.Y > 0)
                        _window.Size = _preFullscreenSize;
                    if (_preFullscreenHasPosition)
                        _window.Position = _preFullscreenPosition;
                }

                _isFullscreen = false;
            }
        }

        /// <summary>Flip fullscreen on/off.</summary>
        internal static void ToggleFullscreen() => SetFullscreen(!_isFullscreen);

        private static void OnResize(Vector2D<int> size)
        {
            _gl.Viewport(size);
            CreateSkiaSurface();
        }

        private static void OnClosing()
        {
            // Persist window state
            // While fullscreen the live geometry is the monitor's, so persist the windowed values instead
            var savedSize = _isFullscreen ? _preFullscreenSize : _window.Size;
            var savedState = _isFullscreen ? _preFullscreenState : _window.WindowState;
            Config.WindowWidth = savedSize.X;
            Config.WindowHeight = savedSize.Y;
            Config.WindowMaximized = savedState == WindowState.Maximized;
            Config.WindowFullscreen = _isFullscreen;

            // Persist widget/panel visibility
            Config.ShowPlayersWidget = PlayerInfoWidget.IsOpen;
            Config.ShowLootWidget = LootWidget.IsOpen;
            Config.ShowAimviewWidget = AimviewWidget.IsOpen;
            Config.ShowSettingsOverlay = SettingsPanel.IsOpen;
            Config.ShowLootFiltersPanel = LootFiltersPanel.IsOpen;
            Config.ShowHotkeyPanel = HotkeyManagerPanel.IsOpen;
            Config.ShowHideoutPanel = HideoutPanel.IsOpen;
            Config.ShowQuestPanel = QuestPanel.IsOpen;
            Config.ShowQuestPlannerPanel = QuestPlannerPanel.IsOpen;
            Config.ShowPlayerHistoryPanel = PlayerHistoryPanel.IsOpen;
            Config.ShowPlayerWatchlistPanel = PlayerWatchlistPanel.IsOpen;
            Config.ShowEspWidget = EspWindow.IsOpen;

            Config.Save();

            // Close ESP window if open
            EspWindow.Close();

            // Signal the memory worker to stop cleanly before we release GPU resources
            Memory.Close();

            // Dispose GPU/UI resources
            _fpsTimer.Dispose();
            _imgui?.Dispose();
            if (_imguiFontHandle.IsAllocated)
                _imguiFontHandle.Free();
            if (_iconGlyphRangesHandle.IsAllocated)
                _iconGlyphRangesHandle.Free();
            _skSurface?.Dispose();
            _lastInRaidSnapshot?.Dispose();
            _lastInRaidSnapshot = null;
            _skBackendRenderTarget?.Dispose();
            _grContext?.Dispose();
            _input?.Dispose();

            Log.WriteLine("[RadarWindow] Closed.");
        }

        private static async Task RunFpsTimerAsync()
        {
            try
            {
                while (await _fpsTimer.WaitForNextTickAsync())
                {
                    _fps = Interlocked.Exchange(ref _fpsCounter, 0);
                }
            }
            catch (ObjectDisposedException) { }
        }
    }
}
