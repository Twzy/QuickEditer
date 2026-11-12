using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Components.Server.ProtectedBrowserStorage;
using Microsoft.JSInterop;
using System.Security.Claims;

namespace QuickEditer.Services
{
    public class CookieAuthStateProvider : AuthenticationStateProvider
    {
        private readonly IJSRuntime _js;
        private readonly AuthService _auth;

        public CookieAuthStateProvider(IJSRuntime js, AuthService auth)
        {
            _js = js;
            _auth = auth;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            try
            {
                var token = await _js.InvokeAsync<string>("quickEditer.getAuthCookie");
                if (_auth.ValidateAuthToken(token))
                {
                    var identity = new ClaimsIdentity(new[]
                    {
                    new Claim(ClaimTypes.Name, "admin")
                }, "QuickEditer");
                    return new AuthenticationState(new ClaimsPrincipal(identity));
                }
            }
            catch { }

            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity()));
        }

        public async Task MarkAuthenticatedAsync()
        {
            var token = _auth.GenerateAuthToken();
            await _js.InvokeVoidAsync("quickEditer.setAuthCookie", token, _auth.CookieDaysValue);
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }

        public async Task MarkLoggedOutAsync()
        {
            await _js.InvokeVoidAsync("quickEditer.clearAuthCookie");
            NotifyAuthenticationStateChanged(GetAuthenticationStateAsync());
        }
    }
}