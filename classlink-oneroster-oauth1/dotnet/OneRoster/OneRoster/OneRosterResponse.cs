namespace ClassLink.OneRoster
{
    public class OneRosterResponse
    {
        public int StatusCode { get; }
        public string Response { get; }

        public OneRosterResponse(int statusCode, string response)
        {
            StatusCode = statusCode;
            Response = response;
        }
    }
}