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
using System.Net.Security;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using System.Xml;

namespace ICRS_NBKI_Request
{
    public class Program
    {
        //Switch logic
        //private static readonly bool _test = false; //true;

        #region App.config

        private static readonly NameValueCollection Settings = ConfigurationManager.AppSettings;

        //Parameters
        private static readonly string Uri              = Settings[nameof(Uri)];
        private static readonly string ServerThumbprint = Helpers.GetThumbprint(Settings[nameof(ServerThumbprint)]);
        private static readonly string MyThumbprint     = Helpers.GetThumbprint(Settings[nameof(MyThumbprint)]);
        private static readonly string MemberCode       = Settings[nameof(MemberCode)];
        private static readonly string UserID           = Settings[nameof(UserID)];
        private static          string Password         = Settings[nameof(Password)];

        //Paths
        private static readonly DirectoryInfo RequestsNew   = Directory.CreateDirectory(Settings[nameof(RequestsNew)]);
        private static readonly DirectoryInfo RequestsBak   = Directory.CreateDirectory(Settings[nameof(RequestsBak)]);
        private static readonly DirectoryInfo RequestsErr   = Directory.CreateDirectory(Settings[nameof(RequestsErr)]);

        private static readonly DirectoryInfo ResultsP7b    = Directory.CreateDirectory(Settings[nameof(ResultsP7b)]);
        private static readonly DirectoryInfo ResultsXml    = Directory.CreateDirectory(Settings[nameof(ResultsXml)]);
        private static readonly DirectoryInfo ResultsExtra  = Directory.CreateDirectory(Settings[nameof(ResultsExtra)]);

        //Options
        private static readonly bool DoRequests     = Settings[nameof(DoRequests)].Equals("1");
        private static readonly bool ValidateServer = Settings[nameof(ValidateServer)].Equals("1");
        private static readonly bool UseCertificate = Settings[nameof(UseCertificate)].Equals("1");
        private static readonly bool SaveSigned     = Settings[nameof(SaveSigned)].Equals("1");
        private static readonly bool DoExtra        = Settings[nameof(DoExtra)].Equals("1");
        private static readonly bool ActiveOnly     = Settings[nameof(ActiveOnly)].Equals("1");
        private static readonly bool PressEnter     = Settings[nameof(PressEnter)].Equals("1");

        #endregion App.config

        //Const values
        private static readonly string requestDateTime = DateTime.Today.ToString("yyyy-MM-dd"); //lowerCase as this XmlNode

        //Cached values 
        private static readonly X509Certificate MyCertificate = Helpers.GetX509Certificate(MyThumbprint);
        private static          NetworkCredential Credential = new NetworkCredential(UserID, Password);

        //Counters
        private static int _total = 0;
        private static int _errors = 0;

        public static void Main(string[] args)
        {
            App.Usage(args);

            try
            {
                foreach (var arg in args)
                {
                    if (arg.StartsWith(nameof(Password) + "=", StringComparison.OrdinalIgnoreCase))
                    {
                        Password = arg.Substring(nameof(Password).Length + 1);
                        Credential = new NetworkCredential(UserID, Password);
                    }
                }

                #region Certificates

                // Use TLS 1.2 (required!)
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;

                // Ignore any Cert validation or install the root ones from http://cpca.cryptopro.ru/cacer.p7b
                //ServicePointManager.ServerCertificateValidationCallback += (se, cert, chain, sslerror) => { return true; };

                if (ValidateServer)
                {
                    ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateServerCertificate);
                }
                else if (!string.IsNullOrEmpty(ServerThumbprint))
                {
                    ServicePointManager.ServerCertificateValidationCallback += new RemoteCertificateValidationCallback(ValidateServerThumbprint);
                }

                #endregion Certificates

                //if (_test)
                //{
                //    TestTLS.Run(MyCertificate);
                //    return;
                //}

                foreach (var file in RequestsNew.GetFiles("*.req"))
                {
                    _total++;
                    string filename = $"{requestDateTime} {file.Name}";
                    string bakFile = Path.Combine(RequestsBak.FullName, filename);
                    string errFile = Path.Combine(RequestsErr.FullName, filename);

                    filename = Path.ChangeExtension(filename, null);
                    string binFile = Path.Combine(ResultsP7b.FullName, filename + ".xml.p7b");
                    string dstFile = Path.Combine(ResultsXml.FullName, filename + ".xml");

                    if (DoRequests)
                    {
                        DownloadFile(file.FullName, binFile, dstFile, bakFile, errFile);
                    }

                    if (DoExtra)
                    {
                        string mask = filename + " *.xml";

                        foreach (var f in ResultsExtra.GetFiles(mask))
                        {
                            f.Delete();
                        }

                        string format = Path.Combine(ResultsExtra.FullName, filename + " {0:000}.xml");
                        ExtractAccountReplies(dstFile, format);
                    }
                }
                Footer(0);
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                Footer(1);
            }
        }

