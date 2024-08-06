/**
 * this package requires you to download and install 2 libraries for it to run
 * Apache http components https://hc.apache.org/downloads.cgi
 * Maven org.json https://mvnrepository.com/artifact/org.json/json
 */

import com.sun.javaws.exceptions.InvalidArgumentException;
import org.apache.http.HttpResponse;
import org.apache.http.client.HttpClient;
import org.apache.http.client.entity.UrlEncodedFormEntity;
import org.apache.http.client.methods.HttpGet;
import org.apache.http.client.methods.HttpPost;
import org.apache.http.client.utils.URLEncodedUtils;
import org.apache.http.impl.client.DefaultHttpClient;
import org.apache.http.NameValuePair;
import org.apache.http.message.BasicNameValuePair;

import java.io.*;
import java.util.ArrayList;
import java.util.List;

import org.json.*;

import javax.xml.ws.http.HTTPException;

public class OneClick {
	private static final String authUrl = "https://launchpad.classlink.com/oauth2/v2/auth";
    private static final String exchangeUrl = "https://launchpad.classlink.com/oauth2/v2/token";
    private static final String infoUrl = "https://nodeapi.classlink.com/";

	private final String clientId;
	private final String clientSecret;

	public final String SCOPE_ONEROSTER = "oneroster";
	public final String SCOPE_FULL = "full";
	public final String SCOPE_PROFILE = "profile";

	/**
	 * ClassLink OneClick helper
	 * @param clientId Client Identifier string.
	 * @param clientSecret Secret to go with the client_Id.
	 */
	public OneClick(String clientId, String clientSecret) {
		this.clientId = clientId;
		this.clientSecret = clientSecret;
	}

	/**
	 * Get the URL for where the client can get their access code
	 * @param scope The scope part of the query string in the returned URL. There are 3 scopes that can be used:
	 *      - profile:  Access to user identity and user specific information info.
	 *      - oneroster: Access to oneroster info and classes.
	 *      - full: Full access to all public APIs.
	 * @param redirectUri Where the URL will take you upon providing correct login information.  Cannot be null, if left out will default to http://localhost:8080/code
	 * @return Returns a URL that the user can curl to or that can be put URL bar.
	 */
	public String getCodeUrl(String scope, String redirectUri) {
		List<NameValuePair> query  = new ArrayList<NameValuePair>();
		query.add(new BasicNameValuePair("client_id", this.clientId));
		query.add(new BasicNameValuePair("scope", scope));
		query.add(new BasicNameValuePair("redirect_uri", redirectUri));
		query.add(new BasicNameValuePair("response_type", "code"));

		String queryString = URLEncodedUtils.format(query, "UTF-8");

		return this.authUrl + "?" + queryString;
	}
	public String getCodeUrl() { return this.getCodeUrl("profile", "https://localhost:8080"); }
	public String getCodeUrl(String scope) { return this.getCodeUrl(scope, "https://localhost:8080"); }

	public String getToken(String code) throws UnsupportedEncodingException, IOException, InvalidArgumentException{
		List<NameValuePair> body  = new ArrayList<NameValuePair>();
		body.add(new BasicNameValuePair("client_id", this.clientId));
		body.add(new BasicNameValuePair("client_secret", this.clientSecret));
		body.add(new BasicNameValuePair("code", code));

		HttpClient client = new DefaultHttpClient();
		HttpPost post = new HttpPost(this.exchangeUrl);

		post.setEntity(new UrlEncodedFormEntity(body));
		HttpResponse response = client.execute(post);
		BufferedReader rd = new BufferedReader(new InputStreamReader(response.getEntity().getContent()));

		StringBuffer result = new StringBuffer();
		String line = "";
		while ((line = rd.readLine()) != null) {
			result.append(line);
		}

		int statusCode = response.getStatusLine().getStatusCode();

		if (statusCode != 200) {
			System.err.println(result.toString());
			throw new HTTPException(statusCode);
		}
		else {
			JSONObject json = new JSONObject(result.toString());
			return json.getString("access_token");
		}
	}

	/**
	 * Get information from the requested endpoint about the user(s) or district
	 * @param bearer The bearer token returned by getToken(String code).
	 * @param endPoint The URI of the server checked for user validation.
	 * @param extractNode The piece of user info returned.  If null or left out the entire JSON will be returned.
	 * @return If extractNode is specified it will return the information in that node as a string.  If extractNode is not specified it will return the JSON encoded response as a string.
	 */
	public String getInfo(String bearer, String endPoint, String extractNode) throws IOException{
		String url = this.infoUrl + endPoint;

		HttpClient client = new DefaultHttpClient();
		HttpGet request = new HttpGet(url);
		request.addHeader("Authorization", "bearer " + bearer);
//		System.out.println(bearer);
//		if(bearer.substring(0, 4) == "bearer".substring(0 ,4)){
//			request.addHeader("Authorization", bearer);
//		}
//		else {
//			request.addHeader("gwstoken", bearer);
//		}

		HttpResponse response = client.execute(request);

		BufferedReader rd = new BufferedReader(new InputStreamReader(response.getEntity().getContent()));
		StringBuffer result = new StringBuffer();
		String line = "";
		while ((line = rd.readLine()) != null) {
			result.append(line);
		}

		int statusCode = response.getStatusLine().getStatusCode();

		if(statusCode != 200) {
			System.err.println(result.toString());
			throw new HTTPException(statusCode);
		}
		else {
			if(extractNode != null) {
				JSONObject json = new JSONObject(result.toString());
				return json.get(extractNode).toString();	//get and not getJSONObject or getJSONArray because it could be both and type errors are a thing
			}
			return result.toString();
		}
	}
	public String getInfo(String bearer, String endPoint) throws IOException {return this.getInfo(bearer, endPoint, null);}

	/**
	 * Get information about the user as a JSON.
	 * @param bearer The bearer token returned by getToken(String code).
	 * @return Returns a JSON containing all of the available user info.
	 * @throws IOException
	 */
	public String getUserInfo(String bearer) throws IOException { return this.getInfo(bearer, "v2/my/info"); }

	/**
	 * Get the district associated with the user.
	 * @param bearer The bearer token returned by getToken(String code).
	 * @return Returns the district as a string.
	 * @throws IOException
	 */
	public String getUserDistrict(String bearer) throws IOException { return this.getInfo(bearer, "v2/my/district");}

	/**
	 * Get info on the user's profile(s).
	 * @param bearer The bearer token returned by getToken(String code).
	 * @return Returns a list of JSONs containing the profile(s) information as a string.
	 * @throws IOException
	 */
	public String getUserProfiles(String bearer) throws IOException { return this.getInfo(bearer, "v2/my/profiles"); }

	/**
	 * If the user is a parent who has registered for Classlink this will get a list of any linked student accounts aka their children.
	 * @param bearer The bearer token returned by getToken(String code).
	 * @return Returns a list JSONs containing all linked information on students. Returns an empty list if the user is not a parent.
	 * @throws IOException
	 */
	public String getUserChildren(String bearer) throws IOException { return this.getInfo(bearer, "v2/my/students"); }

	/**
	 * Get a list of all groups that the user is a part of.
	 * @param bearer The bearer token returned by getToken(String code).
	 * @return Returns a list JSONs containing the name, groupId, and buildingId of the group.
	 * @throws IOException
	 */
	public String getUserGroups(String bearer) throws IOException { return this.getInfo(bearer, "my/groups"); }

}
