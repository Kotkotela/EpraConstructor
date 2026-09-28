using System;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using EpraConstructor.Devices;
using EpraConstructor.Modbus;

namespace EpraConstructor
{
    public partial class EpraWindow : UserControl
    {
        public int DeviceId { get; private set; }
        public EpraDevice Device { get; private set; }

        public event EventHandler<int> DeleteRequested;
        public event EventHandler<int> ExpandRequested;
        public event EventHandler<DeviceWriteArgs> DeviceWriteRequested;
        public event EventHandler<DragStartEventArgs> DragStartRequested;

        private bool _isInitialized = false;
        private bool _isSliderDragging = false;

        private static readonly string[] ErrorBitNames = new string[16]
        {
            "ACU_MIN", "ACU_MAX", "ACU", "DISCH_IGN",
            "LAMP_BRC", "LAMP_SHC", "LEAKAGE", "INVERTOR",
            "LAMP_DIODE", "LAMP_STAB", "OVERHEAT", "CAT_HEAT",
            "HW_EEPROM", "DATA_EEPROM", "MB_CON_BREAK", "Резерв"
        };

        public EpraWindow(EpraDevice device)
        {
            InitializeComponent();

            Device = device;
            DeviceId = device.DeviceAddress;

            // Только адрес — в отдельном бейдже
            TxtDeviceAddress.Text = device.DeviceAddress.ToString();

            SliderCurrent.Value = device.SetupLampPercent;
            TxtCurrentValue.Text = $"{device.SetupLampPercent}%";

            SliderCurrent.ValueChanged += SliderCurrent_ValueChanged;
            SliderCurrent.AddHandler(
                Thumb.DragStartedEvent,
                new DragStartedEventHandler(SliderCurrent_DragStarted));
            SliderCurrent.AddHandler(
                Thumb.DragCompletedEvent,
                new DragCompletedEventHandler(SliderCurrent_DragCompleted));

            // Параметры связи — в бейдж рядом с адресом
            UpdateLinkInfo();

            _isInitialized = true;
        }

