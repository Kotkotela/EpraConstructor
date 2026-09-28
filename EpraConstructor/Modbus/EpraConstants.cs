namespace EpraConstructor.Modbus
{
    /// <summary>
    /// Адреса регистров Modbus.
    /// </summary>
    public static class EpraConstants
    {
        // ═══════════════════════════════════════════════════
        // ANALOG INPUT REGISTERS (30001+)
        // ═══════════════════════════════════════════════════
        public const ushort AIR_AC_VOLTAGE = 30001;
        public const ushort AIR_AC_LOAD_POWER_WT = 30002;
        public const ushort AIR_AC_LOAD_POWER_PRC = 30003;
        public const ushort AIR_TEMPERATURE_DEG = 30004;
        public const ushort AIR_ERROR_FLAGS = 30005;
        public const ushort AIR_CAPTURE_ERROR_FLAGS = 30006;
        public const ushort AIR_SYSTEM_STATE = 30007;
        public const ushort AIR_EPRA_RESOURCE_HOURS = 30008;
        public const ushort AIR_EPRA_RESOURCE_MINUTES = 30009;
        public const ushort AIR_LAMP_RESOURCE_HOURS = 30010;
        public const ushort AIR_LAMP_RESOURCE_MINUTES = 30011;
        public const ushort AIR_LAMP_STARTS = 30012;
        public const ushort AIR_TIME_BEFOR_START_COUNTER_SEC = 30013;
        public const ushort AIR_LAMP_CURRENT_MA = 30014;
        public const ushort AIR_LAMP_CURRENT_PRC = 30015;
        public const ushort AIR_VIEW_CATHODES_RESISTANCE_RATIO_PRC = 30016;
        public const ushort AIR_IGNITION_CYCLES = 30017;
        public const ushort AIR_ACCIDENT_CYCLES = 30018;
        public const ushort AIR_MAX_TEMPERATURE_DEG = 30020;
        public const ushort AIR_MAX_TEMPERATURE_POINTER_HOURS = 30021;
        public const ushort AIR_MAX_TEMPERATURE_POINTER_MINUTES = 30022;
        public const ushort AIR_MAX_VOLTAGE = 30023;
        public const ushort AIR_MAX_VOLTAGE_POINTER_HOURS = 30024;
        public const ushort AIR_MAX_VOLTAGE_POINTER_MINUTES = 30025;
        public const ushort AIR_DISCHARGE_FREQ_KHZ = 30026;
        public const ushort AIR_CATHODES_HEATING_FREQ_KHZ = 30027;
        public const ushort AIR_UART_ERRORS = 30031;
        public const ushort AIR_LEAKAGE_FROM_LAMP_CABLE_HOURS = 30033;
        public const ushort AIR_LEAKAGE_FROM_LAMP_CABLE_MINUTES = 30034;
        public const ushort AIR_LEAKAGE_IN_LAMP_CABLE_HOURS = 30035;
        public const ushort AIR_LEAKAGE_IN_LAMP_CABLE_MINUTES = 30036;
        public const ushort AIR_STAB_LAMP_PRC = 30041;
        public const ushort AIR_TIME_AFTER_YOUNGEST = 30042;
        public const ushort AIR_TIME_AFTER_SENIOR = 30043;

        // ═══════════════════════════════════════════════════
        // HOLDING REGISTERS (40001+)
        // ═══════════════════════════════════════════════════
        public const ushort HR_ON_OFF_SWITCH = 40001;
        public const ushort HR_SETUP_LAMP_PRC = 40002;
        public const ushort HR_SERVICE_LOGIN = 40014;
        public const ushort HR_SAVE_SETTING = 40015;
        public const ushort HR_RESET_LAMP_RESOURCE = 40016;
        public const ushort HR_RESET_SETTING = 40017;
        public const ushort HR_MODBUS_ADDRESS = 40020;
        public const ushort HR_MODBUS_USART_BAUDRATE_MODE = 40021;
        public const ushort HR_MODBUS_USART_PARITY_MODE = 40022;
        public const ushort HR_MODBUS_USART_STOPBITS_MODE = 40023;
        public const ushort HR_NOMINAL_LAMP_POWER_WT = 40024;
        public const ushort HR_AFTER_POWER_UP_TIME_BEFOR_START_SEC = 40025;
        public const ushort HR_HEATING_LAMP_PRC = 40027;
        public const ushort HR_HEATING_LAMP_TIME_SEC = 40028;
        public const ushort HR_CALIBRATE_COEF_AC_VOLTAGE = 40029;
        public const ushort HR_CALIBRATE_COEF_AC_POWER = 40030;
        public const ushort HR_HEATING_CATHODES_TIME_SEC = 40031;
        public const ushort HR_LAMP_START_MODE = 40032;
        public const ushort HR_AFTER_POWER_UP_ON_OFF_SWITCH = 40033;
        public const ushort HR_AFTER_POWER_UP_SETUP_LAMP_PRC = 40034;
        public const ushort HR_MODBUS_AUTO_OFF_TIME_IF_DISCONNECT_SEC = 40035;
        public const ushort HR_LAMP_STAB_MODE = 40036;
        public const ushort HR_NOMINAL_LAMP_CURRENT_MA = 40037;
        public const ushort HR_HEATING_CATHODES_MODE = 40039;
        public const ushort HR_HEATING_CATHODES_CURRENT_MA = 40040;
        public const ushort HR_HEATING_CATHODES_RESISTANCE_RATIO = 40041;
        public const ushort HR_CALIBRATE_COEF_LAMP_CURRENT = 40043;
        public const ushort HR_CONFIG_PROTECTION_LEAKAGE_FROM_LAMP_CABLE = 40044;
        public const ushort HR_CONFIG_PROTECTION_LEAKAGE_IN_LAMP_CABLE = 40045;

        // ═══════════════════════════════════════════════════
        // ДЛИНЫ ПАКЕТОВ
        // ═══════════════════════════════════════════════════
        public const int ANALOG_REG_LENGTH = 43;
        public const int HOLDING_REG_LENGTH = 45;
        public const int SOFTVER_LENGTH = 40;
        public const int DEVNAME_LENGTH = 32;

        public const ushort AIR_PROJECT_NAME_START = 30065;
        public const ushort AIR_SOFT_VERSION_START = 30081;
    }
}