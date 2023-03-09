using System.Security.Cryptography;
using System.Text;
using API.Data;
using API.DTOs;
using API.Entities;
using API.Interfaces;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace API.Controllers
{
    public class AccountController : BaseApiController
    {   
        private readonly DataContext _context;
        private readonly ITokenService _tokenService;
        private readonly UserRepository _userRepository;
        public AccountController(DataContext context, ITokenService tokenService)
        {
            _tokenService = tokenService;
            _context = context;
        }
        
        [HttpPost("register")]
        public async Task<ActionResult<UserDto>> Register(RegisterDto registerDto)
        {
            using var hmac = new HMACSHA512();

            if(await UserExists(registerDto.Username)) return BadRequest("Username is taken");

            var user =new AppUser
            {
                UserName = registerDto.Username.ToLower(),
                PasswordHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(registerDto.Password)),
                PasswordSalt = hmac.Key
            };
            _context.User.Add(user);
            await _context.SaveChangesAsync();

            return new UserDto
            {
                Username = user.UserName,
                Token = _tokenService.CreateToken(user)
            };
        }

        [HttpPost("login")]
        public async Task<ActionResult<UserDto>> Login(LoginDto loginDto)
        {
            var user = await _context.User.Include(x=>x.Photos)          
                                        .SingleOrDefaultAsync(x=> x.UserName == loginDto.Username);

            if(user == null) return Unauthorized("Invalid Username");

            using var hmac = new HMACSHA512(user.PasswordSalt);

            var computedHash = hmac.ComputeHash(Encoding.UTF8.GetBytes(loginDto.Password));

            for(int i=0; i< computedHash.Length; i++)
            {
                if(computedHash[i]!= user.PasswordHash[i]) return Unauthorized("Invalid password");
            }
             return new UserDto
            {
                Username = user.UserName,
                Token = _tokenService.CreateToken(user),
                PhotoUrl = user.Photos.FirstOrDefault(x=>x.IsMain)?.Url
            };
        }

        private async Task<bool> UserExists(string username)
        {
            return await _context.User.AnyAsync(x=> x.UserName == username.ToLower());
        }
    }
}