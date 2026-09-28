using EpraConstructor.Devices;
using EpraConstructor.Modbus;
using EpraConstructor.Services;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EpraConstructor
{
    public partial class MainWindow : Window
    {
        private bool _isInitialized = false;
        private int _deviceCounter = 0;
        private const int MaxDevices = 247;
        private List<EpraWindow> _deviceCards = new List<EpraWindow>();
        private ModbusManager _modbusManager;
        private const int AnimationDurationMs = 220;
        private double _savedWidth;
        private double _savedHeight;
        private WindowState _savedWindowState;

        // ─── DRAG & DROP ───
        private EpraWindow _draggedCard = null;
        private EpraWindow _lastHighlightedCard = null;

        // ─── АВТОСКРОЛЛ ВО ВРЕМЯ DRAG ───
        private DispatcherTimer _autoScrollTimer;
        private double _autoScrollDelta = 0;
        private const double AutoScrollZone = 60.0;
        private const double AutoScrollMaxStep = 18.0;

        // ─── СОСТОЯНИЕ ───
        private AppState _appState;
        private bool _loadingState = false;

        // Тип текущей цели
        private enum DropKind { None, Swap, InsertBefore, InsertAfter }

        private struct DropTarget
        {
            public DropKind Kind;
            public EpraWindow Card;
            public int Index;
        }

        // ═══════════════════════════════════════════════════
        // ТЕМА
        // ═══════════════════════════════════════════════════

        public enum AppTheme { Dark, Light }

        private AppTheme _currentTheme = AppTheme.Dark;

        private void BtnThemeDark_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTheme == AppTheme.Dark) return;
            ApplyTheme(AppTheme.Dark);
        }

        private void BtnThemeLight_Click(object sender, RoutedEventArgs e)
        {
            if (_currentTheme == AppTheme.Light) return;
            ApplyTheme(AppTheme.Light);
        }

        private void ApplyTheme(AppTheme theme)
        {
            var r = Application.Current.Resources;

            if (theme == AppTheme.Dark)
            {
                // Фоны
                r["BgBrush"] = new SolidColorBrush(Color.FromRgb(0x07, 0x07, 0x09));
                r["BgPanelBrush"] = new SolidColorBrush(Color.FromRgb(0x0D, 0x0D, 0x12));
                r["BgCardBrush"] = new SolidColorBrush(Color.FromRgb(0x10, 0x10, 0x1A));
                r["BgInputBrush"] = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0F));
                r["BgPopupBrush"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x0F, 0x18));
                r["BorderDimBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x30));
                r["BorderMidBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x40));

                // Акценты
                r["NeonGreenBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88));
                r["NeonCyanBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));
                r["NeonRedBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x40));
                r["NeonYellowBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xDD, 0x00));
                r["NeonOrangeBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x95, 0x00));
                r["NeonWhiteBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE8, 0xFF));

                // Приглушённые
                r["DimCyanBrush"] = new SolidColorBrush(Color.FromRgb(0x55, 0x99, 0xBB));
                r["DimGreenBrush"] = new SolidColorBrush(Color.FromRgb(0x33, 0x77, 0x55));
                r["DimTextBrush"] = new SolidColorBrush(Color.FromRgb(0x99, 0xAA, 0xBB));
                r["FgBrush"] = new SolidColorBrush(Color.FromRgb(0xD8, 0xE8, 0xF8));

                // Спец
                r["ErrorBtnBgBrush"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x0A, 0x10));
                r["ErrorBtnBgBadBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x00, 0x10));

                // Hover / pressed
                r["HoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x30));
                r["PressedBgBrush"] = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0F));
                r["BtnGreenHoverBrush"] = new SolidColorBrush(Color.FromRgb(0x15, 0x3D, 0x26));
                r["BtnCyanHoverBrush"] = new SolidColorBrush(Color.FromRgb(0x15, 0x35, 0x45));
                r["BtnRedHoverBrush"] = new SolidColorBrush(Color.FromRgb(0x3D, 0x10, 0x18));
                r["BtnYellowHoverBrush"] = new SolidColorBrush(Color.FromRgb(0x3D, 0x33, 0x00));
                r["CaptionBtnHoverBrush"] = new SolidColorBrush(Color.FromRgb(0x1E, 0x1E, 0x30));
                r["CaptionBtnPressedBrush"] = new SolidColorBrush(Color.FromRgb(0x0A, 0x0A, 0x0F));

                // Статусы
                r["BadgeGreenBgBrush"] = new SolidColorBrush(Color.FromRgb(0x0F, 0x2A, 0x10));
                r["BadgeStatusBgBrush"] = new SolidColorBrush(Color.FromRgb(0x05, 0x1A, 0x10));
                r["BadgeStatusBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x22, 0x66, 0x44));
                r["StatusOnlineBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0xAA, 0x55));
                r["ServiceBadgeBgBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x10, 0x00));
                r["SaveOverlayBgBrush"] = new SolidColorBrush(Color.FromArgb(0xCC, 0, 0, 0));

                // Start mode
                r["StartMode7BgBrush"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x0A, 0x10));
                r["StartMode7BorderBrush"] = new SolidColorBrush(Color.FromRgb(0x44, 0x00, 0x20));
                r["StartMode7HoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x10, 0x20));
                r["StartMode7HoverBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x40));

                r["StartMode4BgBrush"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x1A, 0x08));
                r["StartMode4BorderBrush"] = new SolidColorBrush(Color.FromRgb(0x55, 0x55, 0x00));
                r["StartMode4HoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0x2A, 0x2A, 0x10));
                r["StartMode4HoverBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xDD, 0x00));

                r["StartMode1BgBrush"] = new SolidColorBrush(Color.FromRgb(0x0A, 0x1A, 0x08));
                r["StartMode1BorderBrush"] = new SolidColorBrush(Color.FromRgb(0x22, 0x44, 0x22));
                r["StartMode1HoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0x10, 0x2A, 0x18));
                r["StartMode1HoverBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88));

                r["StartMode0BgBrush"] = new SolidColorBrush(Color.FromRgb(0x0A, 0x18, 0x20));
                r["StartMode0BorderBrush"] = new SolidColorBrush(Color.FromRgb(0x22, 0x44, 0x66));
                r["StartMode0HoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0x10, 0x28, 0x38));
                r["StartMode0HoverBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0xE5, 0xFF));

                if (TxtStatusMsg != null) TxtStatusMsg.Text = "Тема: тёмная";
            }
            else // Light
            {
                // Фоны
                r["BgBrush"] = new SolidColorBrush(Color.FromRgb(0xF2, 0xF4, 0xFA));
                r["BgPanelBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                r["BgCardBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                r["BgInputBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xFA, 0xFF));
                r["BgPopupBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xFF));
                r["BorderDimBrush"] = new SolidColorBrush(Color.FromRgb(0xDC, 0xE0, 0xEC));
                r["BorderMidBrush"] = new SolidColorBrush(Color.FromRgb(0xC4, 0xC8, 0xD6));

                // Акценты
                r["NeonGreenBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0xA0, 0x50));
                r["NeonCyanBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0xB8));
                r["NeonRedBrush"] = new SolidColorBrush(Color.FromRgb(0xD0, 0x00, 0x30));
                r["NeonYellowBrush"] = new SolidColorBrush(Color.FromRgb(0xB0, 0x80, 0x00));
                r["NeonOrangeBrush"] = new SolidColorBrush(Color.FromRgb(0xD0, 0x60, 0x00));
                r["NeonWhiteBrush"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x20, 0x30));

                // Приглушённые
                r["DimCyanBrush"] = new SolidColorBrush(Color.FromRgb(0x40, 0x70, 0x88));
                r["DimGreenBrush"] = new SolidColorBrush(Color.FromRgb(0x20, 0x50, 0x40));
                r["DimTextBrush"] = new SolidColorBrush(Color.FromRgb(0x60, 0x70, 0x80));
                r["FgBrush"] = new SolidColorBrush(Color.FromRgb(0x1A, 0x20, 0x30));

                // Спец
                r["ErrorBtnBgBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xF5, 0xF8));
                r["ErrorBtnBgBadBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xE0, 0xE8));

                // Hover / pressed
                r["HoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE6, 0xF0));
                r["PressedBgBrush"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD6, 0xE0));
                r["BtnGreenHoverBrush"] = new SolidColorBrush(Color.FromRgb(0xD8, 0xF0, 0xE0));
                r["BtnCyanHoverBrush"] = new SolidColorBrush(Color.FromRgb(0xD8, 0xE8, 0xF0));
                r["BtnRedHoverBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xE0, 0xE8));
                r["BtnYellowHoverBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF0, 0xD0));
                r["CaptionBtnHoverBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xE6, 0xF0));
                r["CaptionBtnPressedBrush"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xD6, 0xE0));

                // Статусы
                r["BadgeGreenBgBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xF8, 0xE8));
                r["BadgeStatusBgBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xF8, 0xEC));
                r["BadgeStatusBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x90, 0xC8, 0xA8));
                r["StatusOnlineBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0xA0, 0x50));
                r["ServiceBadgeBgBrush"] = new SolidColorBrush(Color.FromRgb(0xFF, 0xF5, 0xD0));
                r["SaveOverlayBgBrush"] = new SolidColorBrush(Color.FromArgb(0xCC, 0xFF, 0xFF, 0xFF));

                // Start mode
                r["StartMode7BgBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xE0, 0xE8));
                r["StartMode7BorderBrush"] = new SolidColorBrush(Color.FromRgb(0xC0, 0x90, 0xA0));
                r["StartMode7HoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0xF0, 0xD0, 0xD8));
                r["StartMode7HoverBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xD0, 0x00, 0x30));

                r["StartMode4BgBrush"] = new SolidColorBrush(Color.FromRgb(0xF8, 0xF4, 0xD8));
                r["StartMode4BorderBrush"] = new SolidColorBrush(Color.FromRgb(0xC0, 0xB0, 0x40));
                r["StartMode4HoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0xF0, 0xE8, 0xC8));
                r["StartMode4HoverBorderBrush"] = new SolidColorBrush(Color.FromRgb(0xB0, 0x80, 0x00));

                r["StartMode1BgBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xF8, 0xE0));
                r["StartMode1BorderBrush"] = new SolidColorBrush(Color.FromRgb(0x90, 0xC8, 0xA0));
                r["StartMode1HoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xF0, 0xD8));
                r["StartMode1HoverBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0xA0, 0x50));

                r["StartMode0BgBrush"] = new SolidColorBrush(Color.FromRgb(0xE0, 0xEE, 0xF8));
                r["StartMode0BorderBrush"] = new SolidColorBrush(Color.FromRgb(0x90, 0xA8, 0xC8));
                r["StartMode0HoverBgBrush"] = new SolidColorBrush(Color.FromRgb(0xD0, 0xE4, 0xF4));
                r["StartMode0HoverBorderBrush"] = new SolidColorBrush(Color.FromRgb(0x00, 0x80, 0xB8));

                if (TxtStatusMsg != null) TxtStatusMsg.Text = "Тема: светлая";
            }

            _currentTheme = theme;

            // Анимация ползунка переключателя
            if (ThemeThumbTransform != null)
            {
                double targetX = theme == AppTheme.Light ? 36 : 0;
                var anim = new System.Windows.Media.Animation.DoubleAnimation
                {
                    To = targetX,
                    Duration = TimeSpan.FromMilliseconds(180),
                    EasingFunction = new System.Windows.Media.Animation.CubicEase
                    {
                        EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                    }
                };
                ThemeThumbTransform.BeginAnimation(
                    System.Windows.Media.TranslateTransform.XProperty, anim);
            }

            SaveAppState();
        }

        public MainWindow()
        {
            InitializeComponent();
            _isInitialized = true;
            _modbusManager = new ModbusManager();

            _modbusManager.StatusChanged += OnStatusChanged;
            _modbusManager.DevicePolled += OnDevicePolled;
            _modbusManager.DeviceFound += OnDeviceFound;
            _modbusManager.AutoScanCompleted += OnAutoScanCompleted;

            _modbusManager.QueueChanged += OnQueueChanged;
            _modbusManager.PollStarted += OnPollStarted;

            // Автоскролл во время drag
            _autoScrollTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(16)
            };
            _autoScrollTimer.Tick += AutoScrollTimer_Tick;

            LoadComPorts();
            UpdateDeviceCount();
            SetAutoModeUI();

            UpdateDisconnectPortButton();

            // Загрузка сохранённого состояния
            LoadAppState();
        }

        // ═══════════════════════════════════════════════════
        // СОСТОЯНИЕ ПРИЛОЖЕНИЯ
        // ═══════════════════════════════════════════════════

        private void LoadAppState()
        {
            _loadingState = true;

            _appState = AppStateService.Load();

            // ─── Размер/положение окна ───
            try
            {
                if (_appState.WindowWidth > 400) this.Width = _appState.WindowWidth;
                if (_appState.WindowHeight > 300) this.Height = _appState.WindowHeight;

                if (!double.IsNaN(_appState.WindowLeft) && !double.IsNaN(_appState.WindowTop))
                {
                    double l = _appState.WindowLeft;
                    double t = _appState.WindowTop;

                    if (l < SystemParameters.VirtualScreenLeft - 50) l = 50;
                    if (t < SystemParameters.VirtualScreenTop - 50) t = 50;
                    if (l > SystemParameters.VirtualScreenLeft +
                            SystemParameters.VirtualScreenWidth - 200) l = 50;
                    if (t > SystemParameters.VirtualScreenTop +
                            SystemParameters.VirtualScreenHeight - 200) t = 50;

                    this.Left = l;
                    this.Top = t;
                }

                if (_appState.WindowMaximized)
                    this.WindowState = WindowState.Maximized;
            }
            catch { }

            // ─── Тема ───
            if (_appState.Theme == "Light")
            {
                ApplyTheme(AppTheme.Light);
            }
            else
            {
                ApplyTheme(AppTheme.Dark);
            }

            // ─── Устройства ───
            if (_appState.Devices != null)
            {
                foreach (var ds in _appState.Devices)
                {
                    try
                    {
                        if (_modbusManager.Registry.Contains(ds.Address)) continue;

                        var device = new EpraDevice(ds.Address)
                        {
                            Port = ds.Port,
                            BaudRate = ds.BaudRate,
                            Parity = ParseParity(ds.Parity),
                            StopBits = ParseStopBits(ds.StopBits)
                        };

                        _modbusManager.AddDevice(device);

                        var card = new EpraWindow(device);
                        card.DeleteRequested += Device_DeleteRequested;
                        card.ExpandRequested += Device_ExpandRequested;
                        card.DeviceWriteRequested += OnDeviceWriteRequested;
                        card.DragStartRequested += Card_DragStartRequested;

                        _deviceCards.Add(card);
                        DeviceCardsPanel.Children.Add(card);
                    }
                    catch { }
                }
            }

            _deviceCounter = _deviceCards.Count;
            UpdateDeviceCount();

            _loadingState = false;
        }

        private static System.IO.Ports.Parity ParseParity(string s)
        {
            switch (s)
            {
                case "Even": return System.IO.Ports.Parity.Even;
                case "Odd": return System.IO.Ports.Parity.Odd;
                case "Mark": return System.IO.Ports.Parity.Mark;
                case "Space": return System.IO.Ports.Parity.Space;
                default: return System.IO.Ports.Parity.None;
            }
        }

        private static System.IO.Ports.StopBits ParseStopBits(string s)
        {
            switch (s)
            {
                case "Two": return System.IO.Ports.StopBits.Two;
                case "OnePointFive": return System.IO.Ports.StopBits.OnePointFive;
                default: return System.IO.Ports.StopBits.One;
            }
        }

        private AppState CaptureCurrentState()
        {
            var state = new AppState
            {
                Theme = _currentTheme == AppTheme.Light ? "Light" : "Dark",
                WindowWidth = this.Width,
                WindowHeight = this.Height,
                WindowMaximized = this.WindowState == WindowState.Maximized
            };

            if (this.WindowState == WindowState.Normal)
            {
                state.WindowLeft = this.Left;
                state.WindowTop = this.Top;
            }
            else
            {
                state.WindowLeft = this.RestoreBounds.Left;
                state.WindowTop = this.RestoreBounds.Top;
            }

            foreach (var card in _deviceCards)
            {
                var d = card.Device;
                if (d == null) continue;

                state.Devices.Add(new DeviceState
                {
                    Address = d.DeviceAddress,
                    BaudRate = d.BaudRate,
                    Parity = d.Parity.ToString(),
                    StopBits = d.StopBits.ToString(),
                    Port = d.Port ?? ""
                });
            }

            return state;
        }

        private void SaveAppState()
        {
            if (_loadingState) return;
            try
            {
                var state = CaptureCurrentState();
                AppStateService.Save(state);
            }
            catch { }
        }

        // ═══════════════════════════════════════════════════
        // ЭКСПОРТ В ФАЙЛ
        // ═══════════════════════════════════════════════════

        private void BtnExportConfig_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                var state = CaptureCurrentState();

                var dlg = new SaveFileDialog
                {
                    Filter = "Конфигурация ЭПРА (*.txt)|*.txt|Все файлы (*.*)|*.*",
                    Title = "Сохранить конфигурацию",
                    FileName = $"epra_config_{DateTime.Now:yyyy-MM-dd_HH-mm}.txt",
                    DefaultExt = ".txt",
                    AddExtension = true,
                    OverwritePrompt = true,
                    InitialDirectory = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
                };

                if (dlg.ShowDialog() != true) return;

                AppStateService.ExportTo(dlg.FileName, state);

                TxtStatusMsg.Text = $"Конфиг сохранён: {Path.GetFileName(dlg.FileName)}";

                MessageBox.Show(
                    $"Конфигурация сохранена:\n{dlg.FileName}",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Не удалось сохранить конфиг: " + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════════════════
        // DRAG & DROP КОНФИГА
        // ═══════════════════════════════════════════════════

        private void Window_DragEnter(object sender, DragEventArgs e)
        {
            UpdateDragOverlay(e);
        }

        private void Window_DragOver(object sender, DragEventArgs e)
        {
            UpdateDragOverlay(e);
        }

        private void Window_DragLeave(object sender, DragEventArgs e)
        {
            HideDropOverlay();
        }

        private void Window_Drop(object sender, DragEventArgs e)
        {
            HideDropOverlay();

            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;

                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0) return;

                string path = files[0];

                if (!AppStateService.IsConfigFile(path))
                {
                    MessageBox.Show(
                        "Это не файл конфигурации ЭПРА (.txt).",
                        "Неверный файл", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                var state = AppStateService.ImportFrom(path);
                if (state == null)
                {
                    MessageBox.Show(
                        "Не удалось прочитать файл конфигурации.",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                int devicesCount = state.Devices?.Count ?? 0;

                var res = MessageBox.Show(
                    $"Загрузить конфигурацию из файла?\n\n" +
                    $"Устройств в файле: {devicesCount}\n" +
                    $"Тема: {state.Theme}\n\n" +
                    $"Текущие карточки будут очищены.",
                    "Загрузка конфига",
                    MessageBoxButton.YesNo, MessageBoxImage.Question,
                    MessageBoxResult.Yes);

                if (res != MessageBoxResult.Yes) return;

                ApplyImportedState(state);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Ошибка загрузки конфига: " + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void UpdateDragOverlay(DragEventArgs e)
        {
            try
            {
                if (!e.Data.GetDataPresent(DataFormats.FileDrop))
                {
                    e.Effects = DragDropEffects.None;
                    HideDropOverlay();
                    e.Handled = true;
                    return;
                }

                var files = e.Data.GetData(DataFormats.FileDrop) as string[];
                if (files == null || files.Length == 0 ||
                    !AppStateService.IsConfigFile(files[0]))
                {
                    e.Effects = DragDropEffects.None;
                    HideDropOverlay();
                    e.Handled = true;
                    return;
                }

                e.Effects = DragDropEffects.Copy;
                ShowDropOverlay();
                e.Handled = true;
            }
            catch
            {
                e.Effects = DragDropEffects.None;
                HideDropOverlay();
                e.Handled = true;
            }
        }

        private void ShowDropOverlay()
        {
            if (DropOverlay != null)
                DropOverlay.Visibility = Visibility.Visible;
        }

        private void HideDropOverlay()
        {
            if (DropOverlay != null)
                DropOverlay.Visibility = Visibility.Collapsed;
        }

        private void ApplyImportedState(AppState state)
        {
            _loadingState = true;

            try
            {
                // ─── Тема ───
                if (state.Theme == "Light")
                {
                    ApplyTheme(AppTheme.Light);
                }
                else
                {
                    ApplyTheme(AppTheme.Dark);
                }

                // ─── Остановить опрос, закрыть порт ───
                try { _modbusManager.StopPolling(); } catch { }
                try { _modbusManager.DisconnectAsync().Wait(1000); } catch { }

                // ─── Очистить ───
                ClearDeviceCards();

                foreach (var d in _modbusManager.Registry.All.ToList())
                    _modbusManager.RemoveDevice(d.DeviceAddress);

                // ─── Загрузить устройства ───
                if (state.Devices != null)
                {
                    foreach (var ds in state.Devices)
                    {
                        if (_modbusManager.Registry.Contains(ds.Address)) continue;

                        var device = new EpraDevice(ds.Address)
                        {
                            Port = ds.Port,
                            BaudRate = ds.BaudRate,
                            Parity = ParseParity(ds.Parity),
                            StopBits = ParseStopBits(ds.StopBits)
                        };

                        _modbusManager.AddDevice(device);

                        var card = new EpraWindow(device);
                        card.DeleteRequested += Device_DeleteRequested;
                        card.ExpandRequested += Device_ExpandRequested;
                        card.DeviceWriteRequested += OnDeviceWriteRequested;
                        card.DragStartRequested += Card_DragStartRequested;

                        _deviceCards.Add(card);
                        DeviceCardsPanel.Children.Add(card);
                    }
                }

                _deviceCounter = _deviceCards.Count;
                UpdateDeviceCount();
                UpdateQueueDisplay();

                TxtStatusMsg.Text = $"Загружен конфиг: {_deviceCards.Count} устройств";
            }
            finally
            {
                _loadingState = false;
            }

            SaveAppState();
        }

        // ═══════════════════════════════════════════════════
        // ОБРАБОТЧИКИ ОКНА
        // ═══════════════════════════════════════════════════

        private void TitleBar_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (e.ClickCount == 2)
                BtnMaximize_Click(sender, e);
            else
            {
                try { this.DragMove(); } catch { }
            }
        }

        private void BtnMinimize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = WindowState.Minimized;
        }

        private void BtnMaximize_Click(object sender, RoutedEventArgs e)
        {
            this.WindowState = this.WindowState == WindowState.Maximized
                ? WindowState.Normal
                : WindowState.Maximized;
        }

        private async void BtnClose_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                _modbusManager?.StopPolling();
                if (_modbusManager != null)
                    await _modbusManager.DisconnectAsync();
            }
            catch { }

            this.Close();
        }

        // ═══════════════════════════════════════════════════
        // РЕЖИМЫ РАБОТЫ
        // ═══════════════════════════════════════════════════

        private void SetAutoModeUI()
        {
            BtnAddDevice.Visibility = Visibility.Collapsed;
            TxtAddress.Visibility = Visibility.Collapsed;
            TextAddress.Visibility = Visibility.Collapsed;

            TextBaud.Visibility = Visibility.Collapsed;
            CmbBaud.Visibility = Visibility.Collapsed;
        }

        private void SetManualModeUI()
        {
            BtnAddDevice.Visibility = Visibility.Visible;
            TxtAddress.Visibility = Visibility.Visible;
            TextAddress.Visibility = Visibility.Visible;

            TextBaud.Visibility = Visibility.Visible;
            CmbBaud.Visibility = Visibility.Visible;
        }

        private async void BtnAuto_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            BtnManual.IsChecked = false;

            try
            {
                _modbusManager?.StopPolling();
                if (_modbusManager != null)
                    await _modbusManager.DisconnectAsync();
            }
            catch { }

            BtnExecute.Content = "▶  ВЫПОЛНИТЬ";
            BtnStartPoll.Content = "▶  НАЧАТЬ";
            BtnStartPoll.IsEnabled = true;
            SetControlsEnabled(true);
            SetAutoModeUI();

            UpdateDisconnectPortButton();
            TxtStatusMsg.Text = $"Режим: АВТО. Устройств: {_modbusManager?.Registry.Count ?? 0}";
        }

        private async void BtnManual_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized) return;

            BtnAuto.IsChecked = false;

            try
            {
                _modbusManager?.StopPolling();
                if (_modbusManager != null)
                    await _modbusManager.DisconnectAsync();
            }
            catch { }

            BtnExecute.Content = "▶  ВЫПОЛНИТЬ";
            BtnStartPoll.Content = "▶  НАЧАТЬ";
            BtnStartPoll.IsEnabled = true;
            SetControlsEnabled(true);
            SetManualModeUI();

            UpdateDisconnectPortButton();
            TxtStatusMsg.Text = $"Режим: РУЧНОЙ. Устройств: {_modbusManager?.Registry.Count ?? 0}";
        }

        private void ClearDeviceCards()
        {
            DeviceCardsPanel.Children.Clear();
            _deviceCards.Clear();
            _deviceCounter = 0;
            UpdateDeviceCount();
        }

        // ═══════════════════════════════════════════════════
        // ВЫПОЛНИТЬ / СТОП
        // ═══════════════════════════════════════════════════

        private async void BtnExecute_Click(object sender, RoutedEventArgs e)
        {
            if (BtnExecute.Content.ToString() == "■  СТОП")
            {
                try
                {
                    _modbusManager.StopPolling();
                    await _modbusManager.DisconnectAsync();
                }
                catch { }

                BtnExecute.Content = "▶  ВЫПОЛНИТЬ";
                BtnStartPoll.IsEnabled = true;
                SetControlsEnabled(true);

                UpdateDisconnectPortButton();

                if (_modbusManager.Registry.Count > 0)
                    TxtStatusMsg.Text = $"Остановлено. Найдено устройств: {_modbusManager.Registry.Count}";
                else
                    TxtStatusMsg.Text = "Остановлено. Устройства не найдены";

                UpdateQueueDisplay();
                return;
            }

            try
            {
                string port = (CmbPort.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "COM1";
                int baudRate = 9600;
                if (CmbBaud.SelectedItem is ComboBoxItem baudItem)
                    int.TryParse(baudItem.Content.ToString(), out baudRate);

                SetControlsEnabled(false);
                BtnExecute.IsEnabled = true;
                BtnExecute.Content = "■  СТОП";
                BtnStartPoll.IsEnabled = false;

                if (BtnAuto.IsChecked == true)
                {
                    var result = MessageBox.Show(
                        "При массовом опросе все текущие ячейки будут очищены.\nПродолжить?",
                        "Подтверждение",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Question);

                    if (result != MessageBoxResult.Yes)
                    {
                        BtnExecute.Content = "▶  ВЫПОЛНИТЬ";
                        BtnStartPoll.IsEnabled = true;
                        SetControlsEnabled(true);
                        return;
                    }

                    ClearDeviceCards();

                    int[] allBaudRates = new int[]
                    {
                        9600, 14400, 19200, 28800,
                        38400, 57600, 76800, 115200
                    };

                    TxtStatusMsg.Text = $"Автосканирование: {allBaudRates.Length} скоростей...";
                    await _modbusManager.AutoScanAllBaudRatesAsync(port, allBaudRates);
                    TxtStatusMsg.Text = $"Найдено: {_modbusManager.Registry.Count} устройств";
                    SaveAppState();
                }
                else
                {
                    if (!byte.TryParse(TxtAddress.Text, out byte address) || address < 1 || address > 247)
                    {
                        MessageBox.Show("Введите адрес от 1 до 247");
                        BtnExecute.Content = "▶  ВЫПОЛНИТЬ";
                        BtnStartPoll.IsEnabled = true;
                        SetControlsEnabled(true);
                        return;
                    }

                    if (_modbusManager.Registry.Contains(address))
                    {
                        MessageBox.Show($"Устройство с адресом {address} уже существует");
                        BtnExecute.Content = "▶  ВЫПОЛНИТЬ";
                        BtnStartPoll.IsEnabled = true;
                        SetControlsEnabled(true);
                        return;
                    }

                    var device = new EpraDevice(address)
                    {
                        Port = port,
                        BaudRate = baudRate
                    };

                    _modbusManager.AddDevice(device);

                    var card = new EpraWindow(device);
                    card.DeleteRequested += Device_DeleteRequested;
                    card.ExpandRequested += Device_ExpandRequested;
                    card.DeviceWriteRequested += OnDeviceWriteRequested;
                    card.DragStartRequested += Card_DragStartRequested;

                    _deviceCards.Add(card);
                    DeviceCardsPanel.Children.Add(card);
                    _deviceCounter++;
                    UpdateDeviceCount();
                    TxtStatusMsg.Text = $"Добавлено устройство с адресом {address}";
                    SaveAppState();
                }

                BtnExecute.Content = "▶  ВЫПОЛНИТЬ";
                BtnStartPoll.IsEnabled = true;
                SetControlsEnabled(true);

                UpdateDisconnectPortButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                try
                {
                    _modbusManager.StopPolling();
                    await _modbusManager.DisconnectAsync();
                }
                catch { }

                BtnExecute.Content = "▶  ВЫПОЛНИТЬ";
                BtnStartPoll.IsEnabled = true;
                SetControlsEnabled(true);
                UpdateDisconnectPortButton();
            }
        }

        // ═══════════════════════════════════════════════════
        // НАЧАТЬ / СТОП
        // ═══════════════════════════════════════════════════

        private async void BtnStartPoll_Click(object sender, RoutedEventArgs e)
        {
            if (BtnStartPoll.Content.ToString() == "■  СТОП")
            {
                try { _modbusManager.StopPolling(); } catch { }

                BtnStartPoll.Content = "▶  НАЧАТЬ";
                BtnExecute.IsEnabled = true;
                SetControlsEnabled(true);
                TxtStatusMsg.Text = "Опрос остановлен";

                UpdateDisconnectPortButton();
                UpdateQueueDisplay();
                return;
            }

            try
            {
                if (_modbusManager.Registry.Count == 0)
                {
                    MessageBox.Show("Нет устройств для опроса. Сначала выполните сканирование или добавьте устройства.");
                    return;
                }

                string port = (CmbPort.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "COM1";
                int baudRate = 9600;
                if (CmbBaud.SelectedItem is ComboBoxItem baudItem)
                    int.TryParse(baudItem.Content.ToString(), out baudRate);

                if (!_modbusManager.IsConnected)
                {
                    if (!await _modbusManager.ConnectAsync(port, baudRate))
                    {
                        MessageBox.Show($"Не удалось открыть {port}",
                            "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
                        return;
                    }
                }

                _modbusManager.StartPolling();
                BtnStartPoll.Content = "■  СТОП";
                BtnExecute.IsEnabled = false;
                SetControlsEnabled(false);
                BtnStartPoll.IsEnabled = true;
                TxtStatusMsg.Text = $"Опрос запущен. Устройств: {_modbusManager.Registry.Count}";

                UpdateDisconnectPortButton();
                UpdateQueueDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);

                BtnStartPoll.Content = "▶  НАЧАТЬ";
                BtnExecute.IsEnabled = true;
                SetControlsEnabled(true);

                UpdateDisconnectPortButton();
            }
        }

        // ═══════════════════════════════════════════════════
        // ПОДКЛЮЧИТЬ / ОТКЛЮЧИТЬ ПОРТ
        // ═══════════════════════════════════════════════════

        private void UpdateDisconnectPortButton()
        {
            if (BtnDisconnectPort == null || _modbusManager == null) return;

            bool portOpen = _modbusManager.IsConnected;
            bool polling = _modbusManager.IsPolling;

            BtnDisconnectPort.IsEnabled = !polling;

            if (portOpen)
            {
                BtnDisconnectPort.Content = "✕  ОТКЛЮЧИТЬ";
                BtnDisconnectPort.Style = (Style)FindResource("BtnRed");
                BtnDisconnectPort.ToolTip = "Закрыть COM-порт";
            }
            else
            {
                BtnDisconnectPort.Content = "🔌  ПОДКЛЮЧИТЬ";
                BtnDisconnectPort.Style = (Style)FindResource("BtnGreen");
                BtnDisconnectPort.ToolTip = "Открыть COM-порт";
            }
        }

        private async void BtnDisconnectPort_Click(object sender, RoutedEventArgs e)
        {
            if (_modbusManager == null) return;
            if (_modbusManager.IsPolling) return;

            if (_modbusManager.IsConnected)
            {
                var result = MessageBox.Show(
                    "Закрыть COM-порт?",
                    "Подтверждение",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result != MessageBoxResult.Yes) return;

                try { await _modbusManager.DisconnectAsync(); } catch { }

                BtnStartPoll.Content = "▶  НАЧАТЬ";
                BtnExecute.Content = "▶  ВЫПОЛНИТЬ";
                BtnExecute.IsEnabled = true;
                BtnStartPoll.IsEnabled = true;
                SetControlsEnabled(true);
                TxtStatusMsg.Text = "COM-порт закрыт";
            }
            else
            {
                string port = (CmbPort.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "COM1";
                int baudRate = 9600;
                if (CmbBaud.SelectedItem is ComboBoxItem baudItem)
                    int.TryParse(baudItem.Content.ToString(), out baudRate);

                bool ok = await _modbusManager.ConnectAsync(port, baudRate);

                if (ok)
                    TxtStatusMsg.Text = $"Подключено к {port} @ {baudRate}";
                else
                    MessageBox.Show($"Не удалось открыть {port}",
                        "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateDisconnectPortButton();
            UpdateQueueDisplay();
        }

        private void SetControlsEnabled(bool enabled)
        {
            CmbPort.IsEnabled = enabled;
            CmbBaud.IsEnabled = enabled;
            CmbModel.IsEnabled = enabled;
            BtnAuto.IsEnabled = enabled;
            BtnManual.IsEnabled = enabled;

            if (BtnAuto.IsChecked == true)
            {
                BtnAddDevice.Visibility = Visibility.Collapsed;
                TxtAddress.Visibility = Visibility.Collapsed;
                TextAddress.Visibility = Visibility.Collapsed;

                TextBaud.Visibility = Visibility.Collapsed;
                CmbBaud.Visibility = Visibility.Collapsed;
            }
            else
            {
                BtnAddDevice.Visibility = Visibility.Visible;
                TxtAddress.Visibility = Visibility.Visible;
                TextAddress.Visibility = Visibility.Visible;

                TextBaud.Visibility = Visibility.Visible;
                CmbBaud.Visibility = Visibility.Visible;

                BtnAddDevice.IsEnabled = enabled;
                TxtAddress.IsEnabled = enabled;
                TextAddress.IsEnabled = enabled;
            }
        }

        // ═══════════════════════════════════════════════════
        // УПРАВЛЕНИЕ УСТРОЙСТВАМИ
        // ═══════════════════════════════════════════════════

        private void BtnAddDevice_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_deviceCounter >= MaxDevices)
                {
                    MessageBox.Show($"Достигнут лимит: {MaxDevices} устройств");
                    return;
                }

                if (!byte.TryParse(TxtAddress.Text, out byte address) || address < 1 || address > 247)
                {
                    MessageBox.Show("Введите адрес от 1 до 247");
                    return;
                }

                if (_modbusManager.Registry.Contains(address))
                {
                    MessageBox.Show($"Устройство с адресом {address} уже существует");
                    return;
                }

                _deviceCounter++;

                string port = (CmbPort.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "COM1";
                int baudRate = 9600;
                if (CmbBaud.SelectedItem is ComboBoxItem baudItem)
                    int.TryParse(baudItem.Content.ToString(), out baudRate);

                var device = new EpraDevice(address)
                {
                    Port = port,
                    BaudRate = baudRate
                };

                _modbusManager.AddDevice(device);

                var card = new EpraWindow(device);
                card.DeleteRequested += Device_DeleteRequested;
                card.ExpandRequested += Device_ExpandRequested;
                card.DeviceWriteRequested += OnDeviceWriteRequested;
                card.DragStartRequested += Card_DragStartRequested;

                _deviceCards.Add(card);
                DeviceCardsPanel.Children.Add(card);

                UpdateDeviceCount();
                TxtStatusMsg.Text = $"Добавлено устройство с адресом {address}";
                SaveAppState();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void Device_DeleteRequested(object sender, int deviceId)
        {
            try
            {
                var card = _deviceCards.Find(d => d.DeviceId == deviceId);
                if (card != null)
                {
                    _modbusManager.RemoveDevice((byte)deviceId);

                    _deviceCards.Remove(card);
                    DeviceCardsPanel.Children.Remove(card);
                    UpdateDeviceCount();

                    UpdateQueueDisplay();

                    TxtStatusMsg.Text = $"Удалено устройство №{deviceId}";
                    SaveAppState();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        // ═══════════════════════════════════════════════════
        // DRAG & DROP
        // ═══════════════════════════════════════════════════

        private void Card_DragStartRequested(object sender, DragStartEventArgs e)
        {
            _draggedCard = sender as EpraWindow;
            if (_draggedCard == null) return;

            _draggedCard.SetDraggingVisual(true);

            var dragData = new DataObject("EpraCard", _draggedCard);
            try
            {
                DragDrop.DoDragDrop(_draggedCard, dragData, DragDropEffects.Move);
            }
            finally
            {
                if (_draggedCard != null)
                    _draggedCard.SetDraggingVisual(false);

                ClearAllHighlight();
                StopAutoScroll();
                _draggedCard = null;
            }
        }

        private void CardsScroll_DragOver(object sender, DragEventArgs e)
        {
            if (e.Data.GetDataPresent(DataFormats.FileDrop))
            {
                UpdateDragOverlay(e);
                return;
            }

            // Дальше — существующая логика для карточек
            if (!e.Data.GetDataPresent("EpraCard"))
            {
                e.Effects = DragDropEffects.None;
                ClearAllHighlight();
                StopAutoScroll();
                e.Handled = true;
                return;
            }

            if (!e.Data.GetDataPresent("EpraCard"))
            {
                e.Effects = DragDropEffects.None;
                ClearAllHighlight();
                StopAutoScroll();
                e.Handled = true;
                return;
            }

            e.Effects = DragDropEffects.Move;

            var posInScroll = e.GetPosition(CardsScroll);
            double h = CardsScroll.ActualHeight;

            double delta = 0;
            if (posInScroll.Y < AutoScrollZone)
            {
                double k = (AutoScrollZone - posInScroll.Y) / AutoScrollZone;
                delta = -AutoScrollMaxStep * Math.Min(1.0, k);
            }
            else if (posInScroll.Y > h - AutoScrollZone)
            {
                double k = (posInScroll.Y - (h - AutoScrollZone)) / AutoScrollZone;
                delta = AutoScrollMaxStep * Math.Min(1.0, k);
            }

            _autoScrollDelta = delta;
            if (Math.Abs(delta) > 0.5)
            {
                if (!_autoScrollTimer.IsEnabled)
                    _autoScrollTimer.Start();
            }
            else
            {
                _autoScrollTimer.Stop();
            }

            var dropPoint = e.GetPosition(DeviceCardsPanel);
            var target = FindDropTarget(dropPoint);

            ClearAllHighlight();

            switch (target.Kind)
            {
                case DropKind.Swap:
                    target.Card?.SetSwapVisual(true);
                    _lastHighlightedCard = target.Card;
                    break;

                case DropKind.InsertBefore:
                    target.Card?.SetInsertVisual(true, true);
                    _lastHighlightedCard = target.Card;
                    break;

                case DropKind.InsertAfter:
                    target.Card?.SetInsertVisual(true, false);
                    _lastHighlightedCard = target.Card;
                    break;
            }

            e.Handled = true;
        }

        private void CardsScroll_Drop(object sender, DragEventArgs e)
        {
            StopAutoScroll();
            ClearAllHighlight();

            if (_draggedCard == null) return;
            if (!e.Data.GetDataPresent("EpraCard")) return;

            var dropPoint = e.GetPosition(DeviceCardsPanel);
            var target = FindDropTarget(dropPoint);

            int draggedIndex = _deviceCards.IndexOf(_draggedCard);
            if (draggedIndex < 0) return;

            switch (target.Kind)
            {
                case DropKind.Swap:
                    if (target.Card == null || target.Card == _draggedCard) return;
                    {
                        int targetIndex = _deviceCards.IndexOf(target.Card);
                        if (targetIndex < 0) return;

                        _deviceCards[draggedIndex] = target.Card;
                        _deviceCards[targetIndex] = _draggedCard;

                        RebuildCardsPanel();
                        SyncQueueOrderWithCards();

                        TxtStatusMsg.Text = $"Обмен: #{_draggedCard.DeviceId} ↔ #{target.Card.DeviceId}";
                    }
                    break;

                case DropKind.InsertBefore:
                case DropKind.InsertAfter:
                    if (target.Card == null) return;
                    {
                        int insertIndex = target.Index;

                        _deviceCards.RemoveAt(draggedIndex);

                        if (draggedIndex < insertIndex)
                            insertIndex--;

                        if (insertIndex < 0) insertIndex = 0;
                        if (insertIndex > _deviceCards.Count) insertIndex = _deviceCards.Count;

                        _deviceCards.Insert(insertIndex, _draggedCard);

                        RebuildCardsPanel();
                        SyncQueueOrderWithCards();

                        TxtStatusMsg.Text = $"Вставлено: #{_draggedCard.DeviceId} → позиция {insertIndex + 1}";
                    }
                    break;
            }
        }

        private void AutoScrollTimer_Tick(object sender, EventArgs e)
        {
            if (_draggedCard == null || CardsScroll == null)
            {
                _autoScrollTimer.Stop();
                return;
            }

            if (Math.Abs(_autoScrollDelta) < 0.5)
                return;

            double newOffset = CardsScroll.VerticalOffset + _autoScrollDelta;
            if (newOffset < 0) newOffset = 0;
            if (newOffset > CardsScroll.ScrollableHeight) newOffset = CardsScroll.ScrollableHeight;

            CardsScroll.ScrollToVerticalOffset(newOffset);
        }

        private void StopAutoScroll()
        {
            _autoScrollDelta = 0;
            _autoScrollTimer?.Stop();
        }

        private void ClearAllHighlight()
        {
            if (_lastHighlightedCard != null)
            {
                _lastHighlightedCard.ClearDropVisual();
                _lastHighlightedCard = null;
            }
        }

        private DropTarget FindDropTarget(Point point)
        {
            var result = new DropTarget { Kind = DropKind.None, Card = null, Index = -1 };

            if (_deviceCards.Count == 0)
                return result;

            foreach (var card in _deviceCards)
            {
                if (card == _draggedCard) continue;

                var pos = card.TranslatePoint(new Point(0, 0), DeviceCardsPanel);
                double left = pos.X;
                double right = pos.X + card.ActualWidth;
                double top = pos.Y;
                double bottom = pos.Y + card.ActualHeight;

                if (point.Y < top || point.Y > bottom) continue;

                double width = card.ActualWidth;
                double leftZone = left + width * 0.25;
                double rightZone = left + width * 0.75;

                if (point.X >= left && point.X < leftZone)
                {
                    int idx = _deviceCards.IndexOf(card);
                    if (idx >= 0)
                    {
                        result.Kind = DropKind.InsertBefore;
                        result.Card = card;
                        result.Index = idx;
                        return result;
                    }
                }

                if (point.X > rightZone && point.X <= right)
                {
                    int idx = _deviceCards.IndexOf(card);
                    if (idx >= 0)
                    {
                        result.Kind = DropKind.InsertAfter;
                        result.Card = card;
                        result.Index = idx + 1;
                        return result;
                    }
                }

                if (point.X >= leftZone && point.X <= rightZone)
                {
                    result.Kind = DropKind.Swap;
                    result.Card = card;
                    return result;
                }
            }

            EpraWindow nearest = null;
            double nearestDist = double.MaxValue;
            bool nearestBefore = false;

            foreach (var card in _deviceCards)
            {
                if (card == _draggedCard) continue;

                var pos = card.TranslatePoint(new Point(0, 0), DeviceCardsPanel);
                double centerX = pos.X + card.ActualWidth / 2;
                double dist = Math.Abs(point.X - centerX);

                if (dist < nearestDist)
                {
                    nearestDist = dist;
                    nearest = card;
                    nearestBefore = point.X < centerX;
                }
            }

            if (nearest != null)
            {
                int idx = _deviceCards.IndexOf(nearest);
                if (idx >= 0)
                {
                    result.Kind = nearestBefore ? DropKind.InsertBefore : DropKind.InsertAfter;
                    result.Card = nearest;
                    result.Index = nearestBefore ? idx : idx + 1;
                }
            }

            return result;
        }

        private void RebuildCardsPanel()
        {
            var oldPositions = new Dictionary<EpraWindow, Point>();
            foreach (var card in _deviceCards)
            {
                if (card.ActualWidth > 0 && card.ActualHeight > 0)
                {
                    var pos = card.TranslatePoint(new Point(0, 0), DeviceCardsPanel);
                    oldPositions[card] = pos;
                }
            }

            DeviceCardsPanel.Children.Clear();
            foreach (var card in _deviceCards)
            {
                DeviceCardsPanel.Children.Add(card);
            }

            DeviceCardsPanel.UpdateLayout();

            foreach (var card in _deviceCards)
            {
                if (!oldPositions.TryGetValue(card, out var oldPos))
                    continue;

                var newPos = card.TranslatePoint(new Point(0, 0), DeviceCardsPanel);
                double dx = oldPos.X - newPos.X;
                double dy = oldPos.Y - newPos.Y;

                if (Math.Abs(dx) < 0.5 && Math.Abs(dy) < 0.5)
                    continue;

                if (card.RenderTransform is TranslateTransform existing)
                    card.RenderTransform = Transform.Identity;

                var tt = new TranslateTransform(dx, dy);
                card.RenderTransform = tt;

                var duration = TimeSpan.FromMilliseconds(AnimationDurationMs);
                var ease = new System.Windows.Media.Animation.CubicEase
                {
                    EasingMode = System.Windows.Media.Animation.EasingMode.EaseOut
                };

                var animX = new System.Windows.Media.Animation.DoubleAnimation(dx, 0, duration)
                {
                    EasingFunction = ease,
                    FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop
                };
                var animY = new System.Windows.Media.Animation.DoubleAnimation(dy, 0, duration)
                {
                    EasingFunction = ease,
                    FillBehavior = System.Windows.Media.Animation.FillBehavior.Stop
                };

                animX.Completed += (s, e) =>
                {
                    card.RenderTransform = Transform.Identity;
                };

                tt.BeginAnimation(TranslateTransform.XProperty, animX);
                tt.BeginAnimation(TranslateTransform.YProperty, animY);
            }
        }

        private void SyncQueueOrderWithCards()
        {
            if (_modbusManager == null) return;

            var ordered = _deviceCards.Select(c => (byte)c.DeviceId).ToList();
            _modbusManager.SetQueueOrder(ordered);

            SaveAppState();
        }

        // ═══════════════════════════════════════════════════
        // РАСШИРЕННЫЙ РЕЖИМ
        // ═══════════════════════════════════════════════════
        private void Device_ExpandRequested(object sender, int deviceId)
        {
            try
            {
                if (!_modbusManager.IsPolling)
                {
                    MessageBox.Show(
                        "Расширенный режим доступен только при включённом опросе.\n" +
                        "Нажмите «▶ НАЧАТЬ» для запуска опроса.",
                        "Опрос не запущен",
                        MessageBoxButton.OK,
                        MessageBoxImage.Information);
                    return;
                }

                var device = _modbusManager.Registry.Get((byte)deviceId) as EpraDevice;
                if (device == null) return;

                _savedWidth = this.Width;
                _savedHeight = this.Height;
                _savedWindowState = this.WindowState;

                _modbusManager.SetTarget(device);

                var expandedView = new EpraExpandedWindow(device);
                expandedView.BackRequested += ExpandedView_BackRequested;
                expandedView.DeviceWriteRequested += OnDeviceWriteRequested;
                expandedView.ReconnectRequested += OnReconnectRequested;

                ExpandedViewHost.Content = expandedView;
                MainView.Visibility = Visibility.Collapsed;
                ExpandedViewHost.Visibility = Visibility.Visible;

                if (this.WindowState != WindowState.Maximized)
                {
                    const double FixedWidth = 1340;
                    const double FixedHeight = 1100;

                    if (this.Width < FixedWidth || this.Height < FixedHeight)
                    {
                        this.WindowState = WindowState.Normal;
                        this.Width = FixedWidth;
                        this.Height = FixedHeight;
                        this.Left = Math.Max(0, (SystemParameters.PrimaryScreenWidth - FixedWidth) / 2);
                        this.Top = Math.Max(0, (SystemParameters.PrimaryScreenHeight - FixedHeight) / 2);
                    }
                }

                UpdateQueueDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Ошибка: {ex.Message}", "Ошибка",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void OnReconnectRequested(object sender, ReconnectRequestedArgs args)
        {
            try
            {
                TxtStatusMsg.Text = "Переподключение к устройству...";

                byte oldAddr = args.DeviceAddress;
                var device = _modbusManager.Registry.Get(oldAddr) as EpraDevice;
                if (device == null)
                {
                    TxtStatusMsg.Text = $"Устройство №{oldAddr} не найдено в реестре";
                    return;
                }

                _modbusManager.StopPolling();
                await _modbusManager.DisconnectAsync();
                await Task.Delay(500);

                int baudRate = ModeToBaudRate(args.NewBaudMode);
                var parity = ModeToParity(args.NewParityMode);
                var stopBits = ModeToStopBits(args.NewStopBitsMode);
                byte newAddr = (byte)args.NewAddress;

                string port = (CmbPort.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "COM1";

                bool ok = await _modbusManager.ConnectAsync(port, baudRate, parity, stopBits);

                if (!ok)
                {
                    TxtStatusMsg.Text = "Не удалось переподключиться. Проверьте настройки";
                    UpdateDisconnectPortButton();
                    return;
                }

                if (newAddr != oldAddr)
                {
                    _modbusManager.RemoveDevice(oldAddr);

                    device.DeviceAddress = newAddr;
                    device.SetHolding(EpraConstants.HR_MODBUS_ADDRESS, newAddr);

                    device.Port = port;
                    device.BaudRate = baudRate;
                    device.Parity = parity;
                    device.StopBits = stopBits;

                    _modbusManager.AddDevice(device);
                }
                else
                {
                    device.Port = port;
                    device.BaudRate = baudRate;
                    device.Parity = parity;
                    device.StopBits = stopBits;
                }

                var card = _deviceCards.Find(c => c.DeviceId == oldAddr);
                if (card == null)
                {
                    card = _deviceCards.Find(c => ReferenceEquals(c.Device, device));
                }

                if (card != null)
                {
                    if (!ReferenceEquals(card.Device, device))
                        card.Device.DeviceAddress = newAddr;

                    card.RefreshAddress();
                }

                _modbusManager.StartPolling();
                _modbusManager.SetTarget(device);

                TxtStatusMsg.Text = $"Переподключено: {port} @ {baudRate}, адрес {newAddr}";

                UpdateDisconnectPortButton();
                UpdateQueueDisplay();

                SaveAppState();
            }
            catch (Exception ex)
            {
                TxtStatusMsg.Text = $"Ошибка переподключения: {ex.Message}";
            }
        }

        // ═══════════════════════════════════════════════════
        // MODE → Реальные параметры
        // ═══════════════════════════════════════════════════
        private int ModeToBaudRate(ushort mode)
        {
            switch (mode)
            {
                case 0: return 1200;
                case 1: return 2400;
                case 2: return 4800;
                case 3: return 9600;
                case 4: return 14400;
                case 5: return 19200;
                case 6: return 28800;
                case 7: return 38400;
                case 8: return 57600;
                case 9: return 76800;
                case 10: return 115200;
                default: return 9600;
            }
        }

        private System.IO.Ports.Parity ModeToParity(ushort mode)
        {
            switch (mode)
            {
                case 0: return System.IO.Ports.Parity.None;
                case 1: return System.IO.Ports.Parity.Even;
                case 2: return System.IO.Ports.Parity.Odd;
                default: return System.IO.Ports.Parity.None;
            }
        }

        private System.IO.Ports.StopBits ModeToStopBits(ushort mode)
        {
            switch (mode)
            {
                case 0: return System.IO.Ports.StopBits.One;
                case 1: return System.IO.Ports.StopBits.Two;
                default: return System.IO.Ports.StopBits.One;
            }
        }

        private void ExpandedView_BackRequested(object sender, EventArgs e)
        {
            _modbusManager.ClearTarget();

            ExpandedViewHost.Content = null;
            ExpandedViewHost.Visibility = Visibility.Collapsed;
            MainView.Visibility = Visibility.Visible;

            if (_savedWindowState == WindowState.Maximized)
                this.WindowState = WindowState.Maximized;
            else
            {
                this.WindowState = WindowState.Normal;
                this.Width = _savedWidth;
                this.Height = _savedHeight;
            }

            UpdateQueueDisplay();
        }

        // ═══════════════════════════════════════════════════
        // ЗАПИСЬ РЕГИСТРА
        // ═══════════════════════════════════════════════════

        private async void OnDeviceWriteRequested(object sender, DeviceWriteArgs args)
        {
            try
            {
                bool ok = await _modbusManager.WriteAndRefreshAsync(args.Address, args.Register, args.Value);
                args.Success = ok;
                args.Completion.TrySetResult(ok);
            }
            catch (Exception ex)
            {
                args.Success = false;
                args.Completion.TrySetResult(false);
                TxtStatusMsg.Text = $"Ошибка записи: {ex.Message}";
            }
        }

        // ═══════════════════════════════════════════════════
        // ОБРАБОТЧИКИ СОБЫТИЙ
        // ═══════════════════════════════════════════════════

        private void OnStatusChanged(string status)
        {
            Dispatcher.Invoke(() => { TxtStatusMsg.Text = status; });
        }

        private void OnDevicePolled(EpraDeviceBase device)
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var card = _deviceCards.Find(c => c.DeviceId == device.DeviceAddress);
                card?.UpdateData();

                if (ExpandedViewHost.Content is EpraExpandedWindow expanded
                    && expanded.Device == device)
                {
                    expanded.UpdateAll();
                }

                UpdateQueueDisplay();
            }));
        }

        private void OnDeviceFound(EpraDeviceBase device)
        {
            Dispatcher.Invoke(() =>
            {
                if (!(device is EpraDevice epra)) return;

                _deviceCounter++;
                var card = new EpraWindow(epra);
                card.DeleteRequested += Device_DeleteRequested;
                card.ExpandRequested += Device_ExpandRequested;
                card.DeviceWriteRequested += OnDeviceWriteRequested;
                card.DragStartRequested += Card_DragStartRequested;

                _deviceCards.Add(card);
                DeviceCardsPanel.Children.Add(card);

                UpdateDeviceCount();
                SaveAppState();
            });
        }

        private void OnAutoScanCompleted(List<EpraDeviceBase> devices)
        {
            Dispatcher.Invoke(() =>
            {
                UpdateDeviceCount();
                UpdateQueueDisplay();
                SaveAppState();
            });
        }

        // ═══════════════════════════════════════════════════
        // ОЧЕРЕДЬ ОПРОСА
        // ═══════════════════════════════════════════════════

        private void OnQueueChanged()
        {
            Dispatcher.BeginInvoke(new Action(UpdateQueueDisplay));
        }

        private void OnPollStarted(byte address)
        {
            Dispatcher.BeginInvoke(new Action(UpdateQueueDisplay));
        }

        private void UpdateQueueDisplay()
        {
            if (_modbusManager == null) return;

            var snap = _modbusManager.GetQueueSnapshot();

            int total = snap.queue.Count + (snap.target != 0 ? 1 : 0);

            foreach (var card in _deviceCards)
            {
                byte addr = (byte)card.DeviceId;

                bool isTarget = (snap.target != 0) && (snap.target == addr);

                byte position = 0;
                for (int i = 0; i < snap.queue.Count; i++)
                {
                    if (snap.queue[i] == addr)
                    {
                        position = (byte)(i + 1);
                        break;
                    }
                }

                if (isTarget && position == 0)
                    position = 1;

                card.SetQueueInfo(position, (byte)total, isTarget, snap.current);
            }
        }

        // ═══════════════════════════════════════════════════
        // ЗАГРУЗКА ПОРТОВ / СЧЁТЧИКИ
        // ═══════════════════════════════════════════════════

        private void LoadComPorts()
        {
            var ports = ModbusManager.GetAvailablePorts();
            CmbPort.Items.Clear();
            foreach (var port in ports)
            {
                CmbPort.Items.Add(new ComboBoxItem { Content = port });
            }
            if (CmbPort.Items.Count > 0)
                CmbPort.SelectedIndex = 0;
        }

        private void UpdateDeviceCount()
        {
            if (TxtActiveCount != null)
                TxtActiveCount.Text = _deviceCards.Count.ToString();
        }

        protected override void OnClosed(EventArgs e)
        {
            SaveAppState();

            try { _autoScrollTimer?.Stop(); } catch { }

            try
            {
                _modbusManager?.Dispose();
            }
            catch { }

            base.OnClosed(e);
        }

        private async void CmbPort_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized) return;

            if (!_modbusManager.IsConnected && !_modbusManager.IsPolling)
                return;

            string newPort = (CmbPort.SelectedItem as ComboBoxItem)?.Content.ToString() ?? "COM1";
            int baudRate = 9600;
            if (CmbBaud.SelectedItem is ComboBoxItem baudItem)
                int.TryParse(baudItem.Content.ToString(), out baudRate);

            bool wasPolling = _modbusManager.IsPolling;

            _modbusManager.StopPolling();
            await _modbusManager.DisconnectAsync();
            await Task.Delay(300);

            bool ok = await _modbusManager.ConnectAsync(newPort, baudRate);

            if (ok)
            {
                foreach (var device in _modbusManager.Registry.All)
                {
                    device.Port = newPort;
                }

                if (wasPolling)
                {
                    _modbusManager.StartPolling();
                }

                TxtStatusMsg.Text = $"Порт изменён на {newPort} @ {baudRate}";
            }
            else
            {
                TxtStatusMsg.Text = $"Не удалось открыть {newPort}";
                MessageBox.Show($"Не удалось открыть {newPort}",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            UpdateDisconnectPortButton();
            UpdateQueueDisplay();
        }
    }

}