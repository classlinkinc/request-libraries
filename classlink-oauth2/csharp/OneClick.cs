using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using Newtonsoft.Json;

namespace ClassLink_OneClick
{
    public class OneClick
    {
        private static readonly string authUrl = "https://launchpad.classlink.com/oauth2/v2/auth";
        private static readonly string exchangeUrl = "https://launchpad.classlink.com/oauth2/v2/token";
        private static readonly string infoUrl = "https://nodeapi.classlink.com/";
        private readonly string clientId;
        private readonly string clientSecret;

        ///<summary>
        /// Create an instance of the ClassLink OneClick helper
        ///</summary>
        ///<param name="client_Id"> Client Identifier string.</param>
        ///<param name="client_Secret"> Unique passphrase to go with the client_Id.</param>
        public OneClick(string client_Id, string client_Secret)
        {
            clientId = client_Id;
            clientSecret = client_Secret;
        }

        /// <summary>
        /// Get the URL where the client can get their access code.
        /// </summary>
        /// <param name="scope">
        /// The scope part of the query string in the returned URL. Cannot be null, will default to <value>"profile"</value> if left out.
        ///     <list type="bullet">
        ///         <listheader>
        ///             Scopes that can be used:
        ///         </listheader>
        ///         <item>
        ///             <term>profile</term>
        ///             <description>Access to user info and identity.</description>
        ///         </item>
        ///         <item>
        ///             <term>oneroster</term>
        ///             <description>Access to oneroster info and classes.</description>
        ///         </item>
        ///         <item>
        ///             <term>full</term>
        ///             <description>Full access to all APIs.</description>
        ///         </item>
        ///     </list>
        /// </param>
        /// <param name="redirectUri">Where the URL will take you upon providing correct login information.  Cannot be null, if left out will default to <value>http://localhost:8080/code</value> </param>
        /// <returns>Returns a plain text URL which can be used to octain an access code.</returns>
        public string getCodeUrl(string scope = "profile", string redirectUri = "http://localhost:8080/code")
        {
            NameValueCollection queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
            queryString["client_id"] = this.clientId;
            queryString["scope"] = scope;
            queryString["redirect_uri"] = redirectUri;
            queryString["response_type"] = "code";
            return authUrl + "?" + queryString.ToString();
        }

        /// <summary>
        /// Get the user's bearer token which will provide access to the other functions.
        /// </summary>
        /// <param name="code">The client access code obtained by <see cref="getCodeUrl(string,string)"/>.</param>
        /// <returns>Returns a bearer token string</returns>
        public string getToken(string code)
        {
            NameValueCollection queryString = System.Web.HttpUtility.ParseQueryString(string.Empty);
            queryString["client_id"] = clientId;
            queryString["client_secret"] = clientSecret;
            queryString["code"] = code;
            var data = Encoding.ASCII.GetBytes(queryString.ToString());

            HttpWebRequest req = (HttpWebRequest) WebRequest.Create(exchangeUrl);
            req.Method = "POST";
            req.ContentType = "application/x-www-form-urlencoded";
            req.ContentLength = data.Length;

            using(var stream = req.GetRequestStream())
            {
                stream.Write(data, 0, data.Length);
            }

            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            string json = new StreamReader(resp.GetResponseStream()).ReadToEnd();

            Dictionary<string, string> resp_vals = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
            return resp_vals["access_token"];
        }

