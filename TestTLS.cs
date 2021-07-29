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
using System.IO;
using System.Net;
using System.Security.Cryptography.X509Certificates;

namespace ICRS_NBKI_Request
{
    /// <summary>
    /// Тест двусторонней аутентификации TLS
    /// </summary>
    public static class TestTLS
    {
        /// <summary>
        /// Тест получения страницы при переходе на двустороннюю аутентификацию TLS
        /// </summary>
        public static void Run(X509Certificate certificate)
        {
            string uri = "https://reports.demo.nbki.ru/";
            string result = Request(uri, certificate);

            Console.WriteLine(result.Length > 200 ? result.Substring(0, 200) : result);
        }

        /// <summary>
        /// Запрос страницы для теста.
        /// </summary>
        /// <param name="uri">Адрес страницы.</param>
        /// <returns>Текст страницы.</returns>
        private static string Request(string uri, X509Certificate certificate)
        {
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.ClientCertificates.Add(certificate);

            return Response(request);
        }

        /// <summary>
        /// Получение страницы для теста.
        /// </summary>
        /// <param name="request">Запрос страницы.</param>
        /// <returns>Текст страницы.</returns>
        private static string Response(HttpWebRequest request)
        {
            var response = (HttpWebResponse)request.GetResponse();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Unexpected behavior! Status code: {response.StatusCode}.");
            }

            using (var streamReader = new StreamReader(response.GetResponseStream()
                ?? throw new InvalidOperationException("Response stream is null.")))
            {
                return streamReader.ReadToEnd();
            }
        }
    }
}
