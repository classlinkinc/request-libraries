const _=require('underscore'),
      async=require("async"),
      querystring=require("querystring");
      request=require("request");
const config={
  authUrl:"https://launchpad.classlink.com/oauth2/v2/auth",
  exchangeUrl:"https://launchpad.classlink.com/oauth2/v2/token",
  infoUrl:"https://nodeapi.classlink.com/"
}
module.exports={
  /**
   * Adds the client information to the config.
   * @param iConfig A JSON object containing clientId and clientSecret.
   * @param cb The callback function.  Will be called if and only if a non-null
   *           callback function is provided.
   * @since release
   */
  setConfig:(iConfig,cb)=>{
    Object.assign(config,iConfig);
    if(cb)cb();
  },

  /**
   * Get the URL where the client can get their access code.
   * @before The function {@link #setConfig({clientId,clientSecret}[,cb]) } must
   *         be called for this to run.
   * @param scope The scope part of the query string in the returned URL. Cannot
   *              be null, will default to {@literal "profile"} if left out.
   * Scopes that can be used:
   * <li>{@link #profile}: Access to user info and identity.</li>
   * <li>{@link #oneroster}: Access to oneroster info and classes.</li>
   * <li>{@link #full}: Full access to all APIs.</li>
   * @param redirectUri Where the URL will take you upon correct login information.
   *                    Cannot be null, if left out will default to
   *                    {@value http://localhost:8080/code}. If provided a scope
   *                    must be provided as well
   * @param cb The callback function.  Uses error-first callback notation,
   *           Will retrun either an error or a string containing the URL.
   * @return Returns a plain text URL which can be used to octain an access code.
   * @since release
   */
  getCodeURL:(scope,redirectUri,cb)=>{
    if(!config.clientId)return new Error("clientId must be set with setConfig()");
    if(_.isFunction(scope)){
      cb=scope;
      scope="profile";
      redirectUri="http://localhost:8080/code";
    }else if(_.isFunction(redirectUri)){
      cb=redirectUri;
      redirectUri="http://localhost:8080/code";
    }else if(!_.isFunction(cb)){
      return new Error("Must provide callback")
    }
    var codeUrl=config.authUrl+"?"+querystring.stringify({
      client_id:config.clientId,
      scope:scope,
      redirect_uri:redirectUri,
      response_type:"code"
    });
    cb(null,codeUrl)
  },

  /**
   * Get the user's bearer token which will provide access to the other functions.
   * @param code The client access code obtained by
   * {@link #getCodeURL([scope,[redirectUri,]]cb) }.
   * @param cb The callback function.  Uses error-first callback notation,
   *           will retrun either an error or a JSON containing the bearer token
   *           and the date/time it wil expire as a unix timestamp.
   *           {@example {bearer: 'c15#########################################78', expireAt: 1520947160 }}
   * @return Returns a JSON containing the bearer token as a string and the
   *         date/time it wil expire as a unix timestamp.
   * @since release
   */
  getToken:(code,cb)=>{
    if(!config.clientId)return new Error("clientId must be set with setConfig()");
    if(!config.clientSecret)return new Error("clientSecret must be set with setConfig()");
    if(_.isFunction(code)){
      return cb(new Error("Must provide code"));
    }else if(!_.isFunction(cb)){
      return cb(new Error("Must provide callback"));
    }
    if(!code)return cb(new Error("Must Provide code"));
    async.autoInject({
      bearer:(cb)=>{
        request.post({url:config.exchangeUrl,form:{client_id:config.clientId,client_secret:config.clientSecret,code:code}},(err,httpResponse,body)=>{
          if(err)return cb(err);
          var data=JSON.parse(body);
          var tk=data.access_token;
          if(!tk)return cb(new Error(JSON.stringify(data)));
          cb(null,tk)
        });
      },
    },(err,data)=>{
      if(err){
        console.log(err)
        return cb(new Error("UnauthorizedError"));
      }
      data.expireAt=Math.floor(new Date()/1000)+(24*60*60);
      cb(null,data)
    })
  },

  /**
   * Get information about the user as a JSON.
   * @param bearer The bearer token returned by {@link #getToken(code,cb)}.
   * @param endpoint The URI of the server checked for user validation.
   * @param extractNode The peice of user info returned.  If null or left out
   *                    the entire JSON will be returned.
   * @param cb The callback function.  Uses error-first callback notation,
   *           will retrun either an error, a string containing the user info
   *           requested, or a JSON containing all of the available user info.
   * @return  Returns a string containing the user info requested, or a JSON
   *          containing all of the available user info.
   * @throws Must provide callback error: The last parameter must be a callback
   *         function.  It will be called with two parameters error and response.
   * @since release
   */
  getInfo:(bearer,endpoint,extractNode,cb)=>{
    if(_.isNull(bearer)||_.isFunction(bearer)){
      return cb(new Error("Must provide bearer"));
    }else if(_.isNull(endpoint)||_.isFunction(endpoint)){
      return cb(new Error("Must provide endpoint"));
    }else if(_.isFunction(extractNode)){
      cb=extractNode;
      extractNode=null;
    }else if(_.isNull(cb)||!_.isFunction(cb)){
      throw new Error("Must provide callback");
    }
    var url=config.infoUrl+endpoint;
    request.get({url:url,headers:{authorization:"Bearer "+bearer}},(err,httpResponse,body)=>{
      if(err)return cb(err);
      if(httpResponse.statusCode>=500){
        return cb(new Error("Server Error: "+httpResponse.statusCode+":"+url));
      }else if(httpResponse.statusCode==401){
        try{
          var message=JSON.parse(body);
          if(message["message"])message=message["message"];
          return cb(new Error("Client Error: "+httpResponse.statusCode+": "+message));
        }catch(e){
          return cb(new Error("Client Error: Error parsing JSON: "+body));
        }
      }else if(httpResponse.statusCode>=400){
        return cb(new Error("Client Error: "+httpResponse.statusCode+": "+body));
      }else if(httpResponse.statusCode>=300){
        return cb(new Error("Redirection: "+httpResponse.statusCode+": "+body));
      }
      try{
        var data=JSON.parse(body);
        if(extractNode)data=data[extractNode];
        cb(null,data)
      }catch(e){
        return cb(new SyntaxError("Error parsing JSON: "+body));
      }
    });
  },

  /**
   * Get information about the user as a JSON.
   * @param bearer The bearer token returned by {@link #getToken(code,cb) }.
   * @param cb The callback function.  Uses error-first callback notation,
   *           will retrun either an error or a JSON containing the user info.
   * @return Returns a JSON containing all of the available user info.
   * @since release
   * @see https://developer.classlink.com/reference#v2myinfo
   */
  getUserInfo:(bearer,cb)=>{
    module.exports.getInfo(bearer,"v2/my/info",cb);
  },

  /**
   * Get the user's district.
   * @param bearer The bearer token returned by {@link #getToken(code,cb)}.
   * @param cb The callback function.  Uses error-first callback notation,
   *           will retrun either an error or the district as a string.
   * @return Returns the district as a string.
   * @see https://developer.classlink.com/reference#v2mydistrict
   * @since release
   */
  getUserDistrict:(bearer,cb)=>{
    module.exports.getInfo(bearer,"v2/my/district",cb);
  },

  /**
   * Get info on the user's profile(s).
   * @param bearer The bearer token returned by {@link #getToken(code,cb)}.
   * @param cb The callback function.  Uses error-first callback notation,
   *           will retrun either an error or a list of JSONs containing the
   *           profile(s) information.
   * @return Returns a list of JSONs containing the profile(s) information.
   * @see https://developer.classlink.com/reference#v2myprofiles
   * @since release
   */
  getUserProfiles:(bearer,cb)=>{
    module.exports.getInfo(bearer,"v2/my/profiles",cb);
  },

  /**
   * If the users is a parent who has registered for ClassLink, this will get
   * a list of any linked student accounts.
   * @param bearer The bearer token returned by {@link #getToken(code,cb)}.
   * @param cb The callback function.  Uses error-first callback notation,
   *           will retrun either an error or a JSON containing the student
   *           information.
   * @return Returns a list JSONs containing all linked information on students.
   *         Returns an empty list if the user is not a parent.
   * @see https://developer.classlink.com/reference#v2mystudents
   * @since release
   */
  getUserChildren:(bearer,cb)=>{
    module.exports.getInfo(bearer,"v2/my/students","response",cb);
  },

  /**
   * Get a list of all groups that the user is a part of.
   * @param bearer The bearer token returned by {@link #getToken(code,cb)}.
   * @param cb The callback function.  Uses error-first callback notation,
   *           will retrun either an error or a list JSONs containing the name,
   *           groupId, and buildingId of the group.
   * @return Returns a list JSONs containing the name, groupId, and buildingId
   *         of the group.
   * @see https://developer.classlink.com/reference#mygroups
   * @since release
   */
  getUserGroups:(bearer,cb)=>{
    module.exports.getInfo(bearer,"my/groups",cb);
  },
}
