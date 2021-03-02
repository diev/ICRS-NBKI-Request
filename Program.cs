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
using System.Reflection;
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
            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.UserAgent = UserAgent();
            request.Method = WebRequestMethods.Http.Post;

            // http://cpca.cryptopro.ru/cacer.p7b
            //request.ServerCertificateValidationCallback = delegate { return true; };

            using (var stream = request.GetRequestStream())
            {
                byte[] bytes = File.ReadAllBytes(src);
                stream.Write(bytes, 0, bytes.Length);
            }

            using (var data = new MemoryStream())
            {
                using (var response = (HttpWebResponse)request.GetResponse())
                {
                    Console.WriteLine($"Response: {response.StatusDescription}.");
                    if (response.StatusCode != HttpStatusCode.OK)
                    {
                        Environment.Exit(1);
                    }

                    using (var stream = response.GetResponseStream())
                    {
                        stream.CopyTo(data);
                    }
                }

                //using (var stream = new FileStream(dst + ".p7s", FileMode.Create))
                //{
                //    data.Position = 0;
                //    data.CopyTo(stream);
                //    data.Flush();
                //}
                //Console.WriteLine($"File {dst}.p7s ready.");

                byte[] bytes = data.GetBuffer();

                // Clean XML from a PCKS#7 signature
                var signedCms = new SignedCms();
                signedCms.Decode(bytes);
                bytes = signedCms.ContentInfo.Content;

                File.WriteAllBytes(dst, bytes);
                Console.WriteLine($"File {dst} ready.");
            }
        }

        private static string UserAgent()
        {
            Assembly asm = Assembly.GetCallingAssembly();
            string name = asm.GetName().Name;
            string ver = asm.GetName().Version.ToString(2);
            return $"{name}/{ver}";
        }
    }
}
