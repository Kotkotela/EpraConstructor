using System;
using EpraConstructor.Modbus;

namespace EpraConstructor.Devices
{
    public class EpraDevice : EpraDeviceBase
    {
        public override string Model => "ЭПРА-36В-1.0";

        // ═══════════════════════════════════════════════════
        // СВОЙСТВА (обновляются из регистров)
        // ═══════════════════════════════════════════════════

        // ─── Analog-производные (обновляются в UpdateFromRegisters) ───
        public double Voltage { get; set; }
        public double Current { get; set; }
        public double Temperature { get; set; }
        public double Power { get; set; }
        public double LoadPowerPercent { get; set; }
        public double LampCurrentPercent { get; set; }
        public double SetupForStab { get; set; }
        public ushort ErrorFlags { get; set; }
        public ushort CaptureErrorFlags { get; set; }
        public ushort SystemState { get; set; }
        public ushort IgnitionCycles { get; set; }
        public ushort AccidentCycles { get; set; }
        public ushort LampStarts { get; set; }
        public ushort UartErrors { get; set; }

        // Ресурсы
        public ushort EpraResourceHours { get; set; }
        public ushort EpraResourceMinutes { get; set; }
        public ushort LampResourceHours { get; set; }
        public ushort LampResourceMinutes { get; set; }
        public ushort TimeAfterSenior { get; set; }
        public ushort TimeAfterYoungest { get; set; }

        public uint TimeAfterMs => ((uint)TimeAfterSenior << 16) | TimeAfterYoungest;

        // Макс. точки
        public ushort MaxTemperature { get; set; }
        public ushort MaxVoltage { get; set; }

        // ─── Метки утечек (Analog 30033–30036) ───
        public ushort LeakageFromLampCableHours { get; set; }
        public ushort LeakageFromLampCableMinutes { get; set; }
        public ushort LeakageInLampCableHours { get; set; }
        public ushort LeakageInLampCableMinutes { get; set; }

        // Служебные
        public ushort DischargeFreqKhz { get; set; }
        public ushort CathodesFreqKhz { get; set; }
        public ushort TimeBeforeStart { get; set; }
        public ushort LampCurrentMa { get; set; }

        public EpraDevice(byte address) : base(address)
        {
            InitializeRegisters();
        }

