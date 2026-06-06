using System.Net;
using System.Text.Json;

namespace NRE.SimAvatar;

public readonly record struct AvatarJsonHttpResponse(HttpStatusCode StatusCode, JsonDocument? Document)
{
    public bool IsSuccessStatusCode => (int)StatusCode is >= 200 and <= 299;
}
