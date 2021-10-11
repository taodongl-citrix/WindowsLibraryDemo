using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Win32;
using System.Diagnostics;
using System.Net;
using Newtonsoft.Json;
using System.Xml;
using System.IO;
using WindowsLibraryDemo.Common.EventHub;
using System.Threading;
using System.Net.Http;


namespace WindowsLibraryDemo
{

    public class TokenInfo
    {
        public string strCustomerId;
        public string strEventHubEndpoint;
        public string strEventHubToken;
        public string strDsAuth;
        public string strUserName;
        public string strTokenExpiry;
    }
    /// <summary>
    /// 
    /// </summary>
    /// /////////
    public class CitrixReceiverAnalytics
    {
        //private TokenInfo m_tokenInfo;

        private const string event_product = "Demo.Windows";
        private const int NUMBER_OF_MILLISECONDS_IN_A_DAY = 86400;
        private Object thisLock = new Object();
        // Address to fetch Public IP Address - No separate url for staging/ prod
        private const string PublicIPBaseAddress = "https://www.cloud.com/";
        private const string PublicIPFetchUrlPath = "api/locateip";

        private JsonSerializerSettings getJasonSettings()
        {
            var settings = new JsonSerializerSettings();
            settings.NullValueHandling = NullValueHandling.Ignore;

            return settings;
        }

        private TokenInfo bypassStoreTokenAndGetFromRegistry(out bool bBypassStore)
        {
            TokenInfo TempTokenInfo = null;
            bBypassStore = false;

            Tracer.DServices.Trace("CAS - bypassStoreTokenAndGetFromRegistry: Check if we need to bypass store for token generation.");

            using (RegistryKey k = Registry.CurrentUser.OpenSubKey(@"Software\Citrix\Receiver\CASTokenInfo"))
            {
                if (k != null)
                {
                    string strTemp = (string)k.GetValue("BypassStore");
                    if (strTemp.IndexOf("True", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        Tracer.DServices.Trace("CAS - bypassStoreTokenAndGetFromRegistry: BypassStore is true.");
                        bBypassStore = true;
                        TempTokenInfo = new TokenInfo();
                        TempTokenInfo.strCustomerId = (string)k.GetValue("CustomerId");
                        TempTokenInfo.strEventHubEndpoint = (string)k.GetValue("EventHubEndpoint");
                        TempTokenInfo.strEventHubToken = (string)k.GetValue("EventHubToken");
                        TempTokenInfo.strDsAuth = (string)k.GetValue("DsAuth");
                    }
                }
            }
            return TempTokenInfo;
        }

        public TokenInfo getToken(string strStoreConfigURL, string strStoreServiceRecordID, bool bRefresh = false)
        {
            string strTokenData = "";
            Tracer.DServices.Trace("CAS - getToken : Trying to fetch token for store {0}", strStoreConfigURL);

            IList<IProvider> providers = new List<IProvider>();
            IProvider pStoreProvider = null;
            bool bBypassStore = false;

            if (providers.Count > 0)
            {
                foreach (IProvider provider in providers)
                {
                    Tracer.DServices.Trace("CAS - getToken: Provider config URL {0}", provider.ConfigURL.ToString());

                    if (strStoreServiceRecordID == provider.ServiceRecordId)
                    {
                        pStoreProvider = provider;
                        break;
                    }
                    else
                    {
                        Tracer.DServices.Error(" CAS - getToken: there is no store avilabe for given store record ID : {0}", strStoreServiceRecordID);
                    }
                }
            }

            TokenInfo TokenFromReg = bypassStoreTokenAndGetFromRegistry(out bBypassStore);

            if (bBypassStore)
            {
                Tracer.DServices.Trace("CAS - getToken: Returns token from registry.");
                if (pStoreProvider != null)
                    TokenFromReg.strUserName = pStoreProvider.UserName;
                return TokenFromReg;
            }

            if (!bRefresh)
            {
                strTokenData = CitrixAnalyticsTokenProtector.CtxDecryptDatastring(strStoreConfigURL, strStoreServiceRecordID);
            }

            if (strTokenData == "")
            {
                if (pStoreProvider != null)
                {

                    string str = pStoreProvider.ToString();

                    if (str != "")
                    {
                        CitrixAnalyticsTokenProtector.CtxEncryptData(strStoreConfigURL, strStoreServiceRecordID, str);
                        strTokenData = str;
                    }
                    else
                    {
                        Tracer.DServices.Trace(" CAS - getToken: No analytics service avilable for store {0}", pStoreProvider.ConfigURL.ToString());
                    }
                }

            }

            TokenInfo tokenInfo = parseXMLDataAndRturnToken(strTokenData);
            if (pStoreProvider != null)
                tokenInfo.strUserName = pStoreProvider.UserName;
            Tracer.DServices.Trace(" CAS - getToken: Setting user name as : {0}", tokenInfo.strUserName);

            return tokenInfo;
        }

        public TokenInfo parseXMLDataAndRturnToken(string xmlString)
        {
            TokenInfo tokenInfo = new TokenInfo();
            try
            {
                // Create an XmlReader
                using (XmlReader reader = XmlReader.Create(new StringReader(xmlString)))
                {
                    while (reader.Read())
                    {
                        switch (reader.NodeType)
                        {
                            case XmlNodeType.Element:
                                {
                                    if (reader.Name == "customerId")
                                    {
                                        reader.Read();
                                        tokenInfo.strCustomerId = reader.Value;
                                    }
                                    else if (reader.Name == "eventHubToken")
                                    {
                                        reader.Read();
                                        tokenInfo.strEventHubToken = reader.Value;
                                    }
                                    else if (reader.Name == "eventHubEndpoint")
                                    {
                                        reader.Read();
                                        tokenInfo.strEventHubEndpoint = reader.Value;
                                    }
                                    else if (reader.Name == "expiry")
                                    {
                                        reader.Read();
                                        tokenInfo.strTokenExpiry = reader.Value;
                                    }
                                    else if (reader.Name == "dsAuth")
                                    {
                                        reader.Read();
                                        tokenInfo.strDsAuth = reader.Value;
                                    }

                                }
                                break;
                        }
                    }

                    return tokenInfo;
                }
            }
            catch (Exception e)
            {
                Tracer.DServices.Trace("CAS - parseXMLDataAndRturnToken : Exception with message {0}", e.Message);
                return null;
            }
        }

        public void RefreshCASTokensForAllProviders(IList<IProvider> providersToRefresh)
        {
            long currentTimeInSeconds = DateTimeOffset.Now.ToUnixTimeSeconds();
            foreach (var provider in providersToRefresh)
            {
                var tokenExpiryValue = provider.GetTokenExpiryValue();
                long tokenExpiryValueLong = 0;
                Int64.TryParse(tokenExpiryValue, out tokenExpiryValueLong);
                if (provider.ConfigURL != null && (tokenExpiryValue.Length == 0 || (tokenExpiryValueLong - currentTimeInSeconds) < NUMBER_OF_MILLISECONDS_IN_A_DAY))
                {
                    string casTokenInfo = provider.ToString();
                    if (!string.IsNullOrWhiteSpace(casTokenInfo))
                    {
                        TokenInfo tokenInfo = parseXMLDataAndRturnToken(casTokenInfo);
                        CitrixAnalyticsTokenProtector.CtxEncryptData(provider.ConfigURL, provider.ServiceRecordId, casTokenInfo);
                    }
                }
            }
        }

        public string getReceiverVersion()
        {

            string installedReceiverVersion = "";

            using (RegistryKey k = Registry.LocalMachine.OpenSubKey(@"SOFTWARE\Demo\Install\{A9852000-047D-11DD-95FF-0800200C9AAA}"))
            {
                if (k != null)
                {
                    installedReceiverVersion = (string)k.GetValue("DisplayVersion");
                }
                else
                {
                    using (RegistryKey v = Registry.CurrentUser.OpenSubKey(@"SOFTWARE\Demo\Install\{A9852000-047D-11DD-95FF-0800200C9AAA}"))
                    {
                        if (v != null)
                        {
                            installedReceiverVersion = (string)v.GetValue("DisplayVersion");
                        }
                    }
                }
            }
            return installedReceiverVersion;
        }

        public string getDeviceIP()
        {
            string myIP = "";
            string clientName = Dns.GetHostName(); // Retrive the Name of HOST 

            IPHostEntry e = Dns.GetHostEntry(clientName);

            if (e != null)
            {
                for (int i = 0; i < e.AddressList.Length; i++)
                {
                    if (e.AddressList[i].AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
                    {
                        myIP = e.AddressList[i].ToString();
                        break;
                    }
                }
            }

            return myIP;

        }

        private TimeZoneInfo getTimeZoneInfo()
        {
            string strIsDaylight = "no";
            double dwBias = (DateTime.UtcNow - DateTime.Now).TotalMinutes;

            dwBias = Math.Round(dwBias, MidpointRounding.AwayFromZero);

            if (TimeZone.CurrentTimeZone.IsDaylightSavingTime(DateTime.Now))
            {
                strIsDaylight = "yes";
            }

            TimeZoneInfo info = TimeZoneInfo.CreateCustomTimeZone(strIsDaylight, TimeSpan.Zero, strIsDaylight, strIsDaylight);
            return info;
        }


        public string getAccountLoginPayload(TokenInfo tokenInfo)
        {
            EventHubPayload<AccountLoginPaylod> payload1 = new EventHubPayload<AccountLoginPaylod>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.AccountLogon;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                payload1.AccountUserInfo = new AccountInfo { UserName = tokenInfo.strUserName };
                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }

            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            payload1.Payload = new AccountLoginPaylod
            {
                LoggedInOS = Environment.OSVersion.VersionString,
                HostName = Dns.GetHostName(),
                TimeZone = getTimeZoneInfo()
            };

            string postData = JsonConvert.SerializeObject(payload1, getJasonSettings());
            return postData;
        }

        public string getSessionLaunchPayload(string strAppName, string strType, TokenInfo tokenInfo)
        {
            EventHubPayload<SessionLaunchPayload> payload1 = new EventHubPayload<SessionLaunchPayload>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.SessionLaunch;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                payload1.AccountUserInfo = new AccountInfo { UserName = tokenInfo.strUserName };
                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }

            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            payload1.Payload = new SessionLaunchPayload
            {
                LoggedInOS = Environment.OSVersion.VersionString,
                AppName = strAppName,
                Type = strType,
                HostName = Dns.GetHostName(),
                TimeZone = getTimeZoneInfo()
            };

            string postData = JsonConvert.SerializeObject(payload1, getJasonSettings());
            return postData;
        }

