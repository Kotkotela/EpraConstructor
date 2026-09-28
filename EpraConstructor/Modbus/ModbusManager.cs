using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EasyModbus;
using EpraConstructor.Devices;

namespace EpraConstructor.Modbus
{
    public class ModbusManager : IDisposable
    {
        // ═══════════════════════════════════════════════════
        // СОБЫТИЯ ДЛЯ UI
        // ═══════════════════════════════════════════════════
        public event Action<byte> PollStarted;   // начался опрос #N
        public event Action QueueChanged;        // очередь изменилась

        public event Action<EpraDeviceBase> DevicePolled;
        public event Action<EpraDeviceBase> DeviceFound;
        public event Action<string> StatusChanged;
        public event Action<List<EpraDeviceBase>> AutoScanCompleted;

        // ═══════════════════════════════════════════════════
        // СВОЙСТВА
        // ═══════════════════════════════════════════════════
        public bool IsConnected => _modbusClient != null;
        public bool IsPolling { get; private set; }
        public bool IsAutoScanning { get; private set; }

        public EpraDeviceRegistry Registry { get; } = new EpraDeviceRegistry();

        // Текущее опрашиваемое устройство (для UI)
        private volatile byte _currentPollingAddress = 0;

        // ═══════════════════════════════════════════════════
        // РАБОЧИЙ ПОТОК И ОЧЕРЕДЬ
        // ═══════════════════════════════════════════════════
        private readonly BlockingCollection<Action> _queue = new BlockingCollection<Action>();
        private Thread _workerThread;
        private volatile bool _workerRunning = false;

        // ═══════════════════════════════════════════════════
        // СОСТОЯНИЕ ПОРТА — ТОЛЬКО ИЗ _workerThread
        // ═══════════════════════════════════════════════════
        private string _currentPort;
        private int _currentBaudRate;
        private System.IO.Ports.Parity _currentParity = System.IO.Ports.Parity.None;
        private System.IO.Ports.StopBits _currentStopBits = System.IO.Ports.StopBits.One;

        // ─── Флаг «пользователь отключил порт» ───
        // Пока true — EnsurePortMatches НЕ открывает порт.
        // Сбрасывается при ConnectInternal.
        private volatile bool _userDisconnected = false;

        private ModbusClient _modbusClient;
        private readonly object _lock = new object();

        // Циклический опрос
        private CancellationTokenSource _pollingCts;
        private Task _pollingTask;
        private readonly Queue<EpraDeviceBase> _pollQueue = new Queue<EpraDeviceBase>();

        // ─── TARGET: приоритетное устройство ───
        private EpraDeviceBase _priorityDevice;
        private volatile bool _targetActive = false;

        // Автосканирование
        private CancellationTokenSource _scanCts;
        private Task _scanTask;

        // ═══════════════════════════════════════════════════
        // КОНСТРУКТОР
        // ═══════════════════════════════════════════════════
        public ModbusManager()
        {
            _workerRunning = true;
            _workerThread = new Thread(WorkerLoop)
            {
                IsBackground = true,
                Name = "ModbusWorker"
            };
            _workerThread.Start();
        }

        private void WorkerLoop()
        {
            Log("[MB] worker thread started");
            try
            {
                foreach (var action in _queue.GetConsumingEnumerable())
                {
                    try { action(); }
                    catch (Exception ex)
                    {
                        Log($"[MB worker] UNHANDLED: {ex.GetType().Name}: {ex.Message}");
                    }
                }
            }
            catch (Exception ex)
            {
                Log($"[MB worker] loop crashed: {ex.Message}");
            }
            Log("[MB] worker thread stopped");
        }

        // ═══════════════════════════════════════════════════
        // ПУБЛИЧНЫЕ API
        // ═══════════════════════════════════════════════════

        public static string[] GetAvailablePorts()
        {
            try { return SerialPort.GetPortNames().OrderBy(p => p).ToArray(); }
            catch { return new string[0]; }
        }

