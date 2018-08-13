<?php

// Uses curl
class OneRoster {
    private $client_id;
    private $client_secret;

    function __construct($client_id, $client_secret) {
        $this->client_id = $client_id;
        $this->client_secret = $client_secret;
    }

    /**
     * Makes a get request to the given url with the stored key and secret
     *
     * @param string $url      The exact url of the request, including all params
     * @return array           Status_code and response from the request
     */
    public function makeRosterRequest($url) {

        // Create the timestamp and nonce
        $timestamp = strval(time());
        $nonce = $this->generateNonce(strlen($timestamp));

        // Assign the oauth params
        $oauth = array( 'oauth_consumer_key' => $this->client_id,
            'oauth_signature_method' => 'HMAC-SHA256',
            'oauth_timestamp' => $timestamp,
            'oauth_nonce' => $nonce);

        // Split the url into the base url and the params
        $url_pieces = explode("?", $url);
        $params = $oauth;

        // Add the url params if they exist
        if (count($url_pieces) == 2) {
            $url_params = $this->paramsToArray($url_pieces[1]);
            $params = array_merge($params, $url_params);
        }

        // Generate the oauth signature
        $base_info = $this->buildBaseString($url_pieces[0], 'GET', $params);
        $composite_key = rawurlencode($this->client_secret) . '&';
        $oauth_signature = base64_encode(hash_hmac('SHA256', $base_info, $composite_key, true));
        $oauth['oauth_signature'] = $oauth_signature;

        // Create the oauth header
        $auth_header = $this->buildAuthorizationHeader($oauth);

        // Return the response from the get request
        return $this->makeCurlRequest($auth_header, $url);
    }

    /**
     * Generates the base string for the generation of the oauth signature
     *
     * @param string $baseURI       The base url
     * @param string $method        The HTTP method
     * @param array $params         The url params
     * @return string               The base string used to generate the signature
     *
     */
    private function buildBaseString($baseURI, $method, $params){
        $r = array();
        ksort($params);
        foreach($params as $key=>$value){
            $r[] = "$key=" . rawurlencode($value);
        }
        return $method."&" . rawurlencode($baseURI) . '&' . rawurlencode(implode('&', $r));
    }

    /**
     * Generates the nonce
     *
     * @param int $length       Length of the nonce
     * @return string           Nonce
     */
    private function generateNonce($length) {
        $characters = '0123456789abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ';
        $charactersLength = strlen($characters);
        $randomString = '';
        for ($i = 0; $i < $length; $i++) {
            $randomString .= $characters[rand(0, $charactersLength - 1)];
        }
        return $randomString;
    }

    /**
     * Generates the authorization header with the auth info
     *
     * @param array $oauthinfo     The parameters for the oauth header
     * @return string               The oauth header
     */
    private function buildAuthorizationHeader($oauthinfo)
    {
        $r = 'Authorization: OAuth ';
        $values = array();
        foreach ($oauthinfo as $key => $value)
            $values[] = "$key=\"" . rawurlencode($value) . "\"";

        $r .= implode(',', $values);
        return $r;
    }

    /**
     * Converts the url params into an array
     *
     * @param string $url_params    The params from the url
     * @return array                An array of the params
     */
    private function paramsToArray($url_params) {
        $params = explode("&", $url_params);
        $result = array();
        foreach ($params as $value) {
            $value = rawurldecode($value);
            $split = explode("=", $value);
            if (count($split) == 2) {
                $result[$split[0]] = $split[1];
            } else {
                $result["filter"] = substr($value, 7);
            }

        }
        return $result;
    }

    /**
     * Performs the desired get request and returns an array with the status_code and response body
     *
     * @param string $auth_header   The auth header
     * @param string $url           The request url
     * @return array                The staus_code and response
     */
    private function makeCurlRequest($auth_header, $url) {
        $curl = curl_init();

        curl_setopt_array($curl, array(
            CURLOPT_URL => str_replace(" ", "%20", $url),
            CURLOPT_RETURNTRANSFER => true,
            CURLOPT_ENCODING => "",
            CURLOPT_HTTP_VERSION => CURL_HTTP_VERSION_1_1,
            CURLOPT_CUSTOMREQUEST => "GET",
            CURLOPT_HTTPHEADER => array(
                $auth_header,
            ),
        ));

        $response = curl_exec($curl);
        $httpcode = curl_getinfo($curl, CURLINFO_HTTP_CODE);

        curl_close($curl);

        if ($httpcode == 0) {
            return array("status_code" => $httpcode,
                "response" => "An error occurred, check your URL");
        }

        return array("status_code" => $httpcode,
                    "response" => $response);
    }
}