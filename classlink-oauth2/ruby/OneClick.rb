require 'net/http'
require 'json'

class OneClick
    attr_accessor :client_id
    attr_accessor :client_secret

    @@authUrl = "https://launchpad.classlink.com/oauth2/v2/auth"
    @@exchangeUrl = "https://launchpad.classlink.com/oauth2/v2/token"
    @@infoUrl = "https://nodeapi.classlink.com/"
    
    #
    # ClassLink Oauth2 helper
    #
    # @param [string] client_id     Client Identifier String
    # @param [string] client_secret Secret to go with the client_Id
    #
    def initialize(client_id, client_secret)
        @client_id = client_id
        @client_secret = client_secret
    end

    #
    # Get the URL that the client can use to get their access code
    #
    # @param [string] scope         The scope part of the query string in the returned URL. There are 3 scopes that can be used:
    #                                   -profile: Access to user identiy and user specific information 
    #                                   -oneroster: Access to oneroster info and classes
    #                                   -full: Access to all public apis
    # @param [string] redirect_uri  Where the url will take you upon login. It cannot be null and defaults to https://localhost:8080/code
    #
    # @return [string] Returns the url that the user can curl or put into url bar for the code
    #
    def get_code_url(scope = "profile", redirect_uri = "http://localhost:8080/code") 
        if @client_id == nil || @client_secret == nil
            raise "Client ID or Client Secret is not set \n"
        end
        queryString = {
            "client_id" => @client_id,
            "scope" => scope,
            "redirect_uri" => redirect_uri,
            "response_type" => "code"
        }
        return @@authUrl + "?" + URI.encode_www_form(queryString)
    end

    #
    # Get the user's bearer token which will provide access to all other functions
    #
    # @param [string] code  The code, which will be returned as part of the url
    #
    # @return [string]      The bearer token that will be used in the Authorization header
    #
    def get_token(code)
        begin
            if @client_id == nil || @client_secret == nil
                raise "Client ID or Client Secret is not set \n"
            end

            url = URI.parse(@@exchangeUrl)
            body = URI.encode_www_form({
                "client_id" => @client_id,
                "client_secret" => @client_secret,
                "code" => code
            })

            http = Net::HTTP.new(url.host, url.port)
            http.use_ssl = true
            request = Net::HTTP::Post.new(url.request_uri + "?" + body)

            response = http.request(request)
            if response.code == "200"
                decoded = JSON.parse(response.body)
                return decoded['access_token']
            else
                return response.body
            end
        rescue
            raise "An error occurred"
        end
    end

    #
    # Get information from the requested endpoint about the user(s) or district
    #
    # @param [string] bearer        The bearer token returned by get_token(code)
    # @param [string] endpoint      The URI of the server checked for user validation
    # @param [string] extractNode   The piece of user info returned. If null or left out the entire JSON response will be returned.
    #
    # @return [String]              If extract_node is specified it will return the information in that node as a string.
    #                               If extract_node is not specified, it will return the JSON encoded response as a string.
    #
    def get_info(bearer, endpoint, extract_node = nil)
        url = URI.parse(@@infoUrl + endpoint)
        req = Net::HTTP::Get.new(url.request_uri)
        req["Authorization"] = "Bearer " + bearer

        http = Net::HTTP.new(url.hostname, url.port)
        http.use_ssl = true
        response = http.request(req)
        
        if response.code != "200"
            return response.body
        end

        if extract_node != nil
            json = JSON.parse(response.body)
            if !json.key?(extract_node)
                raise "extract_node does not exist on response.\n" + response.body + "\n" + endpoint + "\n" + extract_node
            end
            return JSON.generate(json[extract_node], quirks_mode: true)
        end

        return response.body
    end

    #
    # Get information about the user as a JSON
    #
    # @param [string] bearer    The bearer token returned by get_token(code)
    #
    # @return [string]          Returns the JSON containing all of the available user info
    #
    def get_user_info(bearer) 
        return get_info(bearer, "v2/my/info")
    end

    #
    # Get the district associated with the user
    #
    # @param [string] bearer    The bearer token returned by get_token(code)
    #
    # @return [string]          Returns the the district as a string
    #
    def get_user_disctrict(bearer) 
        return get_info(bearer, "v2/my/district")
    end

    #
    # Get info on the user's profile(s).
    #
    # @param [string] bearer    The bearer token returned by get_token(code)
    #
    # @return [string]          Returns a list of JSONs containing the profile(s) inforamation as a string
    #
    def get_user_profiles(bearer) 
        return get_info(bearer, "v2/my/profiles")
    end

    #
    # If the user is a parent who has been registered for ClassLink, this will get a list of any linked student accounts (their children)
    #
    # @param [string] bearer    The bearer token returned by get_token(code)
    #
    # @return [string]          Returns a list of JSONs containing all information on linked students. Returns and empty list if the user is not a parent.
    #
    def get_user_children(bearer) 
        return get_info(bearer, "v2/my/students")
    end

    #
    # Get a list of all groups that the user is a part of
    #
    # @param [string] bearer    The bearer token returned by get_token(code)
    #
    # @return [string]          Returns a list of JSONs containing the name, groupId, and buildingId of the group
    #
    def get_user_groups(bearer) 
        return get_info(bearer, "my/groups")
    end
end