        protected override void InitializeRegisters()
        {
            // ═══ Analog Input Registers (30001+) ═══
            _analogRegisters[EpraConstants.AIR_AC_VOLTAGE] = 0;
            _analogRegisters[EpraConstants.AIR_AC_LOAD_POWER_WT] = 0;
            _analogRegisters[EpraConstants.AIR_AC_LOAD_POWER_PRC] = 0;
            _analogRegisters[EpraConstants.AIR_TEMPERATURE_DEG] = 0;
            _analogRegisters[EpraConstants.AIR_ERROR_FLAGS] = 0;
            _analogRegisters[EpraConstants.AIR_CAPTURE_ERROR_FLAGS] = 0;
            _analogRegisters[EpraConstants.AIR_SYSTEM_STATE] = 0;
            _analogRegisters[EpraConstants.AIR_EPRA_RESOURCE_HOURS] = 0;
            _analogRegisters[EpraConstants.AIR_EPRA_RESOURCE_MINUTES] = 0;
            _analogRegisters[EpraConstants.AIR_LAMP_RESOURCE_HOURS] = 0;
            _analogRegisters[EpraConstants.AIR_LAMP_RESOURCE_MINUTES] = 0;
            _analogRegisters[EpraConstants.AIR_LAMP_STARTS] = 0;
            _analogRegisters[EpraConstants.AIR_TIME_BEFOR_START_COUNTER_SEC] = 0;
            _analogRegisters[EpraConstants.AIR_LAMP_CURRENT_MA] = 0;
            _analogRegisters[EpraConstants.AIR_LAMP_CURRENT_PRC] = 0;
            _analogRegisters[EpraConstants.AIR_VIEW_CATHODES_RESISTANCE_RATIO_PRC] = 0;
            _analogRegisters[EpraConstants.AIR_IGNITION_CYCLES] = 0;
            _analogRegisters[EpraConstants.AIR_ACCIDENT_CYCLES] = 0;
            _analogRegisters[EpraConstants.AIR_MAX_TEMPERATURE_DEG] = 0;
            _analogRegisters[EpraConstants.AIR_MAX_TEMPERATURE_POINTER_HOURS] = 0;
            _analogRegisters[EpraConstants.AIR_MAX_TEMPERATURE_POINTER_MINUTES] = 0;
            _analogRegisters[EpraConstants.AIR_MAX_VOLTAGE] = 0;
            _analogRegisters[EpraConstants.AIR_MAX_VOLTAGE_POINTER_HOURS] = 0;
            _analogRegisters[EpraConstants.AIR_MAX_VOLTAGE_POINTER_MINUTES] = 0;
            _analogRegisters[EpraConstants.AIR_DISCHARGE_FREQ_KHZ] = 0;
            _analogRegisters[EpraConstants.AIR_CATHODES_HEATING_FREQ_KHZ] = 0;
            _analogRegisters[EpraConstants.AIR_UART_ERRORS] = 0;
            _analogRegisters[EpraConstants.AIR_LEAKAGE_FROM_LAMP_CABLE_HOURS] = 0;
            _analogRegisters[EpraConstants.AIR_LEAKAGE_FROM_LAMP_CABLE_MINUTES] = 0;
            _analogRegisters[EpraConstants.AIR_LEAKAGE_IN_LAMP_CABLE_HOURS] = 0;
            _analogRegisters[EpraConstants.AIR_LEAKAGE_IN_LAMP_CABLE_MINUTES] = 0;
            _analogRegisters[EpraConstants.AIR_STAB_LAMP_PRC] = 0;
            _analogRegisters[EpraConstants.AIR_TIME_AFTER_YOUNGEST] = 0;
            _analogRegisters[EpraConstants.AIR_TIME_AFTER_SENIOR] = 0;

            // ═══ Holding Registers (40001+) ═══
            _holdingRegisters[EpraConstants.HR_ON_OFF_SWITCH] = 0;
            _holdingRegisters[EpraConstants.HR_SETUP_LAMP_PRC] = 0;
            _holdingRegisters[EpraConstants.HR_SERVICE_LOGIN] = 0;
            _holdingRegisters[EpraConstants.HR_SAVE_SETTING] = 0;
            _holdingRegisters[EpraConstants.HR_RESET_LAMP_RESOURCE] = 0;
            _holdingRegisters[EpraConstants.HR_RESET_SETTING] = 0;
            _holdingRegisters[EpraConstants.HR_MODBUS_ADDRESS] = 0;
            _holdingRegisters[EpraConstants.HR_MODBUS_USART_BAUDRATE_MODE] = 0;
            _holdingRegisters[EpraConstants.HR_MODBUS_USART_PARITY_MODE] = 0;
            _holdingRegisters[EpraConstants.HR_MODBUS_USART_STOPBITS_MODE] = 0;
            _holdingRegisters[EpraConstants.HR_NOMINAL_LAMP_POWER_WT] = 0;
            _holdingRegisters[EpraConstants.HR_AFTER_POWER_UP_TIME_BEFOR_START_SEC] = 0;
            _holdingRegisters[EpraConstants.HR_HEATING_LAMP_PRC] = 0;
            _holdingRegisters[EpraConstants.HR_HEATING_LAMP_TIME_SEC] = 0;
            _holdingRegisters[EpraConstants.HR_CALIBRATE_COEF_AC_VOLTAGE] = 0;
            _holdingRegisters[EpraConstants.HR_CALIBRATE_COEF_AC_POWER] = 0;
            _holdingRegisters[EpraConstants.HR_HEATING_CATHODES_TIME_SEC] = 0;
            _holdingRegisters[EpraConstants.HR_LAMP_START_MODE] = 0;
            _holdingRegisters[EpraConstants.HR_AFTER_POWER_UP_ON_OFF_SWITCH] = 0;
            _holdingRegisters[EpraConstants.HR_AFTER_POWER_UP_SETUP_LAMP_PRC] = 0;
            _holdingRegisters[EpraConstants.HR_MODBUS_AUTO_OFF_TIME_IF_DISCONNECT_SEC] = 0;
            _holdingRegisters[EpraConstants.HR_LAMP_STAB_MODE] = 0;
            _holdingRegisters[EpraConstants.HR_NOMINAL_LAMP_CURRENT_MA] = 0;
            _holdingRegisters[EpraConstants.HR_HEATING_CATHODES_MODE] = 0;
            _holdingRegisters[EpraConstants.HR_HEATING_CATHODES_CURRENT_MA] = 0;
            _holdingRegisters[EpraConstants.HR_HEATING_CATHODES_RESISTANCE_RATIO] = 0;
            _holdingRegisters[EpraConstants.HR_CALIBRATE_COEF_LAMP_CURRENT] = 0;
            _holdingRegisters[EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_FROM_LAMP_CABLE] = 0;
            _holdingRegisters[EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_IN_LAMP_CABLE] = 0;
        }

