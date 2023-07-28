import sys
from urllib.parse import urlencode
import requests
import json

class Const(object):
    def AUTH_URL(): return "https://launchpad.classlink.com/oauth2/v2/auth"
    def EXCHANGE_URL(): return "https://launchpad.classlink.com/oauth2/v2/token"
    def INFO_URL(): return "https://nodeapi.classlink.com/"

class ConfigNotSetError(Exception):
    def __init__(self, value):
        self.value = f"The config is not set. You need to call setConfig(client_Id, client_Secret) before {value}"
    def __str__(self):
        return repr(self.value)

client_id = None
client_secret = None

def set_config(client_Id, client_Secret):
    global client_id
    global client_secret

    if isinstance(client_Id, str):
        client_id = client_Id
    else:
        print("clientId must be a string")
        sys.exit()

    if isinstance(client_Secret, str):
        client_secret = client_Secret
    else:
        client_id = None
        print("clientSecret must be a string")
        sys.exit()

def get_code_url(scope = None, redirect_uri = None):
    global client_id

    if not client_id:
        raise ConfigNotSetError("getCodeURL")
    if scope is None:
        scope = "profile"
    if redirect_uri is None:
        redirect_uri = "http://localhost:8080/code"
    payload = {"client_id":client_id, "scope":scope, "redirect_uri":redirect_uri, "response_type":"code"}
    query_str = urlencode(payload)
    return f"{Const.AUTH_URL()}?{query_str}"

def get_token(code):
    global client_id
    global client_secret

    if not client_id or not client_secret:
        raise ConfigNotSetError("getToken")
    if not isinstance(code, str):
        raise TypeError("code must be a string")
    payload = {'client_id':client_id, 'client_secret':client_secret, 'code':code}
    resp = requests.post(Const.EXCHANGE_URL(), data=payload)
    return json.loads(resp.content)["access_token"]

def get_info(bearer, endpoint, extract_node=None):
    if not isinstance(bearer, str):
        raise TypeError("code must be a string")
    if not isinstance(endpoint, str):
        raise TypeError("endpoint must be a string")
    if extract_node is not None and not isinstance(extract_node, list):
        raise TypeError("extractNode must be a list of strings")
    URL = f"{Const.INFO_URL()}{endpoint}"
    headers = {'Authorization':f'bearer {bearer}'}
    resp = requests.get(URL, headers=headers)
    if extract_node is None:
        return json.loads(resp.content)
    else:
        content = json.loads(resp.content)
        rtn = dict()
        for node in extract_node:
            if node in list(content.keys()):
                rtn[node] = content[node]
        return rtn

def get_user_info(bearer):
    return get_info(bearer,"v2/my/info")

def get_user_district(bearer):
    return get_info(bearer, "v2/my/district")

def get_user_profiles(bearer):
    return get_info(bearer, "v2/my/profiles")

def get_user_children(bearer):
    return get_info(bearer, "v2/my/students")

def get_user_groups(bearer):
    return get_info(bearer, "my/groups")

def get_user_oneroster_info(bearer):
    return get_info(bearer, "v2/oneroster/my/info")

def get_user_oneroster_classes(bearer):
    return get_info(bearer, "v2/oneroster/my/classes")

def get_user_oneroster_class_teachers(bearer, class_sourced_id):
    return get_info(bearer, f"v2/oneroster/my/classes/{class_sourced_id}/teachers", "teachers")

def get_user_oneroster_class_students(bearer,class_sourced_id):
    return get_info(bearer, f"v2/oneroster/my/classes/{class_sourced_id}/students","students")