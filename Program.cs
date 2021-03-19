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
using System.Security.Cryptography.Pkcs;
using System.Xml;

namespace ICRS_NBKI_Request
{
    class Program
    {
        static readonly string _today = DateTime.Today.ToString("yyyy-MM-dd");

        static void Main(string[] args)
        {
            try
            {
                foreach (var arg in args)
                {
                    Config.SetIf(arg, "Password");
                    Config.Usage(arg);
                }

                // Use TLS 1.2 (required!)
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                // Ignore any Cert validation or install the root ones from http://cpca.cryptopro.ru/cacer.p7b
                //ServicePointManager.ServerCertificateValidationCallback += (se, cert, chain, sslerror) => { return true; };

                string bakPath = Config.CheckDirectory("RequestsBAK", "REQ");
                string dstPath = Config.CheckDirectory("Results", "XML");

                string srcPath = Config.Optional("Requests", ".");
                var dir = new DirectoryInfo(srcPath);
                foreach (var file in dir.GetFiles("*.req"))
                {
                    string reqFile = "request.xml";
                    FormRequestXml(file.FullName, reqFile);

                    string filename = $"{_today} {file.Name}";
                    string bakFile = Path.Combine(bakPath, filename);

                    filename = Path.ChangeExtension(filename, null);
                    string dstFile = Path.Combine(dstPath, filename + ".xml");

                    if (Config.IsSet("Request"))
                    {
                        DownloadFile(reqFile, dstFile, bakFile);
                    }

                    if (Config.IsSet("Extract"))
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
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Environment.Exit(1);
            }
        }

        private static void FormRequestXml(string file, string reqFile)
        {
            var xml = new XmlDocument();
            xml.Load(file);

            var idReqs = xml.SelectNodes("/product/prequest/req/IdReq");
            foreach (XmlNode id in idReqs)
            {
                var type = id.SelectSingleNode("idType");
                if (type != null && type.InnerText.Equals("21")) // Паспорт гражданина РФ
                {
                    var num = id.SelectSingleNode("idNum");
                    if (num != null && num.InnerText.Length < 6) // Номер паспорта
                    {
                        num.InnerText = num.InnerText.PadLeft(6, '0');
                    }
                }
            }    

            string key = "MemberCode";
            xml.GetElementsByTagName(key)[0].InnerText = Config.Required(key);

            key = "UserID";
            xml.GetElementsByTagName(key)[0].InnerText = Config.Required(key);

            key = "Password";
            xml.GetElementsByTagName(key)[0].InnerText = Config.Required(key);

            key = "requestDateTime";
            xml.GetElementsByTagName(key)[0].InnerText = _today;

            xml.Save(reqFile);
        }

        private static void DownloadFile(string src, string dst, string bak)
        {
            if (!File.Exists(src))
            {
                throw new Exception($"File \"{src}\" for Download not found.");
            }

            string uri = Config.Optional("Uri", "https://icrs.nbki.ru/products/B2BRequestServlet");

            var client = new WebClient();
            byte[] response;
            try
            {
                response = client.UploadData(uri, File.ReadAllBytes(src));
            }
            catch (Exception e)
            {
                throw new Exception($"HTTPS error: \"{e.Message}\"", e);
            }

            if (response != null && response.Length > 0)
            {
                if (Config.IsSet("SaveSigned"))
                {
                    File.WriteAllBytes(dst + ".p7s", response);
                }

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
                    throw new Exception($"NBKI error: \"{text.InnerText}\"");
                }

                Console.WriteLine($"Download \"{dst}\" done.");

                if (File.Exists(bak))
                {
                    File.Delete(bak);
                }

                File.Move(src, bak);
            }
        }

        private static void ExtractAccountReplies(string src, string format)
        {
            if (!File.Exists(src))
            {
                throw new Exception($"File \"{src}\" for Extract not found.");
            }

            var xml = new XmlDocument();
            xml.Load(src);

            bool activeOnly = Config.IsSet("ActiveOnly");

            var sections = xml.GetElementsByTagName("AccountReply");
            int done = 0;
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

                Console.WriteLine($"Extract \"{file}\" done.");
                done++;
            }

            string result = activeOnly
                ? $"Extracted {done} active of {sections.Count} total."
                : $"Extracted {done} of {sections.Count} total.";

            Console.WriteLine(result);
        }
    }
}