        // ═══════════════════════════════════════════════════
        // ОБНОВЛЕНИЕ ANALOG-СВОЙСТВ
        // Вызывается после ReadInputRegisters()
        // ═══════════════════════════════════════════════════
        public override void UpdateFromRegisters()
        {
            Voltage = GetAnalog(EpraConstants.AIR_AC_VOLTAGE);
            LoadPowerPercent = GetAnalog(EpraConstants.AIR_AC_LOAD_POWER_PRC);
            Power = GetAnalog(EpraConstants.AIR_AC_LOAD_POWER_WT);
            Temperature = GetAnalog(EpraConstants.AIR_TEMPERATURE_DEG);
            ErrorFlags = GetAnalog(EpraConstants.AIR_ERROR_FLAGS);
            CaptureErrorFlags = GetAnalog(EpraConstants.AIR_CAPTURE_ERROR_FLAGS);
            SystemState = GetAnalog(EpraConstants.AIR_SYSTEM_STATE);
            EpraResourceHours = GetAnalog(EpraConstants.AIR_EPRA_RESOURCE_HOURS);
            EpraResourceMinutes = GetAnalog(EpraConstants.AIR_EPRA_RESOURCE_MINUTES);
            LampResourceHours = GetAnalog(EpraConstants.AIR_LAMP_RESOURCE_HOURS);
            LampResourceMinutes = GetAnalog(EpraConstants.AIR_LAMP_RESOURCE_MINUTES);
            LampStarts = GetAnalog(EpraConstants.AIR_LAMP_STARTS);
            TimeBeforeStart = GetAnalog(EpraConstants.AIR_TIME_BEFOR_START_COUNTER_SEC);
            LampCurrentMa = GetAnalog(EpraConstants.AIR_LAMP_CURRENT_MA);
            LampCurrentPercent = GetAnalog(EpraConstants.AIR_LAMP_CURRENT_PRC);
            IgnitionCycles = GetAnalog(EpraConstants.AIR_IGNITION_CYCLES);
            AccidentCycles = GetAnalog(EpraConstants.AIR_ACCIDENT_CYCLES);
            MaxTemperature = GetAnalog(EpraConstants.AIR_MAX_TEMPERATURE_DEG);
            MaxVoltage = GetAnalog(EpraConstants.AIR_MAX_VOLTAGE);
            DischargeFreqKhz = GetAnalog(EpraConstants.AIR_DISCHARGE_FREQ_KHZ);
            CathodesFreqKhz = GetAnalog(EpraConstants.AIR_CATHODES_HEATING_FREQ_KHZ);
            UartErrors = GetAnalog(EpraConstants.AIR_UART_ERRORS);
            SetupForStab = GetAnalog(EpraConstants.AIR_STAB_LAMP_PRC);
            TimeAfterSenior = GetAnalog(EpraConstants.AIR_TIME_AFTER_SENIOR);
            TimeAfterYoungest = GetAnalog(EpraConstants.AIR_TIME_AFTER_YOUNGEST);
            // ─── Метки утечек ───
            LeakageFromLampCableHours = GetAnalog(EpraConstants.AIR_LEAKAGE_FROM_LAMP_CABLE_HOURS);
            LeakageFromLampCableMinutes = GetAnalog(EpraConstants.AIR_LEAKAGE_FROM_LAMP_CABLE_MINUTES);
            LeakageInLampCableHours = GetAnalog(EpraConstants.AIR_LEAKAGE_IN_LAMP_CABLE_HOURS);
            LeakageInLampCableMinutes = GetAnalog(EpraConstants.AIR_LEAKAGE_IN_LAMP_CABLE_MINUTES);

            // Ток лампы в Амперах
            Current = LampCurrentMa / 1000.0;
        }

