using System.Net;

namespace CsWebIopi
{
    public class CredentialedWebClient : WebClient
    {
        public CredentialedWebClient(string userid, string password)
        {
            base.Credentials = new NetworkCredential(userid, password);
        }
    }
}