        // ═══════════════════════════════════════════════════
        // ОБНОВЛЕНИЕ ПОЛЕЙ
        // ═══════════════════════════════════════════════════
        public void UpdateData()
        {
            if (Device == null) return;

            UpdateLinkInfo();

            if (TxtPollTime != null)
                TxtPollTime.Text = $"{Device.PollTime} мс";

            PbVoltage.Value = Math.Min(100, Device.SetupForStab);
            TxtVoltage.Text = $"{Device.SetupForStab:0}%";

            PbCurrent.Value = Math.Min(100, Device.LoadPowerPercent);
            TxtCurrent.Text = $"{Device.LoadPowerPercent:0}%";

            PbTemp.Value = Math.Min(100, Device.LampCurrentPercent);
            TxtTemp.Text = $"{Device.LampCurrentPercent:0}%";

            if (!_isSliderDragging && SliderCurrent != null)
            {
                ushort setupPrc = Device.SetupLampPercent;
                if (Math.Abs(SliderCurrent.Value - setupPrc) > 0.1)
                {
                    _isInitialized = false;
                    SliderCurrent.Value = setupPrc;
                    TxtCurrentValue.Text = $"{setupPrc}%";
                    _isInitialized = true;
                }
            }

            UpdateLampButton(Device.OnOffSwitch);
            UpdateErrorButton(Device.ErrorFlags, Device.CaptureErrorFlags);

            if (Device.IsConnected)
            {
                TxtStatus.Text = "OK";
                TxtStatus.Foreground = (Brush)FindResource("NeonGreenBrush");
                if (StatusDot != null)
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88));
            }
            else
            {
                TxtStatus.Text = "ОТКЛ.";
                TxtStatus.Foreground = (Brush)FindResource("NeonRedBrush");
                if (StatusDot != null)
                    StatusDot.Fill = new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x40));
            }
        }

        // ═══════════════════════════════════════════════════
        // ПАРАМЕТРЫ СВЯЗИ — БЕЙДЖ РЯДОМ С АДРЕСОМ
        // ═══════════════════════════════════════════════════

        private static string FormatParity(System.IO.Ports.Parity p)
        {
            switch (p)
            {
                case System.IO.Ports.Parity.Even: return "E";
                case System.IO.Ports.Parity.Odd: return "O";
                case System.IO.Ports.Parity.Mark: return "M";
                case System.IO.Ports.Parity.Space: return "S";
                default: return "N";
            }
        }

        private static string FormatParityFull(System.IO.Ports.Parity p)
        {
            switch (p)
            {
                case System.IO.Ports.Parity.Even: return "Even";
                case System.IO.Ports.Parity.Odd: return "Odd";
                case System.IO.Ports.Parity.Mark: return "Mark";
                case System.IO.Ports.Parity.Space: return "Space";
                default: return "None";
            }
        }

        private static string FormatStopBits(System.IO.Ports.StopBits s)
        {
            switch (s)
            {
                case System.IO.Ports.StopBits.Two: return "2";
                case System.IO.Ports.StopBits.OnePointFive: return "1.5";
                default: return "1";
            }
        }

        public void UpdateLinkInfo()
        {
            if (Device == null || TxtLinkInfo == null) return;

            // Компактно: 9600-8N1
            TxtLinkInfo.Text = $"{Device.BaudRate}-8{FormatParity(Device.Parity)}{FormatStopBits(Device.StopBits)}";

            string port = string.IsNullOrEmpty(Device.Port) ? "—" : Device.Port;
            TxtLinkInfo.ToolTip =
                $"{port}\n" +
                $"{Device.BaudRate} baud\n" +
                $"8 data bits\n" +
                $"{FormatParityFull(Device.Parity)} parity\n" +
                $"{FormatStopBits(Device.StopBits)} stop bit(s)";
        }

        // ═══════════════════════════════════════════════════
        // КНОПКА ЛАМПА
        // ═══════════════════════════════════════════════════
        private void UpdateLampButton(ushort onOff)
        {
            if (BtnLamp == null) return;

            if (onOff != 0)
            {
                BtnLamp.Content = "☀  Выкл";
                BtnLamp.Style = (Style)FindResource("BtnGreen");
            }
            else
            {
                BtnLamp.Content = "☀  Вкл";
                BtnLamp.Style = (Style)FindResource("BtnRed");
            }
        }

        // ═══════════════════════════════════════════════════
        // КНОПКА ОШИБОК
        // ═══════════════════════════════════════════════════
        private void UpdateErrorButton(ushort errorFlags, ushort captureFlags)
        {
            if (BtnErrors == null) return;

            ushort allFlags = (ushort)(errorFlags | captureFlags);
            int count = 0;
            for (int i = 0; i < 16; i++)
                if ((allFlags & (1 << i)) != 0) count++;

            if (count == 0)
            {
                BtnErrors.Content = "✕  Нет ошибок";
            }
            else
            {
                BtnErrors.Content = $"⚠  Ошибок: {count}";
            }
        }


        private void BuildErrorsList(ushort errorFlags, ushort captureFlags)
        {
            if (ErrorsList == null) return;
            ErrorsList.Children.Clear();

            ushort allFlags = (ushort)(errorFlags | captureFlags);
            int count = 0;

            for (int i = 0; i < 16; i++)
            {
                if ((allFlags & (1 << i)) == 0) continue;
                count++;

                bool current = (errorFlags & (1 << i)) != 0;
                Brush color = current ? Brushes.Red : Brushes.DarkRed;
                string suffix = current ? " (текущая)" : " (захвачена)";

                ErrorsList.Children.Add(new TextBlock
                {
                    Text = $"✕  [{i}] {ErrorBitNames[i]}{suffix}",
                    FontFamily = new FontFamily("Consolas"),
                    FontSize = 11,
                    Foreground = color,
                    Margin = new Thickness(0, 2, 0, 2)
                });
            }

            if (ErrorsEmptyText != null)
                ErrorsEmptyText.Visibility = count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnErrors_Click(object sender, RoutedEventArgs e)
        {
            if (Device == null) return;

            BuildErrorsList(Device.ErrorFlags, Device.CaptureErrorFlags);
            ErrorsPopup.IsOpen = !ErrorsPopup.IsOpen;
        }

        // ═══════════════════════════════════════════════════
        // ОЧЕРЕДЬ ОПРОСА
        // ═══════════════════════════════════════════════════
        public void SetQueueInfo(byte position, byte total, bool isTarget, byte currentAddress)
        {
            if (TxtQueue == null || TxtQueueSep == null) return;

            if (total == 0)
            {
                TxtQueue.Visibility = Visibility.Collapsed;
                TxtQueueSep.Visibility = Visibility.Collapsed;
                TxtQueue.Text = "";
                return;
            }

            TxtQueueSep.Visibility = Visibility.Visible;
            TxtQueue.Visibility = Visibility.Visible;

            if (isTarget)
            {
                TxtQueue.Text = "🎯 target";
                TxtQueue.Foreground = (Brush)FindResource("NeonGreenBrush");
            }
            else
            {
                TxtQueue.Text = $"#{position}/{total}";
                TxtQueue.Foreground = (Brush)FindResource("NeonCyanBrush");
            }
        }

        // ═══════════════════════════════════════════════════
        // DRAG & DROP — ВИЗУАЛ
        // ═══════════════════════════════════════════════════

        public void SetDraggingVisual(bool dragging)
        {
            if (CardShadow == null || CardBorder == null) return;

            if (dragging)
            {
                CardShadow.Color = Color.FromRgb(0x00, 0xFF, 0x88);
                CardShadow.BlurRadius = 20;
                CardShadow.ShadowDepth = 6;
                CardShadow.Opacity = 0.9;

                CardBorder.Opacity = 0.6;
                Cursor = Cursors.Hand;
            }
            else
            {
                CardShadow.BlurRadius = 0;
                CardShadow.ShadowDepth = 0;
                CardShadow.Opacity = 0;

                CardBorder.Opacity = 1.0;
                Cursor = Cursors.Arrow;
            }
        }


        public void RefreshAddress()
        {
            if (Device == null) return;

            DeviceId = Device.DeviceAddress;

            if (TxtDeviceAddress != null)
                TxtDeviceAddress.Text = Device.DeviceAddress.ToString();

            UpdateLinkInfo();
        }
        public void SetSwapVisual(bool active)
        {
            if (SwapBorder == null) return;
            SwapBorder.Visibility = active ? Visibility.Visible : Visibility.Collapsed;
        }

        public void SetInsertVisual(bool active, bool before)
        {
            if (InsertLeftBar == null || InsertRightBar == null) return;

            InsertLeftBar.Visibility = (active && before) ? Visibility.Visible : Visibility.Collapsed;
            InsertRightBar.Visibility = (active && !before) ? Visibility.Visible : Visibility.Collapsed;
        }

        public void ClearDropVisual()
        {
            if (InsertLeftBar != null) InsertLeftBar.Visibility = Visibility.Collapsed;
            if (InsertRightBar != null) InsertRightBar.Visibility = Visibility.Collapsed;
            if (SwapBorder != null) SwapBorder.Visibility = Visibility.Collapsed;
        }

        // ═══════════════════════════════════════════════════
        // АТОМАРНАЯ ЗАПИСЬ
        // ═══════════════════════════════════════════════════
        private async Task<bool> SendWriteAsync(ushort register, ushort value)
        {
            if (Device == null) return false;

            var args = new DeviceWriteArgs(Device.DeviceAddress, register, value);
            DeviceWriteRequested?.Invoke(this, args);

            var completed = await Task.WhenAny(args.Completion.Task, Task.Delay(5000));
            if (completed == args.Completion.Task)
                return args.Completion.Task.Result;

            return false;
        }

        // ═══════════════════════════════════════════════════
        // СЛАЙДЕР
        // ═══════════════════════════════════════════════════
        private void SliderCurrent_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized) return;
            ushort value = (ushort)e.NewValue;
            if (TxtCurrentValue != null)
                TxtCurrentValue.Text = $"{value}%";
        }

        private void SliderCurrent_DragStarted(object sender, DragStartedEventArgs e)
        {
            _isSliderDragging = true;
        }

        private async void SliderCurrent_DragCompleted(object sender, DragCompletedEventArgs e)
        {
            _isSliderDragging = false;
            if (Device == null) return;

            ushort newValue = (ushort)SliderCurrent.Value;
            ushort oldValue = Device.SetupLampPercent;

            if (newValue == oldValue) return;

            bool ok = await SendWriteAsync(EpraConstants.HR_SETUP_LAMP_PRC, newValue);

            if (!ok)
            {
                _isInitialized = false;
                SliderCurrent.Value = oldValue;
                TxtCurrentValue.Text = $"{oldValue}%";
                _isInitialized = true;
            }
        }

        // ═══════════════════════════════════════════════════
        // ЛАМПА
        // ═══════════════════════════════════════════════════
        private async void BtnLamp_Click(object sender, RoutedEventArgs e)
        {
            if (Device == null) return;

            ushort current = Device.OnOffSwitch;
            ushort newValue = (ushort)(current == 0 ? 1 : 0);

            bool ok = await SendWriteAsync(EpraConstants.HR_ON_OFF_SWITCH, newValue);

            if (ok)
            {
                Device.OnOffSwitch = newValue;
                UpdateLampButton(newValue);
            }
        }

        // ═══════════════════════════════════════════════════
        // РАЗВЕРНУТЬ / УДАЛИТЬ
        // ═══════════════════════════════════════════════════
        private void BtnExpand_Click(object sender, RoutedEventArgs e)
        {
            ExpandRequested?.Invoke(this, DeviceId);
        }

        private void BtnDelete_Click(object sender, RoutedEventArgs e)
        {
            DeleteRequested?.Invoke(this, DeviceId);
        }

        // ═══════════════════════════════════════════════════
        // DRAG & DROP — НАЧАЛО
        // ═══════════════════════════════════════════════════
        private void DeviceHeader_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragStartRequested?.Invoke(this, new DragStartEventArgs(DeviceId, e));
        }
    }

    // ═══════════════════════════════════════════════════
    // АРГУМЕНТЫ ЗАПИСИ
    // ═══════════════════════════════════════════════════
    public class DeviceWriteArgs : EventArgs
    {
        public byte Address { get; }
        public ushort Register { get; }
        public ushort Value { get; }
        public TaskCompletionSource<bool> Completion { get; } = new TaskCompletionSource<bool>();
        public bool Success { get; set; } = false;

        public DeviceWriteArgs(byte address, ushort register, ushort value)
        {
            Address = address;
            Register = register;
            Value = value;
        }
    }

    // ═══════════════════════════════════════════════════
    // АРГУМЕНТЫ DRAG & DROP
    // ═══════════════════════════════════════════════════
    public class DragStartEventArgs : EventArgs
    {
        public int DeviceId { get; }
        public MouseButtonEventArgs MouseArgs { get; }

        public DragStartEventArgs(int deviceId, MouseButtonEventArgs args)
        {
            DeviceId = deviceId;
            MouseArgs = args;
        }
    }
}