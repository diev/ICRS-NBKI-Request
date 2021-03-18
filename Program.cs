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
using System.Net;
using System.Security.Cryptography.Pkcs;
using System.Xml;

namespace ICRS_NBKI_Request
{
    class Program
    {
        static NameValueCollection _settings = ConfigurationManager.AppSettings;
        static readonly string _today = DateTime.Today.ToString("yyyy-MM-dd");

        static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                SetIf(arg, "Password");
                Usage(arg);
            }

            // Use TLS 1.2 (required!)
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

            // Ignore any Cert validation or install the root ones from http://cpca.cryptopro.ru/cacer.p7b
            //ServicePointManager.ServerCertificateValidationCallback += (se, cert, chain, sslerror) => { return true; };

            string bakPath = _settings["RequestsBAK"] ?? "REQ";
            if (!Directory.Exists(bakPath))
            {
                Directory.CreateDirectory(bakPath);
            }

            string dstPath = _settings["Results"] ?? "XML";
            if (!Directory.Exists(dstPath))
            {
                Directory.CreateDirectory(dstPath);
            }

            string srcPath = _settings["Requests"] ?? ".";
            var dir = new DirectoryInfo(srcPath);
            foreach (var file in dir.GetFiles("*.req"))
            {
                var xml = new XmlDocument();
                xml.Load(file.FullName);

                xml.GetElementsByTagName("MemberCode")[0]
                    .InnerText = _settings["MemberCode"]
                    ?? throw new Exception("No value for *MemberCode*.");

                xml.GetElementsByTagName("UserID")[0]
                    .InnerText = _settings["UserID"]
                    ?? throw new Exception("No value for *UserID*.");

                xml.GetElementsByTagName("Password")[0]
                    .InnerText = _settings["Password"]
                    ?? throw new Exception("No value for *Password*.");

                xml.GetElementsByTagName("requestDateTime")[0]
                    .InnerText = _today;

                string reqFile = "request.xml";
                xml.Save(reqFile);

                string filename = $"{_today} {file.Name}";
                string bakFile = Path.Combine(bakPath, filename);

                filename = Path.ChangeExtension(filename, null);
                string dstFile = Path.Combine(dstPath, filename + ".xml");

                if (IsSet("Request"))
                {
                    DownloadFile(reqFile, dstFile, bakFile);
                }

                if (IsSet("Extract"))
                {
                    string mask = filename + " *.xml";
                    var d = new DirectoryInfo(dstPath);
                    foreach (var f in d.GetFiles(mask))
                    {
                        f.Delete();
                    }

                    string format = Path.Combine(dstPath, filename + " {0:000}.xml");
                    ExtractAccountReplies(dstFile, format);
                }
            }

            Environment.Exit(0);
        }

        private static void Usage(string arg)
        {
            var help = new [] { "/?", "-?", "/h", "-h", "/help", "-help", "--help" };
            foreach (string opt in help)
            {
                if (arg.Equals(opt, StringComparison.OrdinalIgnoreCase))
                {
                    Console.WriteLine("Help"); //TODO Help wanted

                    Environment.Exit(1);
                }
            }
        }

        private static void SetIf(string arg, string key)
        {
            if (arg.StartsWith(key + "=", StringComparison.OrdinalIgnoreCase))
            {
                _settings[key] = arg.Substring(key.Length + 1);
            }
        }

        private static bool IsSet(string key)
        {
            return (_settings[key] ?? "0").Equals("1");
        }

        private static void DownloadFile(string src, string dst, string bak)
        {
            string uri = _settings["Uri"] ?? "https://icrs.nbki.ru/products/B2BRequestServlet";

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

                var xml = new XmlDocument();
                xml.Load(dst);
                var text = xml.SelectSingleNode("/product/preply/err/ctErr/Text/text()");
                if (text != null)
                {
                    Console.WriteLine("Error: " + text.InnerText); //TODO Error returned
                }
                else
                {
                    Console.WriteLine($"File {dst} ready.");
                }

                if (File.Exists(bak))
                {
                    File.Delete(bak);
                }

                File.Move(src, bak);
            }
        }

        private static void ExtractAccountReplies(string src, string format)
        {
            var xml = new XmlDocument();
            xml.Load(src);

            bool activeOnly = IsSet("ActiveOnly");

            var sections = xml.GetElementsByTagName("AccountReply");
            for (int i = 0; i < sections.Count; i++)
            {
                var sec = new XmlDocument();
                sec.LoadXml(sections[i].OuterXml);

                if (activeOnly)
                {
                    var text = sec.SelectSingleNode("//accountRating/text()");
                    if (text == null || !text.InnerText.Equals("0"))
                    {
                        continue;
                    }
                }

                var decl = sec.CreateXmlDeclaration("1.0", "windows-1251", null);
                sec.InsertBefore(decl, sec.DocumentElement);

                string file = string.Format(format, i + 1);
                sec.Save(file);
            }
        }
    }
}
