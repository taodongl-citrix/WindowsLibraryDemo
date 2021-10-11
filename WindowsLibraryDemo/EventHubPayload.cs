using Newtonsoft.Json;
using System;

namespace WindowsLibraryDemo.Common.EventHub
{
    struct EventType
    {
        public const string AccountLogon = "Account.Logon";
        public const string SessionLaunch = "Session.Launch";
        public const string SessionLogon = "Session.Logon";
        public const string AppStart = "App.Start";
        public const string AppEnd = "App.End";
        public const string SessionEnd = "Session.End";
        public const string FileDownload = "File.Download";
        public const string Printing = "Printing";
        public const string SaasLaunch = "App.SaaS.Launch";
        public const string SaasEnd = "App.SaaS.End";
        public const string SaasUrlNavigate = "App.SaaS.Url.Navigate";
        public const string SaaSFileDownload = "App.SaaS.File.Download";
        public const string SaaSFilePrint = "App.SaaS.File.Print";
        public const string SaaSClipboard = "App.SaaS.Clipboard";
    }

    public class TokenInfo
    {
        public string strEventHubToken;
        public string strEventHubEndpoint;
        public string strCustomerId;
        public string strTokenExpiry;
        public string strDsAuth;
        public string strUserName;
    }

    public class AccountInfo
    {
        [JsonProperty(PropertyName = "sAMAccountName")]
        public string UserName { get; set; }
    }

    public class tenant
    {
        [JsonProperty(PropertyName = "id")]
        public string uuid { get; set; }
    }

    public class token
    {
        [JsonProperty(PropertyName = "DS-Auth")]
        public string DSAuth { get; set; }
    }

    public class DownloadFiledetails
    {
        [JsonProperty(PropertyName = "size")]
        public double Size { get; set; }

        [JsonProperty(PropertyName = "filename")]
        public string FileName { get; set; }

        [JsonProperty(PropertyName = "format")]
        public string Format { get; set; }
    }
    public class PrintJobDetails
    {
        [JsonProperty(PropertyName = "size")]
        public double Size { get; set; }

        [JsonProperty(PropertyName = "filename")]
        public string FileName { get; set; }

        [JsonProperty(PropertyName = "format")]
        public string Format { get; set; }

    }
    public class ClipboardJobDetails
    {
        [JsonProperty(PropertyName = "result")]
        public string Result { get; set; }

        [JsonProperty(PropertyName = "formattype")]
        public string Formattype { get; set; }

        [JsonProperty(PropertyName = "formatsize")]
        public double Formatsize { get; set; }

        [JsonProperty(PropertyName = "initiator")]
        public string Initiator { get; set; }

    }

