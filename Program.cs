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
using System.Configuration;
using System.IO;
using System.Net;
using System.Security.Cryptography.Pkcs;

namespace ICRS_NBKI_Request
{
    class Program
    {
        static void Main(string[] args)
        {
            var settings = ConfigurationManager.AppSettings;

            string uriPath = settings["Uri"] ?? "https://icrs.nbki.ru/products/B2BRequestServlet";
            string srcPath = settings["Requests"] ?? ".";
            string dstPath = settings["Results"] ?? ".";

            // Use TLS 1.2 (required!)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Ignore any Cert validation or install the root ones from http://cpca.cryptopro.ru/cacer.p7b
            //ServicePointManager.ServerCertificateValidationCallback += (se, cert, chain, sslerror) => { return true; };

            var dir = new DirectoryInfo(srcPath);
            foreach (var file in dir.GetFiles("*.req"))
            {
                string srcFile = file.FullName;
                string dstFile = Path.Combine(dstPath, Path.ChangeExtension(file.Name, ".xml"));

                DownloadFile(uriPath, srcFile, dstFile);
            }

            Environment.Exit(0);
        }

        private static void DownloadFile(string uri, string src, string dst)
        {
            var client = new WebClient();
            byte[] response = client.UploadFile(uri, src);

            if (response != null && response.Length > 0)
            {
                // Clean XML from a PKCS#7 signature
                var signedCms = new SignedCms();
                signedCms.Decode(response);
                byte[] data = signedCms.ContentInfo.Content;

                File.WriteAllBytes(dst, data);
                Console.WriteLine($"File {dst} ready.");
            }
        }
    }
}
