package OneClick

import (
	"fmt"
	"net/url"
	"net/http"
	"io/ioutil"
	"encoding/json"
	"errors"
)

var authUrl = "https://launchpad.classlink.com/oauth2/v2/auth"
var exchangeUrl = "https://launchpad.classlink.com/oauth2/v2/token"
var infoUrl = "https://nodeapi.classlink.com/"

// OneClick is a struct containing the client_id an client_secret.
type OneClick struct {
	clientId string
	clientSecret string
}

// TokenResponse is a struct containing the info returned in a token response.
type TokenResponse struct {
	Access_Token string
	Token_Type string
	Response_Type string
	Id_Token string
}

// Create a new token response.
func New(clientId, clientSecret string) OneClick {
	oc := OneClick {clientId, clientSecret}
	return oc
}

// Get the url for the client to get their access code.
// The scope can be profile, oneroster, or full
// The scope will be defaulted to profile, and the redirectUri to localhost if set as empty strings
func (oc OneClick) GetCodeUrl(scope, redirectUri string) string {
	if scope == "" {
		scope = "profile"
	} 
	if redirectUri == "" {
		redirectUri="https://localhost:8080/code"
	}
	u, _ := url.Parse(authUrl)
	q := u.Query()
	q.Set("client_id", oc.clientId)
	q.Set("scope", scope)
	q.Set("redirect_uri", redirectUri)
	q.Set("response_type", "code")

	u.RawQuery = q.Encode()
	return(fmt.Sprint(u))
}

// Get the user's bearer token which will provide access to all other functions.
// It takes in the code from the url generated from GetCodeUrl(scope, redirectUri)
func (oc OneClick) GetToken(code string) string {
	u, _ := url.Parse(exchangeUrl)
	q := u.Query()
	q.Set("client_id", oc.clientId)
	q.Set("client_secret", oc.clientSecret)
	q.Set("code", code)
	u.RawQuery = q.Encode()

	hc := http.Client{}
	req, _ := http.NewRequest("POST", fmt.Sprint(u), nil)
	resp, err := hc.Do(req)
	if err != nil {
		return fmt.Sprint(err)
	}
	bodyBytes, _ := ioutil.ReadAll(resp.Body)
	var tr TokenResponse
	json.Unmarshal(bodyBytes, &tr)
	return tr.Access_Token
}

// Get information from the requested endpoing about the user(s) or district.GetInfo
// It takes in the bearer token, endpoint, and extractNode (set to "" if not wanted).
func (oc OneClick) GetInfo(bearer, endpoint, extractNode string) (string, error) {
	hc := http.Client{}
	req, _ := http.NewRequest("GET", infoUrl + endpoint, nil)

	req.Header.Add("Authorization", "Bearer " + bearer)
	resp, err := hc.Do(req)
	if (err != nil) {
		return "", errors.New(fmt.Sprint(err))
	}
	bodyBytes, _ := ioutil.ReadAll(resp.Body)
	if extractNode != "" {
		var f interface{}
		json.Unmarshal(bodyBytes, &f)

		switch x := f.(type) {
		case map[string]interface{}:
			value, ok := x[extractNode]
			if ok {
				ret, _ := json.Marshal(value)
				return string(ret), nil
			} else {
				return "", errors.New("Extract node does not exist on response\n" + string(bodyBytes) + "\n" + endpoint + "\n" + extractNode)
			}
		case []interface{}:
			m := x[0].(map[string]interface{})
			value, ok := m[extractNode]
			if ok {
				ret, _ := json.Marshal(value)
				return string(ret), nil
			} else {
				return "", errors.New("Extract node does not exist on response\n" + string(bodyBytes) + "\n" + endpoint + "\n" + extractNode)
			}
		default:
		}

	}
	
	return string(bodyBytes), nil
}

// Get information about the user as a JSON using bearer token from getToken(code)
func (oc OneClick) GetUserInfo(bearer string) (string, error) {
	return oc.GetInfo(bearer, "v2/my/info", "")
}

// Get the district associated with the user as a JSON using bearer token from getToken(code)
func (oc OneClick) GetDistrict(bearer string) (string, error) {
	return oc.GetInfo(bearer, "v2/my/district", "")
}

// Get information on the user's profile(s) as a JSON using bearer token from getToken(code)
func (oc OneClick) GetUserProfiles(bearer string) (string, error) {
	return oc.GetInfo(bearer, "v2/my/profiles", "")
}

// If the user is a parent who is registered for ClassLink, this will get a list of any linked student accounts (their children)
// Uses bearer token from getToken(code)
func (oc OneClick) GetUserChildren(bearer string) (string, error) {
	return oc.GetInfo(bearer, "v2/my/students", "")
}

// Get a list of all groups that the user is a part of using bearer token from getToken(code)
func (oc OneClick) GetUserGroups(bearer string) (string, error) {
	return oc.GetInfo(bearer, "my/groups", "")
}

// Get all the oneRoster info about the user. Only available for districts with oneroster enabled
// Uses bearer token from getToken(code)
func (oc OneClick) GetUserOneRosterInfo(bearer string) (string, error) {
	return oc.GetInfo(bearer, "v2/oneroster/my/info", "")
}

// Get all the oneRoster classes the user is enrolled in. Only available for districts with oneroster enabled
// Uses bearer token from getToken(code)
// See https://developer.classlink.com/reference#2onerostermyclasses
func (oc OneClick) GetUserOneRosterClasses(bearer string) (string, error) {
	return oc.GetInfo(bearer, "v2/oneroster/my/classes", "")
}

// Get info on the teacher of the class represented by a unique sourcedId. Only available for districts with oneroster enabled
// Uses bearer token from getToken(code)
// See https://developer.classlink.com/reference#2onerostermyclassesclass_idteachers
func (oc OneClick) GetUserOneRosterClassTeachers(bearer, sourcedId string) (string, error) {
	return oc.GetInfo(bearer, "v2/oneroster/my/classes/" + sourcedId + "/teachers", "teachers")
}

// Get info on the students enrolled in a class. Only available for districts with oneroster enabled
// Uses bearer token from getToken(code)
// See https://developer.classlink.com/reference#2onerostermyclassesclass_idteachers-1
func (oc OneClick) GetUserOneRosterClassStudents(bearer, sourcedId string) (string, error) {
	return oc.GetInfo(bearer, "v2/oneroster/my/classes/" + sourcedId + "/students", "students")
}