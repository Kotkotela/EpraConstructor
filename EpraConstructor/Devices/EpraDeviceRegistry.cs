using System.Collections.Generic;
using System.Linq;

namespace EpraConstructor.Devices
{
    /// <summary>
    /// Реестр всех устройств. Потокобезопасный.
    /// </summary>
    public class EpraDeviceRegistry
    {
        private readonly Dictionary<byte, EpraDeviceBase> _devices = new Dictionary<byte, EpraDeviceBase>();
        private readonly object _lock = new object();

        public int Count
        {
            get { lock (_lock) return _devices.Count; }
        }

        public IReadOnlyList<EpraDeviceBase> All
        {
            get
            {
                lock (_lock)
                    return _devices.Values.ToList();
            }
        }

        public bool Contains(byte address)
        {
            lock (_lock) return _devices.ContainsKey(address);
        }

        public void Add(EpraDeviceBase device)
        {
            lock (_lock)
                _devices[device.DeviceAddress] = device;
        }

        public void Remove(byte address)
        {
            lock (_lock)
                _devices.Remove(address);
        }

        public EpraDeviceBase Get(byte address)
        {
            lock (_lock)
            {
                _devices.TryGetValue(address, out var d);
                return d;
            }
        }

        public void Clear()
        {
            lock (_lock)
                _devices.Clear();
        }
    }
}