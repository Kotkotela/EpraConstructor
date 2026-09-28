using System.Collections.Generic;

namespace EpraConstructor.Services
{
    /// <summary>
    /// Состояние приложения, сохраняется между запусками.
    /// </summary>
    public class AppState
    {
        public string Theme { get; set; } = "Dark";

        public double WindowWidth { get; set; } = 1234;
        public double WindowHeight { get; set; } = 682;
        public double WindowLeft { get; set; } = double.NaN;
        public double WindowTop { get; set; } = double.NaN;
        public bool WindowMaximized { get; set; } = false;

        public List<DeviceState> Devices { get; set; } = new List<DeviceState>();
    }

    public class DeviceState
    {
        public byte Address { get; set; }
        public int BaudRate { get; set; } = 9600;
        public string Parity { get; set; } = "None";
        public string StopBits { get; set; } = "One";
        public string Port { get; set; } = "";
    }
}