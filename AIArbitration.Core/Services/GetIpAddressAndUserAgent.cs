using Microsoft.AspNetCore.Http;
using UAParser.Interfaces;

namespace AIArbitration.Core.Services
{
    public class GetIpAddressAndUserAgent
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly HttpClient _httpClient;
        private readonly IUserAgentParser _userAgentParser;

        public GetIpAddressAndUserAgent(
            IHttpContextAccessor httpContextAccessor, 
            HttpClient httpClient,
            IUserAgentParser parser) 
        {
            _httpContextAccessor = httpContextAccessor;
            _httpClient = httpClient;
            _userAgentParser = parser;
        }

        public string GetClientIP(HttpContext context)
        {
            var ip = context.Connection.RemoteIpAddress?.ToString();
            return string.IsNullOrEmpty(ip) ? "IP Not Found" : ip;
        }

        public Dictionary<string, string> GetUserAgent(HttpContext context)
        {
            Dictionary<string, string> results = new Dictionary<string, string>();
            var clientInfo = _userAgentParser.ClientInfo;

            var ua = clientInfo.Browser.ToString();
            results["Browser"] = ua;
            var uaFamily = clientInfo.Browser.Family.ToString();
            results["BrowserFamily"] = uaFamily;
            var uaVersion = clientInfo.Browser.Version.ToString();
            results["BrowserVersion"] = uaVersion;
            var uaMajor = clientInfo.Browser.Major.ToString();
            results["BrowserMajor"] = uaMajor;
            var uaMinor = clientInfo.Browser.Minor.ToString();
            results["BrowserMinor"] = uaMinor;
            var os = clientInfo.OS.Family.ToString();
            results["OSFamily"] = os;
            var osVersion = clientInfo.OS.ToString();
            results["OSVersion"] = osVersion;
            var osMajor = clientInfo.OS.Major.ToString();
            results["OSMajor"] = osMajor;
            var osMinor = clientInfo.OS.Minor.ToString();
            results["OSMinor"] = osMinor;
            var device = clientInfo.Device.Family.ToString();
            results["DeviceFamily"] = device;
            var deviceBrand = clientInfo.Device.Brand.ToString();
            results["DeviceBrand"] = deviceBrand;
            var deviceModel = clientInfo.Device.Model.ToString();
            results["DeviceModel"] = deviceModel;
            var deviceFamily = clientInfo.Device.Family.ToString();
            results["DeviceFamily"] = deviceFamily;

            // var userInfo = $"UserAgent: {uaFamily} {uaVersion} (Major: {uaMajor}, Minor: {uaMinor}), OS: {os} {osVersion} (Major: {osMajor}, Minor: {osMinor}), Device: {device} (Brand: {deviceBrand}, Model: {deviceModel}, Family: {deviceFamily})";

            return results;
        }
    }
}