        public Task<bool> ConnectAsync(string port, int baudRate)
        {
            return ConnectAsync(port, baudRate,
                System.IO.Ports.Parity.None,
                System.IO.Ports.StopBits.One);
        }

        public Task<bool> ConnectAsync(string port, int baudRate,
            System.IO.Ports.Parity parity,
            System.IO.Ports.StopBits stopBits)
        {
            var tcs = new TaskCompletionSource<bool>();
            _queue.Add(() =>
            {
                try { tcs.SetResult(ConnectInternal(port, baudRate, parity, stopBits)); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        public Task DisconnectAsync()
        {
            var tcs = new TaskCompletionSource<bool>();
            _queue.Add(() =>
            {
                try { DisconnectInternal(); tcs.SetResult(true); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        public Task<bool> WriteAndRefreshAsync(byte address, ushort register, ushort value)
        {
            var tcs = new TaskCompletionSource<bool>();
            _queue.Add(() =>
            {
                try { tcs.SetResult(WriteAndRefreshInternal(address, register, value)); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        public Task<bool> WriteHoldingAsync(byte address, ushort fullAddress, ushort value)
        {
            var tcs = new TaskCompletionSource<bool>();
            _queue.Add(() =>
            {
                try { tcs.SetResult(WriteHoldingInternal(address, fullAddress, value)); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        public Task<ushort?> ReadHoldingRegisterAsync(byte address, ushort fullAddress)
        {
            var tcs = new TaskCompletionSource<ushort?>();
            _queue.Add(() =>
            {
                try { tcs.SetResult(ReadHoldingRegisterInternal(address, fullAddress)); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        public Task<bool> AutoScanAsync(string port, int baudRate)
        {
            var tcs = new TaskCompletionSource<bool>();
            _queue.Add(() =>
            {
                try { tcs.SetResult(AutoScanInternal(port, baudRate)); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }
        public Task<bool> AutoScanAllBaudRatesAsync(string port, int[] baudRates)
        {
            var tcs = new TaskCompletionSource<bool>();
            _queue.Add(() =>
            {
                try { tcs.SetResult(AutoScanAllBaudRatesInternal(port, baudRates)); }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }
        private bool AutoScanAllBaudRatesInternal(string port, int[] baudRates)
        {
            StopPolling();
            ClearDevices();

            _scanCts = new CancellationTokenSource();
            var localCts = _scanCts;

            IsAutoScanning = true;
            Log($"[MB] AutoScanAll started on {port}, {baudRates.Length} baud rates");
            StatusChanged?.Invoke($"Автосканирование: {baudRates.Length} скоростей...");

            var foundDevices = new List<EpraDeviceBase>();
            var foundAddresses = new HashSet<byte>();

            var parity = System.IO.Ports.Parity.None;
            var stopBits = System.IO.Ports.StopBits.One;

            for (int bi = 0; bi < baudRates.Length; bi++)
            {
                int baudRate = baudRates[bi];

                try { if (localCts.Token.IsCancellationRequested) break; }
                catch { break; }

                Log($"[MB] AutoScanAll: baud #{bi + 1}/{baudRates.Length} = {baudRate}");

                if (!ConnectInternal(port, baudRate, parity, stopBits))
                {
                    Log($"[MB] AutoScanAll: не удалось открыть порт на {baudRate}");
                    continue;
                }

                for (byte address = 1; address <= 247; address++)
                {
                    try { if (localCts.Token.IsCancellationRequested) break; }
                    catch { break; }

                    if (_modbusClient == null) break;

                    if (foundAddresses.Contains(address)) continue;

                    try
                    {
                        StatusChanged?.Invoke(
                            $"Проверка {baudRate} бод, адрес {address}/247 " +
                            $"(найдено: {foundDevices.Count})");

                        if (ProbeDeviceInternal(address))
                        {
                            Log($"[MB] AutoScanAll: found #{address} @ {baudRate}");

                            var device = new EpraDevice(address)
                            {
                                Port = port,
                                BaudRate = baudRate,
                                Parity = parity,
                                StopBits = stopBits,
                                IsConnected = true,
                                Status = "OK"
                            };

                            lock (_lock)
                            {
                                foundDevices.Add(device);
                                foundAddresses.Add(address);

                                if (!Registry.Contains(address))
                                {
                                    Registry.Add(device);
                                    _pollQueue.Enqueue(device);
                                }
                            }

                            DeviceFound?.Invoke(device);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log($"[MB] AutoScanAll probe #{address} @ {baudRate} threw: {ex.Message}");
                    }
                }
            }

            IsAutoScanning = false;
            AutoScanCompleted?.Invoke(foundDevices);
            StatusChanged?.Invoke($"Найдено: {foundDevices.Count} устройств на {baudRates.Length} скоростях");
            Log($"[MB] AutoScanAll finished: {foundDevices.Count} devices");

            try { QueueChanged?.Invoke(); } catch { }

            return true;
        }
        // ═══════════════════════════════════════════════════
        // РЕЕСТР
        // ═══════════════════════════════════════════════════

        public void AddDevice(EpraDeviceBase device)
        {
            Registry.Add(device);
            lock (_lock)
            {
                if (!_pollQueue.Contains(device))
                    _pollQueue.Enqueue(device);
            }
            Log($"[MB] AddDevice #{device.DeviceAddress}");
            try { QueueChanged?.Invoke(); } catch { }
        }

        public void RemoveDevice(byte address)
        {
            lock (_lock)
            {
                Registry.Remove(address);

                var temp = new List<EpraDeviceBase>();
                while (_pollQueue.Count > 0)
                {
                    var d = _pollQueue.Dequeue();
                    if (d.DeviceAddress != address)
                        temp.Add(d);
                }
                foreach (var d in temp)
                    _pollQueue.Enqueue(d);

                if (_targetActive && _priorityDevice != null
                    && _priorityDevice.DeviceAddress == address)
                {
                    _priorityDevice = null;
                    _targetActive = false;
                }

                if (_currentPollingAddress == address)
                    _currentPollingAddress = 0;
            }

            Log($"[MB] RemoveDevice #{address}");
            try { QueueChanged?.Invoke(); } catch { }
        }

        public void ClearDevices()
        {
            lock (_lock)
            {
                Registry.Clear();
                _pollQueue.Clear();
                _priorityDevice = null;
                _targetActive = false;
                _currentPollingAddress = 0;
            }
            Log("[MB] ClearDevices");
            try { QueueChanged?.Invoke(); } catch { }
        }

        // ═══════════════════════════════════════════════════
        // ТАРГЕТ
        // ═══════════════════════════════════════════════════

        public void SetTarget(EpraDeviceBase device)
        {
            lock (_lock)
            {
                _priorityDevice = device;
                _targetActive = true;
            }
            Log($"[MB] SetTarget #{device?.DeviceAddress}");
            try { QueueChanged?.Invoke(); } catch { }
        }

        public void ClearTarget()
        {
            lock (_lock)
            {
                _priorityDevice = null;
                _targetActive = false;
            }
            Log("[MB] ClearTarget");
            try { QueueChanged?.Invoke(); } catch { }
        }

        // ═══════════════════════════════════════════════════
        // СНИМОК ОЧЕРЕДИ
        // ═══════════════════════════════════════════════════

        public (byte target, byte current, List<byte> queue) GetQueueSnapshot()
        {
            var list = new List<byte>();
            byte target = 0;
            byte current = _currentPollingAddress;

            lock (_lock)
            {
                if (_targetActive && _priorityDevice != null)
                    target = _priorityDevice.DeviceAddress;

                foreach (var d in _pollQueue)
                {
                    if (_targetActive && d == _priorityDevice) continue;
                    list.Add(d.DeviceAddress);
                }
            }

            return (target, current, list);
        }

        public void SetQueueOrder(List<byte> orderedAddresses)
        {
            lock (_lock)
            {
                var dict = new Dictionary<byte, EpraDeviceBase>();
                foreach (var d in _pollQueue)
                    dict[d.DeviceAddress] = d;

                _pollQueue.Clear();

                foreach (var addr in orderedAddresses)
                {
                    if (dict.TryGetValue(addr, out var d))
                        _pollQueue.Enqueue(d);
                }

                foreach (var kv in dict)
                {
                    if (!orderedAddresses.Contains(kv.Key))
                        _pollQueue.Enqueue(kv.Value);
                }
            }

            Log("[MB] SetQueueOrder");
            try { QueueChanged?.Invoke(); } catch { }
        }

        // ═══════════════════════════════════════════════════
        // ЦИКЛИЧЕСКИЙ ОПРОС
        // ═══════════════════════════════════════════════════

        public void StartPolling()
        {
            if (IsPolling) return;
            IsPolling = true;
            _pollingCts = new CancellationTokenSource();
            var localCts = _pollingCts;

            _pollingTask = Task.Run(async () =>
            {
                while (true)
                {
                    try { if (localCts.Token.IsCancellationRequested) break; }
                    catch { break; }

                    EpraDeviceBase deviceToPoll = null;

                    lock (_lock)
                    {
                        if (_targetActive && _priorityDevice != null)
                        {
                            deviceToPoll = _priorityDevice;
                        }
                        else if (_pollQueue.Count > 0)
                        {
                            deviceToPoll = _pollQueue.Dequeue();
                            _pollQueue.Enqueue(deviceToPoll);
                        }
                    }

                    if (deviceToPoll != null)
                    {
                        try
                        {
                            await PollOneDeviceAsync(deviceToPoll, localCts.Token);
                        }
                        catch (Exception ex)
                        {
                            Log($"[MB polling] {ex.Message}");
                        }
                    }

                    try { await Task.Delay(50, localCts.Token); }
                    catch { break; }
                }
                IsPolling = false;
                Log("[MB] Polling stopped");
            });
        }

        public void StopPolling()
        {
            try { _pollingCts?.Cancel(); } catch { }
            try { _scanCts?.Cancel(); } catch { }
            _pollingTask = null;
            _scanTask = null;
            _pollingCts = null;
            _scanCts = null;
            IsPolling = false;
            IsAutoScanning = false;
        }

        private Task PollOneDeviceAsync(EpraDeviceBase device, CancellationToken token)
        {
            var tcs = new TaskCompletionSource<bool>();
            _queue.Add(() =>
            {
                try
                {
                    _currentPollingAddress = device.DeviceAddress;
                    try { PollStarted?.Invoke(device.DeviceAddress); } catch { }

                    if (!token.IsCancellationRequested)
                        PollDeviceInternal(device);

                    tcs.SetResult(true);
                }
                catch (Exception ex) { tcs.SetException(ex); }
            });
            return tcs.Task;
        }

        // ═══════════════════════════════════════════════════
        // ПРОВЕРКА / ПЕРЕОТКРЫТИЕ ПОРТА — ТОЛЬКО из _workerThread
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Проверяет, совпадают ли параметры открытого порта с параметрами устройства.
        /// Если нет — переоткрывает порт с нужными параметрами.
        /// Если пользователь отключил порт вручную — возвращает false (порт не открывается).
        /// </summary>
        private bool EnsurePortMatches(EpraDeviceBase device)
        {
            if (device == null) return false;
            if (string.IsNullOrEmpty(device.Port)) return false;

            // ─── Пользователь отключил порт — не открываем ───
            if (_userDisconnected)
                return false;

            // Совпадают ли порт, скорость, чётность, стоп-биты?
            if (_modbusClient != null
                && _currentPort == device.Port
                && _currentBaudRate == device.BaudRate
                && _currentParity == device.Parity
                && _currentStopBits == device.StopBits)
            {
                return true;   // переоткрытие не нужно
            }

            Log($"[MB] Reopen port: {_currentPort}@{_currentBaudRate}/{_currentParity}/{_currentStopBits} " +
                $"→ {device.Port}@{device.BaudRate}/{device.Parity}/{device.StopBits}");

            try { _modbusClient?.Disconnect(); } catch { }
            _modbusClient = null;

            try
            {
                _modbusClient = new ModbusClient(device.Port)
                {
                    Baudrate = device.BaudRate,
                    Parity = device.Parity,
                    StopBits = device.StopBits,
                    ConnectionTimeout = 500
                };
                _modbusClient.Connect();

                _currentPort = device.Port;
                _currentBaudRate = device.BaudRate;
                _currentParity = device.Parity;
                _currentStopBits = device.StopBits;

                Log($"[MB] Port OK: {device.Port} @ {device.BaudRate}, {device.Parity}, {device.StopBits}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[MB] ✗ Reopen FAIL: {ex.GetType().Name}: {ex.Message}");
                _modbusClient = null;
                return false;
            }
        }

        // ═══════════════════════════════════════════════════
        // ВНУТРЕННИЕ ОПЕРАЦИИ — ТОЛЬКО из _workerThread
        // ═══════════════════════════════════════════════════

        private bool ConnectInternal(string port, int baudRate,
            System.IO.Ports.Parity parity = System.IO.Ports.Parity.None,
            System.IO.Ports.StopBits stopBits = System.IO.Ports.StopBits.One)
        {
            try
            {
                // Пользователь сам подключился — снимаем запрет
                _userDisconnected = false;

                try { _modbusClient?.Disconnect(); } catch { }

                _currentPort = port;
                _currentBaudRate = baudRate;
                _currentParity = parity;
                _currentStopBits = stopBits;

                _modbusClient = new ModbusClient(port)
                {
                    Baudrate = baudRate,
                    Parity = parity,
                    StopBits = stopBits,
                    ConnectionTimeout = 500
                };

                _modbusClient.Connect();
                Log($"[MB] Connect OK: {port} @ {baudRate}, parity={parity}, stop={stopBits}");
                StatusChanged?.Invoke($"Подключено к {port} на скорости {baudRate}");
                return true;
            }
            catch (Exception ex)
            {
                Log($"[MB] ✗ Connect FAIL: {ex.GetType().Name}: {ex.Message}");
                StatusChanged?.Invoke($"Ошибка подключения: {ex.Message}");
                _modbusClient = null;
                return false;
            }
        }

        private void DisconnectInternal()
        {
            try { StopPolling(); } catch { }

            // Ставим запрет на авто-переоткрытие
            _userDisconnected = true;

            try { _modbusClient?.Disconnect(); } catch { }
            _modbusClient = null;
            _currentPort = null;
            _currentBaudRate = 0;
            _currentParity = System.IO.Ports.Parity.None;
            _currentStopBits = System.IO.Ports.StopBits.One;

            Log("[MB] Disconnected (user)");
        }

        private bool IsCommandRegister(ushort register)
        {
            return register == EpraConstants.HR_SAVE_SETTING
                || register == EpraConstants.HR_RESET_LAMP_RESOURCE
                || register == EpraConstants.HR_RESET_SETTING;
        }

        private bool WriteAndRefreshInternal(byte address, ushort register, ushort value)
        {
            Log($"[MB] ─── Write #{address} reg={register} val={value} (0x{value:X4}) ───");

            var device = Registry.Get(address);
            if (device == null) return false;

            if (!EnsurePortMatches(device)) return false;

            int idx = register - 40001;

            try
            {
                _modbusClient.UnitIdentifier = address;
                _modbusClient.ConnectionTimeout = 1000;
                _modbusClient.WriteSingleRegister(idx, value);
            }
            catch (Exception ex)
            {
                Log($"[MB] write {ex.GetType().Name} — ignoring, packet sent");
            }

            try
            {
                _modbusClient.UnitIdentifier = address;
                _modbusClient.ConnectionTimeout = 2000;

                int[] analog = _modbusClient.ReadInputRegisters(0, EpraConstants.ANALOG_REG_LENGTH);
                if (analog != null && analog.Length >= EpraConstants.ANALOG_REG_LENGTH)
                {
                    for (int i = 0; i < EpraConstants.ANALOG_REG_LENGTH; i++)
                        device.SetAnalog((ushort)(30001 + i), (ushort)analog[i]);
                    device.UpdateFromRegisters();
                    device.IsConnected = true;
                    device.Status = "OK";
                }

                int[] holding = _modbusClient.ReadHoldingRegisters(0, EpraConstants.HOLDING_REG_LENGTH);
                if (holding != null && holding.Length >= EpraConstants.HOLDING_REG_LENGTH)
                {
                    for (int i = 0; i < EpraConstants.HOLDING_REG_LENGTH; i++)
                        device.SetHolding((ushort)(40001 + i), (ushort)holding[i]);
                    device.UpdateHoldingProperties();
                }
            }
            catch (Exception ex)
            {
                Log($"[MB] read after write failed: {ex.GetType().Name}: {ex.Message}");
                device.IsConnected = false;
                device.Status = "Нет связи";
            }

            try { DevicePolled?.Invoke(device); } catch { }

            return true;
        }

        private bool WriteHoldingInternal(byte address, ushort fullAddress, ushort value)
        {
            var device = Registry.Get(address);
            if (device == null) return false;

            if (!EnsurePortMatches(device)) return false;

            try
            {
                _modbusClient.UnitIdentifier = address;
                _modbusClient.ConnectionTimeout = 3000;
                _modbusClient.WriteSingleRegister(fullAddress - 40001, value);

                device.SetHolding(fullAddress, value);

                return true;
            }
            catch (Exception ex)
            {
                Log($"[MB] WriteHoldingInternal EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                return false;
            }
        }

        private ushort? ReadHoldingRegisterInternal(byte address, ushort fullAddress)
        {
            var device = Registry.Get(address);
            if (device == null) return null;

            if (!EnsurePortMatches(device)) return null;

            try
            {
                _modbusClient.UnitIdentifier = address;
                _modbusClient.ConnectionTimeout = 3000;
                int[] result = _modbusClient.ReadHoldingRegisters(fullAddress - 40001, 1);
                if (result == null || result.Length == 0) return null;

                ushort val = (ushort)result[0];
                device.SetHolding(fullAddress, val);

                return val;
            }
            catch (Exception ex)
            {
                Log($"[MB] ReadHoldingRegisterInternal EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                return null;
            }
        }

        private void PollDeviceInternal(EpraDeviceBase device)
        {
            if (Registry.Get(device.DeviceAddress) == null)
            {
                Log($"[MB] Poll #{device.DeviceAddress} skipped — device removed");
                return;
            }

            // ═══════════════════════════════════════
            // ПРОВЕРИТЬ И ПРИ НЕОБХОДИМОСТИ ПЕРЕОТКРЫТЬ ПОРТ
            // ═══════════════════════════════════════
            if (!EnsurePortMatches(device))
            {
                device.IsConnected = false;
                device.Status = "Нет связи";
                device.PollTime = 0;
                try { DevicePolled?.Invoke(device); } catch { }
                return;
            }

            var startTime = DateTime.Now;

            try
            {
                _modbusClient.UnitIdentifier = device.DeviceAddress;
                _modbusClient.ConnectionTimeout = 500;

                int[] analog = _modbusClient.ReadInputRegisters(0, EpraConstants.ANALOG_REG_LENGTH);
                if (analog != null && analog.Length >= EpraConstants.ANALOG_REG_LENGTH)
                {
                    for (int i = 0; i < EpraConstants.ANALOG_REG_LENGTH; i++)
                        device.SetAnalog((ushort)(30001 + i), (ushort)analog[i]);

                    device.UpdateFromRegisters();
                    device.IsConnected = true;
                    device.Status = "OK";
                }

                int[] holding = _modbusClient.ReadHoldingRegisters(0, EpraConstants.HOLDING_REG_LENGTH);
                if (holding != null && holding.Length >= EpraConstants.HOLDING_REG_LENGTH)
                {
                    for (int i = 0; i < EpraConstants.HOLDING_REG_LENGTH; i++)
                        device.SetHolding((ushort)(40001 + i), (ushort)holding[i]);

                    device.UpdateHoldingProperties();
                }
            }
            catch (Exception ex)
            {
                Log($"[MB] Poll #{device.DeviceAddress} EXCEPTION: {ex.GetType().Name}: {ex.Message}");
                device.IsConnected = false;
                device.Status = "Нет связи";
            }

            device.PollTime = (int)(DateTime.Now - startTime).TotalMilliseconds;
            try { DevicePolled?.Invoke(device); } catch { }
        }

        private bool AutoScanInternal(string port, int baudRate)
        {
            StopPolling();
            ClearDevices();

            _scanCts = new CancellationTokenSource();
            var localCts = _scanCts;

            IsAutoScanning = true;
            Log($"[MB] AutoScan started on {port} @ {baudRate}");
            StatusChanged?.Invoke($"Автосканирование на {port}...");

            if (!ConnectInternal(port, baudRate))
            {
                IsAutoScanning = false;
                return false;
            }

            var foundDevices = new List<EpraDeviceBase>();

            for (byte address = 1; address <= 247; address++)
            {
                try { if (localCts.Token.IsCancellationRequested) break; }
                catch { break; }

                if (_modbusClient == null) break;

                try
                {
                    StatusChanged?.Invoke($"Проверка адреса {address}...");

                    if (ProbeDeviceInternal(address))
                    {
                        Log($"[MB] AutoScan: found device #{address}");

                        var device = new EpraDevice(address)
                        {
                            Port = port,
                            BaudRate = baudRate,
                            Parity = _currentParity,
                            StopBits = _currentStopBits,
                            IsConnected = true,
                            Status = "OK"
                        };

                        lock (_lock)
                        {
                            foundDevices.Add(device);
                            if (!Registry.Contains(address))
                            {
                                Registry.Add(device);
                                _pollQueue.Enqueue(device);
                            }
                        }

                        DeviceFound?.Invoke(device);
                    }
                }
                catch (Exception ex)
                {
                    Log($"[MB] AutoScan probe #{address} threw: {ex.Message}");
                }
            }

            IsAutoScanning = false;
            AutoScanCompleted?.Invoke(foundDevices);
            StatusChanged?.Invoke($"Найдено: {foundDevices.Count} устройств");
            Log($"[MB] AutoScan finished: {foundDevices.Count} devices");

            try { QueueChanged?.Invoke(); } catch { }

            return true;
        }

        private bool ProbeDeviceInternal(byte address)
        {
            try
            {
                _modbusClient.UnitIdentifier = address;
                _modbusClient.ConnectionTimeout = 100;
                int[] response = _modbusClient.ReadHoldingRegisters(0, 1);
                return response != null && response.Length > 0;
            }
            catch { return false; }
        }

        // ═══════════════════════════════════════════════════
        // ЛОГИРОВАНИЕ
        // ═══════════════════════════════════════════════════

        [System.Diagnostics.Conditional("DEBUG")]
        private static void Log(string msg)
        {
            System.Diagnostics.Debug.WriteLine(msg);
        }

        // ═══════════════════════════════════════════════════
        // DISPOSE
        // ═══════════════════════════════════════════════════

        public void Dispose()
        {
            try { StopPolling(); } catch { }

            _workerRunning = false;
            try { _queue.CompleteAdding(); } catch { }
            try { _workerThread?.Join(2000); } catch { }

            try { _modbusClient?.Disconnect(); } catch { }
            try { _queue?.Dispose(); } catch { }
        }
    }
}