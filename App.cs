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
using System.Reflection;

namespace ICRS_NBKI_Request
{
    /// <summary>
    /// Properties of Application.
    /// </summary>
    public static class App
    {
        /// <summary>
        /// Файл приложения.
        /// </summary>
        public static string Exe => Assembly.GetCallingAssembly().Location;

        /// <summary>
        /// Путь к размещению файла приложения.
        /// </summary>
        public static string Dir => AppDomain.CurrentDomain.BaseDirectory;

        /// <summary>
        /// Название приложения.
        /// </summary>
        public static string Name => Assembly.GetCallingAssembly().GetName().Name;

        /// <summary>
        /// Версия приложения (Major.Minor.Build.Revision)
        /// Version.ToString(3) => 1.0.0
        /// Version.ToString(2) => 1.0
        /// </summary>
        public static Version Version => Assembly.GetCallingAssembly().GetName().Version;

        /// <summary>
        /// Полный заголовок для консольных приложений.
        /// </summary>
        public static string Banner
        {
            get
            {
                var assembly = Assembly.GetCallingAssembly();
                var assemblyName = assembly.GetName();
                var name = assemblyName.Name;
                var version = assemblyName.Version; // Major.Minor.Build.Revision
                string build = (version.Revision > 0) ? $" build {version.Revision}" : string.Empty;
                var ver = version.ToString(3);
                var d = Attribute.GetCustomAttribute(assembly, typeof(AssemblyDescriptionAttribute)) as AssemblyDescriptionAttribute;
                var c = Attribute.GetCustomAttribute(assembly, typeof(AssemblyCopyrightAttribute)) as AssemblyCopyrightAttribute;
                string C = c.Copyright.Replace("\u00a9", "(c)");

                return $"{name} v{ver}{build} - {d.Description}\n{C}\n";
            }
        }

        /// <summary>
        /// Краткий заголовок с версией для консольных приложений.
        /// </summary>
        public static string Title
        {
            get
            {
                var assemblyName = Assembly.GetCallingAssembly().GetName();
                var name = assemblyName.Name;
                var ver = assemblyName.Version.ToString(3);

                return $"{name} v{ver}";
            }
        }

        /// <summary>
        /// Есть ли вопросы к параметрам командной строки программы?
        /// </summary>
        /// <param name="args">Параметры программы.</param>
        public static void Usage(string[] args)
        {
            Console.WriteLine(Banner);

            var helpWanted = new[] { "/?", "-?", "/h", "-h", "/help", "-help", "--help" };

            foreach (var arg in args)
            {
                foreach (string opt in helpWanted)
                {
                    if (arg.Equals(opt, StringComparison.OrdinalIgnoreCase))
                    {
                        Console.WriteLine("No options. Set config.");
                        Console.WriteLine("Password=**** (if only)");

                        Environment.Exit(0);
                    }
                }
            }
        }
    }
}
