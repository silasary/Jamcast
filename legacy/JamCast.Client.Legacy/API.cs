using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace JamCast.Client
{
    public static class API
    {
        private static string SessionJson
        {
            get
            {
                if (File.Exists("session.json"))
                    return "session.json";
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "jamcast"));
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "jamcast", "session.json");
            }
        }
        private static string SiteInfoJson
        {
            get
            {
                if (File.Exists("siteinfo.json"))
                    return "siteinfo.json";
                Directory.CreateDirectory(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "jamcast"));
                return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "jamcast", "siteinfo.json");
            }
        }

public class AuthInfo
        {
            public string FullName { get; set; }

            public string EmailAddress { get; set; }

            public bool IsValid { get; set; }

            public string Error { get; set; }
            public string SessionId { get; internal set; }
            public string SecretKey { get; internal set; }
            public string AccountType { get; internal set; }
        }

        public class SiteInfo
        {
            public string Id { get; set; }
            public string SiteName { get; set; }
            public string Url { get; set; }
            [JsonProperty("image_cover")]
            public string ImageCover { get; set; }
            [JsonProperty("image_favicon")]
            public string ImageFavicon { get; set; }
        }

        public static string BaseAddress { get; set; }
        public static AuthInfo Credentials { get; private set; }

        private static WebClient MakeWebClient()
        {
            return new WebClient
            {
                BaseAddress = BaseAddress,
            };
        }
        public static async Task<JToken> GetJamcastEndpoint(JToken endpoint)
        {
            using (var wc = MakeWebClient())
            {
                var json = await wc.DownloadStringTaskAsync("/jamcast/ip");
                endpoint = JToken.Parse(json);
                return endpoint;
            }
        }

        public static async Task<bool> AuthenticateAsync(string emailAddress, string password)
        {
            const string url = "/jamcast/api/authenticate";
            using (var client = MakeWebClient())
            {
                var result = await client.UploadValuesTaskAsync(url, "POST", new NameValueCollection
                {
                    {"email", emailAddress},
                    {"password", password}
                });

                var resultParsed = JsonConvert.DeserializeObject<dynamic>(Encoding.ASCII.GetString(result));
                if ((bool)resultParsed.has_error)
                {
                    Credentials =  new AuthInfo
                    {
                        IsValid = false,
                        Error = (string)resultParsed.error
                    };
                }
                else
                {
                    Credentials = new AuthInfo
                    {
                        IsValid = true,
                        FullName = (string)resultParsed.result.fullName,
                        EmailAddress = (string)resultParsed.result.email,
                        SessionId = (string)resultParsed.result.sessionId,
                        SecretKey = (string)resultParsed.result.secretKey,
                        AccountType = (string)resultParsed.result.accountType,
                    };
                    File.WriteAllText(SessionJson, JsonConvert.SerializeObject(Credentials));

                }
                return Credentials.IsValid;
            }
        }

        public static async Task<SiteInfo> GetSiteInfoAsync()
        {
            const string url = "/jamcast/siteinfo.json";
            using (var client = MakeWebClient())
            {
                var result = await client.DownloadStringTaskAsync(url);
                var resultParsed = JsonConvert.DeserializeObject<SiteInfo>(result);
                File.WriteAllText(SiteInfoJson, JsonConvert.SerializeObject(resultParsed));
                return resultParsed;
            }
        }

        public static async Task<bool> ValidateSession()
        {
            const string url = "/jamcast/api/validatesession";
            if (File.Exists(SessionJson))
            {
                var credentials = JsonConvert.DeserializeObject<API.AuthInfo>(File.ReadAllText(SessionJson));
                var siteinfo = JsonConvert.DeserializeObject<API.SiteInfo>(File.ReadAllText(SiteInfoJson));
                BaseAddress = siteinfo.Url;
                // Validation is a good idea.
                Credentials = credentials;

                //using (var client = MakeWebClient())
                //{
                //    var result = await client.UploadValuesTaskAsync(url, "POST", new NameValueCollection
                //    {
                //        {"sessionId", credentials.SessionId},
                //        {"secretKey", credentials.SecretKey},
                //    }).ConfigureAwait(false);
                //    var resultParsed = JsonConvert.DeserializeObject<dynamic>(Encoding.ASCII.GetString(result));
                //    if ((bool)resultParsed.has_error)
                //    {
                //        Credentials = new AuthInfo
                //        {
                //            IsValid = false,
                //            Error = (string)resultParsed.error
                //        };
                //    }
                //    else
                //    {
                //        Credentials = new AuthInfo
                //        {
                //            IsValid = true,
                //            FullName = (string)resultParsed.result.fullName,
                //            EmailAddress = (string)resultParsed.result.email,
                //            SessionId = (string)resultParsed.result.sessionId,
                //            SecretKey = (string)resultParsed.result.secretKey,
                //            AccountType = (string)resultParsed.result.accountType,
                //        };
                //        File.WriteAllText("session.json", JsonConvert.SerializeObject(Credentials));
                //    }
                //}
                return Credentials.IsValid;
            }
            return false;
        }
    }
}