        private static void Footer(int exitCode = 0)
        {
            Console.WriteLine($"Total: {_total}, errors: {_errors}.");

            if (PressEnter)
            {
                Console.WriteLine("Press Enter to exit...");
                Console.ReadLine();
            }

            Environment.Exit(exitCode);
        }

        /// <summary>
        /// Корректировка исходного XML для пригодности к запросу.
        /// </summary>
        /// <param name="file">Исходный файл.</param>
        /// <param name="req">Файл результата.</param>
        private static void FormRequestXml(string file, string req)
        {
            var xml = new XmlDocument();
            try
            {
                xml.Load(file);

                XmlNodeList idReqs = xml.SelectNodes("/product/prequest/req/IdReq");
                foreach (XmlNode id in idReqs)
                {
                    XmlNode idType = id.SelectSingleNode(nameof(idType));
                    if (idType != null && idType.InnerText.Equals("21")) // Паспорт гражданина РФ
                    {
                        XmlNode idNum = id.SelectSingleNode(nameof(idNum));
                        if (idNum != null && idNum.InnerText.Length < 6) // Номер паспорта
                        {
                            idNum.InnerText = idNum.InnerText.PadLeft(6, '0');
                        }
                    }
                }

                xml.GetElementsByTagName(nameof(MemberCode))[0].InnerText = MemberCode;
                xml.GetElementsByTagName(nameof(UserID))[0].InnerText = UserID;
                xml.GetElementsByTagName(nameof(Password))[0].InnerText = Password;
                xml.GetElementsByTagName(nameof(requestDateTime))[0].InnerText = requestDateTime;

                xml.Save(req);
            }
            catch (Exception e)
            {
                throw new Exception("Error in source XML.", e);
            }
        }