        // ═══════════════════════════════════════════════════
        // ОБНОВЛЕНИЕ HOLDING-СВОЙСТВ
        // Вызывается после ReadHoldingRegisters()
        // (Сейчас Holding-свойства — обёртки над GetHolding, метод может быть пустым)
        // ═══════════════════════════════════════════════════
        public override void UpdateHoldingProperties()
        {
            // пусто — свойства-обёртки читают напрямую из _holdingRegisters
        }

        // ═══════════════════════════════════════════════════
        // ВЫСОКОУРОВНЕВЫЕ ОПЕРАЦИИ (Holding)
        // ═══════════════════════════════════════════════════

        public ushort OnOffSwitch
        {
            get => GetHolding(EpraConstants.HR_ON_OFF_SWITCH);
            set => SetHolding(EpraConstants.HR_ON_OFF_SWITCH, value);
        }

        public ushort SetupLampPercent
        {
            get => GetHolding(EpraConstants.HR_SETUP_LAMP_PRC);
            set => SetHolding(EpraConstants.HR_SETUP_LAMP_PRC, value);
        }

        public ushort ServiceLogin
        {
            get => GetHolding(EpraConstants.HR_SERVICE_LOGIN);
            set => SetHolding(EpraConstants.HR_SERVICE_LOGIN, value);
        }

        public ushort SaveSetting
        {
            get => GetHolding(EpraConstants.HR_SAVE_SETTING);
            set => SetHolding(EpraConstants.HR_SAVE_SETTING, value);
        }

        public ushort LampStartMode
        {
            get => GetHolding(EpraConstants.HR_LAMP_START_MODE);
            set => SetHolding(EpraConstants.HR_LAMP_START_MODE, value);
        }

        public ushort LampStabMode
        {
            get => GetHolding(EpraConstants.HR_LAMP_STAB_MODE);
            set => SetHolding(EpraConstants.HR_LAMP_STAB_MODE, value);
        }

        public ushort AfterPowerUpSetPower
        {
            get => GetHolding(EpraConstants.HR_AFTER_POWER_UP_SETUP_LAMP_PRC);
            set => SetHolding(EpraConstants.HR_AFTER_POWER_UP_SETUP_LAMP_PRC, value);
        }

        public ushort AfterPowerUpOnOff
        {
            get => GetHolding(EpraConstants.HR_AFTER_POWER_UP_ON_OFF_SWITCH);
            set => SetHolding(EpraConstants.HR_AFTER_POWER_UP_ON_OFF_SWITCH, value);
        }