        public string getSaaSLaunchPayload(string strTimeStamp, string strURL, string browser, string userAgent, string saasAppName, TokenInfo tokenInfo)
        {
            EventHubPayload<SaaSLaunchPayload> payload1 = new EventHubPayload<SaaSLaunchPayload>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.SaasLaunch;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                payload1.AccountUserInfo = new AccountInfo { UserName = tokenInfo.strUserName };

                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }

            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            payload1.Payload = new SaaSLaunchPayload
            {
                LoggedInOS = Environment.OSVersion.VersionString,
                PlatformExtraInfo = "",

                HostName = Dns.GetHostName(),
                TimeStamp = strTimeStamp,
                URL = strURL,
                Browser = browser,
                UserAgent = userAgent,
                SaasAppName = saasAppName,
                TimeZone = getTimeZoneInfo()
            };

            string postData = JsonConvert.SerializeObject(payload1, getJasonSettings());
            return postData;
        }

        public string getSaaSEndPayload(string strTimeStamp, string strURL, string browser, string userAgent, string saasAppName, TokenInfo tokenInfo)
        {
            EventHubPayload<SaaSEndPayload> payload1 = new EventHubPayload<SaaSEndPayload>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.SaasEnd;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                payload1.AccountUserInfo = new AccountInfo { UserName = tokenInfo.strUserName };

                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }

            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            payload1.Payload = new SaaSEndPayload
            {
                HostName = Dns.GetHostName(),
                PlatformExtraInfo = "",
                LoggedInOS = Environment.OSVersion.VersionString,
                TimeStamp = strTimeStamp,
                Browser = browser,
                UserAgent = userAgent,
                SaasAppName = saasAppName,
                URL = strURL,
                TimeZone = getTimeZoneInfo()



            };