        /// <summary>
        /// Отправка запроса и получение результата от сервера.
        /// </summary>
        /// <param name="src">Исходный файл запроса.</param>
        /// <param name="bin">Имя файла для бинарного результата с подписью.</param>
        /// <param name="dst">Имя файла для XML результата без подписи.</param>
        /// <param name="bak">Имя файла для сохранения исходного файла запроса в случае успеха.</param>
        /// <param name="err">Имя файла для сохранения исходного файла запроса в случае получения ошибки.</param>
        private static void DownloadFile(string src, string bin, string dst, string bak, string err)
        {
            const string req = "request.xml";
            FormRequestXml(src, req);

            if (!File.Exists(req))
            {
                throw new FileNotFoundException("File for Request not found.", req);
            }

            byte[] data = File.ReadAllBytes(req);

            var request = (HttpWebRequest)WebRequest.Create(Uri);
            request.Method = "POST";

            request.ContentType = "binary/octed-stream;character=windows-1251";
            request.ContentLength = data.Length;

            if (MyCertificate != null)
            {
                request.ClientCertificates.Add(MyCertificate);
            }

            request.Credentials = Credential;

            using (var writer = new BinaryWriter(request.GetRequestStream()
                ?? throw new InvalidOperationException("Request stream is null.")))
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
                    ?? throw new InvalidOperationException("Response stream is null."))
                {
                    stream.CopyTo(ms); // to get the actual length of data (server does't set ContentLength!)
                    data = ms.ToArray();
                }
            }
            catch (Exception e)
            {
                Console.WriteLine(e.Message);
                return;
            }

            if (SaveSigned)
            {
                File.WriteAllBytes(bin, data);
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
                _errors++;
                File.Copy(req, err, true);
                File.Delete(req);
                File.Delete(src);
                throw new InvalidOperationException($"NBKI error: \"{text.InnerText}\".");
            }

            File.Copy(req, bak, true);
            File.Delete(req);
            File.Delete(src);
            Console.WriteLine($"Download \"{dst}\" done.");
        }

        /// <summary>
        /// Извлечение кредитов из общего файла в отдельные для детального разбора вручную в случае ошибок.
        /// </summary>
        /// <param name="src">Исходный полученный общий файл.</param>
        /// <param name="format">Формат имени для сохраняемых по отдельности файлов.</param>
        private static void ExtractAccountReplies(string src, string format)
        {
            if (!File.Exists(src))
            {
                throw new FileNotFoundException("File for Extract not found.", src);
            }

            var xml = new XmlDocument();
            xml.Load(src);

            XmlNodeList AccountReply = xml.GetElementsByTagName(nameof(AccountReply));
            int done = 0;
            for (int i = 0; i < AccountReply.Count; i++)
            {
                var sec = new XmlDocument();
                sec.LoadXml(AccountReply[i].OuterXml);

                if (ActiveOnly)
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

            string result = ActiveOnly
                ? $"Extracted {done} active of {AccountReply.Count} total."
                : $"Extracted {done} of {AccountReply.Count} total.";

            Console.WriteLine(result);
        }

        /// <summary>
        /// Вызывается из RemoteCertificateValidationDelegate.
        /// Проверяет удаленный сертификат SSL/TLS, используемый для проверки подлинности.
        /// </summary>
        /// <param name="sender">Объект, содержащий сведения о состоянии для данной проверки.</param>
        /// <param name="certificate">Сертификат, используемый для проверки подлинности удаленной стороны.</param>
        /// <param name="chain">Цепочка центров сертификации, связанная с удаленным сертификатом.</param>
        /// <param name="sslPolicyErrors">Одна или более ошибок, связанных с удаленным сертификатом.</param>
        /// <returns>Значение типа Boolean, определяющее, принимается ли указанный сертификат для проверки подлинности.</returns>
        public static bool ValidateServerCertificate(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            if (sslPolicyErrors != SslPolicyErrors.None)
            {
                Console.WriteLine($"Server certificate error: {sslPolicyErrors}.");
                return false;
            }

            if (!string.IsNullOrEmpty(ServerThumbprint) && !certificate.GetCertHashString().Equals(ServerThumbprint))
            {
                Console.WriteLine($"Server certificate has wrong thumbprint.");
                return false;
            }

            return true; // valid
        }

        /// <summary>
        /// Вызывается из RemoteCertificateValidationDelegate.
        /// Проверяет удаленный самоподписанный сертификат SSL/TLS, используемый для проверки подлинности только по его отпечатку.
        /// </summary>
        /// <param name="sender">Объект, содержащий сведения о состоянии для данной проверки.</param>
        /// <param name="certificate">Сертификат, используемый для проверки подлинности удаленной стороны.</param>
        /// <param name="chain">Цепочка центров сертификации, связанная с удаленным сертификатом (игнорируется).</param>
        /// <param name="sslPolicyErrors">Одна или более ошибок, связанных с удаленным сертификатом (игнорируется).</param>
        /// <returns>Значение типа Boolean, определяющее, принимается ли указанный сертификат для проверки подлинности.</returns>
        public static bool ValidateServerThumbprint(
              object sender,
              X509Certificate certificate,
              X509Chain chain,
              SslPolicyErrors sslPolicyErrors)
        {
            if (!certificate.GetCertHashString().Equals(ServerThumbprint))
            {
                Console.WriteLine($"Server certificate has wrong thumbprint.");
                return false;
            }

            return true; // valid
        }
    }
}
