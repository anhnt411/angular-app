using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using NG_Core_Auth.Helper;
using NG_Core_Auth.Models;

namespace NG_Core_Auth.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountController : Controller
    {
        private readonly UserManager<IdentityUser> _userManager;
        private readonly SignInManager<IdentityUser> _signManager;
        private readonly AppSettings _appSettings;

        public AccountController(UserManager<IdentityUser> userManager,SignInManager<IdentityUser> signManager,IOptions<AppSettings> appSetting)
        {
            this._userManager = userManager;
            this._signManager = signManager;
            this._appSettings = appSetting.Value;
        }
        [HttpPost("Register")]
        public async Task<IActionResult> Register([FromBody] RegisterViewModel formData)
        {
            // Hander error list
            List<string> errorList = new List<string>();

            var user = new IdentityUser
            {
                Email = formData.Email,
                UserName = formData.UserName,
                SecurityStamp = Guid.NewGuid().ToString()
            };

            var result = await _userManager.CreateAsync(user, formData.PassWord);
            if (result.Succeeded)
            {
                if(user.UserName == "admin")
                {
                    await _userManager.AddToRoleAsync(user, "Admin");
                }
                else
                {
                    await _userManager.AddToRoleAsync(user, "Customer");
                }
                

                //Sending confirm email
                return Ok(new
                {
                    username = user.UserName,
                    email = user.Email,
                    status = 1,
                    message = "Registration Successful"
                });
            }
            else
            {
                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError("", error.Description);
                    errorList.Add(error.Description);
                }
            }
            return BadRequest(new JsonResult(errorList));
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginViewModel formData)
        {
            try
            {
                var user = await _userManager.FindByNameAsync(formData.UserName);
                var roles = await _userManager.GetRolesAsync(user);
                var key = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(_appSettings.Secret));
                double tokenExpiriTime = Convert.ToDouble(_appSettings.ExpireTime);   
                if(user!=null && await _userManager.CheckPasswordAsync(user,formData.PassWord))
                {

                    //setting token

                    var tokenHandler = new JwtSecurityTokenHandler();
                    var tokenDescriptor = new SecurityTokenDescriptor()
                    {
                        Subject = new ClaimsIdentity(new Claim[]
                        {
                            new Claim(JwtRegisteredClaimNames.Sub, formData.UserName),
                            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                            new Claim(ClaimTypes.NameIdentifier, user.Id),
                            new Claim(ClaimTypes.Role, roles.FirstOrDefault()),
                            new Claim("LoggedOn", DateTime.Now.ToString())
                        }),
                        SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256Signature),
                        Issuer = _appSettings.Site,
                        Audience = _appSettings.Audience,
                        Expires = DateTime.UtcNow.AddMinutes(tokenExpiriTime)
                    };

                    // generate Token

                    var token = tokenHandler.CreateToken(tokenDescriptor);
                    

                    return Ok(new {token = tokenHandler.WriteToken(token),expiration = token.ValidTo,username = user.UserName,userRole = roles.FirstOrDefault() });
                }
                ModelState.AddModelError("", "UserName/PassWord not found");
                return Unauthorized(new { LoginError = "Please check login credential username and password" });
            }catch(Exception ex)
            {
                return BadRequest(new { ErrorMessage = "Login failed" });
            }
            
        }
    }
}