        /// <summary>
        /// Get information from the requested endpoint about the user(s) or district
        /// </summary>
        /// <param name="bearer">The bearer token returned by <see cref="getToken(string)"/>.</param>
        /// <param name="endpoint">The URL of the server checked for user validation</param>
        /// <param name="extractNodes">
        /// The peice of user info returned.  If it contains multiple items the
        /// returned string will be a json (or json list) containing the items.
        /// If only a single item the returned string will be a single value or
        /// list of values.  If null or left out the entire JSON will be returned.
        /// </param>
        /// <returns>Returns a string containing the user/client info requested, or a JSON containing all of the available user/client info.</returns>
        public string getInfo(string bearer, string endpoint, string[] extractNodes = null)
        {
            HttpWebRequest req = (HttpWebRequest) WebRequest.Create(infoUrl + endpoint);
            req.Method = "GET";
            req.Headers.Set("Authorization", "bearer " + bearer);
            HttpWebResponse resp = (HttpWebResponse)req.GetResponse();
            string json = new StreamReader(resp.GetResponseStream()).ReadToEnd();

            if (extractNodes != null)
            {
                if (extractNodes.Length == 1)
                {
                    if (json[0] == '[')
                    {
                        List<Dictionary<string, string>> resp_vals = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json);
                        List<string> rtn = new List<string>();
                        foreach (Dictionary<string, string> d in resp_vals)
                        {
                            rtn.Add(d[extractNodes[0]]);
                        }
                        json = JsonConvert.SerializeObject(rtn/*, Formatting.Indented*/, Formatting.None);
                    }
                    else if (json[0] == '{')
                    {
                        return JsonConvert.DeserializeObject<Dictionary<string, string>>(json)[extractNodes[0]];
                    }
                    else
                    {
                        // invalid response like expired token :(
                        Console.Error.WriteLine(json);
                        return null;
                    }
                }
                else
                {
                    if (json[0] == '[')
                    {
                        List<Dictionary<string, string>> resp_vals = JsonConvert.DeserializeObject<List<Dictionary<string, string>>>(json);
                        List<Dictionary<string, string>> rtn = new List<Dictionary<string, string>>();
                        Dictionary<string, string> tmp = new Dictionary<string, string>();
                        foreach (Dictionary<string, string> d in resp_vals)
                        {
                            tmp.Clear();
                            foreach (KeyValuePair<string, string> kvp in d)
                            {
                                if (extractNodes.Contains(kvp.Key))
                                {
                                    tmp.Add(kvp.Key, kvp.Value);
                                }
                            }
                            rtn.Add(tmp);
                        }

                        json = JsonConvert.SerializeObject(rtn/*, Formatting.Indented*/, Formatting.None);
                    }
                    else if (json[0] == '{')
                    {
                        Dictionary<string, string> resp_val = JsonConvert.DeserializeObject<Dictionary<string, string>>(json);
                        Dictionary<string, string> rtn = new Dictionary<string, string>();
                        foreach (KeyValuePair<string, string> kvp in resp_val)
                        {
                            if (extractNodes.Contains(kvp.Key))
                            {
                                rtn.Add(kvp.Key, kvp.Value);
                            }
                        }

                        json = JsonConvert.SerializeObject(rtn/*, Formatting.Indented*/, Formatting.None);
                    }
                    else
                    {
                        // invalid response like expired token :(
                        Console.Error.WriteLine(json);
                        return null;
                    }
                }
            }
            return json;
        }

        /// <summary>
        /// Get information about the user as a JSON string.
        /// </summary>
        /// <param name="bearer">The bearer token returned by <see cref="getToken(string)"/>.</param>
        /// <returns>Returns a JSON string containing all of the available user info.</returns>
        public string getUserInfo(string bearer) => getInfo(bearer, "v2/my/info");

        /// <summary>
        /// Get the user's district.
        /// </summary>
        /// <param name="bearer">The bearer token returned by <see cref="getToken(string)"/>.</param>
        /// <returns>Returns the district as a string. </returns>
        public string getUserDistrict(string bearer) => getInfo(bearer, "v2/my/district");

        /// <summary>
        /// Get info opn the user's profile(s).
        /// </summary>
        /// <param name="bearer">The bearer token returned by <see cref="getToken(string)"/>.</param>
        /// <returns>Returns a string containing list of JSONs of the profile(s) information.</returns>
        public string getUserProfiles(string bearer) => getInfo(bearer, "v2/my/profiles");

        /// <summary>
        /// If the users is a parent who has registered for Classlink, this will get a list of any linked student accounts.
        /// </summary>
        /// <param name="bearer">The bearer token returned by <see cref="getToken(string)"/>.</param>
        /// <returns>Returns a list JSONs containing all linked information on students.  Returns an empty list if the user is not a parent.</returns>
        public string getUserChildren(string bearer) => getInfo(bearer, "v2/my/students");

        /// <summary>
        /// Get a list of all groups that the user is a part of.
        /// </summary>
        /// <param name="bearer">The bearer token returned by <see cref="getToken(string)"/>.</param>
        /// <returns>Returns a string containing a list of JSONs with the name, groupId, and buildingId of the group.</returns>
        public string getUserGroups(string bearer) => getInfo(bearer, "my/groups");

    }
}
