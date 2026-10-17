namespace WolfConquer.Core.Web;

public sealed class LoginLoaderHttpResponse
{
    public LoginLoaderHttpResponse(int statusCode, string reasonPhrase, string contentType, byte[] body)
    {
        StatusCode = statusCode;
        ReasonPhrase = reasonPhrase;
        ContentType = contentType;
        Body = body.ToArray();
    }

    public int StatusCode { get; }

    public string ReasonPhrase { get; }

    public string ContentType { get; }

    public byte[] Body { get; }
}
