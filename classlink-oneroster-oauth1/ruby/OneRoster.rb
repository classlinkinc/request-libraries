require 'cgi'
require 'uri'
require 'openssl'
require 'base64'
require 'net/http'
require 'net/https'

class OneRoster
    attr_accessor :client_id
    attr_accessor :client_secret

    def initialize(client_id, client_secret)
        @client_id = client_id
        @client_secret = client_secret
    end

    public

        #
        # Make a GET request to the given url with the stored key and secret
        #
        # @param [string] url   The url for the request
        #
        # @return [Hash]        A hash with the status_code and response
        #
        def make_roster_request(url)
            # Generate timestamp and nocne
            timestamp = (Time.now.to_f).to_i.to_s
            nonce = (0...8).map { (65 + rand(26)).chr }.join
            
            oauth = {
                "oauth_consumer_key" => @client_id,
                "oauth_signature_method" => "HMAC-SHA256",
                "oauth_timestamp" => timestamp,
                "oauth_nonce" => nonce
            }

            # Combine oauth params and url params
            params = deepcopy(oauth)
            url_pieces = url.split("?")
            if url_pieces.length == 2
                path_params = url_pieces[1]
                url_params = paramsToHash(url_pieces[1])
                params = params.merge(url_params)
            else 
                path_params = ""
            end
            
            # Generate the auth signature
            base_info = build_base_string(url_pieces[0], "GET", params)
            composite_key = CGI.escape(@client_secret) + "&"
            auth_signature = generate_auth_signature(base_info, composite_key)
            oauth["oauth_signature"] = auth_signature

            # Generate auth header and make the request
            auth_header = build_auth_header(oauth)
            return make_get_request(url, auth_header, path_params)
        end

    private

        #
        # Make a deep copy of the Hash
        #
        # @param [Hash] h     The Hash to copy
        #
        # @return [Hash]      The copied hash
        #
        def deepcopy(h)
            Marshal.load(Marshal.dump(h))
        end


        #
        # Convert the string of url params into a Hash
        #
        # @param [string] url_params    The string of url params
        #
        # @return [Hash]                The hash of url params
        #
        def paramsToHash(url_params)
            params = url_params.split("&")
            result = Hash.new

            params.each do |value|
                value = CGI.unescape(value)
                pieces = value.split("=")
                if pieces.length == 2
                    result[pieces[0]] = pieces[1]
                else 
                    result["filter"] = value[7..value.length]
                end
            end
            return result
        end


        #
        # Generate the base string for the generation of the auth signature
        #
        # @param [string] base_url      The url without the params
        # @param [string] method        The HTTP method
        # @param [Hash] params          The url and oauth params
        #
        # @return [string]              The base string for generating the auth signature
        #
        def build_base_string(base_url, method, params)
            r = Array.new
            params.keys.sort.each do |key|
                r.push("#{key}=#{CGI.escape(params[key])}".gsub("+", "%20"))
            end
            return method + "&" + CGI.escape(base_url) + "&" + CGI.escape(r.join("&"))
        end

        
        #
        # Generates the auth signature
        #
        # @param [string] base_info     The generated base string
        # @param [string] composite_key The key created from the secret
        #
        # @return [string]              The auth signature
        #
        def generate_auth_signature(base_info, composite_key)
            return Base64.encode64(OpenSSL::HMAC::digest(
                OpenSSL::Digest.new("sha256"), composite_key, base_info))
        end


        #
        # Generate the auth header from the oauth params
        #
        # @param [Hash] oauth   The Hash of the oauth params
        #
        # @return [string]      The auth header for the request
        #
        def build_auth_header(oauth)
            result = "OAuth "
            values = Array.new
            oauth.keys.each do |key|
                values.push("#{key}=\"#{CGI.escape(oauth[key])}\"")
            end
            values[values.length - 1] = values[values.length - 1][0..-5] + "\""
            result += values.join(",")
        end

        #
        # Makes the GET request to the url with the auth header
        #
        # @param [string] url       The base url without the params
        # @param [string] header    The auth header
        # @param [Hash] params      The url params
        #
        # @return [Hash]            A Hash of the status_code and response 
        #
        def make_get_request(url, header, params)
            begin
                uri = URI.parse(url)
                req = Net::HTTP::Get.new(uri.path + "?" + params)
                req["Authorization"] = header

                http = Net::HTTP.new(uri.hostname, uri.port) 
                http.use_ssl = true
                res = http.request(req)
                result = {"status_code" => res.code.to_i, "response" => res.body}
                return result
            rescue
                return {"status_code" => 0, "response" => "An error occurred, check your URL"}
            end
        end
end