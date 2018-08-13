package com.classlink.roster;

public class OneRosterResponse {
    private int statusCode;
    private String response;

    public int getStatusCode() {
        return statusCode;
    }

    public String getResponse() {
        return response;
    }

    public OneRosterResponse(int statusCode, String response) {
        this.statusCode = statusCode;
        this.response = response;
    }
}
