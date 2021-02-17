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
using System.Reflection;
using System.Security.Cryptography.Pkcs;

namespace ICRS_NBKI_Request
{
    class Program
    {
        //const string Url = "http://icrs.demo.nbki.ru/products/B2BRequestServlet"; //demo
        const string Url = "https://icrs.nbki.ru/products/B2BRequestServlet";

        static void Main(string[] args)
        {
            string src = "request.xml";
            string dst = "result.xml.p7s";
            string dsx = "result.xml";

            if (!File.Exists(src))
            {
                Console.WriteLine($"File {src} not found.");
                Environment.Exit(2);
            }

            DownloadFile(src, dst, dsx);
            Environment.Exit(0);
        }

        private static void DownloadFile(string src, string dst, string dsx)
        {
            var request = (HttpWebRequest)WebRequest.Create(Url);
            request.UserAgent = UserAgent();
            request.Method = WebRequestMethods.Http.Post;
            request.Proxy = null;

            ServicePointManager.ServerCertificateValidationCallback = delegate { return true; };

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

                using (var stream = new FileStream(dst, FileMode.Create))
                {
                    data.Position = 0;
                    data.CopyTo(stream);
                    data.Flush();
                }
                Console.WriteLine($"File {dst} ready.");

                byte[] bytes = data.GetBuffer();

                var signedCms = new SignedCms();
                signedCms.Decode(bytes);
                bytes = signedCms.ContentInfo.Content;

                File.WriteAllBytes(dsx, bytes);
                Console.WriteLine($"File {dsx} ready.");
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
