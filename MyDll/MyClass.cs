using System.Windows.Forms;
using System.Data;
using System;
using System.Collections.Generic;
using System.Threading;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using static System.Collections.Specialized.BitVector32;
using System.Drawing;
using System;
using System.Net;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace MyDll
{
    public sealed class ParameterManager
    {
        #region INI Helpers
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileString(string section, string key, string def, StringBuilder retVal, int size, string filePath);

        [DllImport("kernel32")]
        private static extern long WritePrivateProfileString(string section, string key, string val, string filePath);

        //[DllImport("kernel32", CharSet = CharSet.Unicode, SetLastError = true)]
        [DllImport("kernel32")]
        private static extern int GetPrivateProfileSectionNames(byte[] lpszReturnBuffer, int nSize, string lpFileName);

        [DllImport("kernel32")]
        private static extern int GetPrivateProfileSection(string lpAppName, byte[] lpReturnedString, int nSize, string lpFileName);

        private List<string> GetSectionNames(string filePath)
        {
            byte[] buffer = new byte[32768];
            int bytesRead = GetPrivateProfileSectionNames(buffer, buffer.Length, filePath) * sizeof(char);
            //Console.WriteLine("sec bytesRead : " + bytesRead.ToString() + ", " + Encoding.Default.GetString(buffer, 0, bytesRead));
            if (bytesRead == 0) return new List<string>();
            return Encoding.Default.GetString(buffer, 0, bytesRead)
                .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                .ToList();
        }

        private List<string> GetKeys(string filePath, string section)
        {
            byte[] buffer = new byte[32768];
            int bytesRead = GetPrivateProfileSection(section, buffer, buffer.Length, filePath) * sizeof(char);
            //Console.WriteLine("key bytesRead : " + bytesRead.ToString() + ", " + Encoding.Default.GetString(buffer, 0, bytesRead));
            if (bytesRead == 0) return new List<string>();
            return Encoding.Default.GetString(buffer, 0, bytesRead)
                .Split(new[] { '\0' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(pair => Regex.Replace(pair, @"=.*$", "").Trim())
                .ToList();
        }

        private string ReadValue(string section, string key, string filePath)
        {
            StringBuilder sb = new StringBuilder(65535);
            GetPrivateProfileString(section, key, "", sb, sb.Capacity, filePath);
            return sb.ToString();
        }

        private void WriteValue(string section, string key, string value, string filePath)
        {
            WritePrivateProfileString(section, key, value, filePath);
        }

        private object ReadValueFromIni(string section, string key, Type type)
        {
            try
            {
                return ConvertStringToType(ReadValue(section, key, _iniFilePath), type);
            }
            catch
            {
                return null;
            }
        }

        private void WriteValueToIni(string section, string key, object value)
        {
            WriteValue(section, key, ConvertTypeToString(value), _iniFilePath);
        }

        private string ConvertTypeToString(object value)
        {
            if (value is List<int> list_int) return string.Join(",", list_int);
            if (value is List<string> list_str) return string.Join(",", list_str);
            if (value is string str) return str;
            if (value is List<List<int>> list_list_int) return string.Join(",", list_list_int.Select(r => string.Join("_", r)));
            if (value is List<Rectangle> list_rect) return string.Join(",", list_rect.Select(r => $"{r.Left}_{r.Top}_{r.Right}_{r.Bottom}"));
            return value.ToString();
        }

        private object ConvertStringToType(string value, Type targetType)
        {
            if (targetType == typeof(int)) return int.Parse(value);
            if (targetType == typeof(string)) return value;
            if (targetType == typeof(List<int>)) return string.IsNullOrEmpty(value) ? new List<int>() : value.Split(',').Select(int.Parse).ToList();
            if (targetType == typeof(List<string>)) return string.IsNullOrEmpty(value) ? new List<string>() : value.Split(',').ToList();
            if (targetType == typeof(List<List<int>>)) return string.IsNullOrEmpty(value) ? new List<List<int>>()
                    : value.Split(',').Select(val => { return val.Split('_').Select(int.Parse).ToList(); }).ToList();
            if (targetType == typeof(List<Rectangle>)) return string.IsNullOrEmpty(value) ? new List<Rectangle>()
                    : value.Split(',').Select(val =>
                    {
                        var parts = val.Split('_');
                        return Rectangle.FromLTRB(int.Parse(parts[0]), int.Parse(parts[1]), int.Parse(parts[2]), int.Parse(parts[3]));
                    }).ToList();
            throw new NotSupportedException($"Type {targetType} not supported");
        }
        #endregion

        #region Singleton Implementation
        private static readonly Lazy<ParameterManager> _instance = new Lazy<ParameterManager>(() => new ParameterManager());
        public static ParameterManager Instance => _instance.Value;
        private ParameterManager() { }
        #endregion

        private readonly Dictionary<string, Dictionary<string, object>> _parameterCache = 
            new Dictionary<string, Dictionary<string, object>>(StringComparer.OrdinalIgnoreCase);
        //private bool _isInitialized;
        private string _iniFilePath;
        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        #region Initialize
        public void Initialize(string iniFilePath, List<ParameterDefinition> defaultParams)
        {
            _lock.EnterWriteLock();

            try
            {
                _iniFilePath = iniFilePath;
                var directory = Path.GetDirectoryName(iniFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                if (File.Exists(iniFilePath))
                {
                    var existingParams = ReadAllParameters(iniFilePath);
                    //Console.WriteLine(existingParams.Count.ToString());
                    //foreach (var section in existingParams)
                    //{   
                    //    Console.WriteLine("[" + section.Key + "] " + section.Value.Count.ToString());
                    //    foreach (var item in section.Value)
                    //    {
                    //        Console.WriteLine("    " + item.Key + " = " + item.Value);
                    //    }
                    //}

                    if (!ValidateStructure(existingParams, defaultParams))
                    {
                        BackupAndCreateNew(iniFilePath, existingParams, defaultParams);
                    }
                }
                else
                {
                    CreateIniFile(iniFilePath, defaultParams);
                }

                LoadParameters(iniFilePath, defaultParams);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        private void LoadParameters(string filePath, List<ParameterDefinition> defaultParams)
        {
            var defaultSections = defaultParams.Select(x => x.Section).Distinct().ToList();

            foreach (var section in defaultSections)
            {
                var sectionDict = new Dictionary<string, object>(
                    StringComparer.OrdinalIgnoreCase);

                foreach (var param in defaultParams.Where(x => x.Section == section))
                {
                    var valueStr = ReadValue(param.Section, param.Key, filePath);
                    sectionDict[param.Key] = ConvertStringToType(valueStr, param.ValueType);
                }

                _parameterCache[section] = sectionDict;
            }
        }

        private Dictionary<string, Dictionary<string, string>> ReadAllParameters(string filePath)
        {
            var parameters = new Dictionary<string, Dictionary<string, string>>();

            var sections = GetSectionNames(filePath);

            foreach (var section in sections)
            {
                var sectionDict = new Dictionary<string, string>();
                var keys = GetKeys(filePath, section);

                foreach (var key in keys)
                {
                    sectionDict[key] = ReadValue(section, key, filePath);
                }

                parameters[section] = sectionDict;
            }

            return parameters;
        }

        //private bool ValidateStructure(string filePath,
        //    List<ParameterDefinition> defaultParams)
        //{
        //    var existingSections = GetSectionNames(filePath);
        //    var defaultSections = defaultParams.Select(x => x.Section).Distinct().ToList();
        //    if (!CompareCollections(existingSections, defaultSections, true))
        //        return false;
        //    foreach (var section in defaultSections)
        //    {
        //        var existingKeys = GetKeys(filePath, section);
        //        var defaultKeys = defaultParams.Where(x => x.Section == section).Select(x => x.Key).ToList();
        //        if (!CompareCollections(existingKeys, defaultKeys, true))
        //            return false;
        //    }
        //    return true;
        //}

        private bool ValidateStructure(
            Dictionary<string, Dictionary<string, string>> existingParams,
            List<ParameterDefinition> defaultParams)
        {
            foreach (var param in defaultParams)
            {
                if (!existingParams.ContainsKey(param.Section) ||
                    !existingParams[param.Section].ContainsKey(param.Key))
                {
                    return false;
                }
            }
            return true;
        }

        private bool CompareCollections(IEnumerable<string> col1,
            IEnumerable<string> col2, bool ignoreCase)
        {
            var comparer = ignoreCase ?
                StringComparer.OrdinalIgnoreCase :
                StringComparer.Ordinal;

            return col1.OrderBy(x => x, comparer)
                       .SequenceEqual(col2.OrderBy(x => x, comparer), comparer);
        }

        //private void BackupAndCreateNew(string originalPath,
        //    List<ParameterDefinition> defaultParams)
        //{
        //    var backupPath = $"{originalPath}.{DateTime.Now:yyyyMMddHHmmss}.bak";
        //    File.Move(originalPath, backupPath);
        //    CreateIniFile(originalPath, defaultParams);
        //}

        private void BackupAndCreateNew(string originalPath,
            Dictionary<string, Dictionary<string, string>> existingParams,
            List<ParameterDefinition> defaultParams)
        {
            // Backup original file
            var backupPath = $"{originalPath}.{DateTime.Now:yyyyMMddHHmmss}.bak";
            File.Move(originalPath, backupPath);

            // Merge parameters
            var mergedParams = MergeParameters(existingParams, defaultParams);
            CreateIniFile(originalPath, mergedParams);
        }

        private List<ParameterDefinition> MergeParameters(
            Dictionary<string, Dictionary<string, string>> existingParams,
            List<ParameterDefinition> defaultParams)
        {
            var merged = new List<ParameterDefinition>(defaultParams);
            for (int i=0; i < defaultParams.Count; i++)
            {
                var defaultParam = defaultParams[i];
                if (existingParams.TryGetValue(defaultParam.Section, out var existingSection) &&
                    existingSection.TryGetValue(defaultParam.Key, out var existingValue))
                {
                    try
                    {
                        merged[i] = new ParameterDefinition(defaultParam.Section, defaultParam.Key, ConvertStringToType(existingValue, defaultParam.ValueType));
                    }
                    catch
                    {
                        merged[i] = new ParameterDefinition(defaultParam.Section, defaultParam.Key, defaultParam.DefaultValue);
                    }
                }
            }
            return merged;
        }

        private void CreateIniFile(string path,
            List<ParameterDefinition> defaultParams)
        {
            foreach (var param in defaultParams)
            {
                WriteValue(param.Section, param.Key, ConvertTypeToString(param.DefaultValue), path);
            }
        }
        #endregion

        //private void SaveAllValues()
        //{
        //    foreach (var section in _parameterCache)
        //    {
        //        foreach (var kvp in section.Value)
        //        {
        //            WriteValueToIni(section.Key, kvp.Key, kvp.Value);
        //        }
        //    }
        //}

        //private void CreateDefaultConfig(List<ParameterDefinition> parameterDefinitions)
        //{
        //    _lock.EnterWriteLock();
        //    try
        //    {
        //        foreach (var definition in parameterDefinitions)
        //        {
        //            SetValueInternal(definition.Section, definition.Key, definition.DefaultValue);
        //            WriteValueToIni(definition.Section, definition.Key, definition.DefaultValue);
        //        }
        //    }
        //    finally
        //    {
        //        _lock.ExitWriteLock();
        //    }
        //}

        //private void SetValueInternal(string section, string key, object value)
        //{
        //    if (!_parameterCache.ContainsKey(section))
        //        _parameterCache[section] = new Dictionary<string, object>();

        //    _parameterCache[section][key] = value;
        //}

        public T GetValue<T>(string section, string key)
        {
            _lock.EnterReadLock();
            try
            {
                if (_parameterCache.TryGetValue(section, out var sectionDict) && sectionDict.TryGetValue(key, out var value))
                {
                    return (T)value;
                }
                throw new KeyNotFoundException($"Parameter {section}.{key} not found");
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        // 参数设置窗口调用此方法更新参数
        public void UpdateParameters(List<ParameterDefinition> newParameters)
        {
            _lock.EnterWriteLock();
            try
            {
                foreach (var kvp in newParameters)
                {
                    //if (!_parameterDefinitions.ContainsKey(kvp.Key)) continue;

                    //var definition = _parameterDefinitions[kvp.Key];
                    //string strValue = ConvertToString(kvp.Value);
                    //WritePrivateProfileString(definition.Section, kvp.Key, strValue, _iniFilePath);
                    //_parameterCache[kvp.Key] = kvp.Value;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }
        public void SetValue<T>(ParameterDefinition param, T value)
        {
            _lock.EnterWriteLock();
            try
            {
                if (!_parameterCache.ContainsKey(param.Section))
                {
                    _parameterCache[param.Section] = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
                }

                _parameterCache[param.Section][param.Key] = value;
                WriteValueToIni(param.Section, param.Key, value);
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public class ParameterDefinition
        {
            public string Section { get; }
            public string Key { get; }
            public object DefaultValue { get; }
            public Type ValueType { get; }

            public ParameterDefinition(string section, string key, object defaultValue)
            {
                Section = section;
                Key = key;
                DefaultValue = defaultValue;
                ValueType = defaultValue.GetType();
                ValidateType();
            }

            private void ValidateType()
            {
                var validTypes = new[] { typeof(int), typeof(string), typeof(List<int>), typeof(List<string>), typeof(List<List<int>>), typeof(List<Rectangle>) };
                if (!validTypes.Contains(ValueType))
                    throw new ArgumentException($"Invalid parameter type: {ValueType}");
            }
        }

    }

    public class MyClass
    {
        public static void Test()
        {
            MessageBox.Show("调用成功666");
        }


}
}