            string postData = JsonConvert.SerializeObject(payload1, getJasonSettings());
            return postData;
        }

        public string getSaaSUrlNavigatePayload(string strTimeStamp, string strURL, string browser, string userAgent, string saasAppName, TokenInfo tokenInfo)
        {
            EventHubPayload<SaaSUrlNavigatePayload> payload1 = new EventHubPayload<SaaSUrlNavigatePayload>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.SaasUrlNavigate;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                payload1.AccountUserInfo = new AccountInfo { UserName = tokenInfo.strUserName };

                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }

            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            payload1.Payload = new SaaSUrlNavigatePayload
            {
                HostName = Dns.GetHostName(),
                PlatformExtraInfo = "",
                LoggedInOS = Environment.OSVersion.VersionString,
                TimeStamp = strTimeStamp,
                Browser = browser,
                UserAgent = userAgent,
                SaasAppName = saasAppName,
                URL = strURL,
                TimeZone = getTimeZoneInfo()
            };

            string postData = JsonConvert.SerializeObject(payload1, getJasonSettings());
            return postData;
        }

        public string getSaaSFileDownloadPayload(double dwFileSize, string strFileName, string strFileFormat,
                                                 string strDeviceType, string strFilePath, string strTimeStamp,
                                                 string strURL, string browser, string userAgent, string saasAppName, TokenInfo tokenInfo)
        {
            EventHubPayload<SaaSFileDownloadPayload> payload1 = new EventHubPayload<SaaSFileDownloadPayload>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.SaaSFileDownload;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                payload1.AccountUserInfo = new AccountInfo { UserName = tokenInfo.strUserName };

                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }
            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            payload1.Payload = new SaaSFileDownloadPayload
            {
                HostName = Dns.GetHostName(),
                PlatformExtraInfo = " ",
                LoggedInOS = Environment.OSVersion.VersionString,
                TimeStamp = strTimeStamp,
                Browser = browser,
                UserAgent = userAgent,
                SaasAppName = saasAppName,
                URL = strURL,
                TimeZone = getTimeZoneInfo(),
                DeviceType = strDeviceType,
                FilePath = strFilePath,
                FileDetails = new DownloadFiledetails { Size = dwFileSize, FileName = strFileName, Format = strFileFormat }
            };

            string postData = JsonConvert.SerializeObject(payload1, getJasonSettings());
            return postData;
        }

        public string getSaaSFilePrintingPayload(double dwFileSize, string strFileName, string strFileFormat,
                                                 string strPrinterName, string strTimeStamp,
                                                 string strURL, string browser, string userAgent, string saasAppName, TokenInfo tokenInfo)
        {
            EventHubPayload<SaaSFilePrintingPayload> payload1 = new EventHubPayload<SaaSFilePrintingPayload>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.SaaSFilePrint;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                payload1.AccountUserInfo = new AccountInfo { UserName = tokenInfo.strUserName };

                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }
            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            payload1.Payload = new SaaSFilePrintingPayload
            {
                HostName = Dns.GetHostName(),
                PlatformExtraInfo = " ",
                LoggedInOS = Environment.OSVersion.VersionString,
                TimeStamp = strTimeStamp,
                Browser = browser,
                UserAgent = userAgent,
                SaasAppName = saasAppName,
                URL = strURL,
                TimeZone = getTimeZoneInfo(),
                PrinterName = strPrinterName,
                JobDetails = new PrintJobDetails { Size = dwFileSize, FileName = strFileName, Format = strFileFormat }
            };

            string postData = JsonConvert.SerializeObject(payload1, getJasonSettings());
            return postData;
        }

        public string getSaaSClipboardPayload(string strResult, string strFormatType, double dwFormatsize, string strInitiator,
                                                         string strOperation, string strTimeStamp,
                                                         string strURL, string browser, string userAgent, string saasAppName, TokenInfo tokenInfo)
        {
            EventHubPayload<SaaSClipboardPayload> payload1 = new EventHubPayload<SaaSClipboardPayload>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.SaaSClipboard;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                payload1.AccountUserInfo = new AccountInfo { UserName = tokenInfo.strUserName };

                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }
            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            payload1.Payload = new SaaSClipboardPayload
            {
                HostName = Dns.GetHostName(),
                PlatformExtraInfo = " ",
                LoggedInOS = Environment.OSVersion.VersionString,
                TimeStamp = strTimeStamp,
                Browser = browser,
                UserAgent = userAgent,
                SaasAppName = saasAppName,
                URL = strURL,
                Operation = strOperation,
                TimeZone = getTimeZoneInfo(),
                JobDetails = new ClipboardJobDetails { Result = strResult, Formattype = strFormatType, Formatsize = dwFormatsize, Initiator = strInitiator }
            };

            string postData = JsonConvert.SerializeObject(payload1, getJasonSettings());
            return postData;
        }

        public string getSessionLogonPayload(string strAppName, string strType, string strServerName, string strSessionUserName, string strDomain, string strSessionGUID, TokenInfo tokenInfo)
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;
            string strUserName = strDomain + "\\" + strSessionUserName;

            EventHubPayload<SessionLogonPayload> payload1 = new EventHubPayload<SessionLogonPayload>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.SessionLogon;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                if (tokenInfo.strUserName != "")
                {
                    strUserName = tokenInfo.strUserName;
                }

                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }

            payload1.AccountUserInfo = new AccountInfo { UserName = strUserName };

            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            //############################### Start - Fetch Public IP Address ############################### //
            // We make a REST call to an end point hosted by CAS-Platform to get the Public IP Address
            // We make a best effort to fetch the Public IP Address (Wait time for API to return is Max 3 seconds!)
            // Regsitry entry for feature toggle - SendPublicIPAddress (boolean)
            PublicIPAddressPayload publicIpAddressPayload = GetPublicIpAddress();
            payload1.PublicIPv4 = publicIpAddressPayload.PublicIPv4;
            payload1.PublicIPv6 = publicIpAddressPayload.PublicIPv6;
            //############################### End - Fetch Public IP Address ############################### //

            payload1.Payload = new SessionLogonPayload
            {
                LoggedInOS = Environment.OSVersion.VersionString,
                ServerName = strServerName,
                UserName = strSessionUserName,
                Domain = strDomain,
                SessionGUID = strSessionGUID,
                HostName = Dns.GetHostName(),
                TimeStamp = secondsSinceEpoch.ToString(),
                TimeZone = getTimeZoneInfo()
            };

            string postData = JsonConvert.SerializeObject(payload1, getJasonSettings());
            return postData;
        }

        public string getSessionEndPayload(string strAppName, string strType, string strServerName, string strSessionUserName, string strDomain, string strSessionGUID, TokenInfo tokenInfo)
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;
            string strUserName = strDomain + "\\" + strSessionUserName;

            EventHubPayload<SessionEndPayload> payload1 = new EventHubPayload<SessionEndPayload>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.SessionEnd;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                if (tokenInfo.strUserName != "")
                {
                    strUserName = tokenInfo.strUserName;
                }

                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }

            payload1.AccountUserInfo = new AccountInfo { UserName = strUserName };

            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            payload1.Payload = new SessionEndPayload
            {
                LoggedInOS = Environment.OSVersion.VersionString,
                ServerName = strServerName,
                UserName = strSessionUserName,
                Domain = strDomain,
                SessionGUID = strSessionGUID,
                HostName = Dns.GetHostName(),
                TimeStamp = secondsSinceEpoch.ToString(),
                TimeZone = getTimeZoneInfo()
            };

            string postData = JsonConvert.SerializeObject(payload1, getJasonSettings());
            return postData;
        }

        public string getAppStartPayload(string strAppName, string strType, string strServerName, string strSessionUserName, string strDomain, string strApplicationName, string strModuleFilePath, string strSessionGUID, TokenInfo tokenInfo)
        {
            string postData = "";
            string strUserName = strDomain + "\\" + strSessionUserName;

            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;

            EventHubPayload<AppStartPayload> payload1 = new EventHubPayload<AppStartPayload>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.AppStart;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                if (tokenInfo.strUserName != "")
                {
                    strUserName = tokenInfo.strUserName;
                }

                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }

            payload1.AccountUserInfo = new AccountInfo { UserName = strUserName };

            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            payload1.Payload = new AppStartPayload
            {
                LoggedInOS = Environment.OSVersion.VersionString,
                ServerName = strServerName,
                UserName = strSessionUserName,
                Domain = strDomain,
                SessionGUID = strSessionGUID,
                AppName = strApplicationName,
                ModuleFilePath = strModuleFilePath,
                HostName = Dns.GetHostName(),
                TimeStamp = secondsSinceEpoch.ToString(),
                TimeZone = getTimeZoneInfo()
            };

            postData = JsonConvert.SerializeObject(payload1, getJasonSettings());

            return postData;
        }

        public string getAppEndPayload(string strAppName, string strType, string strServerName, string strSessionUserName, string strDomain, string strApplicationName, string strModuleFilePath, string strSessionGUID, TokenInfo tokenInfo)
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;
            string strUserName = strDomain + "\\" + strSessionUserName;

            EventHubPayload<AppEndPayload> payload1 = new EventHubPayload<AppEndPayload>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.AppEnd;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                if (tokenInfo.strUserName != "")
                {
                    strUserName = tokenInfo.strUserName;
                }

                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }

            payload1.AccountUserInfo = new AccountInfo { UserName = strUserName };

            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            payload1.Payload = new AppEndPayload
            {
                LoggedInOS = Environment.OSVersion.VersionString,
                ServerName = strServerName,
                UserName = strSessionUserName,
                Domain = strDomain,
                SessionGUID = strSessionGUID,
                AppName = strApplicationName,
                ModuleFilePath = strModuleFilePath,
                HostName = Dns.GetHostName(),
                TimeStamp = secondsSinceEpoch.ToString(),
                TimeZone = getTimeZoneInfo()
            };

            string postData = JsonConvert.SerializeObject(payload1, getJasonSettings());
            return postData;
        }

        public string getFileDownloadPayload(string strFileName, string strFilePath, string strServerName, string strSessionUserName, string strDomain, string strDeviceType, double dwFileSize, string strSessionGUID, TokenInfo tokenInfo)
        {
            TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1);
            int secondsSinceEpoch = (int)t.TotalSeconds;
            string strUserName = strDomain + "\\" + strSessionUserName;

            EventHubPayload<FileDownloadPayload> payload1 = new EventHubPayload<FileDownloadPayload>();
            payload1.EventVersion = "1";
            payload1.EventUID = Guid.NewGuid().ToString();
            payload1.EventType = EventType.FileDownload;
            payload1.EventProduct = event_product;
            payload1.EventProductVersion = getReceiverVersion();
            payload1.EventDvc = Dns.GetHostName();

            if (null != tokenInfo)
            {
                if (tokenInfo.strUserName != "")
                {
                    strUserName = tokenInfo.strUserName;
                }

                if (tokenInfo.strCustomerId != null)
                {
                    payload1.tenantId = new tenant { uuid = tokenInfo.strCustomerId };
                }

                if (tokenInfo.strDsAuth != null)
                {
                    payload1.AuthToken = new token { DSAuth = tokenInfo.strDsAuth };

                }
            }
            payload1.AccountUserInfo = new AccountInfo { UserName = strUserName };

            payload1.SystemIP = getDeviceIP();
            payload1.EventSentTime = DateTime.UtcNow.ToString("o");

            payload1.Payload = new FileDownloadPayload
            {
                LoggedInOS = Environment.OSVersion.VersionString,
                ServerName = strServerName,
                UserName = strSessionUserName,
                Domain = strDomain,
                SessionGUID = strSessionGUID,
                DeviceType = strDeviceType,
                FileName = strFileName,
                FilePath = strFilePath,
                FileSize = dwFileSize,
                HostName = Dns.GetHostName(),
                TimeStamp = secondsSinceEpoch.ToString(),
                TimeZone = getTimeZoneInfo()
            };

            string postData = JsonConvert.SerializeObject(payload1, getJasonSettings());
            return postData;
        }



        public HttpWebResponse SendEventRequestToEventHub(string strUrl, string strToken, string strPayloadData)
        {
            Tracer.DServices.Error("CAS: SendEventRequestToEventHub. Enter.");

            Uri tempURL = new Uri(strUrl);
            HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(tempURL);
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "POST";
            httpWebRequest.Headers.Add(HttpRequestHeader.Authorization, strToken);

            System.IO.Stream requestStream = httpWebRequest.GetRequestStream();

            // now send it
            requestStream.Write(Encoding.UTF8.GetBytes(strPayloadData), 0, strPayloadData.Length);
            requestStream.Close();

            HttpWebResponse response = (HttpWebResponse)httpWebRequest.GetResponse();
            return response;
        }

        public void SendLoginInfoToEventHub(string strStoreConfigUrl, string strStoreServiceRecordID)
        {
            string strPayload = "";
            TokenInfo tokenInfo = null;
            try
            {

                tokenInfo = getToken(strStoreConfigUrl, strStoreServiceRecordID);

                if (null != tokenInfo)
                {

                    strPayload = getAccountLoginPayload(tokenInfo);

                    HttpWebResponse response = SendEventRequestToEventHub(tokenInfo.strEventHubEndpoint, tokenInfo.strEventHubToken, strPayload);

                    if (HttpStatusCode.Unauthorized == response.StatusCode)
                    {
                        Tracer.DServices.Error("CAS: InvalidToken. refresh the token and resend the event.");
                        RefreshTokenAndResendPayload(strPayload, strStoreConfigUrl, strStoreServiceRecordID, tokenInfo);
                    }
                    else if (HttpStatusCode.Created == response.StatusCode ||
                              HttpStatusCode.OK == response.StatusCode ||
                              HttpStatusCode.Accepted == response.StatusCode)
                    {
                        Tracer.DServices.Trace("CAS: Account login data got uploaded for store {0}", strStoreConfigUrl);
                    }
                    else
                    {
                        Tracer.DServices.Error("CAS: Error while sending account logon event. Received response {0}", response.StatusCode.ToString());
                    }
                    response.Close();

                    GC.Collect();
                    GC.WaitForPendingFinalizers();
                }

            }
            catch (Exception ex)
            {
                Tracer.DServices.Error("Exception while sending the event to eventhub");
                RefreshTokenAndResendPayload(strPayload, strStoreConfigUrl, strStoreServiceRecordID, tokenInfo);
            }
        }

        public void parseICAFileAnalyticsData(string strEventType, string icafileAnalyticsData)
        {
            ThreadPool.QueueUserWorkItem(_ => parseICAFileAnalyticsDataImpl(strEventType, icafileAnalyticsData));
        }

        public bool parseICAFileAnalyticsDataImpl(string strEventType, string icafileAnalyticsData)
        {
            TokenInfo tokenInfo = null;
            string strStoreUrl = "", strServiceRecordId = "";
            string strPayload = "";
            string[] strEventData = { "" };
            try
            {

                Tracer.DServices.Trace("CAS: Processng event of type {0}", strEventType);

                if (strEventType.Contains("SaaS"))
                {
                    strEventData = icafileAnalyticsData.Split('~');
                }
                else
                {
                    strEventData = icafileAnalyticsData.Split(',');
                }

                for (int i = 0; i < strEventData.Length; i++)
                {
                    string temp = strEventData[i];

                    if (temp.IndexOf("StoreUrl", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string[] temp1 = temp.Split('=');
                        strStoreUrl = temp1[1];
                    }

                    if (temp.IndexOf("StoreServiceRecordId", StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        string[] temp1 = temp.Split('=');
                        strServiceRecordId = temp1[1].ToLower().Trim();
                    }
                }

                lock (thisLock)
                {

                    tokenInfo = getToken(strStoreUrl, strServiceRecordId);
                }

                if (tokenInfo == null)
                    return false;

                if (strEventType.Equals(EventType.SessionLaunch, StringComparison.OrdinalIgnoreCase))
                {
                    Tracer.DServices.Trace("CAS: Session Launch event received.");

                    string strAppName = "";
                    string strAppType = "";

                    for (int i = 0; i < strEventData.Length; i++)
                    {
                        string temp = strEventData[i];

                        if (temp.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strAppName = temp1[1];
                        }

                        if (temp.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strAppType = temp1[1].ToLower();
                        }
                    }

                    Tracer.DServices.Trace("EventType = '{0}' ApplicationName = '{1}' ApplicationType = {2}", strEventType, strAppName, strAppType);

                    strPayload = getSessionLaunchPayload(strAppName, strAppType, tokenInfo);
                }
                else if (strEventType.Equals(EventType.SessionLogon, StringComparison.OrdinalIgnoreCase))
                {
                    Tracer.DServices.Trace("CAS: Session Logon event received.");

                    string strAppName = "";
                    string strAppType = "";
                    string strServerName = "";
                    string strUserName = "";
                    string strDomain = "";
                    string strSessionGUID = "";

                    for (int i = 0; i < strEventData.Length; i++)
                    {
                        string temp = strEventData[i];

                        if (temp.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strAppName = temp1[1];
                        }

                        if (temp.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strAppType = temp1[1].ToLower();
                        }

                        if (temp.IndexOf("Session-ServerName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strServerName = temp1[1];
                        }

                        if (temp.IndexOf("Session-UserName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strUserName = temp1[1];
                        }

                        if (temp.IndexOf("Session-Domain", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strDomain = temp1[1];
                        }

                        if (temp.IndexOf("sessionGuid", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strSessionGUID = temp1[1];
                        }
                    }

                    Tracer.DServices.Trace("EventType = '{0}' ApplicationName = '{1}' ApplicationType = {2} ServerName = '{3}' UserName = '{4}' Domain = '{5}'", strEventType, strAppName, strAppType, strServerName, strUserName, strDomain);

                    strPayload = getSessionLogonPayload(strAppName, strAppType, strServerName, strUserName, strDomain, strSessionGUID, tokenInfo);
                }
                else if (strEventType.Equals(EventType.SessionEnd, StringComparison.OrdinalIgnoreCase))
                {
                    Tracer.DServices.Trace("CAS: Session End event received");
                    string strAppName = "";
                    string strAppType = "";
                    string strServerName = "";
                    string strUserName = "";
                    string strDomain = "";
                    string strSessionGUID = "";

                    for (int i = 0; i < strEventData.Length; i++)
                    {
                        string temp = strEventData[i];

                        if (temp.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strAppName = temp1[1];
                        }

                        if (temp.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strAppType = temp1[1];
                        }

                        if (temp.IndexOf("Session-ServerName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strServerName = temp1[1];
                        }

                        if (temp.IndexOf("Session-UserName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strUserName = temp1[1];
                        }

                        if (temp.IndexOf("Session-Domain", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strDomain = temp1[1];
                        }

                        if (temp.IndexOf("sessionGuid", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strSessionGUID = temp1[1];
                        }
                    }

                    Tracer.DServices.Trace("EventType = '{0}' ApplicationName = '{1}' ApplicationType = {2} ServerName = '{3}' UserName = '{4}' Domain = '{5}'", strEventType, strAppName, strAppType, strServerName, strUserName, strDomain);
                    strPayload = getSessionEndPayload(strAppName, strAppType, strServerName, strUserName, strDomain, strSessionGUID, tokenInfo);
                }

                else if (strEventType.Equals(EventType.AppStart, StringComparison.OrdinalIgnoreCase))
                {
                    Tracer.DServices.Trace("CAS: App start event received");
                    string strAppName = "";
                    string strAppType = "";
                    string strServerName = "";
                    string strUserName = "";
                    string strDomain = "";
                    string strappName = "";
                    string strModuleFilePath = "";
                    string strSessionGUID = "";

                    for (int i = 0; i < strEventData.Length; i++)
                    {
                        string temp = strEventData[i];

                        if (temp.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strAppName = temp1[1];
                        }

                        if (temp.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strAppType = temp1[1];
                        }

                        if (temp.IndexOf("Session-ServerName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strServerName = temp1[1];
                        }

                        if (temp.IndexOf("Session-UserName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strUserName = temp1[1];
                        }

                        if (temp.IndexOf("Session-Domain", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strDomain = temp1[1];
                        }

                        if (temp.IndexOf("appName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strappName = temp1[1];
                        }

                        if (temp.IndexOf("moduleFilePath", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strModuleFilePath = temp1[1];
                        }
                        if (temp.IndexOf("sessionGuid", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strSessionGUID = temp1[1];
                        }
                    }

                    //Tracer.DServices.Trace("EventType = '{0}' ApplicationName = '{1}' ApplicationType = {2} ServerName = '{3}' UserName = '{4}' Domain = '{5}' AppName = '{6}' ModuleFilePath = '{7}'", strEventType, strAppName, strAppType, strServerName, strUserName, strDomain, strappName, strModuleFilePath);
                    strPayload = getAppStartPayload(strAppName, strAppType, strServerName, strUserName, strDomain, strappName, strModuleFilePath, strSessionGUID, tokenInfo);
                }

                else if (strEventType.Equals(EventType.AppEnd, StringComparison.OrdinalIgnoreCase))
                {
                    Tracer.DServices.Trace("CAS: App end event received");
                    string strAppName = "";
                    string strAppType = "";
                    string strServerName = "";
                    string strUserName = "";
                    string strDomain = "";
                    string strappName = "";
                    string strModuleFilePath = "";
                    string strSessionGUID = "";

                    for (int i = 0; i < strEventData.Length; i++)
                    {
                        string temp = strEventData[i];

                        if (temp.IndexOf("Title", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strAppName = temp1[1];
                        }

                        if (temp.IndexOf("Type", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strAppType = temp1[1];
                        }

                        if (temp.IndexOf("Session-ServerName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strServerName = temp1[1];
                        }

                        if (temp.IndexOf("Session-UserName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strUserName = temp1[1];
                        }

                        if (temp.IndexOf("Session-Domain", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strDomain = temp1[1];
                        }

                        if (temp.IndexOf("appName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strappName = temp1[1];
                        }

                        if (temp.IndexOf("moduleFilePath", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strModuleFilePath = temp1[1];
                        }

                        if (temp.IndexOf("sessionGuid", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strSessionGUID = temp1[1];
                        }
                    }

                    //Tracer.DServices.Trace("EventType = '{0}' ApplicationName = '{1}' ApplicationType = {2} ServerName = '{3}' UserName = '{4}' Domain = '{5}' AppName = '{6}' ModuleFilePath = '{7}'", strEventType, strAppName, strAppType, strServerName, strUserName, strDomain, strappName, strModuleFilePath);
                    strPayload = getAppEndPayload(strAppName, strAppType, strServerName, strUserName, strDomain, strappName, strModuleFilePath, strSessionGUID, tokenInfo);
                }

                else if (strEventType.Equals(EventType.FileDownload, StringComparison.OrdinalIgnoreCase))
                {
                    Tracer.DServices.Trace("CAS: File download event received");
                    string strFileName = "";
                    string strFilePath = "";
                    string strServerName = "";
                    string strUserName = "";
                    string strDomain = "";
                    string strDeviceType = "";
                    string strSessionGUID = "";
                    double dwFileSize = 0.0;

                    for (int i = 0; i < strEventData.Length; i++)
                    {
                        string temp = strEventData[i];

                        if (temp.IndexOf("fileName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strFileName = temp1[1];
                        }

                        if (temp.IndexOf("filePath", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strFilePath = temp1[1];
                        }

                        if (temp.IndexOf("Session-ServerName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strServerName = temp1[1];
                        }

                        if (temp.IndexOf("Session-UserName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strUserName = temp1[1];
                        }

                        if (temp.IndexOf("Session-Domain", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strDomain = temp1[1];
                        }

                        if (temp.IndexOf("deviceType", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strDeviceType = temp1[1];
                        }
                        if (temp.IndexOf("sessionGuid", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strSessionGUID = temp1[1];
                        }

                        if (temp.IndexOf("fileSize", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            dwFileSize = (double)(Convert.ToDouble(temp1[1]) / 1024);
                        }
                    }

                    //Tracer.DServices.Trace("EventType = '{0}' ApplicationName = '{1}' ApplicationType = {2} ServerName = '{3}' UserName = '{4}' Domain = '{5}' AppName = '{6}' ModuleFilePath = '{7}'", strEventType, strAppName, strAppType, strServerName, strUserName, strDomain, strappName, strModuleFilePath);
                    strPayload = getFileDownloadPayload(strFileName, strFilePath, strServerName, strUserName, strDomain, strDeviceType, dwFileSize, strSessionGUID, tokenInfo);
                }

                else if (strEventType.Equals(EventType.SaasLaunch, StringComparison.OrdinalIgnoreCase))
                {
                    Tracer.DServices.Trace("CAS: Secure App launch event received");
                    string strTimeStamp = "";           //mandatory
                    string strURL = "";                 //mandatory
                    string strbrowser = "";
                    string strUserAgent = "";
                    string strSaasAppName = "";
                    for (int i = 0; i < strEventData.Length; i++)
                    {
                        string temp = strEventData[i];
                        if (temp.IndexOf("timestamp", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: Secure App launch event , extracting timestamp");
                            string[] temp1 = temp.Split('=');
                            strTimeStamp = temp1[1];
                        }

                        if (temp.IndexOf("url", StringComparison.Ordinal) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: Secure App launch event , extracting url");
                            string[] temp1 = temp.Split('=');
                            strURL = temp1[1];
                        }

                        if (temp.IndexOf("browser", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: Secure App launch event , extracting browser");
                            string[] temp1 = temp.Split('=');
                            strbrowser = temp1[1];
                        }

                        if (temp.IndexOf("useragent", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: Secure App launch event , extracting useragent");
                            string[] temp1 = temp.Split('=');
                            strUserAgent = temp1[1];
                        }

                        if (temp.IndexOf("saasappname", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: Secure App launch event , extracting saasappname");
                            string[] temp1 = temp.Split('=');
                            strSaasAppName = temp1[1];
                        }
                    }

                    Tracer.DServices.Trace("EventType = '{0}' TimeStamp = '{1}' URL = '{2}' Browser = '{3}' UserAgent = '{4}' SaaSAppName = '{5}'", strEventType, strTimeStamp, strURL, strbrowser, strUserAgent, strSaasAppName);
                    if (strTimeStamp == "" || strURL == "")
                    {
                        Tracer.DServices.Trace("Mandatory fields is/are not present, not sending data to CAS server.");
                        return false;
                    }
                    else
                    {
                        strPayload = getSaaSLaunchPayload(strTimeStamp, strURL, strbrowser, strUserAgent, strSaasAppName, tokenInfo);
                    }
                }
                else if (strEventType.Equals(EventType.SaasEnd, StringComparison.OrdinalIgnoreCase))
                {
                    Tracer.DServices.Trace("CAS: Secure App End event received");
                    string strTimeStamp = "";                          //mandatory
                    string strURL = "";                                //mandatory
                    string strbrowser = "";
                    string strUserAgent = "";
                    string strSaasAppName = "";
                    for (int i = 0; i < strEventData.Length; i++)
                    {
                        string temp = strEventData[i];
                        if (temp.IndexOf("timestamp", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strTimeStamp = temp1[1];
                        }

                        if (temp.IndexOf("url", StringComparison.Ordinal) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strURL = temp1[1];
                        }

                        if (temp.IndexOf("browser", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strbrowser = temp1[1];
                        }

                        if (temp.IndexOf("useragent", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strUserAgent = temp1[1];
                        }

                        if (temp.IndexOf("saasappname", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            string[] temp1 = temp.Split('=');
                            strSaasAppName = temp1[1];
                        }
                    }

                    Tracer.DServices.Trace("EventType = '{0}' TimeStamp = '{1}' URL = '{2}' Browser = '{3}' UserAgent = '{4}' SaaSAppName = '{5}'", strEventType, strTimeStamp, strURL, strbrowser, strUserAgent, strSaasAppName);
                    if (strTimeStamp == "" || strURL == "")
                    {
                        Tracer.DServices.Trace("Mandatory fields is/are not present, not sending data to CAS server.");
                        return false;
                    }
                    else
                    {
                        strPayload = getSaaSEndPayload(strTimeStamp, strURL, strbrowser, strUserAgent, strSaasAppName, tokenInfo);
                    }

                }
                else if (strEventType.Equals(EventType.SaasUrlNavigate, StringComparison.OrdinalIgnoreCase))
                {
                    Tracer.DServices.Trace("CAS: Secure App Url Navigate event received");
                    string strTimeStamp = "";                            //mandatory
                    string strURL = "";                                  //mandatory
                    string strbrowser = "";
                    string strUserAgent = "";
                    string strSaasAppName = "";

                    for (int i = 0; i < strEventData.Length; i++)
                    {
                        string temp = strEventData[i];

                        if (temp.IndexOf("timestamp", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: Secure App Url Navigate event , extracting timestamp");
                            string[] temp1 = temp.Split('=');
                            strTimeStamp = temp1[1];
                        }

                        if (temp.IndexOf("url", StringComparison.Ordinal) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: Secure App Url Navigate event , extracting url");
                            string[] temp1 = temp.Split('=');
                            strURL = temp1[1];
                        }

                        if (temp.IndexOf("browser", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: Secure App Url Navigate event , extracting browser");
                            string[] temp1 = temp.Split('=');
                            strbrowser = temp1[1];
                        }

                        if (temp.IndexOf("useragent", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: Secure App Url Navigate event , extracting useragent");
                            string[] temp1 = temp.Split('=');
                            strUserAgent = temp1[1];
                        }

                        if (temp.IndexOf("saasappname", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: Secure App Url Navigate event , extracting saasappname");
                            string[] temp1 = temp.Split('=');
                            strSaasAppName = temp1[1];
                        }
                    }
                    Tracer.DServices.Trace("EventType = '{0}' TimeStamp = '{1}' URL = '{2}' Browser = '{3}' UserAgent = '{4}' SaaSAppName = '{5}'", strEventType, strTimeStamp, strURL, strbrowser, strUserAgent, strSaasAppName);
                    if (strTimeStamp == "" || strURL == "")
                    {
                        Tracer.DServices.Trace("Mandatory fields is/are not present, not sending data to CAS server.");
                        return false;
                    }
                    else
                    {
                        strPayload = getSaaSUrlNavigatePayload(strTimeStamp, strURL, strbrowser, strUserAgent, strSaasAppName, tokenInfo);
                    }




                }
                else if (strEventType.Equals(EventType.SaaSFileDownload, StringComparison.OrdinalIgnoreCase))
                {
                    Tracer.DServices.Trace("CAS: SaaS File download event received");
                    string strTimeStamp = "";
                    string strURL = "";
                    string strbrowser = "";
                    string strUserAgent = "";
                    string strSaasAppName = "";
                    string strFileName = "";
                    string strFilePath = "";
                    string strDeviceType = "";
                    string strFileFormat = "";
                    double dwFileSize = 0.0;

                    for (int i = 0; i < strEventData.Length; i++)
                    {
                        string temp = strEventData[i];
                        if (temp.IndexOf("timestamp", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFileDownload event , extracting timestamp");
                            string[] temp1 = temp.Split('=');
                            strTimeStamp = temp1[1];
                        }

                        if (temp.IndexOf("url", StringComparison.Ordinal) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFileDownload event , extracting url");
                            string[] temp1 = temp.Split('=');
                            strURL = temp1[1];
                        }

                        if (temp.IndexOf("browser", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFileDownload event , extracting browser");
                            string[] temp1 = temp.Split('=');
                            strbrowser = temp1[1];
                        }

                        if (temp.IndexOf("useragent", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFileDownload event , extracting useragent");
                            string[] temp1 = temp.Split('=');
                            strUserAgent = temp1[1];
                        }

                        if (temp.IndexOf("saasappname", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFileDownload event , extracting saasappname");
                            string[] temp1 = temp.Split('=');
                            strSaasAppName = temp1[1];
                        }
                        if (temp.IndexOf("size", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFileDownload event , extracting size");
                            string[] temp1 = temp.Split('=');
                            dwFileSize = string.IsNullOrEmpty(temp1[1].Trim()) ? 0 : (double)(Convert.ToDouble(temp1[1]) / 1024);
                        }

                        if (temp.IndexOf("fileName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFileDownload event , extracting fileName");
                            string[] temp1 = temp.Split('=');
                            strFileName = temp1[1];
                        }

                        if (temp.IndexOf("format", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFileDownload event , extracting format");
                            string[] temp1 = temp.Split('=');
                            strFileFormat = temp1[1];
                        }

                        if (temp.IndexOf("deviceType", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFileDownload event , extracting deviceType");
                            string[] temp1 = temp.Split('=');
                            strDeviceType = temp1[1];
                        }

                        if (temp.IndexOf("filePath", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFileDownload event , extracting filePath");
                            string[] temp1 = temp.Split('=');
                            strFilePath = temp1[1];
                        }

                    }
                    Tracer.DServices.Trace("EventType = '{0}' TimeStamp = '{1}' URL = '{2}' Browser = '{3}' UserAgent = '{4}' SaaSAppName = '{5}' Size = '{6}' fileName = '{7}' format = '{8}' deviceType = '{9}' filePath = {10}", strEventType, strTimeStamp, strURL, strbrowser, strUserAgent, strSaasAppName, dwFileSize, strFileName, strFileFormat, strDeviceType, strFilePath);
                    if (strTimeStamp == "" || strURL == "" || strFileName == "" || strFileFormat == "" || strDeviceType == "" || dwFileSize == 0)
                    {
                        Tracer.DServices.Trace("Mandatory fields is/are not present, not sending data to CAS server.");
                        return false;
                    }
                    else
                    {
                        strPayload = getSaaSFileDownloadPayload(dwFileSize, strFileName, strFileFormat, strDeviceType, strFilePath, strTimeStamp, strURL, strbrowser, strUserAgent, strSaasAppName, tokenInfo);
                    }
                }
                else if (strEventType.Equals(EventType.SaaSFilePrint, StringComparison.OrdinalIgnoreCase))
                {
                    Tracer.DServices.Trace("CAS: SaaS File print event received");
                    string strTimeStamp = "";
                    string strURL = "";
                    string strbrowser = "";
                    string strUserAgent = "";
                    string strSaasAppName = "";
                    string strFileName = "";
                    string strPrinterName = "";
                    string strFileFormat = "";
                    double dwFileSize = 0.0;

                    for (int i = 0; i < strEventData.Length; i++)
                    {
                        string temp = strEventData[i];
                        if (temp.IndexOf("timestamp", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFilePrint event , extracting timestamp");
                            string[] temp1 = temp.Split('=');
                            strTimeStamp = temp1[1];
                        }

                        if (temp.IndexOf("url", StringComparison.Ordinal) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFilePrint event , extracting url");
                            string[] temp1 = temp.Split('=');
                            strURL = temp1[1];
                        }

                        if (temp.IndexOf("browser", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFilePrint event , extracting browser");
                            string[] temp1 = temp.Split('=');
                            strbrowser = temp1[1];
                        }

                        if (temp.IndexOf("useragent", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFilePrint event , extracting useragent");
                            string[] temp1 = temp.Split('=');
                            strUserAgent = temp1[1];
                        }

                        if (temp.IndexOf("saasappname", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFilePrint event , extracting saasappname");
                            string[] temp1 = temp.Split('=');
                            strSaasAppName = temp1[1];
                        }
                        if (temp.IndexOf("size", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFilePrint event , extracting size");
                            string[] temp1 = temp.Split('=');
                            dwFileSize = string.IsNullOrEmpty(temp1[1].Trim()) ? 0 : (double)(Convert.ToDouble(temp1[1]) / 1024);
                        }

                        if (temp.IndexOf("filename", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFilePrint event , extracting filename");
                            string[] temp1 = temp.Split('=');
                            strFileName = temp1[1];
                        }

                        if (temp.IndexOf("format", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFilePrint event , extracting format");
                            string[] temp1 = temp.Split('=');
                            strFileFormat = temp1[1];
                        }

                        if (temp.IndexOf("printerName", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSFilePrint event , extracting printerName");
                            string[] temp1 = temp.Split('=');
                            strPrinterName = temp1[1];
                        }

                    }
                    Tracer.DServices.Trace("EventType = '{0}' TimeStamp = '{1}' URL = '{2}' Browser = '{3}' UserAgent = '{4}' SaaSAppName = '{5}' Size = '{6}' fileName = '{7}' format = '{8}' printerName = '{9}'", strEventType, strTimeStamp, strURL, strbrowser, strUserAgent, strSaasAppName, dwFileSize, strFileName, strFileFormat, strPrinterName);
                    if (strTimeStamp == "" || strURL == "")
                    {
                        Tracer.DServices.Trace("Mandatory fields is/are not present, not sending data to CAS server.");
                        return false;
                    }
                    else
                    {
                        strPayload = getSaaSFilePrintingPayload(dwFileSize, strFileName, strFileFormat, strPrinterName, strTimeStamp, strURL, strbrowser, strUserAgent, strSaasAppName, tokenInfo);
                    }
                }
                else if (strEventType.Equals(EventType.SaaSClipboard, StringComparison.OrdinalIgnoreCase))
                {
                    Tracer.DServices.Trace("CAS: SaaS File Clipboard event received");
                    string strTimeStamp = "";
                    string strURL = "";
                    string strbrowser = "";
                    string strUserAgent = "";
                    string strSaasAppName = "";
                    string strResult = "";
                    string strFormatType = "";
                    double dwFormatsize = 0.0;
                    string strInitiator = "";
                    string strOperation = "";

                    for (int i = 0; i < strEventData.Length; i++)
                    {
                        string temp = strEventData[i];
                        if (temp.IndexOf("timestamp", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSClipboard event , extracting timestamp");
                            string[] temp1 = temp.Split('=');
                            strTimeStamp = temp1[1];
                        }

                        if (temp.IndexOf("url", StringComparison.Ordinal) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSClipboard event , extracting url");
                            string[] temp1 = temp.Split('=');
                            strURL = temp1[1];
                        }

                        if (temp.IndexOf("browser", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSClipboard event , extracting browser");
                            string[] temp1 = temp.Split('=');
                            strbrowser = temp1[1];
                        }

                        if (temp.IndexOf("useragent", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSClipboard event , extracting useragent");
                            string[] temp1 = temp.Split('=');
                            strUserAgent = temp1[1];
                        }

                        if (temp.IndexOf("saasappname", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSClipboard event , extracting saasappname");
                            string[] temp1 = temp.Split('=');
                            strSaasAppName = temp1[1];
                        }
                        if (temp.IndexOf("operation", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSClipboard event , extracting operation");
                            string[] temp1 = temp.Split('=');
                            strOperation = temp1[1];
                        }

                        if (temp.IndexOf("result", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSClipboard event , extracting result");
                            string[] temp1 = temp.Split('=');
                            strResult = temp1[1];
                        }

                        if (temp.IndexOf("formattype", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSClipboard event , extracting formattype");
                            string[] temp1 = temp.Split('=');
                            strFormatType = temp1[1];
                        }

                        if (temp.IndexOf("formatsize", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSClipboard event , extracting formatsize");
                            string[] temp1 = temp.Split('=');
                            dwFormatsize = string.IsNullOrEmpty(temp1[1].Trim()) ? 0 : (double)(Convert.ToDouble(temp1[1]));
                        }

                        if (temp.IndexOf("initiator", StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            Tracer.DServices.Trace("CAS: SaaSClipboard event , extracting initiator");
                            string[] temp1 = temp.Split('=');
                            strInitiator = temp1[1];
                        }
                    }
                    Tracer.DServices.Trace("EventType = '{0}' TimeStamp = '{1}' URL = '{2}' Browser = '{3}' UserAgent = '{4}' SaaSAppName = '{5}' Operation = '{6}' Result = '{7}' formattype = '{8}' formatsize = '{9}' Initiator = '{10}'", strEventType, strTimeStamp, strURL, strbrowser, strUserAgent, strSaasAppName, strOperation, strResult, strFormatType, dwFormatsize, strInitiator);
                    if (strTimeStamp == "" || strURL == "" || strOperation == "")
                    {
                        Tracer.DServices.Trace("Mandatory fields is/are not present, not sending data to CAS server.");
                        return false;
                    }
                    else
                    {
                        strPayload = getSaaSClipboardPayload(strResult, strFormatType, dwFormatsize, strInitiator, strOperation, strTimeStamp, strURL, strbrowser, strUserAgent, strSaasAppName, tokenInfo);
                    }
                }
                if (strPayload != "")
                {
                    try
                    {
                        HttpWebResponse response = SendEventRequestToEventHub(tokenInfo.strEventHubEndpoint, tokenInfo.strEventHubToken, strPayload);

                        if (HttpStatusCode.Unauthorized == response.StatusCode)
                        {
                            Tracer.DServices.Error("CAS: InvalidToken. trying again after refreshing the token.");
                            RefreshTokenAndResendPayload(strPayload, strStoreUrl, strServiceRecordId, tokenInfo);

                        }
                        else if (HttpStatusCode.Created == response.StatusCode ||
                                  HttpStatusCode.OK == response.StatusCode ||
                                  HttpStatusCode.Accepted == response.StatusCode)

                        {
                            Tracer.DServices.Trace("CAS: Data got uploaded to event hub for store : {0} and EventType : {1}", strStoreUrl, strEventType);
                        }
                        else
                        {
                            Tracer.DServices.Error("CAS: Error while sending event of type : {0}. Response code : {1}", strEventType, response.StatusCode.ToString());
                        }

                        response.Close();
                    }
                    catch (Exception ex)
                    {
                        Tracer.DServices.Error("CAS: parseICAFileAnalyticsData exception while sending the payload");
                        RefreshTokenAndResendPayload(strPayload, strStoreUrl, strServiceRecordId, tokenInfo);
                    }
                }
                else
                {
                    Tracer.DServices.Error("CAS: Payload is empty");
                }

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
            catch (Exception ex)
            {
                Tracer.DServices.Error("CAS: Exception in parseICAFileAnalyticsData with message {0}", ex.Message);
                return false;
            }
            return true;
        }

        public void RefreshTokenAndResendPayload(string strPayload,
                                                  string strStoreUrl,
                                                  string strServiceRecordId,
                                                  TokenInfo oldTokenInfo)
        {
            TokenInfo Newtoken = null;

            HttpWebResponse response = null;

            try
            {

                lock (thisLock)
                {
                    Newtoken = getToken(strStoreUrl, strServiceRecordId, true);
                }

                if (null != Newtoken)
                {
                    if (oldTokenInfo.strCustomerId != null)
                        strPayload.Replace(oldTokenInfo.strCustomerId, Newtoken.strCustomerId);

                    if (oldTokenInfo.strDsAuth != null)
                        strPayload.Replace(oldTokenInfo.strDsAuth, Newtoken.strDsAuth);

                    response = SendEventRequestToEventHub(Newtoken.strEventHubEndpoint, Newtoken.strEventHubToken, strPayload);

                    if (HttpStatusCode.Created == response.StatusCode || HttpStatusCode.OK == response.StatusCode || HttpStatusCode.Accepted == response.StatusCode)
                    {
                        Tracer.DServices.Trace("CAS- RefreshTokenAndResendPayloadData. Event got uploaded to event hub");
                    }
                    else if (HttpStatusCode.Unauthorized == response.StatusCode)
                    {
                        Tracer.DServices.Error("CAS: InvalidToken. 2nd time fail with Invalid token exiting now.");
                    }
                    else
                    {
                        Tracer.DServices.Error("CAS: RefreshTokenAndResendPayloadData. Error while sending event. Response code : {1}", response.StatusCode.ToString());
                    }
                    response.Close();
                }
                else
                {
                    Tracer.DServices.Error("CAS: RefreshTokenAndResendPayloadData. Received new token as null.");
                }
            }
            catch (Exception ex)
            {
                Tracer.DServices.Error("CAS: Exception in RefreshTokenAndResendPayload with message {0}", ex.Message);
            }
        }

        /// <summary>
        /// Fetches Public IP Address
        /// </summary>
        /// <param></param>
        /// <returns>A payload containing public IP Address in IPv4 and IPv6 (if available) format</returns>
        public PublicIPAddressPayload GetPublicIpAddress()
        {
            PublicIPAddressPayload fetchedPublicIPAddress = new PublicIPAddressPayload()
            {
                PublicIPv4 = "",
                PublicIPv6 = ""
            };

            HttpResponseMessage result = null;

            try
            {

                using (var client = new HttpClient())
                {
                    // timeout set to 3 seconds
                    var sw = Stopwatch.StartNew();
                    client.Timeout = TimeSpan.FromSeconds(3);
                    result = client.GetAsync(PublicIPBaseAddress + PublicIPFetchUrlPath).GetAwaiter().GetResult();
                    sw.Stop();
                    Tracer.DServices.Trace("CAS: GetPublicIPAddress : Time taken to fetch Public IP Address: {0} and Status code {1}", sw.Elapsed.TotalMilliseconds, result.StatusCode);

                    if (result != null && result.IsSuccessStatusCode)
                    {
                        string jsonBody = result.Content.ReadAsStringAsync().GetAwaiter().GetResult();
                        var returnedPublicIPAddress = JsonConvert.DeserializeObject<PublicIPAddressModel>(jsonBody);
                        fetchedPublicIPAddress.PublicIPv4 = returnedPublicIPAddress.PublicIPv4;
                        fetchedPublicIPAddress.PublicIPv6 = returnedPublicIPAddress.PublicIPv6;
                        Tracer.DServices.Trace("CAS: GetPublicIPAddress : Fetched successfully");
                    }
                    else
                    {
                        Tracer.DServices.Trace("CAS: GetPublicIPAddress : Response is either null or has no success code. Response received : {0}", result);
                    }
                }

            }
            catch (Exception ex)
            {
                // Log if we are not able to fetch it
                Tracer.DServices.Error("CAS: GetPublicIPAddress : Exception : {0} : Response Body : {1}", ex.Message, result);
            }

            return fetchedPublicIPAddress;
        }

    }

    public class PublicIPAddressModel
    {
        [JsonProperty(PropertyName = "publicIPv4")]
        public string PublicIPv4 { get; set; }

        [JsonProperty(PropertyName = "publicIPv6")]
        public string PublicIPv6 { get; set; }
    }
}