    public class AccountLoginPaylod
    {
        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }

        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }
    }

    public class SessionEndPayload
    {

        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }

        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }

        [JsonProperty(PropertyName = "session-serverName")]
        public string ServerName { get; set; }

        [JsonProperty(PropertyName = "session-userName")]
        public string UserName { get; set; }

        [JsonProperty(PropertyName = "session-domain")]
        public string Domain { get; set; }

        [JsonProperty(PropertyName = "sessionGuid")]
        public string SessionGUID { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public string TimeStamp { get; set; }

    }

    public class AppStartPayload
    {

        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }

        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }

        [JsonProperty(PropertyName = "session-serverName")]
        public string ServerName { get; set; }

        [JsonProperty(PropertyName = "session-userName")]
        public string UserName { get; set; }

        [JsonProperty(PropertyName = "session-domain")]
        public string Domain { get; set; }

        [JsonProperty(PropertyName = "sessionGuid")]
        public string SessionGUID { get; set; }

        [JsonProperty(PropertyName = "appName")]
        public string AppName { get; set; }

        [JsonProperty(PropertyName = "moduleFilePath")]
        public string ModuleFilePath { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public string TimeStamp { get; set; }

    }

    public class AppEndPayload
    {

        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }

        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }

        [JsonProperty(PropertyName = "session-serverName")]
        public string ServerName { get; set; }

        [JsonProperty(PropertyName = "session-userName")]
        public string UserName { get; set; }

        [JsonProperty(PropertyName = "session-domain")]
        public string Domain { get; set; }

        [JsonProperty(PropertyName = "sessionGuid")]
        public string SessionGUID { get; set; }

        [JsonProperty(PropertyName = "appName")]
        public string AppName { get; set; }

        [JsonProperty(PropertyName = "moduleFilePath")]
        public string ModuleFilePath { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public string TimeStamp { get; set; }

    }

    public class SessionLogonPayload
    {

        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }


        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }

        [JsonProperty(PropertyName = "session-serverName")]
        public string ServerName { get; set; }

        [JsonProperty(PropertyName = "session-userName")]
        public string UserName { get; set; }

        [JsonProperty(PropertyName = "session-domain")]
        public string Domain { get; set; }

        [JsonProperty(PropertyName = "sessionGuid")]
        public string SessionGUID { get; set; }

        //[JsonProperty(PropertyName = "appName")]
        //public string AppName { get; set; }

        //[JsonProperty(PropertyName = "type")]
        //public string Type { get; set; }

        //[JsonProperty(PropertyName = "longCmdLine")]
        //public string LongCmdLine { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public string TimeStamp { get; set; }

    }

    public class SessionLaunchPayload
    {
        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }

        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }

        [JsonProperty(PropertyName = "appName")]
        public string AppName { get; set; }

        [JsonProperty(PropertyName = "type")]
        public string Type { get; set; }

        //[JsonProperty(PropertyName = "longCmdLine")]
        //public string LongCmdLine { get; set; }

    }

    public class SaaSLaunchPayload
    {
        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }

        [JsonProperty(PropertyName = "platformExtraInfo")]
        public string PlatformExtraInfo { get; set; }

        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public string TimeStamp { get; set; }

        //[JsonProperty(PropertyName = "userName")]
        //public string UserName { get; set; }

        //[JsonProperty(PropertyName = "domain")]
        //public string Domain { get; set; }

        [JsonProperty(PropertyName = "browser")]
        public string Browser { get; set; }

        [JsonProperty(PropertyName = "userAgent")]
        public string UserAgent { get; set; }

        [JsonProperty(PropertyName = "saasAppName")]
        public string SaasAppName { get; set; }

        [JsonProperty(PropertyName = "url")]
        public string URL { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }

    }

    public class SaaSEndPayload
    {
        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }

        [JsonProperty(PropertyName = "platformExtraInfo")]
        public string PlatformExtraInfo { get; set; }

        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public string TimeStamp { get; set; }

        //[JsonProperty(PropertyName = "userName")]
        //public string UserName { get; set; }

        //[JsonProperty(PropertyName = "domain")]
        //public string Domain { get; set; }

        [JsonProperty(PropertyName = "browser")]
        public string Browser { get; set; }

        [JsonProperty(PropertyName = "userAgent")]
        public string UserAgent { get; set; }

        [JsonProperty(PropertyName = "saasAppName")]
        public string SaasAppName { get; set; }

        [JsonProperty(PropertyName = "url")]
        public string URL { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }

    }
    public class SaaSUrlNavigatePayload
    {

        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }

        [JsonProperty(PropertyName = "platformExtraInfo")]
        public string PlatformExtraInfo { get; set; }

        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public string TimeStamp { get; set; }

        //[JsonProperty(PropertyName = "userName")]
        //public string UserName { get; set; }

        //[JsonProperty(PropertyName = "domain")]
        //public string Domain { get; set; }

        [JsonProperty(PropertyName = "browser")]
        public string Browser { get; set; }

        [JsonProperty(PropertyName = "userAgent")]
        public string UserAgent { get; set; }

        [JsonProperty(PropertyName = "saasAppName")]
        public string SaasAppName { get; set; }

        [JsonProperty(PropertyName = "url")]
        public string URL { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }

    }

    public class SaaSFileDownloadPayload
    {
        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }

        [JsonProperty(PropertyName = "platformExtraInfo")]
        public string PlatformExtraInfo { get; set; }

        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public string TimeStamp { get; set; }

        [JsonProperty(PropertyName = "browser")]
        public string Browser { get; set; }

        [JsonProperty(PropertyName = "userAgent")]
        public string UserAgent { get; set; }

        [JsonProperty(PropertyName = "saasAppName")]
        public string SaasAppName { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }

        [JsonProperty(PropertyName = "url")]
        public string URL { get; set; }

        [JsonProperty(PropertyName = "filePath")]
        public string FilePath { get; set; }

        [JsonProperty(PropertyName = "fileDetails")]
        public DownloadFiledetails FileDetails { get; set; }

        [JsonProperty(PropertyName = "deviceType")]
        public string DeviceType { get; set; }
    }

    public class SaaSFilePrintingPayload
    {
        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }

        [JsonProperty(PropertyName = "platformExtraInfo")]
        public string PlatformExtraInfo { get; set; }

        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public string TimeStamp { get; set; }

        [JsonProperty(PropertyName = "browser")]
        public string Browser { get; set; }

        [JsonProperty(PropertyName = "userAgent")]
        public string UserAgent { get; set; }

        [JsonProperty(PropertyName = "saasAppName")]
        public string SaasAppName { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }

        [JsonProperty(PropertyName = "url")]
        public string URL { get; set; }

        [JsonProperty(PropertyName = "printerName")]
        public string PrinterName { get; set; }

        [JsonProperty(PropertyName = "jobDetails")]
        public PrintJobDetails JobDetails { get; set; }

    }

    public class SaaSClipboardPayload
    {
        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }

        [JsonProperty(PropertyName = "platformExtraInfo")]
        public string PlatformExtraInfo { get; set; }

        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public string TimeStamp { get; set; }

        [JsonProperty(PropertyName = "browser")]
        public string Browser { get; set; }

        [JsonProperty(PropertyName = "userAgent")]
        public string UserAgent { get; set; }

        [JsonProperty(PropertyName = "saasAppName")]
        public string SaasAppName { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }

        [JsonProperty(PropertyName = "url")]
        public string URL { get; set; }

        [JsonProperty(PropertyName = "operation")]
        public string Operation { get; set; }

        [JsonProperty(PropertyName = "details")]
        public ClipboardJobDetails JobDetails { get; set; }

    }


    public class FileDownloadPayload
    {

        [JsonProperty(PropertyName = "deviceId")]
        public string HostName { get; set; }

        [JsonProperty(PropertyName = "os")]
        public string LoggedInOS { get; set; }

        [JsonProperty(PropertyName = "timezone")]
        public TimeZoneInfo TimeZone { get; set; }

        [JsonProperty(PropertyName = "session-serverName")]
        public string ServerName { get; set; }

        [JsonProperty(PropertyName = "session-userName")]
        public string UserName { get; set; }

        [JsonProperty(PropertyName = "session-domain")]
        public string Domain { get; set; }

        [JsonProperty(PropertyName = "sessionGuid")]
        public string SessionGUID { get; set; }

        [JsonProperty(PropertyName = "fileName")]
        public string FileName { get; set; }

        [JsonProperty(PropertyName = "fileSize")]
        public double FileSize { get; set; }

        [JsonProperty(PropertyName = "deviceType")]
        public string DeviceType { get; set; }

        [JsonProperty(PropertyName = "filePath")]
        public string FilePath { get; set; }

        [JsonProperty(PropertyName = "timestamp")]
        public string TimeStamp { get; set; }

    }

    public class EventHubPayload<T>
    {
        [JsonProperty(PropertyName = "ver")]
        public string EventVersion { get; set; }

        [JsonProperty(PropertyName = "id")]
        public string EventUID { get; set; }

        [JsonProperty(PropertyName = "type")]
        public string EventType { get; set; }

        [JsonProperty(PropertyName = "st")]
        public string EventSentTime;

        [JsonProperty(PropertyName = "prod")]
        public string EventProduct { get; set; }

        [JsonProperty(PropertyName = "prodVer")]
        public string EventProductVersion { get; set; }

        [JsonProperty(PropertyName = "dvc")]
        public string EventDvc { get; set; }

        [JsonProperty(PropertyName = "user")]
        public AccountInfo AccountUserInfo { get; set; }

        [JsonProperty(PropertyName = "tenant")]
        public tenant tenantId { get; set; }

        [JsonProperty(PropertyName = "token")]
        public token AuthToken { get; set; }

        [JsonProperty(PropertyName = "ip")]
        public string SystemIP { get; set; }

        [JsonProperty(PropertyName = "publicIPv4")]
        public string PublicIPv4 { get; set; }

        [JsonProperty(PropertyName = "publicIPv6")]
        public string PublicIPv6 { get; set; }

        [JsonProperty(PropertyName = "payload")]
        public T Payload { get; set; }

    }

    public class PublicIPAddressPayload
    {
        [JsonProperty(PropertyName = "publicIPv4")]
        public string PublicIPv4 { get; set; }

        [JsonProperty(PropertyName = "publicIPv6")]
        public string PublicIPv6 { get; set; }
    }
}