        public ushort AfterPowerUpTimeBeforeStart
        {
            get => GetHolding(EpraConstants.HR_AFTER_POWER_UP_TIME_BEFOR_START_SEC);
            set => SetHolding(EpraConstants.HR_AFTER_POWER_UP_TIME_BEFOR_START_SEC, value);
        }

        public ushort NominalLampPower
        {
            get => GetHolding(EpraConstants.HR_NOMINAL_LAMP_POWER_WT);
            set => SetHolding(EpraConstants.HR_NOMINAL_LAMP_POWER_WT, value);
        }

        public ushort NominalLampCurrentMa
        {
            get => GetHolding(EpraConstants.HR_NOMINAL_LAMP_CURRENT_MA);
            set => SetHolding(EpraConstants.HR_NOMINAL_LAMP_CURRENT_MA, value);
        }

        public ushort HeatingCathodesCurrentMa
        {
            get => GetHolding(EpraConstants.HR_HEATING_CATHODES_CURRENT_MA);
            set => SetHolding(EpraConstants.HR_HEATING_CATHODES_CURRENT_MA, value);
        }

        public ushort HeatingCathodesTime
        {
            get => GetHolding(EpraConstants.HR_HEATING_CATHODES_TIME_SEC);
            set => SetHolding(EpraConstants.HR_HEATING_CATHODES_TIME_SEC, value);
        }

        public ushort HeatingLampPercent
        {
            get => GetHolding(EpraConstants.HR_HEATING_LAMP_PRC);
            set => SetHolding(EpraConstants.HR_HEATING_LAMP_PRC, value);
        }

        public ushort HeatingLampTime
        {
            get => GetHolding(EpraConstants.HR_HEATING_LAMP_TIME_SEC);
            set => SetHolding(EpraConstants.HR_HEATING_LAMP_TIME_SEC, value);
        }

        public ushort LeakageFromCableProtection
        {
            get => GetHolding(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_FROM_LAMP_CABLE);
            set => SetHolding(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_FROM_LAMP_CABLE, value);
        }

        public ushort LeakageInCableProtection
        {
            get => GetHolding(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_IN_LAMP_CABLE);
            set => SetHolding(EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_IN_LAMP_CABLE, value);
        }

        public ushort ModbusAddress
        {
            get => GetHolding(EpraConstants.HR_MODBUS_ADDRESS);
            set => SetHolding(EpraConstants.HR_MODBUS_ADDRESS, value);
        }

        public ushort ModbusBaudRate
        {
            get => GetHolding(EpraConstants.HR_MODBUS_USART_BAUDRATE_MODE);
            set => SetHolding(EpraConstants.HR_MODBUS_USART_BAUDRATE_MODE, value);
        }

        public ushort ModbusParity
        {
            get => GetHolding(EpraConstants.HR_MODBUS_USART_PARITY_MODE);
            set => SetHolding(EpraConstants.HR_MODBUS_USART_PARITY_MODE, value);
        }

        public ushort ModbusStopBits
        {
            get => GetHolding(EpraConstants.HR_MODBUS_USART_STOPBITS_MODE);
            set => SetHolding(EpraConstants.HR_MODBUS_USART_STOPBITS_MODE, value);
        }

        public ushort ModbusAutoOffTime
        {
            get => GetHolding(EpraConstants.HR_MODBUS_AUTO_OFF_TIME_IF_DISCONNECT_SEC);
            set => SetHolding(EpraConstants.HR_MODBUS_AUTO_OFF_TIME_IF_DISCONNECT_SEC, value);
        }

        // ═══════════════════════════════════════════════════
        // СЕРВИСНЫЙ РЕЖИМ
        // ═══════════════════════════════════════════════════

        public const ushort NO_SERVICE = 0;
        public const ushort INPUT_SERVICE = 30345;

        public bool IsServiceMode => ServiceLogin == INPUT_SERVICE;
    }
}