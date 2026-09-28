using System.Collections.Generic;

namespace EpraConstructor.Devices
{
    /// <summary>
    /// Абстрактный базовый класс для всех ЭПРА-устройств.
    /// Хранит свой адрес и набор регистров — изолированно для каждого экземпляра.
    /// </summary>
    public abstract class EpraDeviceBase
    {
        // Адрес устройства на шине Modbus (1-247)
        public byte DeviceAddress { get; internal set; }

        // COM-порт
        public string Port { get; set; }

        // Скорость порта
        public int BaudRate { get; set; }
        public System.IO.Ports.Parity Parity { get; set; } = System.IO.Ports.Parity.None;
        public System.IO.Ports.StopBits StopBits { get; set; } = System.IO.Ports.StopBits.One;
        // Состояние
        public bool IsConnected { get; set; }
        public string Status { get; set; } = "—";
        public int PollTime { get; set; }

        // Название модели
        public abstract string Model { get; }

        // ═══════════════════════════════════════════════════
        // РЕГИСТРЫ — у каждого экземпляра свои!
        // Аналоговые (только чтение, 30001+)
        // Holding (чтение/запись, 40001+)
        // ═══════════════════════════════════════════════════
        protected Dictionary<ushort, ushort> _analogRegisters = new Dictionary<ushort, ushort>();
        protected Dictionary<ushort, ushort> _holdingRegisters = new Dictionary<ushort, ushort>();

        protected EpraDeviceBase(byte address)
        {
            DeviceAddress = address;
        }

        // Инициализация списка регистров (в наследнике)
        protected abstract void InitializeRegisters();

        // Обновить Analog-свойства из регистров (в наследнике)
        public abstract void UpdateFromRegisters();

        // Обновить Holding-свойства из регистров (в наследнике)
        public abstract void UpdateHoldingProperties();

        // ═══════════════════════════════════════════════════
        // АНАЛОГОВЫЕ РЕГИСТРЫ
        // ═══════════════════════════════════════════════════
        public ushort GetAnalog(ushort address)
        {
            return _analogRegisters.TryGetValue(address, out var val) ? val : (ushort)0;
        }

        public void SetAnalog(ushort address, ushort value)
        {
            // Авто-создание ключа — не теряем данные, если адрес не был предварительно объявлен
            _analogRegisters[address] = value;
        }

        // ═══════════════════════════════════════════════════
        // HOLDING РЕГИСТРЫ
        // ═══════════════════════════════════════════════════
        public ushort GetHolding(ushort address)
        {
            return _holdingRegisters.TryGetValue(address, out var val) ? val : (ushort)0;
        }

        public void SetHolding(ushort address, ushort value)
        {
            // Авто-создание ключа — не теряем данные, если адрес не был предварительно объявлен
            _holdingRegisters[address] = value;
        }

        // Списки адресов
        public IEnumerable<ushort> AnalogAddresses => _analogRegisters.Keys;
        public IEnumerable<ushort> HoldingAddresses => _holdingRegisters.Keys;
    }
}