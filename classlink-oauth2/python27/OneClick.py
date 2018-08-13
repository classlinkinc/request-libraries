import sys
from urllib import urlencode
import requests
import json

#this loosk stupid but hey... constants in python!
class __CONST(object):
    @staticmethod
    def authUrl(): return "https://launchpad.classlink.com/oauth2/v2/auth"
    @staticmethod
    def exchangeUrl(): return "https://launchpad.classlink.com/oauth2/v2/token"
    @staticmethod
    def infoUrl(): return "https://nodeapi.classlink.com/"

class ConfigNotSetError(Exception):
    def __init__(self, value):
        self.value = "The config is not set.  You need to call setConfig(client_Id, client_Secret) before " + value
    def __str__(self):
        return repr(self.value)


clientId = None
clientSecret = None

def setConfig(client_Id, client_Secret):
    global clientId
    if isinstance(client_Id, basestring):
        clientId = client_Id
    else:
        print("clientId must be a string")
        sys.exit()
    global clientSecret
    if isinstance(client_Secret, basestring):
        clientSecret = client_Secret
    else:
        clientId = None
        print("clientSecret must be a string")
        sys.exit()

def getCodeURL(scope=None, redirectURI=None):
    if not clientId:
        raise ConfigNotSetError("getCodeURL")
    if scope is None:
        scope = "profile"
    if redirectURI is None:
        redirectURI = "http://localhost:8080/code"
    queryStr = urlencode({"client_id":clientId, "scope":scope, "redirect_uri":redirectURI, "response_type":"code"})
    return __CONST.authUrl() + "?" + queryStr

def getToken(code):
    if not clientId or not clientSecret:
        raise ConfigNotSetError("getToken")
    if not isinstance(code, basestring):
        raise TypeError("code must be a string")
    payload = {'client_id':clientId, 'client_secret':clientSecret, 'code':code}
    resp = requests.post(__CONST.exchangeUrl(), data=payload)
    return json.loads(resp.content)["access_token"]

def getInfo(bearer, endpoint, extractNode=None):
    if not isinstance(bearer, basestring):
        raise TypeError("code must be a string")
    if not isinstance(endpoint, basestring):
        raise TypeError("endpoint must be a string")
    if extractNode is not None and not isinstance(extractNode, list):
        raise TypeError("extractNode must be a list of strings")
    URL = __CONST.infoUrl() + endpoint
    headers = {'Authorization':'bearer ' + bearer}
    resp = requests.get(URL, headers=headers)
    print resp.text
    if extractNode is None:
        return json.loads(resp.content)
    else:
        content = json.loads(resp.content)
        rtn = dict()
        for node in extractNode:
            if node in content.keys():
                rtn[node] = content[node]
        return rtn

def getUserInfo(bearer):
    return getInfo(bearer,"v2/my/info")

def getUserDistrict(bearer):
    return getInfo(bearer, "v2/my/district")

def getUserProfiles(bearer):
    return getInfo(bearer, "v2/my/profiles")

def getUserChildren(bearer):
    return getInfo(bearer, "v2/my/students")

def getUserGroups(bearer):
    return getInfo(bearer, "my/groups")

def getUserOneRosterInfo(bearer):
    return getInfo(bearer, "v2/oneroster/my/info")

def getUserOneRosterClasses(bearer):
    return getInfo(bearer, "v2/oneroster/my/classes")

def getUserOneRosterClassTeachers(bearer, classSourcedId):
    return getInfo(bearer, "v2/oneroster/my/classes/"+classSourcedId+"/teachers", "teachers")

def getUserOneRosterClassStudents(bearer,classSourcedId):
    return getInfo(bearer, "v2/oneroster/my/classes/"+classSourcedId+"/students","students")

