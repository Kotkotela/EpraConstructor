using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace EpraConstructor.Services
{
    /// <summary>
    /// Сервис сохранения/загрузки состояния приложения.
    /// Формат файла — текстовый: "KEY: VALUE" + строки "DEVICE: ...".
    /// Путь: %APPDATA%\EpraConstructor\config.txt
    /// </summary>
    public static class AppStateService
    {
        private const string Header = "# EpraConstructor config";

        private static readonly string FolderPath =
            Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "EpraConstructor");

        public static readonly string ConfigPath =
            Path.Combine(FolderPath, "config.txt");

        // ═══════════════════════════════════════════════════
        // LOAD
        // ═══════════════════════════════════════════════════

        public static AppState Load()
        {
            try
            {
                if (!File.Exists(ConfigPath))
                    return new AppState();

                return Parse(File.ReadAllLines(ConfigPath));
            }
            catch
            {
                return new AppState();
            }
        }

        public static AppState ImportFrom(string path)
        {
            try
            {
                if (!File.Exists(path)) return null;
                string[] lines = File.ReadAllLines(path);
                var state = Parse(lines);
                // считаем валидным, если в файле есть хотя бы заголовок
                // или хотя бы одно из наших полей
                foreach (var line in lines)
                {
                    var t = line?.Trim();
                    if (string.IsNullOrEmpty(t)) continue;
                    if (t.StartsWith("#")) continue;
                    if (t.StartsWith("THEME:") ||
                        t.StartsWith("WINDOW_") ||
                        t.StartsWith("DEVICE:"))
                        return state;
                }
                return null;
            }
            catch
            {
                return null;
            }
        }

        private static AppState Parse(string[] lines)
        {
            var state = new AppState();

            if (lines == null) return state;

            foreach (var raw in lines)
            {
                if (raw == null) continue;
                string line = raw.Trim();
                if (line.Length == 0) continue;
                if (line.StartsWith("#")) continue;

                if (line.StartsWith("DEVICE:"))
                {
                    var dev = ParseDevice(line.Substring("DEVICE:".Length).Trim());
                    if (dev != null) state.Devices.Add(dev);
                    continue;
                }

                int colon = line.IndexOf(':');
                if (colon <= 0) continue;

                string key = line.Substring(0, colon).Trim().ToUpperInvariant();
                string value = line.Substring(colon + 1).Trim();

                switch (key)
                {
                    case "THEME":
                        state.Theme = string.IsNullOrEmpty(value) ? "Dark" : value;
                        break;

                    case "WINDOW_WIDTH":
                        if (double.TryParse(value, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out double w))
                            state.WindowWidth = w;
                        break;

                    case "WINDOW_HEIGHT":
                        if (double.TryParse(value, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out double h))
                            state.WindowHeight = h;
                        break;

                    case "WINDOW_LEFT":
                        if (double.TryParse(value, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out double l))
                            state.WindowLeft = l;
                        break;

                    case "WINDOW_TOP":
                        if (double.TryParse(value, NumberStyles.Float,
                                CultureInfo.InvariantCulture, out double t))
                            state.WindowTop = t;
                        break;

                    case "WINDOW_MAXIMIZED":
                        state.WindowMaximized = value == "1" || value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                }
            }

            return state;
        }

        private static DeviceState ParseDevice(string line)
        {
            if (string.IsNullOrWhiteSpace(line)) return null;

            var dev = new DeviceState();

            // токены Key=Value через пробел
            var parts = line.Split(new[] { ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (var p in parts)
            {
                int eq = p.IndexOf('=');
                if (eq <= 0) continue;

                string key = p.Substring(0, eq).Trim().ToUpperInvariant();
                string value = p.Substring(eq + 1).Trim();

                switch (key)
                {
                    case "ADDRESS":
                        if (byte.TryParse(value, out byte a)) dev.Address = a;
                        break;

                    case "BAUDRATE":
                        if (int.TryParse(value, out int b)) dev.BaudRate = b;
                        break;

                    case "PARITY":
                        dev.Parity = value;
                        break;

                    case "STOPBITS":
                        dev.StopBits = value;
                        break;

                    case "PORT":
                        dev.Port = value;
                        break;
                }
            }

            return dev;
        }

        // ═══════════════════════════════════════════════════
        // SAVE
        // ═══════════════════════════════════════════════════

        public static void Save(AppState state)
        {
            try
            {
                Directory.CreateDirectory(FolderPath);
                File.WriteAllText(ConfigPath, BuildText(state), Encoding.UTF8);
            }
            catch
            {
                // игнорируем
            }
        }

        public static void ExportTo(string path, AppState state)
        {
            try
            {
                File.WriteAllText(path, BuildText(state), Encoding.UTF8);
            }
            catch (Exception ex)
            {
                throw new IOException("Не удалось сохранить файл: " + ex.Message, ex);
            }
        }

        private static string BuildText(AppState state)
        {
            var sb = new StringBuilder();
            sb.AppendLine(Header);
            sb.AppendLine();

            sb.AppendLine("THEME: " + (state.Theme ?? "Dark"));
            sb.AppendLine();

            sb.AppendLine("WINDOW_WIDTH: " + state.WindowWidth.ToString(CultureInfo.InvariantCulture));
            sb.AppendLine("WINDOW_HEIGHT: " + state.WindowHeight.ToString(CultureInfo.InvariantCulture));

            if (!double.IsNaN(state.WindowLeft))
                sb.AppendLine("WINDOW_LEFT: " + state.WindowLeft.ToString(CultureInfo.InvariantCulture));

            if (!double.IsNaN(state.WindowTop))
                sb.AppendLine("WINDOW_TOP: " + state.WindowTop.ToString(CultureInfo.InvariantCulture));

            sb.AppendLine("WINDOW_MAXIMIZED: " + (state.WindowMaximized ? "1" : "0"));
            sb.AppendLine();

            if (state.Devices != null)
            {
                foreach (var d in state.Devices)
                {
                    sb.Append("DEVICE: ");
                    sb.Append("Address=").Append(d.Address).Append(' ');
                    sb.Append("BaudRate=").Append(d.BaudRate).Append(' ');
                    sb.Append("Parity=").Append(string.IsNullOrEmpty(d.Parity) ? "None" : d.Parity).Append(' ');
                    sb.Append("StopBits=").Append(string.IsNullOrEmpty(d.StopBits) ? "One" : d.StopBits).Append(' ');
                    sb.Append("Port=").Append(string.IsNullOrEmpty(d.Port) ? "" : d.Port);
                    sb.AppendLine();
                }
            }

            return sb.ToString();
        }

        // ═══════════════════════════════════════════════════
        // HELPERS
        // ═══════════════════════════════════════════════════

        /// <summary>
        /// Быстрая проверка: подходит ли файл для DnD.
        /// Читает только первые ~50 строк.
        /// </summary>
        public static bool IsConfigFile(string path)
        {
            if (string.IsNullOrEmpty(path)) return false;
            if (!File.Exists(path)) return false;
            if (!path.EndsWith(".txt", StringComparison.OrdinalIgnoreCase) &&
                !path.EndsWith(".cfg", StringComparison.OrdinalIgnoreCase))
                return false;

            try
            {
                int n = 0;
                foreach (var raw in File.ReadLines(path))
                {
                    if (n++ > 50) break;
                    var t = raw?.Trim();
                    if (string.IsNullOrEmpty(t)) continue;
                    if (t.StartsWith("#")) continue;
                    if (t.StartsWith("THEME:") ||
                        t.StartsWith("WINDOW_") ||
                        t.StartsWith("DEVICE:"))
                        return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }
    }
}