/**
 * The MIT License
 * Copyright (c) 2012 Codenauts UG (haftungsbeschränkt). All rights reserved.
 *
 * Permission is hereby granted, free of charge, to any person
 * obtaining a copy of this software and associated documentation
 * files (the "Software"), to deal in the Software without
 * restriction, including without limitation the rights to use,
 * copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the
 * Software is furnished to do so, subject to the following
 * conditions:
 * 
 * The above copyright notice and this permission notice shall be
 * included in all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES
 * OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
 * NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT
 * HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY,
 * WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING
 * FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE. 
 */

using System;
using System.IO;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Diagnostics;
using System.IO.IsolatedStorage;
using System.Windows;
using System.Windows.Threading;

namespace HockeyApp
{
    public sealed class CrashReporter
    {
        private const String SdkName = "HockeySDK";
        private const String SdkVersion = "1.0.0";

        private static readonly CrashReporter instance = new CrashReporter();

        private Application _application;
        private string _identifier;

        static CrashReporter() { }
        private CrashReporter() { }

        public static CrashReporter Instance
        {
            get
            {
                return instance;
            }
        }


        public void Configure(Application application, string identifier)
        {
            if (_application == null)
            {
                _application = application;
                _identifier = identifier;
            }
            else
            {
                throw new InvalidOperationException("CrashReporter was already configured!");
            }

        }

        private string CreteCrashLog(Exception ex)
        {
            var builder = new StringBuilder();
            builder.Append(CreateHeader());
            builder.AppendLine();
            builder.Append(CreateStackTrace(ex));

            return builder.ToString();
        }

        public String CreateHeader()
        {
            var builder = new StringBuilder();
            builder.AppendFormat("Package: {0}\n", _application.GetType().Namespace);
            builder.AppendFormat("Product-ID: {0}\n", ProductId);
            builder.AppendFormat("Version: {0}\n", AppVersion);
            builder.AppendFormat("OS: {0} {1}\n", Environment.OSVersion.Platform, Environment.OSVersion.Version);
            builder.AppendFormat("Date: {0}\n", DateTime.UtcNow.ToString("o"));

            return builder.ToString();
        }

        private String CreateStackTrace(Exception exception)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.IsNullOrEmpty(exception.Message) ? "No reason" : exception.Message);
            builder.Append(string.IsNullOrEmpty(exception.StackTrace) ? "  at unknown location" : exception.StackTrace);

            Exception inner = exception.InnerException;
            if ((inner != null) && (!string.IsNullOrEmpty(inner.StackTrace)))
            {
                builder.AppendLine();
                builder.AppendLine();
                builder.AppendLine("Inner Exception");
                builder.AppendLine(string.IsNullOrEmpty(inner.Message) ? "No reason" : inner.Message);
                builder.Append(string.IsNullOrEmpty(inner.StackTrace) ? "  at unknown location" : inner.StackTrace);
            }

            return builder.ToString().Trim();
        }

        private static string AppVersion
        {
            get { return Assembly.GetEntryAssembly().GetName().Version.ToString(3); }
        }

        public static string ProductId
        {
            get
            {
                object[] attributes = Assembly.GetEntryAssembly().GetCustomAttributes(typeof(AssemblyProductAttribute), false);
                return attributes.Length == 0 ? string.Empty : ((AssemblyProductAttribute)attributes[0]).Product;
            }
        }

        public void SendCrash(Exception ex)
        {
            SendCrashLog(CreteCrashLog(ex));
        }

        private void SendCrashLog(string log)
        {
            string body = "";
            body += "raw=" + Uri.EscapeDataString(log);
            body += "&sdk=" + SdkName;
            body += "&sdk_version=" + SdkVersion;

            HttpWebRequest request = (HttpWebRequest)WebRequest.Create(new Uri("https://rink.hockeyapp.net/api/2/apps/" + _identifier + "/crashes"));
            request.Method = "POST";
            request.ContentType = "application/x-www-form-urlencoded";
            request.UserAgent = "Hockey/Windows";
            
            try
            {
                using (Stream stream = request.GetRequestStream())
                {
                    byte[] byteArray = Encoding.UTF8.GetBytes(body);
                    stream.Write(byteArray, 0, body.Length);
                    stream.Close();
                }

                request.GetResponse();
            }
            catch(Exception ex)
            {
                //we can't do anything more bacause we are 
                System.Diagnostics.Debug.WriteLine(ex.Message);
            }
        }
    }
}
