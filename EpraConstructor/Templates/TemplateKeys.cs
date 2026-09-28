using EpraConstructor.Modbus;

namespace EpraConstructor.Templates
{
    /// <summary>
    /// Соответствие ключей в файле шаблона (.txt) и регистров Modbus.
    /// Формат ключей совместим со старой C++ программой.
    /// </summary>
    public static class TemplateKeys
    {
        public static readonly (string Key, ushort Register)[] Map = new[]
        {
            ("START_MODE",                      EpraConstants.HR_LAMP_START_MODE),
            ("AFTER_POWER_UP_ON_OFF",           EpraConstants.HR_AFTER_POWER_UP_ON_OFF_SWITCH),
            ("AFTER_POWER_UP_SET_PRC",          EpraConstants.HR_AFTER_POWER_UP_SETUP_LAMP_PRC),
            ("AFTER_POWER_UP_TIME_BEFOR_START", EpraConstants.HR_AFTER_POWER_UP_TIME_BEFOR_START_SEC),
            ("NOMINAL_LAMP_CURRENT_MA",         EpraConstants.HR_NOMINAL_LAMP_CURRENT_MA),
            ("NOMINAL_LAMP_POWER_WT",           EpraConstants.HR_NOMINAL_LAMP_POWER_WT),
            ("STAB_MODE",                       EpraConstants.HR_LAMP_STAB_MODE),
            ("HEATING_CATHODES_CURRENT_MA",     EpraConstants.HR_HEATING_CATHODES_CURRENT_MA),
            ("HEATING_CATHODES_TIME_SEC",       EpraConstants.HR_HEATING_CATHODES_TIME_SEC),
            ("HEATING_LAMP_PRC",                EpraConstants.HR_HEATING_LAMP_PRC),
            ("HEATING_LAMP_TIME_SEC",           EpraConstants.HR_HEATING_LAMP_TIME_SEC),
            ("LEAKAGE_FROM_LAMP_CABLE_ON_OFF",  EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_FROM_LAMP_CABLE),
            ("LEAKAGE_IN_LAMP_CABLE_ON_OFF",    EpraConstants.HR_CONFIG_PROTECTION_LEAKAGE_IN_LAMP_CABLE),
        };
    }
}