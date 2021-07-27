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
using System.Net.Security;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace ICRS_NBKI_Request
{
    public class Program
    {
        private static readonly bool _test = false; //true;
        private static readonly string _today = DateTime.Today.ToString("yyyy-MM-dd");
        private static X509Certificate _serverCertificate = null;
        private static X509Certificate _myCertificate = null;

        public static void Main(string[] args)
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
                string errPath = Config.CheckDirectory("ErrorsBAK", "ERR");
                string dstPath = Config.CheckDirectory("Results", "XML");
                string dxtPath = Config.CheckDirectory("ExtraResults", @"XML\Extra");

                string srcPath = Config.Optional("Requests", ".");

                if (Config.TryGet("ServerThumbprint", out string serverThumbprint))
                { 
                    _serverCertificate = GetX509Certificate(serverThumbprint);
                }

                if (Config.TryGet("MyThumbprint", out string myThumbprint))
                {
                    _myCertificate = GetX509Certificate(myThumbprint);
                }

                if (_test)
                {
                    Test();
                    return;
                }

                var dir = new DirectoryInfo(srcPath);
                foreach (var file in dir.GetFiles("*.req"))
                {
                    string srcFile = file.FullName;

                    string reqFile = "request.xml";
                    FormRequestXml(srcFile, reqFile);

                    string filename = $"{_today} {file.Name}";
                    string bakFile = Path.Combine(bakPath, filename);
                    string errFile = Path.Combine(errPath, filename);

                    filename = Path.ChangeExtension(filename, null);
                    string dstFile = Path.Combine(dstPath, filename + ".xml");

                    if (Config.IsSet("Request"))
                    {
                        DownloadFile(srcFile, reqFile, dstFile, bakFile, errFile);
                    }

                    if (Config.IsSet("Extract"))
                    {
                        string mask = filename + " *.xml";
                        var d = new DirectoryInfo(dxtPath);
                        foreach (var f in d.GetFiles(mask))
                        {
                            f.Delete();
                        }

                        string format = Path.Combine(dxtPath, filename + " {0:000}.xml");
                        ExtractAccountReplies(dstFile, format);
                    }
                }

                if (Config.IsSet("PressEnter"))
                {
                    Console.WriteLine("Press Enter to exit...");
                    Console.ReadLine();
                }
                Environment.Exit(0);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                if (Config.IsSet("PressEnter"))
                {
                    Console.WriteLine("Press Enter to exit...");
                    Console.ReadLine();
                }
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

        private static void DownloadFile(string src, string req, string dst, string bak, string err)
        {
            if (!File.Exists(req))
            {
                throw new FileNotFoundException($"File for Download not found.", req);
            }

            byte[] data = File.ReadAllBytes(req);
            string uri = Config.Optional("Uri", "https://icrs.demo.nbki.ru/products/B2BRequestServlet");

            var request = (HttpWebRequest)WebRequest.Create(uri);
            request.Method = "POST";
            request.ContentType = "binary/octed-stream;character=windows-1251";
            request.ContentLength = data.Length;

            if (_serverCertificate != null)
            {
                request.ServerCertificateValidationCallback = new RemoteCertificateValidationCallback(ValidateServerCertificate);
            }

            if (_myCertificate != null)
            {
                request.ClientCertificates.Add(_myCertificate);
            }

            using (var writer = new BinaryWriter(request.GetRequestStream()
                ?? throw new InvalidOperationException("Request stream is null")))
            {
                writer.Write(data);
                writer.Flush();
            }

            try
            {
                var response = (HttpWebResponse)request.GetResponse();
                if (response.StatusCode != HttpStatusCode.OK)
                {
                    throw new WebException($"Error {response.StatusCode} of Web connection.");
                }

                using (var ms = new MemoryStream())
                using (var stream = response.GetResponseStream()
                    ?? throw new InvalidOperationException("Response stream is null"))
                {
                    stream.CopyTo(ms);
                    data = ms.ToArray();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
            }

            if (Config.IsSet("SaveSigned"))
            {
                File.WriteAllBytes(dst + ".p7s", data);
            }

            // Clean XML from a PKCS#7 signature
            var signedCms = new SignedCms();
            signedCms.Decode(data);
            data = signedCms.ContentInfo.Content;

            File.WriteAllBytes(dst, data);

            var xml = new XmlDocument();
            xml.Load(dst);
            var text = xml.SelectSingleNode("/product/preply/err/ctErr/Text/text()");
            if (text != null)
            {
                File.Copy(req, err, true);
                File.Delete(req);
                File.Delete(src);
                throw new InvalidOperationException($"NBKI error: \"{text.InnerText}\"");
            }

            File.Copy(req, bak, true);
            File.Delete(req);
            File.Delete(src);
            Console.WriteLine($"Download \"{dst}\" done.");
        }

        private static void ExtractAccountReplies(string src, string format)
        {
            if (!File.Exists(src))
            {
                throw new FileNotFoundException("File for Extract not found.", src);
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

        private static void Test()
        {
            string uri = "https://reports.demo.nbki.ru/";
            string result = Request(uri);

            Console.WriteLine(result.Length > 200 ? result.Substring(0, 200) : result);
        }

        private static string Request(string uri)
        {
            var request = (HttpWebRequest) WebRequest.Create(uri);
            request.ClientCertificates.Add(_myCertificate);

            return Response(request);
        }

        private static string Response(HttpWebRequest request)
        {
            var response = (HttpWebResponse) request.GetResponse();
            if (response.StatusCode != HttpStatusCode.OK)
            {
                throw new InvalidOperationException($"Unexpected behavior! Status code: {response.StatusCode}.");
            }

            using (var streamReader = new StreamReader(response.GetResponseStream()
                ?? throw new InvalidOperationException("Response stream is null")))
            {
                return streamReader.ReadToEnd();
            }
        }

        private static X509Certificate GetX509Certificate(string thumbprint, bool validOnly = true)
        {
            thumbprint = thumbprint.ToUpper().Replace(" ", string.Empty); //By fact ToUpper() not required

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

                throw new ArgumentNullException("~Thumbprint", $"Certificate with thumbprint \"{thumbprint}\" not found.");
            }
        }

        // The following method is invoked by the RemoteCertificateValidationDelegate.
        public static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors == SslPolicyErrors.None)
            {
                return true; //TODO: Check _serverThumbprint additionally
            }

            Console.WriteLine($"Certificate error: {sslPolicyErrors}");

            // Do not allow this client to communicate with unauthenticated servers.
            return false;
        }
    }
}
