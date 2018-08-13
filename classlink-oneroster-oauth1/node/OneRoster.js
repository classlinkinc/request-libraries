const CryptoJs = require('crypto-js');
const request = require('request');

/**
 * Key and secret for the request
 * @constructor
 * @param {string} clientId
 * @param {string} clientSecret 
 */
function OneRoster(clientId, clientSecret) {
    this.clientId = clientId;
    this.clientSecret = clientSecret;
}

/**
 * Makes a GET request to the given url with the stored key and secret
 * @param {*} url  The url of the request
 * @param {*} callback   The callback to be executed after the requesta
 */
OneRoster.prototype.makeRosterRequest = function(url, callback) {

    // Generate timestamp and nonce
    let timestamp = time().toString();
    let nonce = generateNonce(timestamp.length);
    
    // Definte oauth params
    let oauth = {
        'oauth_consumer_key': this.clientId,
        'oauth_signature_method': 'HMAC-SHA256',
        'oauth_timestamp': timestamp,
        'oauth_nonce': nonce
    };

    // Create object with all the params from the url mixed with oauth params
    let urlPieces = url.split("?");
    let params;
    let query;
    if (urlPieces.length == 2) {
        let urlParams = paramsToObject(urlPieces[1]);
        query = Object.assign({}, urlParams);
        params = Object.assign(urlParams, oauth);
    } else {
        query = {};
        params = oauth;
    }
    
    // Generate oauth signature
    let baseInfo = buildBaseString(urlPieces[0], 'GET', params);
    let compositeKey = encodeURIComponent(this.clientSecret) + "&";
    let oauthSignature = CryptoJs.enc.Base64.stringify(CryptoJs.HmacSHA256(baseInfo, compositeKey));
    oauth.oauth_signature = oauthSignature;

    // Generate header and make request
    let authHeader = buildAuthorizationHeader(oauth);

    makeRequest(authHeader, urlPieces[0], query, callback);
};

/**
 * Builds an authorization header for the request
 * @param {object} oauthInfo The oauth header params
 * @returns Returns the authorization header for the request as a string
 */
let buildAuthorizationHeader = function(oauthInfo) {
    let result = 'OAuth ';
    values = [];
    for (let key in oauthInfo) {
        values.push(key + '="' + encodeURIComponent(oauthInfo[key]) + '"');
    }
    result += values.join(",");
    return result;
};

/**
 * Generates a random nonce
 * @param {number} length The length of the non
 * @returns The nonce as a string
 */
let generateNonce = function(length) {
    let text = "";
    let possible = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
  
    for (let i = 0; i < length; i++)
      text += possible.charAt(Math.floor(Math.random() * possible.length));
  
    return text;
  };

/**
 * Converts a string of url parameters into an object
 * @param {string} urlParams A string with the parameters from the URL
 * @returns An object with the parameters
 */
let paramsToObject = function(urlParams) {
    let params = urlParams.trim().split('&');
    let result = {};
    for(let i = 0; i < params.length; i++) {
        if (typeof(params[i]) == undefined) {
            break;
        }

        let value = decodeURIComponent(params[i]);
        let paramParts = value.split("=");
        if (paramParts.length == 2) {
            result[paramParts[0]] = paramParts[1];
        } else {
            result.filter = value.substr(7);
        }
    }
    return result;
};

/**
 * Sorts the keys of an object
 * @param {object} o the object to be sorted
 * @returns The object with its keys sorted
 */
let sortObject = function(o) {
    return Object.keys(o).sort().reduce((r, k) => (r[k] = o[k], r), {});
};

let buildBaseString = function(baseURI, method, params) {
    let r = [];
    params = sortObject(params);
    for (let key in params) {
        let value = params[key];
        value = encodeURIComponent(value);
        value = value.replace(/'/g, "%27");
        r.push(key + "=" + value);
    }

    return method + "&" + encodeURIComponent(baseURI) + "&" + encodeURIComponent(r.join("&"));

};

/**
 * Generates a timestamp
 * @returns a timestamp
 */
let time = function() {
    let timestamp = Math.floor(new Date().getTime() / 1000);
    return timestamp;
};

/**
 * Makes a GET request to the url with the given parameters and the generated authoriztaion header
 * @param {string} authHeader The authoriation header for the request
 * @param {string} url The base url for the request
 * @param {ojbect} urlParams The url params for the request
 * @param {function} callback The callback to be executed after the request
 */
let makeRequest = function(authHeader, url, urlParams, callback) {

    let options = { method: 'GET',
        url: url,
        qs: urlParams,
        headers: 
        { 'Authorization':  authHeader}
    };

    return request(options, function (error, response, body) {
        if (error) {
            callback(error, 0, body);
        } else {
            callback(error, response.statusCode, body);
        }
    });
};

module.exports = OneRoster;