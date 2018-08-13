package com.classlink.roster;

import javax.crypto.Mac;
import javax.crypto.spec.SecretKeySpec;
import javax.net.ssl.HttpsURLConnection;
import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.net.*;
import java.nio.charset.StandardCharsets;
import java.security.InvalidKeyException;
import java.security.NoSuchAlgorithmException;
import java.security.SecureRandom;
import java.util.*;

public class OneRoster {
    private String clientId;
    private String clientSecret;

    public OneRoster(String clientId, String clientSecret) {
        this.clientId = clientId;
        this.clientSecret = clientSecret;
    }

    /**
     * Make a get request on the given url with the stored key and secret
     *
     * @param url       The url for the request
     * @return          A Map with the status_code and response
     */
    public OneRosterResponse makeRosterRequest(String url) {
        // Create the timestamp and nonce
        String timestamp = Long.toString(System.currentTimeMillis() / 1000);
        String nonce = generateNonce(timestamp.length());

        // Define the oauth params
        Map<String, String> oauth = new LinkedHashMap<>();
        oauth.put("oauth_consumer_key", this.clientId);
        oauth.put("oauth_signature_method", "HMAC-SHA256");
        oauth.put("oauth_timestamp", timestamp);
        oauth.put("oauth_nonce", nonce);

        // Split the string into base url and params
        String[] urlPieces = url.split("\\?");

        Map<String, String> allParams;

        // Handle urls with and without params
        if (urlPieces.length == 2) {
            Map<String, String> urlParams = paramsToMap(urlPieces[1]);
            allParams = sortAllParams(oauth, urlParams);
        } else {
            allParams = sortAllParams(oauth, new HashMap<>());
        }

        // Generate signature
        String baseInfo = buildBaseString(urlPieces[0], "GET", allParams);
        String compositeKey = encodeURL(clientSecret) + "&";
        String authSignature = generateAuthSignature(baseInfo, compositeKey);
        oauth.put("oauth_signature", authSignature);

        // Generate header
        String authHeader = buildAuthHeader(oauth);
        
        return makeGetRequest(url, authHeader);
    }


    /**
     * Generate the nonce
     *
     * @param len   The length of the nonce
     * @return      The nonce
     */
    private String generateNonce(int len) {
        String AB = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz";
        SecureRandom rnd = new SecureRandom();

        StringBuilder sb = new StringBuilder( len );
        for( int i = 0; i < len; i++ )
            sb.append( AB.charAt( rnd.nextInt(AB.length()) ) );
        return sb.toString();
    }

    /**
     * Makes the get request
     *
     * @param url       The url for the request
     * @param header    The oauth header
     * @return          A Map of the status_code and the response
     */
    private OneRosterResponse makeGetRequest(String url, String header) {
        try {
            URL theUrl = new URL(url);

            HttpsURLConnection connection = null;
            try {
                connection = (HttpsURLConnection) theUrl.openConnection();

                connection.setRequestMethod("GET");

                connection.setRequestProperty("Authorization", header);

                int responseCode = connection.getResponseCode();

                BufferedReader in = new BufferedReader(
                        new InputStreamReader(connection.getInputStream())
                );

                String inputLine;
                StringBuffer response = new StringBuffer();

                while ((inputLine = in.readLine()) != null) {
                    response.append(inputLine);
                }

                String res = response.toString();
                in.close();
                return new OneRosterResponse(responseCode, res);
            } catch (IOException e) {
                try {
                    if (connection != null) {
                        BufferedReader in = new BufferedReader(
                                new InputStreamReader(connection.getErrorStream())
                        );
                        String inputLine;
                        StringBuilder response = new StringBuilder();

                        while ((inputLine = in.readLine()) != null) {
                            response.append(inputLine);
                        }

                        String res = response.toString();
                        in.close();
                        return new OneRosterResponse(connection.getResponseCode(), res);
                    } else {
                        return new OneRosterResponse(0, "No connection found");
                    }


                } catch (Exception ex) {
                    return new OneRosterResponse(0, "An error occurred, check your url");

                }

            }
        } catch (MalformedURLException e) {
            return new OneRosterResponse(0, "An error occurred, check your url");
        }
    }

        /**
     * Generate the auth header
     *
     * @param oauth     The oauth params
     * @return          The auth header
     */
    private String buildAuthHeader(Map<String, String> oauth) {
        String result = "OAuth ";
        String[] values = new String[oauth.size()];
        int i = 0;
        for (String key : oauth.keySet()) {
            values[i] = String.format("%s=\"%s\"", key, encodeURL(oauth.get(key)));
            i++;
        }
        result += String.join(",", values);
        return result;
    }

    /**
     * Generate the auth signature
     *
     * @param baseInfo          The generated base string
     * @param compositeKey      The secret key
     * @return                  The auth signature
     */
    private String generateAuthSignature(String baseInfo, String compositeKey) {
        try {
            Mac sha256 = Mac.getInstance("HmacSHA256");
            sha256.init(new SecretKeySpec(compositeKey.getBytes(StandardCharsets.UTF_8), "HmacSHA256"));
            byte[] hash = sha256.doFinal(baseInfo.getBytes(StandardCharsets.UTF_8));
            return Base64.getEncoder().encodeToString(hash);
        } catch (NoSuchAlgorithmException | InvalidKeyException e) {
            e.printStackTrace();
        }
        return null;
    }

    /**
     * Generate the base string for the signature
     *
     * @param baseUrl       The base url
     * @param method        The HTTP method
     * @param allParams     The url and oauth params
     * @return              The base string for the signature
     */
    private String buildBaseString(String baseUrl, String method, Map<String, String> allParams) {
        String[] result = new String[allParams.size()];
        int i =0;
        for (String key : allParams.keySet()) {
            result[i] = String.format("%s=%s", key, encodeURL(allParams.get(key)));
            i++;
        }

        return method + "&" + encodeURL(baseUrl) + "&" + encodeURL(String.join("&", result));
    }

    /**
     * URL encode the given string
     *
     * @param str       The string to encode
     * @return          The urlencoded string
     */
    private String encodeURL(String str) {
        String converted = URLEncoder.encode(str, StandardCharsets.UTF_8);
        return converted.replace("+", "%20");
    }

    /**
     * Combine the maps and sort
     *
     * @param oauth         The oauth params
     * @param urlParams     The url params
     * @return              A TreeMap of the combined Maps
     */
    private Map<String, String> sortAllParams(Map<String, String> oauth, Map<String, String> urlParams) {
        Map<String, String> result = new TreeMap<>();

        for (String key : oauth.keySet()) {
            result.put(key, oauth.get(key));
        }

        for (String key : urlParams.keySet()) {
            result.put(key, urlParams.get(key));
        }

        return result;
    }

    /**
     * Converts the url string of params to a map
     *
     * @param urlPiece  The string with the params from the url
     * @return          The Map of the params
     */
    private Map<String, String> paramsToMap(String urlPiece) {
        String[] theParams = urlPiece.split("&");
        Map<String, String> result = new HashMap<>();
        for (String value : theParams) {
            String decodedValue = URLDecoder.decode(value, StandardCharsets.UTF_8);
            String[] split = decodedValue.split("=");
            if (split.length == 2) {
                result.put(split[0], split[1]);
            } else {
                result.put("filter", decodedValue.substring(7));
            }
        }

        return result;
    }
}
