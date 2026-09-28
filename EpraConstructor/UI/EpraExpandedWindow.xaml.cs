using EpraConstructor.Devices;
using EpraConstructor.Modbus;
using EpraConstructor.Templates;
using Microsoft.Win32;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;

namespace EpraConstructor
{
    public partial class EpraExpandedWindow : UserControl
    {
        public event EventHandler BackRequested;
        public event EventHandler<DeviceWriteArgs> DeviceWriteRequested;
        public event EventHandler<ReconnectRequestedArgs> ReconnectRequested;

        public EpraDevice Device { get; private set; }

        private bool _isInitialized = false;

        private bool _isSliderLeftDragging = false;
        private bool _isSliderAfterPowerUpDragging = false;

        private bool _isUserEditing = false;
        private bool _saveInProgress = false;
        private bool _isReloadingTemplates = false;

        private readonly Dictionary<ushort, ushort> _pendingWrites = new Dictionary<ushort, ushort>();

        private const ushort NO_SERVICE = 0;
        private const ushort INPUT_SERVICE = 30345;

        private const ushort DO_SAVE_SETTING = 22136;
        private const ushort RESET_LAMP_RESOURCE = 22357;
        private const ushort SAVE_SETTING_OK = 0;
        private const ushort SETTING_IS_CHANGED = 1;
        private const ushort SAVE_SETTING_ERROR = 3;

        private const int BAUD_MODE_OFFSET = 3;

        public EpraExpandedWindow(EpraDevice device)
        {
            InitializeComponent();

            Device = device;
            TxtTitle.Text = $"{device.Model} №{device.DeviceAddress}";

            TemplateManager.LoadSettings();
            ReloadTemplateList();

            _isInitialized = true;

            UpdateAll();
        }

        // ═══════════════════════════════════════════════════
        // MODE-КОДЫ
        // ═══════════════════════════════════════════════════
        private static int ModeToBaudRate(ushort mode)
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

        private static System.IO.Ports.Parity ModeToParity(ushort mode)
        {
            switch (mode)
            {
                case 0: return System.IO.Ports.Parity.None;
                case 1: return System.IO.Ports.Parity.Even;
                case 2: return System.IO.Ports.Parity.Odd;
                default: return System.IO.Ports.Parity.None;
            }
        }

        private static System.IO.Ports.StopBits ModeToStopBits(ushort mode)
        {
            switch (mode)
            {
                case 0: return System.IO.Ports.StopBits.One;
                case 1: return System.IO.Ports.StopBits.Two;
                default: return System.IO.Ports.StopBits.One;
            }
        }

        // ═══════════════════════════════════════════════════
        // ХЕЛПЕРЫ
        // ═══════════════════════════════════════════════════

        private ushort GetEffectiveHolding(ushort register)
        {
            if (_pendingWrites.TryGetValue(register, out var pending))
                return pending;
            return Device?.GetHolding(register) ?? 0;
        }

        private void SetPending(ushort register, ushort value)
        {
            _pendingWrites[register] = value;
        }

        private void ClearPending(ushort register)
        {
            _pendingWrites.Remove(register);
        }

        private void SetToggleSilently(ToggleButton toggle, bool value)
        {
            if (toggle == null) return;
            bool saved = _isInitialized;
            _isInitialized = false;
            toggle.IsChecked = value;
            _isInitialized = saved;
        }

        private void SetSliderSilently(Slider slider, double value)
        {
            if (slider == null) return;
            bool saved = _isInitialized;
            _isInitialized = false;
            slider.Value = value;
            _isInitialized = saved;
        }

        private void SetComboSilently(ComboBox cmb, int index)
        {
            if (cmb == null) return;
            if (index < 0 || index >= cmb.Items.Count) return;
            bool saved = _isInitialized;
            _isInitialized = false;
            cmb.SelectedIndex = index;
            _isInitialized = saved;
        }

        private void SetTextSilently(TextBox tb, string text)
        {
            if (tb == null || tb.IsFocused) return;
            tb.Text = text;
        }

        // ═══════════════════════════════════════════════════
        // СЕРВИСНЫЙ РЕЖИМ
        // ═══════════════════════════════════════════════════

        private bool IsServiceMode => Device?.IsServiceMode ?? false;

        private bool CanSaveSettings()
        {
            if (Device == null) return false;
            return Device.GetHolding(EpraConstants.HR_SAVE_SETTING) == SETTING_IS_CHANGED;
        }

        private void UpdateControlsEnabled()
        {
            bool service = IsServiceMode;

            if (TxtNomPower != null) TxtNomPower.IsEnabled = service;
            if (TxtNomCurrent != null) TxtNomCurrent.IsEnabled = service;
            if (ToggleStabMode != null) ToggleStabMode.IsEnabled = service;
            if (ToggleAfterPowerUp != null) ToggleAfterPowerUp.IsEnabled = service;
            if (SliderAfterPowerUpSet != null) SliderAfterPowerUpSet.IsEnabled = service;
            if (TxtStartPause != null) TxtStartPause.IsEnabled = service;

            if (TxtHeatCathodesCurrent != null) TxtHeatCathodesCurrent.IsEnabled = service;
            if (TxtHeatCathodesTime != null) TxtHeatCathodesTime.IsEnabled = service;
            if (TxtHeatLampPercent != null) TxtHeatLampPercent.IsEnabled = service;
            if (TxtHeatLampTime != null) TxtHeatLampTime.IsEnabled = service;

            if (ToggleLeakCable != null) ToggleLeakCable.IsEnabled = service;
            if (ToggleLeakInternal != null) ToggleLeakInternal.IsEnabled = service;

            if (TxtModbusAddress != null) TxtModbusAddress.IsEnabled = service;
            if (CmbBaudRate != null) CmbBaudRate.IsEnabled = service;
            if (CmbParity != null) CmbParity.IsEnabled = service;
            if (CmbStopBits != null) CmbStopBits.IsEnabled = service;
            if (TxtTimeout != null) TxtTimeout.IsEnabled = service;

            if (BtnSaveSettings != null)
                BtnSaveSettings.IsEnabled = CanSaveSettings();

            if (BtnResetLampResource != null)
                BtnResetLampResource.Visibility = service
                    ? Visibility.Visible
                    : Visibility.Collapsed;

            if (StartModeSelector != null)
                StartModeSelector.IsEnabled = service;

            if (SliderLeftCurrent != null) SliderLeftCurrent.IsEnabled = true;
            if (BtnLamp != null) BtnLamp.IsEnabled = true;

            if (ParamsLampSection != null) ParamsLampSection.IsEnabled = service;
            if (StartupParamsSection != null) StartupParamsSection.IsEnabled = service;
            if (HeatingSection != null) HeatingSection.IsEnabled = service;
            if (ProtectionSection != null) ProtectionSection.IsEnabled = service;
            if (ModbusSection != null) ModbusSection.IsEnabled = service;

            if (BtnApplyTemplate != null) BtnApplyTemplate.IsEnabled = service;
            if (BtnCreateTemplate != null) BtnCreateTemplate.IsEnabled = service;
            if (BtnDeleteTemplate != null) BtnDeleteTemplate.IsEnabled = true;
        }

