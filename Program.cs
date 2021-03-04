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
            string bakPath = settings["RequestsBAK"] ?? "REQ";
            string dstPath = settings["Results"] ?? "XML";

            // Use TLS 1.2 (required!)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Ignore any Cert validation or install the root ones from http://cpca.cryptopro.ru/cacer.p7b
            //ServicePointManager.ServerCertificateValidationCallback += (se, cert, chain, sslerror) => { return true; };

            if (!Directory.Exists(bakPath))
            {
                Directory.CreateDirectory(bakPath);
            }

            if (!Directory.Exists(dstPath))
            {
                Directory.CreateDirectory(dstPath);
            }

            var dir = new DirectoryInfo(srcPath);
            foreach (var file in dir.GetFiles("*.req"))
            {
                string srcFile = file.FullName;
                string bakFile = Path.Combine(bakPath, file.Name);
                string dstFile = Path.Combine(dstPath, Path.ChangeExtension(file.Name, ".xml"));

                DownloadFile(uriPath, srcFile, dstFile, bakFile);
            }

            Environment.Exit(0);
        }

        private static void DownloadFile(string uri, string src, string dst, string bak)
        {
            var client = new WebClient();
            byte[] response = client.UploadData(uri, File.ReadAllBytes(src));

            if (response != null && response.Length > 0)
            {
                //File.WriteAllBytes(dst + ".p7s", response);

                // Clean XML from a PKCS#7 signature
                var signedCms = new SignedCms();
                signedCms.Decode(response);
                response = signedCms.ContentInfo.Content;

                File.WriteAllBytes(dst, response);
                Console.WriteLine($"File {dst} ready.");

                if (File.Exists(bak))
                {
                    File.Delete(bak);
                }

                File.Move(src, bak);
            }
        }
    }
}
