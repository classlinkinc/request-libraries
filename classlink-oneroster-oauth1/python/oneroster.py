import collections
import time
from random import randint
import urllib.parse
import hmac
import base64
import hashlib
import requests


class OneRoster(object):
    def __init__(self, client_id, client_secret):
        self._client_id = client_id
        self._client_secret = client_secret

    def make_roster_request(self, url):

        """
        make a request to a given url with the stored key and secret

        :param url:     The url for the request
        :return:        A dictionary containing the status_code and response
        """

        # Generate timestamp and nonce
        timestamp = str(int(time.time()))
        nonce = self.__generate_nonce(len(timestamp))

        # Define oauth params
        oauth = {
            'oauth_consumer_key': self._client_id,
            'oauth_signature_method': 'HMAC-SHA256',
            'oauth_timestamp': timestamp,
            'oauth_nonce': nonce
        }

        # Split the url into base url and params
        url_pieces = url.split("?")

        url_params = {}

        # Add the url params if they exist
        if len(url_pieces) == 2:
            url_params = self.__paramsToDict(url_pieces[1])
            all_params = self.__merge_dicts(oauth, url_params)
        else:
            all_params = oauth.copy()

        # Generate the auth signature
        base_info = self.__build_base_string(url_pieces[0], 'GET', all_params)
        composite_key = urllib.parse.quote_plus(self._client_secret) + "&"
        auth_signature = self.__generate_auth_signature(base_info, composite_key)
        oauth["oauth_signature"] = auth_signature

        # Generate the auth header
        auth_header = self.__build_auth_header(oauth)

        return self.__make_get_request(url_pieces[0], auth_header, url_params)




    def __merge_dicts(self, oauth, params):
        """
        Merge the oauth and param dictionaries

        :param oauth:       The oauth params
        :param params:      The url params
        :return:            A merged dictionary
        """
        result = oauth.copy()
        result.update(params)
        return result

    def __generate_nonce(self, nonce_len):
        """
        Generate a random nonce

        :param nonce_len:   Length of the nonce
        :return:            The nonce
        """
        characters = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz"
        result = ""
        for i in range (0, nonce_len):
            result += characters[randint(0, len(characters) - 1)]

        return result

    def __paramsToDict(self, url_params):
        """
        Convert the url params to a dict

        :param url_params:      The url params
        :return:                A dictionary of the url params
        """
        params = url_params.split("&")
        result = {}
        for value in params:
            value = urllib.parse.unquote(value)
            split = value.split("=")
            if len(split) == 2:
                result[split[0]] = split[1]
            else:
                result["filter"] = value[7:]
        return result

    def __build_base_string(self, baseurl, method, all_params):
        """
        Generate the base string for the generation of the oauth signature

        :param baseurl:     The base url
        :param method:      The HTTP method
        :param all_params:  The url and oauth params
        :return:            The base string for the generation of the oauth signature
        """
        result = []
        params = collections.OrderedDict(sorted(all_params.items()))
        for key, value in params.items():
            result.append(key + "=" + urllib.parse.quote(value))
        return method + "&" + urllib.parse.quote_plus(baseurl) + "&" + urllib.parse.quote_plus("&".join(result))

    def __generate_auth_signature(self, base_info, composite_key):
        """
        Generate the oauth signature

        :param base_info:       The base string generated from method, url, and params
        :param composite_key:   The componsite key of secret and &
        :return:                The oauth signature
        """
        digest = hmac.new(str.encode(composite_key), msg=str.encode(base_info), digestmod=hashlib.sha256).digest()
        return base64.b64encode(digest).decode()

    def __build_auth_header(self, oauth):
        """
        Generates the oauth header from the oauth params

        :param oauth:   The oauth params
        :return:        The oauth header for the request
        """
        result = "OAuth "
        values = []
        for key, value in oauth.items():
            values.append(key + "=\"" + urllib.parse.quote_plus(value) + "\"")

        result += ",".join(values)
        return result

    def __make_get_request(self, url, auth_header, url_params):
        """
        Make the get request

        :param url:             The base url of the request
        :param auth_header:     The auth header
        :param url_params:      The params from the url
        :return:                A dictionary of the status_code and response
        """

        try:
            r = requests.get(url=url, headers={"Authorization": auth_header}, params=url_params)
            return {"status_code": r.status_code, "response": r.text}
        except Exception as e:
            return {"status_code": 0, "response": "An error occurred, check your URL"}

