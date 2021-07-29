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
using System.Security.Cryptography.X509Certificates;

namespace ICRS_NBKI_Request
{
    /// <summary>
    /// Вспомогательные функции.
    /// </summary>
    public static class Helpers
    {
        /// <summary>
        /// Приведение любого скопированного отпечатка к системному (без пробелов, uppercase).
        /// </summary>
        /// <param name="value">Скопированное значение.</param>
        /// <returns>Приведенная строка.</returns>
        public static string GetThumbprint(string value)
        {
            return value.Replace(" ", string.Empty).ToUpper();
        }

        /// <summary>
        /// Получение сертификата из хранилища по его отпечатку.
        /// </summary>
        /// <param name="thumbprint">Отпечаток сертификата.</param>
        /// <param name="validOnly">Отбирать только действительные.</param>
        /// <returns>Искомый сертификат.</returns>
        public static X509Certificate GetX509Certificate(string thumbprint, bool validOnly = true)
        {
            if (string.IsNullOrEmpty(thumbprint))
            {
                return null;
            }

            using (var store = new X509Store(StoreName.My, StoreLocation.CurrentUser)) // mmc: Сертификаты - Пользователя - Личные
            {
                store.Open(OpenFlags.ReadOnly);

                //var cert = store.Certificates.Cast<X509Certificate>().FirstOrDefault(x =>
                //    x.GetSerialNumberString().Equals(certificateSerialNumber, StringComparison.InvariantCultureIgnoreCase));

                var found = store.Certificates.Find(X509FindType.FindByThumbprint, thumbprint, validOnly);
                if (found.Count == 1)
                {
                    return found[0];
                }

                throw new ArgumentNullException("Thumbprint", $"Certificate with thumbprint \"{thumbprint}\" not found.");
            }
        }
    }
}
