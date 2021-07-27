#region License
//------------------------------------------------------------------------------
// Copyright (c) Dmitrii Evdokimov
// Source https://github.com/diev/
// 
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//------------------------------------------------------------------------------
#endregion

using System;
using System.Collections.Specialized;
using System.Configuration;
using System.IO;

namespace ICRS_NBKI_Request
{
    public class Config
    {
        static readonly NameValueCollection _settings = ConfigurationManager.AppSettings;

        public static void Set(string key, string value)
        {
            _settings[key] = value;
        }

        public static void SetIf(string arg, string key)
        {
            if (arg.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                _settings[key] = arg.Substring(key.Length + 1);
            }
        }

        public static bool IsSet(string key)
        {
            return (_settings[key] ?? "0").Equals("1");
        }

        public static string Required(string key)
        {
            string value = _settings[key];
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentNullException($"No value for key \"{key}\".");
            }
            return value;
        }

        public static string Optional(string key, string defaultValue)
        {
            string value = _settings[key];
            return string.IsNullOrWhiteSpace(value)
                ? defaultValue
                : value;
        }

        public static bool TryGet(string key, out string value)
        {
            value = _settings[key];
            return !string.IsNullOrWhiteSpace(value);
        }

        public static string CheckDirectory(string key, string defaultValue)
        {
            string path = Optional(key, defaultValue);
            if (!Directory.Exists(path))
            {
                Directory.CreateDirectory(path);
            }
            return path;
        }

        public static void Usage(string arg)
        {
            var help = new[] { "/?", "-?", "/h", "-h", "/help", "-help", "--help" };
            foreach (string opt in help)
            {
                if (arg.Equals(opt, StringComparison.OrdinalIgnoreCase))
                {
                    throw new Exception("Help wanted.");
                }
            }
        }
    }
}
