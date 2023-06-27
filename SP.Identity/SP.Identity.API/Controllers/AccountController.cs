﻿using AutoMapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using SP.Identity.API.ViewModels;
using SP.Identity.BusinessLayer.DTOs;
using SP.Identity.BusinessLayer.Exceptions;
using SP.Identity.BusinessLayer.Interfaces;
using SP.Identity.DataAccessLayer.Models;

namespace SP.Identity.API.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    public class AccountController : ControllerBase
    {
        private readonly UserManager<User?> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly IAccountService _accountService;
        private readonly IMapper _mapper;

        public AccountController(
            UserManager<User?> userManager,
            SignInManager<User> signInManager,
            IAccountService accountService,
            IMapper mapper
            )
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _accountService = accountService;
            _mapper = mapper;
        }

        [HttpPut]
        public async Task<IActionResult> Register(UserRegisterDTO model)
        {
            if (!ModelState.IsValid) return BadRequest(model);

            if (await _accountService.CheckGivenEmailForExistingInDB(model.Email)) return Conflict(model);

            var user = _mapper.Map<User>(model);
            IdentityResult result = await _userManager.CreateAsync(user, model.Password);
            
            if (!result.Succeeded) return BadRequest(result);
            
            await _signInManager.SignInAsync(user, false);
            
            var viewModel = _mapper.Map<UserAuthenticationVM>(model);
            viewModel.UserId = _accountService.GetUserIDFromUserEmail(model.Email).Result.UserId;
            
            return Ok(viewModel);
        }

        [HttpPost]
        [Produces("application/json")]
        public async Task<IActionResult> Login(UserLoginDTO model)
        {
            var result =
                await _signInManager.PasswordSignInAsync(model.Email, model.Password, model.RememberMe, false);

            if (!result.Succeeded) return BadRequest(model);

            UserEmailIdDTO emailIdDTO = await _accountService.GetUserIDFromUserEmail(model.Email);
            var viewModel = _mapper.Map<UserAuthenticationVM>(model);
            viewModel.UserId = emailIdDTO.UserId;

            return Ok(viewModel);
        }

        [Authorize]
        [HttpGet]
        public async Task<UserEmailIdDTO> GetUserIdByEmail(string userEmail)
        {
            return await _accountService.GetUserIDFromUserEmail(userEmail);
        }

        //[Authorize]
        //[HttpGet]
        //public async Task<UserEmailIdDTO> GetUserEmailById(string userId)
        //{
        //    return await _accountService.GetUserEmailFromUserID(userId);
        //}

        [Authorize]
        [HttpPatch]
        public async Task<IActionResult> ChangeUserEmail(UserEmailIdDTO model)
        {
            //return await _accountService.SetNewUserEmail(model.Email!, model.UserId);

            try
            {
                User? user = await _accountService.GetUserById(model.UserId);

                if (user is null) throw new NotFoundException();

                var token = await _userManager.GenerateChangeEmailTokenAsync(user, model.Email);
                var result = await _userManager.ChangeEmailAsync(user, model.Email, token);
                if (result.Succeeded)
                {
                    await _userManager.SetUserNameAsync(user, model.Email);
                    return Ok(model);
                }

                return BadRequest(model);
            }
            catch (NotFoundException)
            {
                return NotFound(model);
            }
        }

        [Authorize]
        [HttpPatch]
        public async Task<IActionResult> ChangeUserPassword(UserPasswordIdDTO model)
        {
            try
            {
                User? user = await _accountService.GetUserById(model.UserId);

                if (user is null) throw new NotFoundException();

                var token = await _userManager.GeneratePasswordResetTokenAsync(user);
                var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);

                if (result.Succeeded)
                {
                    return Ok(model);
                }

                return BadRequest(model);
            }
            catch (NotFoundException)
            {
                return NotFound(model);
            }

        }

        [Authorize]
        [HttpGet]
        public async Task<IActionResult> GetUserById(string id)
        {
            try
            {
                User user = await _accountService.GetUserById(id);

                return Ok(_mapper.Map<UserBaseVM>(user));
            }
            catch (NotFoundException)
            {
                UserAuthenticationVM user = new()
                {
                    UserId = id
                };

                return BadRequest(user);
            }
            
        }
    }
}