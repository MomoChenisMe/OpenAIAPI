using Google.Apis.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using OpenAIAPI.Models.Partial;
using OpenAIAPI.Services;

namespace OpenAIAPI.Controllers
{
    [Route("[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IJWTToken _jwtToken;
        private readonly IConfiguration _configuration;
        private readonly string authRedirectUrl = "";

        public AuthController(IJWTToken jwtToken, IConfiguration configuration)
        {
            _jwtToken = jwtToken;
            _configuration = configuration;
            authRedirectUrl = _configuration.GetValue<string>("JwtSettings:AuthRedirectURI");
        }

        /// <summary>
        /// 用於驗證Google Sign In的API
        /// </summary>
        /// <param name="googleOAuth"></param>
        /// <returns></returns>
        [HttpPost("GoogleSignIn", Name = nameof(GetGoogleSignIn))]
        [AllowAnonymous]
        public async Task<IActionResult> GetGoogleSignIn([FromForm] GoogleOAuthModel googleOAuth)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(googleOAuth.credential);
                if (payload.Audience.ToString() != _configuration.GetValue<string>("JwtSettings:GoogleClientId"))
                {
                    return BadRequest("錯誤的IdToken");
                }
                var openAIAPItoken = await _jwtToken.GetJWTToken(payload);

                // 驗證成功，處理登入請求，創建用戶會話等
                return Redirect($"{authRedirectUrl}?Token={openAIAPItoken}");
            }
            catch (InvalidJwtException)
            {
                // 驗證失敗，返回適當的錯誤訊息
                return BadRequest();
            }
        }

        /// <summary>
        /// 用於驗證Google OpenID Connect
        /// </summary>
        /// <param name="idToken"></param>
        /// <returns></returns>
        [HttpGet("GoogleOpenIDConnect/{idToken}", Name = nameof(GetGoogleOpenIDConnect))]
        [AllowAnonymous]
        public async Task<IActionResult> GetGoogleOpenIDConnect(string idToken)
        {
            try
            {
                var payload = await GoogleJsonWebSignature.ValidateAsync(idToken);
                if (payload.Audience.ToString() != _configuration.GetValue<string>("JwtSettings:GoogleClientId"))
                {
                    return BadRequest("錯誤的IdToken");
                }
                var openAIAPItoken = await _jwtToken.GetJWTToken(payload);

                // 驗證成功，處理登入請求，創建用戶會話等
                return Ok(new { token = openAIAPItoken });
            }
            catch (InvalidJwtException)
            {
                // 驗證失敗，返回適當的錯誤訊息
                return BadRequest();
            }
        }
    }
}
