using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;

namespace ClassLink.OneRoster
{
    public class OneRoster
    {
        private readonly string _clientId;
        private readonly string _clientSecret;

        public OneRoster(string clientId, string clientSecret)
        {
            _clientId = clientId;
            _clientSecret = clientSecret;
        }

        /// <summary>
        /// Make a GET request to the given url with the stored key and secret
        /// </summary>
        /// <param name="url">The url for the request, with params included</param>
        /// <returns>A dictionary of the status_code and response</returns>
        public OneRosterResponse MakeRosterRequest(string url)
        {
            // Generate timestamp and nonce
            string timestamp = ((int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds).ToString();
            string nonce = GenerateNonce(timestamp.Length);

            // Definte oauth params
            Dictionary<string, string> oauth = new Dictionary<string, string>
            {
                {"oauth_consumer_key", _clientId},
                {"oauth_signature_method", "HMAC-SHA256"},
                {"oauth_timestamp", timestamp},
                {"oauth_nonce", nonce}
            };

            // Combine url params and oauth params into a sorted dictionary
            string[] urlPieces = url.Split('?');
            SortedDictionary<string, string> allParams;
            if (urlPieces.Length == 2)
            {
                Dictionary<string, string> urlParams = ParamsToDic(urlPieces[1]);
                allParams = SortAllParams(urlParams, oauth);
            } else
            {
                allParams = SortAllParams(oauth, new Dictionary<string, string>());
            }


            // Generate the signature
            string baseInfo = BuildBaseString(urlPieces[0], "GET", allParams);
            string compositeKey = UrlEncodeUpperCase(_clientSecret) + "&";
            string authSignature = GenerateSig(baseInfo, compositeKey);
            oauth.Add("oauth_signature", authSignature);

            // Generate the header and make the request 
            string authHeader = BuildAuthHeader(oauth);
            return MakeGetRequest(authHeader, url);
        }

        /// <summary>
        /// Create a random string for the nonce
        /// </summary>
        /// <param name="len">The length of the nonce</param>
        /// <returns></returns>
        private string GenerateNonce(int len)
        {
            const string allowedChars = "ABCDEFGHJKLMNOPQRSTUVWXYZabcdefghijkmnopqrstuvwxyz0123456789";
            char[] chars = new char[len];

            Random rd = new Random();

            for (int i = 0; i < len; i++)
            {
                chars[i] = allowedChars[rd.Next(0, allowedChars.Length)];
            }

            return new string(chars);
        }

        /// <summary>
        /// Make the request to the given url with generated header
        /// </summary>
        /// <param name="authHeader">The generated auth header</param>
        /// <param name="url">The url for the request</param>
        /// <returns>A dictionary with the status_code and response</returns>
        private OneRosterResponse MakeGetRequest(string authHeader, string url)
        {
            try
            {
                HttpWebRequest webRequest = (HttpWebRequest)HttpWebRequest.Create(url);
                if (webRequest != null)
                {
                    webRequest.Method = "GET";
                    webRequest.Timeout = 25000;
                    webRequest.Headers.Add("Authorization", authHeader);

                    HttpWebResponse webResponse = (HttpWebResponse)webRequest.GetResponse();

                    using (Stream s = webResponse.GetResponseStream())
                    {
                        using (StreamReader sr = new StreamReader(s))
                        {
                            var response = sr.ReadToEnd();
                            return new OneRosterResponse((int)webResponse.StatusCode, response);
                        }
                    }
                }
            } catch (WebException webException)
            {
                try
                {
                    using (var sr = new StreamReader(webException.Response.GetResponseStream()
                                                     ?? throw new Exception()))
                    {
                        return new OneRosterResponse((int)webException.Status, sr.ReadToEnd());
                    }
                } catch (Exception)
                {
                    return new OneRosterResponse(0, "An error occurred, check your URL");
                }

            } catch (Exception)
            {
                return new OneRosterResponse(0, "An error occurred, check your URL");
            }

            return null;
        }

        /// <summary>
        /// Generates the auth header from the oauth params
        /// </summary>
        /// <param name="oauth">A dictionary of oauth params</param>
        /// <returns>The auth header</returns>
        private string BuildAuthHeader(Dictionary<string, string> oauth)
        {
            string result = "OAuth ";
            string[] values = new string[oauth.Count];
            int i = 0;
            foreach (var key in oauth.Keys)
            {
                values[i] = $"{key}=\"{UrlEncodeUpperCase(oauth[key])}\"";
                i++;
            }
            result += string.Join(",", values);
            return result;
        }

        /// <summary>
        /// Generate the oauth signature from the base string and key
        /// </summary>
        /// <param name="baseString">The base string generated from method, url, and params</param>
        /// <param name="key">The key created from ClientSecret</param>
        /// <returns>The generated oauth signature</returns>
        private string GenerateSig(string baseString, string key)
        {
            Encoding encoding = new ASCIIEncoding();
            byte[] byteKey = encoding.GetBytes(key);
            byte[] stringBytes = encoding.GetBytes(baseString);

            string result = string.Empty;
            using (HMACSHA256 sha256 = new HMACSHA256(byteKey))
            {
                var hashed = sha256.ComputeHash(stringBytes);
                result = Convert.ToBase64String(hashed);
            }


            return result;

        }

        /// <summary>
        /// Builds the base string for the generation of the oauth signature
        /// </summary>
        /// <param name="baseUrl">The base url without params</param>
        /// <param name="method">The request's HTTP method</param>
        /// <param name="allParams">The url params and oauth params</param>
        /// <returns>The base string</returns>
        private string BuildBaseString(string baseUrl, string method, SortedDictionary<string, string> allParams)
        {
            string[] result = new string[allParams.Count];
            int i = 0;
            foreach (var key in allParams.Keys)
            {
                result[i] = $"{key}={UrlEncodeUpperCase(allParams[key])}";
                i++;

            }

            return method + "&" + UrlEncodeUpperCase(baseUrl) + "&" +
                   UrlEncodeUpperCase(string.Join("&", result));
        }

        /// <summary>
        /// URL encodes the string
        /// </summary>
        /// <param name="value">The string to encode</param>
        /// <returns>The encoded string</returns>
        private string UrlEncodeUpperCase(string value)
        {
            value = HttpUtility.UrlEncode(value);
            value = value.Replace("+", "%20");
            return Regex.Replace(value, "(%[0-9a-f][0-9a-f])", c => c.Value.ToUpper());
        }

        /// <summary>
        /// Combines all the params into one sorted dictionary
        /// </summary>
        /// <param name="urlParams">The params from the url</param>
        /// <param name="oauth">The oauth params</param>
        /// <returns>A sorted dictionary of all the params</returns>
        private SortedDictionary<string, string> SortAllParams(Dictionary<string, string> urlParams,
            Dictionary<string, string> oauth)
        {
            SortedDictionary<string, string> result = new SortedDictionary<string, string>();
            foreach (var key in urlParams.Keys)
            {
                result.Add(key, urlParams[key]);
            }

            foreach (var key in oauth.Keys)
            {
                result.Add(key, oauth[key]);
            }

            return result;
        }

        /// <summary>
        /// Converts the params from the url to a dictionary
        /// </summary>
        /// <param name="urlPiece">The params in the url</param>
        /// <returns>A dictionary of the params</returns>
        private Dictionary<string, string> ParamsToDic(string urlPiece)
        {
            string[] theParams = urlPiece.Split('&');
            Dictionary<string, string> result = new Dictionary<string, string>();
            foreach (var value in theParams)
            {
                string decodedVal = HttpUtility.UrlDecode(value);
                string[] split = decodedVal.Split('=');
                if (split.Length == 2)
                {
                    result.Add(split[0], split[1]);
                } else
                {
                    result.Add("filter", decodedVal.Substring(7));
                }
            }

            return result;
        }
    }
}