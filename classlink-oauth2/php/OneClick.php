<?php
    class OneClick
    {
        const authUrl = "https://launchpad.classlink.com/oauth2/v2/auth";
        const exchangeUrl = "https://launchpad.classlink.com/oauth2/v2/token";
        const infoUrl = "https://nodeapi.classlink.com/";
        private $clientId;
        private $clientSecret;

        /**
         * ClassLink OneClick helper constructor.
         * @param string $client_id Client Identifier string.
         * @param string $client_secret Secret to go with the client_Id.
         */
        function __construct($client_id, $client_secret)
        {
            $this->clientId = $client_id;  
            $this->clientSecret = $client_secret;
        }

        /**
         * ClassLink Oauth2 helper destructor.
         */
        function __destruct()
        {
            unset($clientId);
            unset($clientSecret);
        }

        /**
         * Get the URL for where the client can get their access code
         * @param string $scope The scope part of the query string in the returned URL. There are 3 scopes that can be used:
         *      - profile:  Access to user identity and user specific information info.
         *      - oneroster: Access to oneroster info and classes.
         *      - full: Full access to all public APIs.
         * @param string $redirectUri Where the URL will take you upon providing correct login information.  Cannot be null, if left out will default to http://localhost:8080/code
         * @return string Returns a URL that the user can curl to or paste into a URL bar.
         * @throws Exception: The Client Id and Client Secret must be set for this function to run properly.
         */
        function getCodeUrl($scope = 'profile', $redirectUri = 'http://localhost:8080/code') {
            if(!isset($this->clientId) || !isset($this->clientSecret)) { throw new Exception("Client Id or Client Secret is not set.\n"); }
            return self::authUrl . '?' . http_build_query(
                array(
                    'client_id' => $this->clientId,
                    'scope' => $scope,
                    'redirect_uri' => $redirectUri,
                    'response_type' => 'code'
                )
            );
        }

        /**
         * Get the user's bearer token which will provide access to all other functions.
         * @param string $code will be returned as a part of the URL
         * @return string The bearer token that will be used in the Authorization header.
         * @throws Exception: The Client Id and Client Secret must be set for this function to run properly.
         */
        function getToken($code) {
            if(!isset($this->clientId) || !isset($this->clientSecret)) { throw new Exception("Client Id or Client Secret is not set.\n"); }
            $url = self::exchangeUrl;
            $req = curl_init($url);
            $body = http_build_query(
                array(
                    'client_id' => $this->clientId,
                    'client_secret' => $this->clientSecret,
                    'code' => $code
                )
            );

            curl_setopt($req, CURLOPT_POST, 1);
            curl_setopt($req, CURLOPT_POSTFIELDS, $body);
            curl_setopt($req, CURLOPT_RETURNTRANSFER, true);

            $jsonString = curl_exec($req);

            if (curl_error($req)) $error_msg = curl_error($req);
            curl_close($req);
            if (isset($error_msg)) throw new Exception($error_msg);

            $json = json_decode($jsonString);
            return $json->{'access_token'};
        }

        /**
         * Get information from the requested endpoint about the user(s) or district
         * @param string $bearer The bearer token returned by getToken($code).
         * @param string $endpoint The URI of the server checked for user validation.
         * @param string $extractNode The piece of user info returned.  If null or left out the entire JSON will be returned.
         * @throws Exception:
         *      - If extractNode is not on the specified request this function will throw an error.
         *      - If the curl request does not go through this function will throw an error.
         *      - If the the API end point sends an invalid response this will throw an error
         * @return string If extractNode is specified it will return the information in that node as a string.  If extractNode is not specified it will return the JSON encoded response as a string.
         */
        function getInfo($bearer, $endpoint, $extractNode = null) {
            $url = self::infoUrl . $endpoint;
            $req = curl_init($url);
            curl_setopt($req, CURLOPT_HTTPHEADER, array('Authorization: bearer ' . $bearer));
            curl_setopt($req, CURLOPT_RETURNTRANSFER, true);
            $resp = curl_exec($req);

            // error handling
            if (curl_error($req)) $error_msg = curl_error($req);
            curl_close($req);
            if (isset($error_msg)) throw new Exception($error_msg);
            if (isset($resp->error)) throw new Exception($resp);

            if($extractNode){
                $json = json_decode($resp);
                if(!isset($json->{$extractNode})){
                    throw new Exception("Error: extractNode does not exist on response.\n".$resp."\n".$endpoint."\n".$extractNode);
                }
                return json_encode($json->{$extractNode});
            }
            return $resp;
        }

        /**
         * Get information about the user as a JSON.
         * @param string $bearer The bearer token returned by getToken($code).
         * @return string Returns a JSON containing all of the available user info.
         * @throws Exception:
         *      - If extractNode is not on the specified request this function will throw an error.
         *      - If the curl request does not go through this function will throw an error.
         *      - If the the API end point sends an invalid response this will throw an error.
         */
        public function getUserInfo($bearer) {return $this->getInfo($bearer, "v2/my/info");}

        /**
         * Get the district associated with the user.
         * @param string $bearer The bearer token returned by getToken($code).
         * @return string Returns the district as a string
         * @throws Exception:
         *      - If extractNode is not on the specified request this function will throw an error.
         *      - If the curl request does not go through this function will throw an error.
         *      - If the the API end point sends an invalid response this will throw an error.
         */
        public function getUserDistrict($bearer) {return $this->getInfo($bearer, "v2/my/district");}

        /**
         * Get info on the user's profile(s).
         * @param string $bearer The bearer token returned by getToken($code).
         * @return string Returns a list of JSONs containing the profile(s) information as a string.
         * @throws Exception:
         *      - If extractNode is not on the specified request this function will throw an error.
         *      - If the curl request does not go through this function will throw an error.
         *      - If the the API end point sends an invalid response this will throw an error.
         */
        public function getUserProfiles($bearer) {return $this->getInfo($bearer, "v2/my/profiles");}

        /**
         * If the user is a parent who has registered for Classlink this will get a list of any linked student accounts aka their children.
         * @param string $bearer The bearer token returned by getToken($code).
         * @return string Returns a list JSONs containing all linked information on students. Returns an empty list if the user is not a parent.
         * @throws Exception:
         *      - If extractNode is not on the specified request this function will throw an error.
         *      - If the curl request does not go through this function will throw an error.
         *      - If the the API end point sends an invalid response this will throw an error.
         */
        public function getUserChildren($bearer) {return $this->getInfo($bearer, "v2/my/students");}

        /**
         * Get a list of all groups that the user is a part of.
         * @param string $bearer The bearer token returned by getToken($code).
         * @return string Returns a list JSONs containing the name, groupId, and buildingId of the group.
         * @throws Exception:
         *      - If extractNode is not on the specified request this function will throw an error.
         *      - If the curl request does not go through this function will throw an error.
         *      - If the the API end point sends an invalid response this will throw an error.
         */
        public function getUserGroups($bearer) {return $this->getInfo($bearer, "my/groups");}

        /**
         * Get all the oneRoster info about the user. Only available for districts with oneroster enabled.
         * @param string $bearer The bearer token returned by getToken($code).
         * @return string Returns a JSON containing the sourceId, status, date created or last modified, metdata, a list of orgs, role, username, a list of userIds, first name, last name, middle name, identifier, email, phone and cell numbers, agents, grades, and password.
         * @see https://developer.classlink.com/reference#v2onerostermyinfo
         * @throws Exception:
         *      - If extractNode is not on the specified request this function will throw an error.
         *      - If the curl request does not go through this function will throw an error.
         *      - If the the API end point sends an invalid response this will throw an error.
         */
        public function getUserOneRosterInfo($bearer) {return $this->getInfo($bearer, "v2/oneroster/my/info");}

        /**
         * Get all the oneRoster classes the user is enrolled in.  Only available for districts with oneroster enabled.
         * @param string $bearer The bearer token returned by getToken($code).
         * @return string Returns a JSON containing info listed below.
         * @see https://developer.classlink.com/reference#v2onerostermyclasses
         * @throws Exception:
         *      - If extractNode is not on the specified request this function will throw an error.
         *      - If the curl request does not go through this function will throw an error.
         *      - If the the API end point sends an invalid response this will throw an error.
         */
        public function getUserOneRosterClasses($bearer) {return $this->getInfo($bearer, "v2/oneroster/my/classes");}

        /**
         * Get info on the teacher of the class represented by a unique classSourcedId.  Only available for districts with oneroster enabled.
         * @param string $bearer The bearer token returned by getToken($code).
         * @param string $classSourcedId A string id representing the class gotten from getUserOneRosterClasses($bearer)
         * @return string Returns a list of JSONs containing info listed below.
         * @see https://developer.classlink.com/reference#v2onerostermyclassesclass_idteachers
         * @throws Exception:
         *      - If extractNode is not on the specified request this function will throw an error.
         *      - If the curl request does not go through this function will throw an error.
         *      - If the the API end point sends an invalid response this will throw an error.
         *      - If the classSourcedId is invalid this function will throw an error.
         */
        public function getUserOneRosterClassTeachers($bearer, $classSourcedId) {return $this->getInfo($bearer, "v2/oneroster/my/classes/" . $classSourcedId . "/teachers", "teachers");}

        /**
         * Get info on the students enrolled in a class.  Only available for districts with oneroster enabled.
         * @param string $bearer The bearer token returned by getToken($code).
         * @param string $classSourcedId A string id representing the class gotten from getUserOneRosterClasses($bearer)
         * @return string Returns a list of JSONs containing student info.
         * @see https://developer.classlink.com/reference#v2onerostermyclassesclass_idteachers-1
         * @throws Exception:
         *      - If extractNode is not on the specified request this function will throw an error.
         *      - If the curl request does not go through this function will throw an error.
         *      - If the the API end point sends an invalid response this will throw an error.
         *      - If the classSourcedId is invalid this function will throw an error.
         */
        public function getUserOneRosterClassStudents($bearer, $classSourcedId) {return $this->getInfo($bearer, "v2/oneroster/my/classes/" . $classSourcedId . "/students", "students");}
    }
?>