        private void UpdateServiceModeUI()
        {
            bool isService = IsServiceMode;

            if (BtnServiceMode != null)
            {
                if (isService)
                {
                    BtnServiceMode.Content = "🔓 Выйти из сервиса";
                    BtnServiceMode.Style = (Style)FindResource("BtnServiceExit");
                }
                else
                {
                    BtnServiceMode.Content = "🔒 Войти в сервис";
                    BtnServiceMode.Style = (Style)FindResource("BtnServiceEnter");
                }
            }

            if (ServiceModeBadge != null)
            {
                ServiceModeBadge.Visibility = isService
                    ? Visibility.Visible
                    : Visibility.Collapsed;

                ServiceModeBadge.Background = (Brush)FindResource("ServiceBadgeBgBrush");
                ServiceModeBadge.BorderBrush = (Brush)FindResource("NeonYellowBrush");
            }

            if (TxtServiceBadge != null)
                TxtServiceBadge.Text = "🔓 СЕРВИС";

            UpdateControlsEnabled();
        }

        // ═══════════════════════════════════════════════════
        // РЕЖИМ ЗАПУСКА
        // ═══════════════════════════════════════════════════
        private void UpdateStartModeIndicator(ushort mode)
        {
            if (StartModeBorder7 == null || StartModeBorder4 == null
                || StartModeBorder1 == null || StartModeBorder0 == null)
                return;

            SetStartModeVisual(StartModeBorder7, false);
            SetStartModeVisual(StartModeBorder4, false);
            SetStartModeVisual(StartModeBorder1, false);
            SetStartModeVisual(StartModeBorder0, false);

            switch (mode)
            {
                case 7: SetStartModeVisual(StartModeBorder7, true); break;
                case 4:
                case 5:
                case 6: SetStartModeVisual(StartModeBorder4, true); break;
                case 1:
                case 2:
                case 3: SetStartModeVisual(StartModeBorder1, true); break;
                case 0: SetStartModeVisual(StartModeBorder0, true); break;
            }
        }

        private void SetStartModeVisual(Border b, bool active)
        {
            if (b == null) return;

            if (active)
            {
                b.Opacity = 1.0;
                b.BorderThickness = new Thickness(3);
                b.Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    Color = System.Windows.Media.Colors.White,
                    BlurRadius = 12,
                    ShadowDepth = 0,
                    Opacity = 0.85
                };
            }
            else
            {
                b.Opacity = 0.75;   
                b.BorderThickness = new Thickness(1);
                b.Effect = null;
            }
        }

        private async void StartModeIcon_Click(object sender, MouseButtonEventArgs e)
        {
            if (Device == null || !IsServiceMode) return;

            var border = sender as Border;
            if (border?.Tag == null) return;

            if (!ushort.TryParse(border.Tag.ToString(), out ushort newValue)) return;

            ushort oldValue = Device.LampStartMode;
            if (newValue == oldValue) return;

            bool ok = await SendWriteAsync(EpraConstants.HR_LAMP_START_MODE, newValue);

            if (ok)
            {
                Device.LampStartMode = newValue;
                UpdateStartModeIndicator(newValue);
            }
        }

