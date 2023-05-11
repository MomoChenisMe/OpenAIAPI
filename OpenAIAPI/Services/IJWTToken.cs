using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using OpenAIAPI.Models.Partial;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using OpenAIAPI.Models;
using static Google.Apis.Auth.GoogleJsonWebSignature;

namespace OpenAIAPI.Services
{
    public interface IJWTToken
    {
        Task<string> GetJWTToken(Payload payload);
    }

    public class JWTToken : IJWTToken
    {
        private readonly OpenAIContext _dbContext;
        private readonly IConfiguration _configuration;

        public JWTToken(OpenAIContext dbContext, IConfiguration configuration)
        {
            _dbContext = dbContext;
            _configuration = configuration;
        }

        public async Task<string> GetJWTToken(Payload payload)
        {
            var existingUser = await _dbContext.TB_User.FirstOrDefaultAsync(u => u.AccountEmail == payload.Email);

            if (existingUser == null)
            {
                existingUser = new TB_User
                {
                    AccountEmail = payload.Email,
                    AccountPassword = Guid.NewGuid().ToString(),
                    UserName = payload.Name,
                    LoginType = LoginType.Google.ToString(),
                    HeadshotUrl = payload.Picture
                };

                _dbContext.TB_User.Add(existingUser);
                await _dbContext.SaveChangesAsync();
            }

            var claims = new[]
            {
                new Claim(JwtRegisteredClaimNames.Sub, payload.Subject),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(JwtRegisteredClaimNames.Iat, DateTimeOffset.Now.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64),
                new Claim("name", existingUser.UserName),
                new Claim("account", existingUser.AccountEmail),
                new Claim("email", existingUser.AccountEmail),
                new Claim("picture", existingUser.HeadshotUrl),
            };

            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration.GetValue<string>("JwtSettings:SignKey")));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration.GetValue<string>("JwtSettings:Issuer"),
                claims: claims,
                expires: DateTime.Now.AddDays(30),
                signingCredentials: creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
