using System;
using System.Collections.Generic;
using System.IO;

namespace EpraConstructor.Templates
{
    /// <summary>
    /// Общее хранилище шаблонов.
    /// Папка по умолчанию: %USERPROFILE%\Documents\EpraConstructor\
    /// Путь хранится в settings.ini там же.
    /// Формат файла — "KEY: VALUE", совместим со старой C++ программой.
    /// </summary>
    public static class TemplateManager
    {
        private const string SettingsFileName = "settings.ini";
        private const string TemplatesSubFolder = "EpraConstructor";

        /// <summary>Спец-пункт в списке — открывает диалог импорта.</summary>
        public const string ADD_NEW_MARKER = "(Добавить созданный шаблон)";

        // ═══════════════════════════════════════════════════
        // ПУТИ
        // ═══════════════════════════════════════════════════

        public static string DefaultFolder
        {
            get
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(docs, TemplatesSubFolder);
            }
        }

        public static string SettingsFile
        {
            get
            {
                string docs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
                return Path.Combine(docs, TemplatesSubFolder, SettingsFileName);
            }
        }

        public static string FolderPath { get; private set; } = DefaultFolder;

        // ═══════════════════════════════════════════════════
        // НАСТРОЙКИ
        // ═══════════════════════════════════════════════════

        public static void LoadSettings()
        {
            try
            {
                if (File.Exists(SettingsFile))
                {
                    string path = File.ReadAllText(SettingsFile).Trim();
                    if (!string.IsNullOrEmpty(path))
                        FolderPath = path;
                }
            }
            catch { /* ignore */ }

            EnsureFolderExists();
        }

        public static void SaveFolderPath(string folderPath)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile));
                File.WriteAllText(SettingsFile, folderPath ?? "");
            }
            catch { /* ignore */ }

            FolderPath = string.IsNullOrEmpty(folderPath) ? DefaultFolder : folderPath;
            EnsureFolderExists();
        }

        private static void EnsureFolderExists()
        {
            try
            {
                if (!Directory.Exists(FolderPath))
                    Directory.CreateDirectory(FolderPath);
            }
            catch { /* ignore */ }
        }

        // ═══════════════════════════════════════════════════
        // СПИСОК ШАБЛОНОВ
        // ═══════════════════════════════════════════════════

        public static List<string> GetTemplateNames()
        {
            var result = new List<string>();

            try
            {
                if (!Directory.Exists(FolderPath))
                    return result;

                var files = Directory.GetFiles(FolderPath, "*.txt");
                foreach (var file in files)
                {
                    string name = Path.GetFileNameWithoutExtension(file);
                    if (!string.IsNullOrEmpty(name))
                        result.Add(name);
                }
            }
            catch { /* ignore */ }

            result.Sort(StringComparer.OrdinalIgnoreCase);
            return result;
        }

        public static string GetFullPath(string templateName)
        {
            return Path.Combine(FolderPath, templateName + ".txt");
        }

        public static bool Exists(string templateName)
        {
            try { return File.Exists(GetFullPath(templateName)); }
            catch { return false; }
        }

        public static void Delete(string templateName)
        {
            try
            {
                string path = GetFullPath(templateName);
                if (File.Exists(path))
                    File.Delete(path);
            }
            catch { /* ignore */ }
        }

        // ═══════════════════════════════════════════════════
        // ЧТЕНИЕ / ЗАПИСЬ
        // ═══════════════════════════════════════════════════

        public static Dictionary<string, ushort> Load(string templateName)
        {
            var result = new Dictionary<string, ushort>(StringComparer.OrdinalIgnoreCase);

            try
            {
                string path = GetFullPath(templateName);
                if (!File.Exists(path)) return result;

                foreach (var raw in File.ReadAllLines(path))
                {
                    string line = raw?.Trim();
                    if (string.IsNullOrEmpty(line)) continue;

                    int colon = line.IndexOf(':');
                    if (colon <= 0) continue;

                    string key = line.Substring(0, colon).Trim();
                    string valStr = line.Substring(colon + 1).Trim();

                    if (string.IsNullOrEmpty(key)) continue;

                    if (ushort.TryParse(valStr, out ushort value))
                        result[key] = value;
                }
            }
            catch { /* ignore */ }

            return result;
        }

        public static void Save(string templateName, Dictionary<string, ushort> values)
        {
            EnsureFolderExists();

            string path = GetFullPath(templateName);
            var lines = new List<string>();

            foreach (var (key, _) in TemplateKeys.Map)
            {
                if (values.TryGetValue(key, out ushort v))
                    lines.Add($"{key}: {v}");
            }

            File.WriteAllLines(path, lines);
        }


        public static string ImportFile(string sourceFilePath)
        {
            EnsureFolderExists();

            string baseName = Path.GetFileNameWithoutExtension(sourceFilePath);
            string name = baseName;
            string destPath = GetFullPath(name);

            int counter = 1;
            while (File.Exists(destPath))
            {
                name = $"{baseName} ({counter})";
                destPath = GetFullPath(name);
                counter++;
            }

            File.Copy(sourceFilePath, destPath);
            return name;
        }
    }
}