        // ═══════════════════════════════════════════════════
        // ОБНОВЛЕНИЕ ВСЕХ ПОЛЕЙ
        // ═══════════════════════════════════════════════════
        public void UpdateAll()
        {
            if (Device == null) return;

            PbVoltage.Value = Math.Min(100, Device.SetupForStab);
            TxtVoltage.Text = $"{Device.SetupForStab:0}%";

            PbCurrent.Value = Math.Min(100, Device.LoadPowerPercent);
            TxtCurrent.Text = $"{Device.LoadPowerPercent:0}%";

            PbTemp.Value = Math.Min(100, Device.LampCurrentPercent);
            TxtTemp.Text = $"{Device.LampCurrentPercent:0}%";

            TxtMonVoltage.Text = $"{Device.Voltage:0} В";
            TxtMonStartTime.Text = $"{Device.TimeBeforeStart} с";
            TxtMonPower.Text = $"{Device.Power} Вт";
            TxtMonFreqCathode.Text = $"{Device.CathodesFreqKhz} кГц";
            TxtMonCurrent.Text = $"{Device.Current:0.00} А";
            TxtMonFreqDischarge.Text = $"{Device.DischargeFreqKhz} кГц";
            TxtMonTemp.Text = $"{Device.Temperature:0} °C";
            TxtMonUartErrors.Text = Device.UartErrors.ToString();
            TxtMonIgnitionCycles.Text = Device.IgnitionCycles.ToString();
            TxtMonAlarmCycles.Text = Device.AccidentCycles.ToString();

            if (Device.IsConnected)
            {
                uint timeMs = Device.TimeAfterMs;

                uint hours = timeMs / 3600000;
                uint afterHours = timeMs % 3600000;

                uint minutes = afterHours / 60000;
                uint afterMinutes = afterHours % 60000;

                uint seconds = afterMinutes / 1000;
                uint milliseconds = afterMinutes % 1000;

                TxtUptime.Text = $"{hours}ч :{minutes:00}м :{seconds:00}с :{milliseconds:000}мс";
            }
            else
            {
                TxtUptime.Text = "";
            }

            UpdateErrorFlags(Device.ErrorFlags, Device.CaptureErrorFlags);
            TxtErrorHex.Text = $"[0x{Device.ErrorFlags:X4}]";

            TxtWorkHours.Text = $"{Device.EpraResourceHours} ч";
            TxtWorkMinutes.Text = $"{Device.EpraResourceMinutes} мин";
            TxtMaxVoltage.Text = $"{Device.MaxVoltage} В";
            TxtMaxTemp.Text = $"{Device.MaxTemperature} °C";
            TxtLeakCurrent.Text = $"{Device.LeakageFromLampCableHours:D3}:{Device.LeakageFromLampCableMinutes:D2}";
            TxtLeakCable.Text = $"{Device.LeakageInLampCableHours:D3}:{Device.LeakageInLampCableMinutes:D2}";

            if (StatusDot != null)
                StatusDot.Fill = Device.IsConnected
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0xFF, 0x88))
                    : new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x40));

            if (TxtConnectionStatus != null)
            {
                TxtConnectionStatus.Text = Device.IsConnected ? "ОНЛАЙН" : "ОФФЛАЙН";
                TxtConnectionStatus.Foreground = Device.IsConnected
                    ? new SolidColorBrush(Color.FromRgb(0x00, 0xAA, 0x55))   
                    : new SolidColorBrush(Color.FromRgb(0xFF, 0x00, 0x40)); 
            }

            if (!_isUserEditing)
            {
                ushort setupPrc = GetEffectiveHolding(EpraConstants.HR_SETUP_LAMP_PRC);
                TxtLeftCurrent.Text = $"{setupPrc}%";
                if (!_isSliderLeftDragging)
                    SetSliderSilently(SliderLeftCurrent, setupPrc);

                UpdateStartModeIndicator(GetEffectiveHolding(EpraConstants.HR_LAMP_START_MODE));

                ushort stabMode = GetEffectiveHolding(EpraConstants.HR_LAMP_STAB_MODE);
                SetToggleSilently(ToggleStabMode, stabMode != 0);

                SetTextSilently(TxtNomPower,
                    GetEffectiveHolding(EpraConstants.HR_NOMINAL_LAMP_POWER_WT).ToString());

                SetTextSilently(TxtNomCurrent,
                    (GetEffectiveHolding(EpraConstants.HR_NOMINAL_LAMP_CURRENT_MA) / 1000.0).ToString("0.000"));

                UpdateLampButton(GetEffectiveHolding(EpraConstants.HR_ON_OFF_SWITCH));

                ushort afterPowerUp = GetEffectiveHolding(EpraConstants.HR_AFTER_POWER_UP_ON_OFF_SWITCH);
                SetToggleSilently(ToggleAfterPowerUp, afterPowerUp != 0);

                ushort afterSetPrc = GetEffectiveHolding(EpraConstants.HR_AFTER_POWER_UP_SETUP_LAMP_PRC);
                if (TxtAfterPowerUpSet != null)
                    TxtAfterPowerUpSet.Text = $"{afterSetPrc}%";
                if (!_isSliderAfterPowerUpDragging)
                    SetSliderSilently(SliderAfterPowerUpSet, afterSetPrc);

                SetTextSilently(TxtStartPause,
                    GetEffectiveHolding(EpraConstants.HR_AFTER_POWER_UP_TIME_BEFOR_START_SEC).ToString());

                SetTextSilently(TxtHeatCathodesCurrent,
                    (GetEffectiveHolding(EpraConstants.HR_HEATING_CATHODES_CURRENT_MA) / 1000.0).ToString("0.000"));

                SetTextSilently(TxtHeatCathodesTime,
                    GetEffectiveHolding(EpraConstants.HR_HEATING_CATHODES_TIME_SEC).ToString());

                SetTextSilently(TxtHeatLampPercent,
                    GetEffectiveHolding(EpraConstants.HR_HEATING_LAMP_PRC).ToString());

                SetTextSilently(TxtHeatLampTime,
                    GetEffectiveHolding(EpraConstants.HR_HEATING_LAMP_TIME_SEC).ToString());

                SetToggleSilently(ToggleLeakCable,
                    GetEffectiveHolding(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_FROM_LAMP_CABLE) != 0);
                SetToggleSilently(ToggleLeakInternal,
                    GetEffectiveHolding(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_IN_LAMP_CABLE) != 0);

                SetTextSilently(TxtModbusAddress,
                    GetEffectiveHolding(EpraConstants.HR_MODBUS_ADDRESS).ToString());

                ushort baudMode = GetEffectiveHolding(EpraConstants.HR_MODBUS_USART_BAUDRATE_MODE);
                int baudIdx = baudMode - BAUD_MODE_OFFSET;
                if (baudIdx < 0) baudIdx = 0;
                if (baudIdx >= CmbBaudRate.Items.Count) baudIdx = CmbBaudRate.Items.Count - 1;
                SetComboSilently(CmbBaudRate, baudIdx);

                SetComboSilently(CmbParity,
                    GetEffectiveHolding(EpraConstants.HR_MODBUS_USART_PARITY_MODE));

                SetComboSilently(CmbStopBits,
                    GetEffectiveHolding(EpraConstants.HR_MODBUS_USART_STOPBITS_MODE));

                SetTextSilently(TxtTimeout,
                    GetEffectiveHolding(EpraConstants.HR_MODBUS_AUTO_OFF_TIME_IF_DISCONNECT_SEC).ToString());
            }

            UpdateServiceModeUI();
        }

        // ═══════════════════════════════════════════════════
        // ФЛАГИ ОШИБОК
        // ═══════════════════════════════════════════════════
        private void UpdateErrorFlags(ushort errorFlags, ushort captureFlags)
        {
            SetBit(Bit0, errorFlags, captureFlags, 0);
            SetBit(Bit1, errorFlags, captureFlags, 1);
            SetBit(Bit2, errorFlags, captureFlags, 2);
            SetBit(Bit3, errorFlags, captureFlags, 3);
            SetBit(Bit4, errorFlags, captureFlags, 4);
            SetBit(Bit5, errorFlags, captureFlags, 5);
            SetBit(Bit6, errorFlags, captureFlags, 6);
            SetBit(Bit7, errorFlags, captureFlags, 7);
            SetBit(Bit8, errorFlags, captureFlags, 8);
            SetBit(Bit9, errorFlags, captureFlags, 9);
            SetBit(Bit10, errorFlags, captureFlags, 10);
            SetBit(Bit11, errorFlags, captureFlags, 11);
            SetBit(Bit12, errorFlags, captureFlags, 12);
            SetBit(Bit13, errorFlags, captureFlags, 13);
            SetBit(Bit14, errorFlags, captureFlags, 14);
            SetBit(Bit15, errorFlags, captureFlags, 15);
        }

        private void SetBit(TextBlock box, ushort errorFlags, ushort captureFlags, int bit)
        {
            if (box == null) return;

            if ((errorFlags & (1 << bit)) != 0)
            {
                box.Text = "✕";
                box.Foreground = Brushes.Red;
            }
            else if ((captureFlags & (1 << bit)) != 0)
            {
                box.Text = "✕";
                box.Foreground = Brushes.DarkRed;
            }
            else
            {
                box.Text = "·";
                box.Foreground = (Brush)FindResource("DimTextBrush");
            }
        }

        // ═══════════════════════════════════════════════════
        // ОТПРАВКА ЗАПИСИ
        // ═══════════════════════════════════════════════════
        private async Task<bool> SendWriteAsync(ushort register, ushort value)
        {
            if (Device == null) return false;

            var args = new DeviceWriteArgs(Device.DeviceAddress, register, value);
            DeviceWriteRequested?.Invoke(this, args);

            return await args.Completion.Task;
        }

        private void ShowSaveOverlay(bool show)
        {
            if (SaveOverlay != null)
                SaveOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        }

        // ═══════════════════════════════════════════════════
        // НАВИГАЦИЯ И СЕРВИС
        // ═══════════════════════════════════════════════════
        private void BtnBack_Click(object sender, RoutedEventArgs e)
        {
            BackRequested?.Invoke(this, EventArgs.Empty);
        }

        private async void BtnServiceMode_Click(object sender, RoutedEventArgs e)
        {
            if (Device == null) return;

            bool wantEnter = !IsServiceMode;
            ushort newValue = wantEnter ? INPUT_SERVICE : NO_SERVICE;

            bool ok = await SendWriteAsync(EpraConstants.HR_SERVICE_LOGIN, newValue);

            if (!ok) return;

            UpdateAll();
        }

        private async void BtnSaveSettings_Click(object sender, RoutedEventArgs e)
        {
            if (Device == null) return;
            if (!CanSaveSettings()) return;
            if (_saveInProgress) return;

            _saveInProgress = true;
            ShowSaveOverlay(true);

            try
            {
                // ─── Новые значения из UI ───
                ushort newAddr = GetEffectiveHolding(EpraConstants.HR_MODBUS_ADDRESS);
                ushort newBaudMode = GetEffectiveHolding(EpraConstants.HR_MODBUS_USART_BAUDRATE_MODE);
                ushort newParity = GetEffectiveHolding(EpraConstants.HR_MODBUS_USART_PARITY_MODE);
                ushort newStopBits = GetEffectiveHolding(EpraConstants.HR_MODBUS_USART_STOPBITS_MODE);

                System.Diagnostics.Debug.WriteLine(
                    $"[SaveSettings] new: addr={newAddr}, baud={newBaudMode}, " +
                    $"parity={newParity}, stop={newStopBits}");

                // ─── SaveSetting — команда применить и сохранить во Flash ───
                // Устройство применит новые адрес/baud/parity/stopbits.
                System.Diagnostics.Debug.WriteLine(
                    "[SaveSettings] → write HR_SAVE_SETTING=22136");

                try
                {
                    await SendWriteAsync(EpraConstants.HR_SAVE_SETTING, DO_SAVE_SETTING);
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine(
                        $"[SaveSettings] SaveSetting threw (ожидаемо): {ex.Message}");
                }

                // Ждём, пока устройство применит новые параметры
                await Task.Delay(3500);

                // ─── Переподключение ───
                System.Diagnostics.Debug.WriteLine(
                    "[SaveSettings] → ВЫЗОВ ReconnectRequested");

                TxtConnectionStatus.Text = "ПЕРЕПОДКЛЮЧЕНИЕ...";
                TxtConnectionStatus.Foreground = new SolidColorBrush(Color.FromRgb(0xFF, 0xAA, 0x00));

                ReconnectRequested?.Invoke(this, new ReconnectRequestedArgs
                {
                    DeviceAddress = Device.DeviceAddress,   // СТАРЫЙ (для поиска в реестре)
                    NewAddress = newAddr,                // НОВЫЙ
                    NewBaudMode = newBaudMode,
                    NewParityMode = newParity,
                    NewStopBitsMode = newStopBits
                });

                await Task.Delay(2000);

                TxtTitle.Text = $"{Device.Model} №{Device.DeviceAddress}";
                UpdateAll();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"[SaveSettings] ✗ EXCEPTION: {ex.GetType().Name}: {ex.Message}");

                MessageBox.Show(
                    "Ошибка сохранения настроек: " + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
            finally
            {
                ShowSaveOverlay(false);
                _saveInProgress = false;
                UpdateControlsEnabled();
            }
        }

        private async void BtnResetLampResource_Click(object sender, RoutedEventArgs e)
        {
            if (Device == null || !IsServiceMode) return;

            var result = MessageBox.Show("Сбросить ресурс лампы?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Question);

            if (result != MessageBoxResult.Yes) return;

            await SendWriteAsync(EpraConstants.HR_RESET_LAMP_RESOURCE, RESET_LAMP_RESOURCE);
        }

        // ═══════════════════════════════════════════════════
        // ШАБЛОНЫ
        // ═══════════════════════════════════════════════════
        private void ReloadTemplateList()
        {
            if (CmbTemplates == null) return;

            string previous = CmbTemplates.SelectedItem as string;

            _isReloadingTemplates = true;

            CmbTemplates.Items.Clear();

            foreach (var name in TemplateManager.GetTemplateNames())
                CmbTemplates.Items.Add(name);

            CmbTemplates.Items.Add(TemplateManager.ADD_NEW_MARKER);

            if (!string.IsNullOrEmpty(previous) && previous != TemplateManager.ADD_NEW_MARKER)
            {
                int idx = CmbTemplates.Items.IndexOf(previous);
                if (idx >= 0)
                    CmbTemplates.SelectedIndex = idx;
                else
                    CmbTemplates.SelectedIndex = -1;
            }
            else
            {
                CmbTemplates.SelectedIndex = -1;
            }

            _isReloadingTemplates = false;
        }

        private void CmbTemplates_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isReloadingTemplates) return;
            if (CmbTemplates == null) return;

            string sel = CmbTemplates.SelectedItem as string;
            if (string.IsNullOrEmpty(sel)) return;

            if (sel == TemplateManager.ADD_NEW_MARKER)
            {
                _isReloadingTemplates = true;
                CmbTemplates.SelectedIndex = -1;
                _isReloadingTemplates = false;

                ImportExternalTemplate();
                return;
            }
        }

        private void ImportExternalTemplate()
        {
            TemplateManager.LoadSettings();

            var dlg = new OpenFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                Title = "Выберите файл шаблона",
                InitialDirectory = TemplateManager.FolderPath,
                CheckFileExists = true,
                Multiselect = false
            };

            if (dlg.ShowDialog() != true)
                return;

            try
            {
                string imported = TemplateManager.ImportFile(dlg.FileName);

                ReloadTemplateList();

                int idx = CmbTemplates.Items.IndexOf(imported);
                if (idx >= 0)
                    CmbTemplates.SelectedIndex = idx;

                MessageBox.Show(
                    $"Шаблон добавлен: {imported}",
                    "Импорт", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось добавить шаблон: " + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private async void BtnApplyTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (Device == null) return;

            if (!IsServiceMode)
            {
                MessageBox.Show(
                    "Применение шаблона доступно только в сервисном режиме.",
                    "Сервисный режим", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            string sel = CmbTemplates.SelectedItem as string;
            if (string.IsNullOrEmpty(sel) || sel == TemplateManager.ADD_NEW_MARKER)
            {
                MessageBox.Show(
                    "Сначала выберите шаблон из списка.",
                    "Шаблон не выбран", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var values = TemplateManager.Load(sel);
            if (values.Count == 0)
            {
                MessageBox.Show(
                    $"Не удалось прочитать шаблон «{sel}» (файл пуст или повреждён).",
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            int applied = 0;
            int failed = 0;

            foreach (var (key, register) in TemplateKeys.Map)
            {
                if (!values.TryGetValue(key, out ushort v))
                    continue;

                bool ok = await SendWriteAsync(register, v);
                if (ok) applied++;
                else failed++;
            }

            UpdateAll();

            string msg = $"Шаблон «{sel}» применён.\nЗаписано: {applied}" +
                         (failed > 0 ? $"\nОшибок: {failed}" : "");

            MessageBox.Show(msg, "Готово",
                MessageBoxButton.OK,
                failed > 0 ? MessageBoxImage.Warning : MessageBoxImage.Information);
        }

        private void BtnCreateTemplate_Click(object sender, RoutedEventArgs e)
        {
            if (Device == null) return;

            string suggested = "LAMP-" +
                               GetEffectiveHolding(EpraConstants.HR_NOMINAL_LAMP_POWER_WT) + "-" +
                               GetEffectiveHolding(EpraConstants.HR_NOMINAL_LAMP_CURRENT_MA);

            TemplateManager.LoadSettings();

            var dlg = new SaveFileDialog
            {
                Filter = "Текстовые файлы (*.txt)|*.txt|Все файлы (*.*)|*.*",
                Title = "Сохранить шаблон",
                InitialDirectory = TemplateManager.FolderPath,
                FileName = suggested,
                DefaultExt = ".txt",
                AddExtension = true,
                OverwritePrompt = true
            };

            if (dlg.ShowDialog() != true)
                return;

            string name = Path.GetFileNameWithoutExtension(dlg.FileName);
            if (string.IsNullOrEmpty(name))
                return;

            var values = ReadCurrentValuesFromUI();

            try
            {
                TemplateManager.Save(name, values);
                ReloadTemplateList();

                int idx = CmbTemplates.Items.IndexOf(name);
                if (idx >= 0)
                    CmbTemplates.SelectedIndex = idx;

                MessageBox.Show(
                    $"Шаблон сохранён: {name}",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось сохранить шаблон: " + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnDeleteTemplate_Click(object sender, RoutedEventArgs e)
        {
            string sel = CmbTemplates.SelectedItem as string;
            if (string.IsNullOrEmpty(sel) || sel == TemplateManager.ADD_NEW_MARKER)
            {
                MessageBox.Show(
                    "Сначала выберите шаблон из списка.",
                    "Шаблон не выбран", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            var res = MessageBox.Show(
                $"Удалить шаблон «{sel}»?",
                "Подтверждение", MessageBoxButton.YesNo, MessageBoxImage.Warning,
                MessageBoxResult.No);

            if (res != MessageBoxResult.Yes)
                return;

            try
            {
                TemplateManager.Delete(sel);
                ReloadTemplateList();

                MessageBox.Show($"Шаблон «{sel}» удалён.",
                    "Готово", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Не удалось удалить шаблон: " + ex.Message,
                    "Ошибка", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private Dictionary<string, ushort> ReadCurrentValuesFromUI()
        {
            var values = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

            ushort startMode = 0;
            if (StartModeBorder7 != null && IsBorderHighlighted(StartModeBorder7)) startMode = 7;
            else if (StartModeBorder4 != null && IsBorderHighlighted(StartModeBorder4)) startMode = 4;
            else if (StartModeBorder1 != null && IsBorderHighlighted(StartModeBorder1)) startMode = 1;
            else startMode = 0;
            values["START_MODE"] = startMode;

            values["AFTER_POWER_UP_ON_OFF"] = (ushort)(ToggleAfterPowerUp?.IsChecked == true ? 1 : 0);
            values["AFTER_POWER_UP_SET_PRC"] = (ushort)(SliderAfterPowerUpSet?.Value ?? 0);

            if (ushort.TryParse(TxtStartPause?.Text, out ushort s)) values["AFTER_POWER_UP_TIME_BEFOR_START"] = s;
            if (double.TryParse(TxtNomCurrent?.Text, out double nc)) values["NOMINAL_LAMP_CURRENT_MA"] = (ushort)(nc * 1000);
            if (ushort.TryParse(TxtNomPower?.Text, out ushort np)) values["NOMINAL_LAMP_POWER_WT"] = np;

            values["STAB_MODE"] = (ushort)(ToggleStabMode?.IsChecked == true ? 1 : 0);

            if (double.TryParse(TxtHeatCathodesCurrent?.Text, out double hc)) values["HEATING_CATHODES_CURRENT_MA"] = (ushort)(hc * 1000);
            if (ushort.TryParse(TxtHeatCathodesTime?.Text, out ushort hct)) values["HEATING_CATHODES_TIME_SEC"] = hct;
            if (ushort.TryParse(TxtHeatLampPercent?.Text, out ushort hlp)) values["HEATING_LAMP_PRC"] = hlp;
            if (ushort.TryParse(TxtHeatLampTime?.Text, out ushort hlt)) values["HEATING_LAMP_TIME_SEC"] = hlt;

            values["LEAKAGE_FROM_LAMP_CABLE_ON_OFF"] = (ushort)(ToggleLeakCable?.IsChecked == true ? 1 : 0);
            values["LEAKAGE_IN_LAMP_CABLE_ON_OFF"] = (ushort)(ToggleLeakInternal?.IsChecked == true ? 1 : 0);

            return values;
        }

        private static bool IsBorderHighlighted(Border b)
        {
            if (b?.BorderBrush is SolidColorBrush brush)
            {
                var c = brush.Color;
                return c.R > 0x80 || c.G > 0x80 || c.B > 0x80;
            }
            return false;
        }

        // ═══════════════════════════════════════════════════
        // ЛАМПА
        // ═══════════════════════════════════════════════════
        private void UpdateLampButton(ushort onOff)
        {
            if (BtnLamp == null) return;

            if (onOff != 0)
            {
                BtnLamp.Content = "☀  Выкл";
                BtnLamp.Style = (Style)FindResource("BtnRed");
            }
            else
            {
                BtnLamp.Content = "☀  Вкл";
                BtnLamp.Style = (Style)FindResource("BtnGreen");
            }
        }

        private async void BtnLamp_Click(object sender, RoutedEventArgs e)
        {
            if (Device == null) return;

            ushort current = GetEffectiveHolding(EpraConstants.HR_ON_OFF_SWITCH);
            ushort newValue = (ushort)(current == 0 ? 1 : 0);

            bool ok = await SendWriteAsync(EpraConstants.HR_ON_OFF_SWITCH, newValue);

            if (ok)
            {
                Device.OnOffSwitch = newValue;
                UpdateLampButton(newValue);
            }
        }

        // ═══════════════════════════════════════════════════
        // СЛАЙДЕР ТОКА УСТАВКИ
        // ═══════════════════════════════════════════════════
        private void SliderLeftCurrent_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || Device == null) return;
            if (!_isSliderLeftDragging) return;

            ushort value = (ushort)e.NewValue;
            TxtLeftCurrent.Text = $"{value}%";
            SetPending(EpraConstants.HR_SETUP_LAMP_PRC, value);
        }

        private void SliderLeftCurrent_DragStarted(object sender, RoutedEventArgs e)
        {
            _isSliderLeftDragging = true;
            _isUserEditing = true;
        }

        private async void SliderLeftCurrent_DragCompleted(object sender, RoutedEventArgs e)
        {
            _isSliderLeftDragging = false;
            if (Device == null) { _isUserEditing = false; return; }

            ushort newValue = (ushort)SliderLeftCurrent.Value;
            ushort oldValue = Device.SetupLampPercent;

            if (newValue == oldValue)
            {
                ClearPending(EpraConstants.HR_SETUP_LAMP_PRC);
                _isUserEditing = false;
                return;
            }

            bool ok = await SendWriteAsync(EpraConstants.HR_SETUP_LAMP_PRC, newValue);

            if (ok)
            {
                Device.SetupLampPercent = newValue;
                ClearPending(EpraConstants.HR_SETUP_LAMP_PRC);
            }
            else
            {
                ClearPending(EpraConstants.HR_SETUP_LAMP_PRC);
                SetSliderSilently(SliderLeftCurrent, oldValue);
                TxtLeftCurrent.Text = $"{oldValue}%";
            }

            _isUserEditing = false;
        }

        // ═══════════════════════════════════════════════════
        // ПАРАМЕТРЫ ЗАПУСКА
        // ═══════════════════════════════════════════════════
        private void SliderAfterPowerUpSet_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (!_isInitialized || Device == null) return;
            if (!_isSliderAfterPowerUpDragging) return;

            ushort value = (ushort)e.NewValue;
            if (TxtAfterPowerUpSet != null)
                TxtAfterPowerUpSet.Text = $"{value}%";
            SetPending(EpraConstants.HR_AFTER_POWER_UP_SETUP_LAMP_PRC, value);
        }

        private void SliderAfterPowerUpSet_DragStarted(object sender, RoutedEventArgs e)
        {
            _isSliderAfterPowerUpDragging = true;
            _isUserEditing = true;
        }

        private async void SliderAfterPowerUpSet_DragCompleted(object sender, RoutedEventArgs e)
        {
            _isSliderAfterPowerUpDragging = false;
            if (Device == null || !IsServiceMode) { _isUserEditing = false; return; }

            ushort newValue = (ushort)SliderAfterPowerUpSet.Value;
            ushort oldValue = Device.AfterPowerUpSetPower;

            if (newValue == oldValue)
            {
                ClearPending(EpraConstants.HR_AFTER_POWER_UP_SETUP_LAMP_PRC);
                _isUserEditing = false;
                return;
            }

            bool ok = await SendWriteAsync(EpraConstants.HR_AFTER_POWER_UP_SETUP_LAMP_PRC, newValue);

            if (ok)
            {
                Device.AfterPowerUpSetPower = newValue;
                ClearPending(EpraConstants.HR_AFTER_POWER_UP_SETUP_LAMP_PRC);
            }
            else
            {
                ClearPending(EpraConstants.HR_AFTER_POWER_UP_SETUP_LAMP_PRC);
                SetSliderSilently(SliderAfterPowerUpSet, oldValue);
                if (TxtAfterPowerUpSet != null)
                    TxtAfterPowerUpSet.Text = $"{oldValue}%";
            }

            _isUserEditing = false;
        }

        private async void ToggleAfterPowerUp_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || Device == null) return;

            if (!IsServiceMode)
            {
                SetToggleSilently(ToggleAfterPowerUp, Device.AfterPowerUpOnOff != 0);
                return;
            }

            ushort newValue = ToggleAfterPowerUp.IsChecked == true ? (ushort)1 : (ushort)0;
            ushort oldValue = Device.AfterPowerUpOnOff;

            if (newValue == oldValue) return;

            _isUserEditing = true;
            SetPending(EpraConstants.HR_AFTER_POWER_UP_ON_OFF_SWITCH, newValue);

            bool ok = await SendWriteAsync(EpraConstants.HR_AFTER_POWER_UP_ON_OFF_SWITCH, newValue);

            if (ok)
            {
                Device.AfterPowerUpOnOff = newValue;
                ClearPending(EpraConstants.HR_AFTER_POWER_UP_ON_OFF_SWITCH);
            }
            else
            {
                ClearPending(EpraConstants.HR_AFTER_POWER_UP_ON_OFF_SWITCH);
                SetToggleSilently(ToggleAfterPowerUp, oldValue != 0);
            }

            _isUserEditing = false;
        }

        private void AnyEditTextBox_GotFocus(object sender, RoutedEventArgs e)
        {
            _isUserEditing = true;
        }

        private void AnyEditTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            _isUserEditing = false;
        }

        private async void TxtStartPause_LostFocus(object sender, RoutedEventArgs e)
        {
            AnyEditTextBox_LostFocus(sender, e);

            if (Device == null || !IsServiceMode) return;

            if (ushort.TryParse(TxtStartPause.Text, out ushort value))
            {
                ushort oldValue = Device.AfterPowerUpTimeBeforeStart;

                if (value == oldValue) { ClearPending(EpraConstants.HR_AFTER_POWER_UP_TIME_BEFOR_START_SEC); return; }

                SetPending(EpraConstants.HR_AFTER_POWER_UP_TIME_BEFOR_START_SEC, value);
                bool ok = await SendWriteAsync(EpraConstants.HR_AFTER_POWER_UP_TIME_BEFOR_START_SEC, value);

                if (ok)
                {
                    Device.AfterPowerUpTimeBeforeStart = value;
                    ClearPending(EpraConstants.HR_AFTER_POWER_UP_TIME_BEFOR_START_SEC);
                }
                else
                {
                    ClearPending(EpraConstants.HR_AFTER_POWER_UP_TIME_BEFOR_START_SEC);
                    TxtStartPause.Text = oldValue.ToString();
                }
            }
            else
            {
                TxtStartPause.Text = Device.AfterPowerUpTimeBeforeStart.ToString();
            }
        }

        // ═══════════════════════════════════════════════════
        // ПАРАМЕТРЫ ЛАМПЫ
        // ═══════════════════════════════════════════════════
        private async void ToggleStabMode_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || Device == null) return;

            if (!IsServiceMode)
            {
                SetToggleSilently(ToggleStabMode, Device.LampStabMode != 0);
                return;
            }

            ushort newValue = ToggleStabMode.IsChecked == true ? (ushort)1 : (ushort)0;
            ushort oldValue = Device.LampStabMode;

            if (newValue == oldValue) return;

            _isUserEditing = true;
            SetPending(EpraConstants.HR_LAMP_STAB_MODE, newValue);

            bool ok = await SendWriteAsync(EpraConstants.HR_LAMP_STAB_MODE, newValue);

            if (ok)
            {
                Device.LampStabMode = newValue;
                ClearPending(EpraConstants.HR_LAMP_STAB_MODE);
            }
            else
            {
                ClearPending(EpraConstants.HR_LAMP_STAB_MODE);
                SetToggleSilently(ToggleStabMode, oldValue != 0);
            }

            _isUserEditing = false;
        }

        private async void TxtNomPower_LostFocus(object sender, RoutedEventArgs e)
        {
            AnyEditTextBox_LostFocus(sender, e);

            if (Device == null || !IsServiceMode) return;

            if (ushort.TryParse(TxtNomPower.Text, out ushort value))
            {
                ushort oldValue = Device.NominalLampPower;

                if (value == oldValue) { ClearPending(EpraConstants.HR_NOMINAL_LAMP_POWER_WT); return; }

                SetPending(EpraConstants.HR_NOMINAL_LAMP_POWER_WT, value);
                bool ok = await SendWriteAsync(EpraConstants.HR_NOMINAL_LAMP_POWER_WT, value);

                if (ok)
                {
                    Device.NominalLampPower = value;
                    ClearPending(EpraConstants.HR_NOMINAL_LAMP_POWER_WT);
                }
                else
                {
                    ClearPending(EpraConstants.HR_NOMINAL_LAMP_POWER_WT);
                    TxtNomPower.Text = oldValue.ToString();
                }
            }
            else
            {
                TxtNomPower.Text = Device.NominalLampPower.ToString();
            }
        }

        private async void TxtNomCurrent_LostFocus(object sender, RoutedEventArgs e)
        {
            AnyEditTextBox_LostFocus(sender, e);

            if (Device == null || !IsServiceMode) return;

            if (double.TryParse(TxtNomCurrent.Text, out double amps))
            {
                ushort value = (ushort)(amps * 1000);
                ushort oldValue = Device.NominalLampCurrentMa;

                if (value == oldValue) { ClearPending(EpraConstants.HR_NOMINAL_LAMP_CURRENT_MA); return; }

                SetPending(EpraConstants.HR_NOMINAL_LAMP_CURRENT_MA, value);
                bool ok = await SendWriteAsync(EpraConstants.HR_NOMINAL_LAMP_CURRENT_MA, value);

                if (ok)
                {
                    Device.NominalLampCurrentMa = value;
                    ClearPending(EpraConstants.HR_NOMINAL_LAMP_CURRENT_MA);
                }
                else
                {
                    ClearPending(EpraConstants.HR_NOMINAL_LAMP_CURRENT_MA);
                    TxtNomCurrent.Text = (oldValue / 1000.0).ToString("0.000");
                }
            }
            else
            {
                TxtNomCurrent.Text = (Device.NominalLampCurrentMa / 1000.0).ToString("0.000");
            }
        }

        // ═══════════════════════════════════════════════════
        // ПРОГРЕВ
        // ═══════════════════════════════════════════════════
        private async void TxtHeatCathodesCurrent_LostFocus(object sender, RoutedEventArgs e)
        {
            AnyEditTextBox_LostFocus(sender, e);

            if (Device == null || !IsServiceMode) return;

            if (double.TryParse(TxtHeatCathodesCurrent.Text, out double amps))
            {
                ushort value = (ushort)(amps * 1000);
                ushort oldValue = Device.HeatingCathodesCurrentMa;

                if (value == oldValue) { ClearPending(EpraConstants.HR_HEATING_CATHODES_CURRENT_MA); return; }

                SetPending(EpraConstants.HR_HEATING_CATHODES_CURRENT_MA, value);
                bool ok = await SendWriteAsync(EpraConstants.HR_HEATING_CATHODES_CURRENT_MA, value);

                if (ok)
                {
                    Device.HeatingCathodesCurrentMa = value;
                    ClearPending(EpraConstants.HR_HEATING_CATHODES_CURRENT_MA);
                }
                else
                {
                    ClearPending(EpraConstants.HR_HEATING_CATHODES_CURRENT_MA);
                    TxtHeatCathodesCurrent.Text = (oldValue / 1000.0).ToString("0.000");
                }
            }
            else
            {
                TxtHeatCathodesCurrent.Text = (Device.HeatingCathodesCurrentMa / 1000.0).ToString("0.000");
            }
        }

        private async void TxtHeatCathodesTime_LostFocus(object sender, RoutedEventArgs e)
        {
            AnyEditTextBox_LostFocus(sender, e);

            if (Device == null || !IsServiceMode) return;

            if (ushort.TryParse(TxtHeatCathodesTime.Text, out ushort value))
            {
                ushort oldValue = Device.HeatingCathodesTime;

                if (value == oldValue) { ClearPending(EpraConstants.HR_HEATING_CATHODES_TIME_SEC); return; }

                SetPending(EpraConstants.HR_HEATING_CATHODES_TIME_SEC, value);
                bool ok = await SendWriteAsync(EpraConstants.HR_HEATING_CATHODES_TIME_SEC, value);

                if (ok)
                {
                    Device.HeatingCathodesTime = value;
                    ClearPending(EpraConstants.HR_HEATING_CATHODES_TIME_SEC);
                }
                else
                {
                    ClearPending(EpraConstants.HR_HEATING_CATHODES_TIME_SEC);
                    TxtHeatCathodesTime.Text = oldValue.ToString();
                }
            }
            else
            {
                TxtHeatCathodesTime.Text = Device.HeatingCathodesTime.ToString();
            }
        }

        private async void TxtHeatLampPercent_LostFocus(object sender, RoutedEventArgs e)
        {
            AnyEditTextBox_LostFocus(sender, e);

            if (Device == null || !IsServiceMode) return;

            if (ushort.TryParse(TxtHeatLampPercent.Text, out ushort value))
            {
                ushort oldValue = Device.HeatingLampPercent;

                if (value == oldValue) { ClearPending(EpraConstants.HR_HEATING_LAMP_PRC); return; }

                SetPending(EpraConstants.HR_HEATING_LAMP_PRC, value);
                bool ok = await SendWriteAsync(EpraConstants.HR_HEATING_LAMP_PRC, value);

                if (ok)
                {
                    Device.HeatingLampPercent = value;
                    ClearPending(EpraConstants.HR_HEATING_LAMP_PRC);
                }
                else
                {
                    ClearPending(EpraConstants.HR_HEATING_LAMP_PRC);
                    TxtHeatLampPercent.Text = oldValue.ToString();
                }
            }
            else
            {
                TxtHeatLampPercent.Text = Device.HeatingLampPercent.ToString();
            }
        }

        private async void TxtHeatLampTime_LostFocus(object sender, RoutedEventArgs e)
        {
            AnyEditTextBox_LostFocus(sender, e);

            if (Device == null || !IsServiceMode) return;

            if (ushort.TryParse(TxtHeatLampTime.Text, out ushort value))
            {
                ushort oldValue = Device.HeatingLampTime;

                if (value == oldValue) { ClearPending(EpraConstants.HR_HEATING_LAMP_TIME_SEC); return; }

                SetPending(EpraConstants.HR_HEATING_LAMP_TIME_SEC, value);
                bool ok = await SendWriteAsync(EpraConstants.HR_HEATING_LAMP_TIME_SEC, value);

                if (ok)
                {
                    Device.HeatingLampTime = value;
                    ClearPending(EpraConstants.HR_HEATING_LAMP_TIME_SEC);
                }
                else
                {
                    ClearPending(EpraConstants.HR_HEATING_LAMP_TIME_SEC);
                    TxtHeatLampTime.Text = oldValue.ToString();
                }
            }
            else
            {
                TxtHeatLampTime.Text = Device.HeatingLampTime.ToString();
            }
        }

        // ═══════════════════════════════════════════════════
        // ЗАЩИТА
        // ═══════════════════════════════════════════════════
        private async void ToggleLeakCable_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || Device == null) return;

            if (!IsServiceMode)
            {
                SetToggleSilently(ToggleLeakCable, Device.LeakageFromCableProtection != 0);
                return;
            }

            ushort newValue = ToggleLeakCable.IsChecked == true ? (ushort)1 : (ushort)0;
            ushort oldValue = Device.LeakageFromCableProtection;

            if (newValue == oldValue) return;

            _isUserEditing = true;
            SetPending(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_FROM_LAMP_CABLE, newValue);

            bool ok = await SendWriteAsync(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_FROM_LAMP_CABLE, newValue);

            if (ok)
            {
                Device.LeakageFromCableProtection = newValue;
                ClearPending(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_FROM_LAMP_CABLE);
            }
            else
            {
                ClearPending(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_FROM_LAMP_CABLE);
                SetToggleSilently(ToggleLeakCable, oldValue != 0);
            }

            _isUserEditing = false;
        }

        private async void ToggleLeakInternal_Changed(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || Device == null) return;

            if (!IsServiceMode)
            {
                SetToggleSilently(ToggleLeakInternal, Device.LeakageInCableProtection != 0);
                return;
            }

            ushort newValue = ToggleLeakInternal.IsChecked == true ? (ushort)1 : (ushort)0;
            ushort oldValue = Device.LeakageInCableProtection;

            if (newValue == oldValue) return;

            _isUserEditing = true;
            SetPending(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_IN_LAMP_CABLE, newValue);

            bool ok = await SendWriteAsync(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_IN_LAMP_CABLE, newValue);

            if (ok)
            {
                Device.LeakageInCableProtection = newValue;
                ClearPending(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_IN_LAMP_CABLE);
            }
            else
            {
                ClearPending(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_IN_LAMP_CABLE);
                SetToggleSilently(ToggleLeakInternal, oldValue != 0);
            }

            _isUserEditing = false;
        }

        // ═══════════════════════════════════════════════════
        // MODBUS
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Пользователь ввёл новый адрес. Пишем его СРАЗУ в устройство (в RAM).
        /// Устройство применит его только после HR_SAVE_SETTING.
        /// </summary>
        private async void TxtModbusAddress_LostFocus(object sender, RoutedEventArgs e)
        {
            AnyEditTextBox_LostFocus(sender, e);
            if (Device == null || !IsServiceMode) return;

            if (ushort.TryParse(TxtModbusAddress.Text, out ushort value) && value >= 1 && value <= 247)
            {
                ushort oldValue = Device.GetHolding(EpraConstants.HR_MODBUS_ADDRESS);

                if (value == oldValue)
                {
                    ClearPending(EpraConstants.HR_MODBUS_ADDRESS);
                    return;
                }

                SetPending(EpraConstants.HR_MODBUS_ADDRESS, value);
                bool ok = await SendWriteAsync(EpraConstants.HR_MODBUS_ADDRESS, value);

                if (ok)
                {
                    Device.SetHolding(EpraConstants.HR_MODBUS_ADDRESS, value);
                    ClearPending(EpraConstants.HR_MODBUS_ADDRESS);
                }
                else
                {
                    ClearPending(EpraConstants.HR_MODBUS_ADDRESS);
                    TxtModbusAddress.Text = oldValue.ToString();
                }
            }
            else
            {
                TxtModbusAddress.Text = GetEffectiveHolding(EpraConstants.HR_MODBUS_ADDRESS).ToString();
            }
        }

        /// <summary>
        /// Пользователь выбрал скорость. Пишем её СРАЗУ в устройство (в RAM).
        /// Применение — по HR_SAVE_SETTING.
        /// </summary>
        private async void CmbBaudRate_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || Device == null) return;
            if (CmbBaudRate.SelectedIndex < 0) return;

            if (!IsServiceMode)
            {
                int curIdx = Device.ModbusBaudRate - BAUD_MODE_OFFSET;
                if (curIdx < 0) curIdx = 0;
                if (curIdx >= CmbBaudRate.Items.Count) curIdx = CmbBaudRate.Items.Count - 1;
                SetComboSilently(CmbBaudRate, curIdx);
                return;
            }

            ushort newValue = (ushort)(CmbBaudRate.SelectedIndex + BAUD_MODE_OFFSET);
            ushort oldValue = Device.ModbusBaudRate;

            if (newValue == oldValue) return;

            _isUserEditing = true;
            SetPending(EpraConstants.HR_MODBUS_USART_BAUDRATE_MODE, newValue);

            bool ok = await SendWriteAsync(EpraConstants.HR_MODBUS_USART_BAUDRATE_MODE, newValue);

            if (ok)
            {
                Device.ModbusBaudRate = newValue;
                ClearPending(EpraConstants.HR_MODBUS_USART_BAUDRATE_MODE);
            }
            else
            {
                ClearPending(EpraConstants.HR_MODBUS_USART_BAUDRATE_MODE);
                int oldIdx = oldValue - BAUD_MODE_OFFSET;
                if (oldIdx < 0) oldIdx = 0;
                if (oldIdx >= CmbBaudRate.Items.Count) oldIdx = CmbBaudRate.Items.Count - 1;
                SetComboSilently(CmbBaudRate, oldIdx);
            }

            _isUserEditing = false;
        }

        /// <summary>
        /// Пользователь выбрал чётность. Пишем сразу в RAM. Применение — по HR_SAVE_SETTING.
        /// </summary>
        private async void CmbParity_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || Device == null) return;
            if (CmbParity.SelectedIndex < 0) return;

            if (!IsServiceMode)
            {
                SetComboSilently(CmbParity, Device.ModbusParity);
                return;
            }

            ushort newValue = (ushort)CmbParity.SelectedIndex;
            ushort oldValue = Device.ModbusParity;

            if (newValue == oldValue) return;

            _isUserEditing = true;
            SetPending(EpraConstants.HR_MODBUS_USART_PARITY_MODE, newValue);

            bool ok = await SendWriteAsync(EpraConstants.HR_MODBUS_USART_PARITY_MODE, newValue);

            if (ok)
            {
                Device.ModbusParity = newValue;
                ClearPending(EpraConstants.HR_MODBUS_USART_PARITY_MODE);
            }
            else
            {
                ClearPending(EpraConstants.HR_MODBUS_USART_PARITY_MODE);
                SetComboSilently(CmbParity, oldValue);
            }

            _isUserEditing = false;
        }

        /// <summary>
        /// Пользователь выбрал стоп-биты. Пишем сразу в RAM. Применение — по HR_SAVE_SETTING.
        /// </summary>
        private async void CmbStopBits_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isInitialized || Device == null) return;
            if (CmbStopBits.SelectedIndex < 0) return;

            if (!IsServiceMode)
            {
                SetComboSilently(CmbStopBits, Device.ModbusStopBits);
                return;
            }

            ushort newValue = (ushort)CmbStopBits.SelectedIndex;
            ushort oldValue = Device.ModbusStopBits;

            if (newValue == oldValue) return;

            _isUserEditing = true;
            SetPending(EpraConstants.HR_MODBUS_USART_STOPBITS_MODE, newValue);

            bool ok = await SendWriteAsync(EpraConstants.HR_MODBUS_USART_STOPBITS_MODE, newValue);

            if (ok)
            {
                Device.ModbusStopBits = newValue;
                ClearPending(EpraConstants.HR_MODBUS_USART_STOPBITS_MODE);
            }
            else
            {
                ClearPending(EpraConstants.HR_MODBUS_USART_STOPBITS_MODE);
                SetComboSilently(CmbStopBits, oldValue);
            }

            _isUserEditing = false;
        }

        private async void TxtTimeout_LostFocus(object sender, RoutedEventArgs e)
        {
            AnyEditTextBox_LostFocus(sender, e);

            if (Device == null || !IsServiceMode) return;

            if (ushort.TryParse(TxtTimeout.Text, out ushort value))
            {
                if (value >= 1 && value <= 15) value = 30;
                else if (value >= 16 && value < 30) value = 0;

                ushort oldValue = Device.ModbusAutoOffTime;

                if (value == oldValue) { ClearPending(EpraConstants.HR_MODBUS_AUTO_OFF_TIME_IF_DISCONNECT_SEC); return; }

                SetPending(EpraConstants.HR_MODBUS_AUTO_OFF_TIME_IF_DISCONNECT_SEC, value);
                bool ok = await SendWriteAsync(EpraConstants.HR_MODBUS_AUTO_OFF_TIME_IF_DISCONNECT_SEC, value);

                if (ok)
                {
                    Device.ModbusAutoOffTime = value;
                    TxtTimeout.Text = value.ToString();
                    ClearPending(EpraConstants.HR_MODBUS_AUTO_OFF_TIME_IF_DISCONNECT_SEC);
                }
                else
                {
                    ClearPending(EpraConstants.HR_MODBUS_AUTO_OFF_TIME_IF_DISCONNECT_SEC);
                    TxtTimeout.Text = oldValue.ToString();
                }
            }
            else
            {
                TxtTimeout.Text = Device.ModbusAutoOffTime.ToString();
            }
        }
    }

    // ═══════════════════════════════════════════════════
    // АРГУМЕНТЫ ПЕРЕПОДКЛЮЧЕНИЯ
    // ═══════════════════════════════════════════════════
    public class ReconnectRequestedArgs : EventArgs
    {
        public byte DeviceAddress { get; set; }
        public ushort NewAddress { get; set; }
        public ushort NewBaudMode { get; set; }
        public ushort NewParityMode { get; set; }
        public ushort NewStopBitsMode { get; set; }
    }
}