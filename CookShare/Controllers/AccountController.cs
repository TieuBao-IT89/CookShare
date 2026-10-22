using CookShare.Models.Identity;
using CookShare.Services;
using CookShare.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;

namespace CookShare.Controllers
{
    public class AccountController : Controller
    {
        public const string PasswordResetNotice =
            "Nếu email đã được đăng ký, bạn sẽ nhận hướng dẫn đặt lại mật khẩu.";

        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly IAccountEmailSender _emailSender;
        private readonly AccountDeletionService _accountDeletion;

        public AccountController(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            IAccountEmailSender emailSender,
            AccountDeletionService accountDeletion)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _emailSender = emailSender;
            _accountDeletion = accountDeletion;
        }

        // GET: /Account/Register
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Register()
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        // POST: /Account/Register
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(
            RegisterViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = new ApplicationUser
            {
                UserName = model.UserName.Trim(),
                Email = model.Email.Trim(),
                DisplayName =
                    $"{model.LastName.Trim()} {model.FirstName.Trim()}"
            };

            var result = await _userManager.CreateAsync(
                user, model.Password);

            if (!result.Succeeded)
            {
                AddErrors(result);
                return View(model);
            }

            var sent = await TrySendConfirmationAsync(user);
            await _signInManager.SignInAsync(
                user, isPersistent: false);

            TempData["StatusMessage"] = sent
                ? "Đã tạo tài khoản. Hãy kiểm tra email để xác minh địa chỉ."
                : "Đã tạo tài khoản. Bạn có thể xác minh email sau trong phần tài khoản.";

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/Login
        [HttpGet]
        [AllowAnonymous]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity?.IsAuthenticated == true)
            {
                return RedirectToAction("Index", "Home");
            }

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        // POST: /Account/Login
        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(
            LoginViewModel model,
            string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var input = model.UserNameOrEmail.Trim();

            var user = input.Contains('@')
                ? await _userManager.FindByEmailAsync(input)
                : await _userManager.FindByNameAsync(input);

            user ??= await _userManager.FindByNameAsync(input);

            if (user == null)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Tên đăng nhập hoặc mật khẩu không chính xác.");

                return View(model);
            }

            var result =
                await _signInManager.PasswordSignInAsync(
                    user,
                    model.Password,
                    model.RememberMe,
                    lockoutOnFailure: true);

            if (result.Succeeded)
            {
                if (!string.IsNullOrEmpty(returnUrl) &&
                    Url.IsLocalUrl(returnUrl))
                {
                    return LocalRedirect(returnUrl);
                }

                return RedirectToAction("Index", "Home");
            }

            if (result.IsLockedOut)
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Tài khoản tạm thời bị khóa do đăng nhập sai nhiều lần.");
            }
            else
            {
                ModelState.AddModelError(
                    string.Empty,
                    "Tên đăng nhập hoặc mật khẩu không chính xác.");
            }

            return View(model);
        }

        // POST: /Account/Logout
        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await _signInManager.SignOutAsync();

            return RedirectToAction("Index", "Home");
        }

        // GET: /Account/AccessDenied
        [HttpGet]
        public IActionResult AccessDenied()
        {
            return Content("Bạn không có quyền truy cập.");
        }

        // GET: /Account/Settings
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Settings()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToLogin();
            }

            return View(BuildSettings(user));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateDisplayName(
            [Bind(Prefix = "DisplayNameForm")] DisplayNameInput model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToLogin();
            }

            if (!ModelState.IsValid)
            {
                ViewData["ActiveForm"] = "display";
                return View(
                    "Settings",
                    BuildSettings(user, displayName: model));
            }

            user.DisplayName = model.DisplayName.Trim();
            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                AddErrors(result);
                ViewData["ActiveForm"] = "display";
                return View(
                    "Settings",
                    BuildSettings(user, displayName: model));
            }

            TempData["StatusMessage"] = "Đã cập nhật tên hiển thị.";
            return RedirectToAction(nameof(Settings));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangeEmail(
            [Bind(Prefix = "EmailForm")] ChangeEmailInput model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToLogin();
            }

            ForgetPostedPasswords();

            if (!ModelState.IsValid)
            {
                ViewData["ActiveForm"] = "email";
                return View(
                    "Settings",
                    BuildSettings(user, email: model));
            }

            var newEmail = model.NewEmail.Trim();
            var normalized = _userManager.NormalizeEmail(newEmail);

            if (string.Equals(
                    user.NormalizedEmail,
                    normalized,
                    StringComparison.Ordinal))
            {
                ModelState.AddModelError(
                    "EmailForm.NewEmail",
                    "Email mới trùng email hiện tại.");
            }
            else if (!await _userManager.CheckPasswordAsync(
                    user, model.CurrentPassword))
            {
                ModelState.AddModelError(
                    "EmailForm.CurrentPassword",
                    "Mật khẩu hiện tại không chính xác.");
            }
            else
            {
                var existing =
                    await _userManager.FindByEmailAsync(newEmail);

                if (existing != null && existing.Id != user.Id)
                {
                    ModelState.AddModelError(
                        "EmailForm.NewEmail",
                        "Email này đã được sử dụng.");
                }
            }

            if (!ModelState.IsValid)
            {
                ViewData["ActiveForm"] = "email";
                model.CurrentPassword = string.Empty;
                return View(
                    "Settings",
                    BuildSettings(user, email: model));
            }

            var sent = await TrySendChangeEmailAsync(user, newEmail);
            TempData[sent ? "StatusMessage" : "ErrorMessage"] = sent
                ? "Hãy mở liên kết vừa gửi tới email mới để hoàn tất đổi địa chỉ."
                : "Chưa gửi được email xác nhận. Hãy thử lại sau.";

            return RedirectToAction(nameof(Settings));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(
            [Bind(Prefix = "PasswordForm")] ChangePasswordInput model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToLogin();
            }

            ForgetPostedPasswords();

            if (!ModelState.IsValid)
            {
                ViewData["ActiveForm"] = "password";
                return View(
                    "Settings",
                    BuildSettings(user, password: model));
            }

            var result = await _userManager.ChangePasswordAsync(
                user,
                model.CurrentPassword,
                model.NewPassword);

            if (!result.Succeeded)
            {
                AddErrors(result);
                ViewData["ActiveForm"] = "password";
                return View(
                    "Settings",
                    BuildSettings(user));
            }

            await _signInManager.RefreshSignInAsync(user);
            TempData["StatusMessage"] = "Đã đổi mật khẩu.";
            return RedirectToAction(nameof(Settings));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendEmailConfirmation()
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToLogin();
            }

            if (user.EmailConfirmed)
            {
                TempData["StatusMessage"] = "Email đã được xác minh.";
                return RedirectToAction(nameof(Settings));
            }

            var sent = await TrySendConfirmationAsync(user);
            TempData[sent ? "StatusMessage" : "ErrorMessage"] = sent
                ? "Đã gửi lại email xác minh."
                : "Chưa gửi được email xác minh. Hãy thử lại sau.";

            return RedirectToAction(nameof(Settings));
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteAccount(
            [Bind(Prefix = "DeleteForm")] DeleteAccountInput model)
        {
            var user = await GetCurrentUserAsync();
            if (user == null)
            {
                return RedirectToLogin();
            }

            ForgetPostedPasswords();

            if (!model.Confirm)
            {
                ModelState.AddModelError(
                    "DeleteForm.Confirm",
                    "Hãy xác nhận rằng bạn muốn xóa tài khoản.");
            }

            if (!ModelState.IsValid)
            {
                ViewData["ActiveForm"] = "delete";
                return View(
                    "Settings",
                    BuildSettings(user, delete: model));
            }

            if (!await _userManager.CheckPasswordAsync(
                    user, model.Password))
            {
                ModelState.AddModelError(
                    "DeleteForm.Password",
                    "Mật khẩu không chính xác.");
                ViewData["ActiveForm"] = "delete";
                return View(
                    "Settings",
                    BuildSettings(user));
            }

            var result = await _accountDeletion.DeleteAsync(user);

            if (!result.Succeeded)
            {
                AddErrors(result);
                ViewData["ActiveForm"] = "delete";
                return View(
                    "Settings",
                    BuildSettings(user));
            }

            await _signInManager.SignOutAsync();
            TempData["StatusMessage"] = "Đã xóa tài khoản.";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPassword()
        {
            return View(new ForgotPasswordInput());
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(
            ForgotPasswordInput model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByEmailAsync(
                model.Email.Trim());

            if (user != null)
            {
                await TrySendPasswordResetAsync(user);
            }

            return RedirectToAction(nameof(ForgotPasswordConfirmation));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ForgotPasswordConfirmation()
        {
            return Notice(
                "Kiểm tra email",
                PasswordResetNotice);
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPassword(
            string? userId,
            string? code)
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(code))
            {
                return Notice(
                    "Liên kết không dùng được",
                    "Liên kết không hợp lệ hoặc đã hết hạn.");
            }

            return View(new ResetPasswordInput
            {
                UserId = userId,
                Code = code
            });
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(
            ResetPasswordInput model)
        {
            ForgetPostedPasswords();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.UserId);
            IdentityResult result;

            if (user == null)
            {
                result = IdentityResult.Failed(
                    _userManager.ErrorDescriber.InvalidToken());
            }
            else
            {
                result = await _userManager.ResetPasswordAsync(
                    user,
                    model.Code,
                    model.Password);
            }

            if (!result.Succeeded)
            {
                AddErrors(result);
                return View(model);
            }

            if (user != null && !user.EmailConfirmed)
            {
                user.EmailConfirmed = true;
                await _userManager.UpdateAsync(user);
            }

            return RedirectToAction(nameof(ResetPasswordConfirmation));
        }

        [HttpGet]
        [AllowAnonymous]
        public IActionResult ResetPasswordConfirmation()
        {
            return Notice(
                "Đã đặt lại mật khẩu",
                "Bạn có thể đăng nhập bằng mật khẩu mới.");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmail(
            string? userId,
            string? code)
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(code))
            {
                return Notice(
                    "Không xác minh được email",
                    "Liên kết không hợp lệ hoặc đã hết hạn.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Notice(
                    "Không xác minh được email",
                    "Liên kết không hợp lệ hoặc đã hết hạn.");
            }

            var result = await _userManager.ConfirmEmailAsync(
                user, code);

            if (!result.Succeeded)
            {
                return Notice(
                    "Không xác minh được email",
                    "Liên kết không hợp lệ hoặc đã hết hạn.");
            }

            return Notice(
                "Đã xác minh email",
                "Địa chỉ email của tài khoản đã được xác minh.");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> ConfirmEmailChange(
            string? userId,
            string? email,
            string? code)
        {
            if (string.IsNullOrWhiteSpace(userId) ||
                string.IsNullOrWhiteSpace(email) ||
                string.IsNullOrWhiteSpace(code))
            {
                return Notice(
                    "Không đổi được email",
                    "Liên kết không hợp lệ hoặc đã hết hạn.");
            }

            var user = await _userManager.FindByIdAsync(userId);
            if (user == null)
            {
                return Notice(
                    "Không đổi được email",
                    "Liên kết không hợp lệ hoặc đã hết hạn.");
            }

            var result = await _userManager.ChangeEmailAsync(
                user, email, code);

            if (!result.Succeeded)
            {
                return Notice(
                    "Không đổi được email",
                    "Liên kết không hợp lệ hoặc đã hết hạn.");
            }

            var current = await _userManager.GetUserAsync(User);
            if (current?.Id == user.Id)
            {
                await _signInManager.RefreshSignInAsync(user);
            }

            return Notice(
                "Đã đổi email",
                "Địa chỉ email của tài khoản đã được cập nhật.");
        }

        private async Task<ApplicationUser?> GetCurrentUserAsync()
        {
            return await _userManager.GetUserAsync(User);
        }

        private RedirectToActionResult RedirectToLogin()
        {
            return RedirectToAction(nameof(Login));
        }

        private ViewResult Notice(string title, string message)
        {
            return View(
                "Notice",
                new AccountNoticeViewModel
                {
                    Title = title,
                    Message = message
                });
        }

        private static AccountSettingsViewModel BuildSettings(
            ApplicationUser user,
            DisplayNameInput? displayName = null,
            ChangeEmailInput? email = null,
            ChangePasswordInput? password = null,
            DeleteAccountInput? delete = null)
        {
            return new AccountSettingsViewModel
            {
                UserName = user.UserName ?? string.Empty,
                Email = user.Email ?? string.Empty,
                EmailConfirmed = user.EmailConfirmed,
                DisplayName = user.DisplayName ?? string.Empty,
                DisplayNameForm = displayName ?? new DisplayNameInput
                {
                    DisplayName = user.DisplayName ?? string.Empty
                },
                EmailForm = email ?? new ChangeEmailInput(),
                PasswordForm = password ?? new ChangePasswordInput(),
                DeleteForm = delete ?? new DeleteAccountInput()
            };
        }

        private async Task<bool> TrySendConfirmationAsync(
            ApplicationUser user)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return false;
            }

            var code = await _userManager
                .GenerateEmailConfirmationTokenAsync(user);
            var url = ActionUrl(
                nameof(ConfirmEmail),
                new { userId = user.Id, code });

            if (url == null)
            {
                return false;
            }

            var message = AccountEmailMessages.ConfirmEmail(url);
            return await TrySendAsync(
                user.Email, message.Subject, message.Html);
        }

        private async Task<bool> TrySendChangeEmailAsync(
            ApplicationUser user,
            string newEmail)
        {
            var code = await _userManager
                .GenerateChangeEmailTokenAsync(user, newEmail);
            var url = ActionUrl(
                nameof(ConfirmEmailChange),
                new { userId = user.Id, email = newEmail, code });

            if (url == null)
            {
                return false;
            }

            var message = AccountEmailMessages.ChangeEmail(url);
            return await TrySendAsync(
                newEmail, message.Subject, message.Html);
        }

        private async Task TrySendPasswordResetAsync(
            ApplicationUser user)
        {
            if (string.IsNullOrWhiteSpace(user.Email))
            {
                return;
            }

            var code = await _userManager
                .GeneratePasswordResetTokenAsync(user);
            var url = ActionUrl(
                nameof(ResetPassword),
                new { userId = user.Id, code });

            if (url == null)
            {
                return;
            }

            var message = AccountEmailMessages.PasswordReset(url);
            await TrySendAsync(
                user.Email, message.Subject, message.Html);
        }

        private string? ActionUrl(string action, object values)
        {
            return Url.Action(action, "Account", values, Request.Scheme);
        }

        private async Task<bool> TrySendAsync(
            string email,
            string subject,
            string html)
        {
            try
            {
                await _emailSender.SendAsync(email, subject, html);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private void AddErrors(IdentityResult result)
        {
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(
                    string.Empty, error.Description);
            }
        }

        private void ForgetPostedPasswords()
        {
            foreach (var key in ModelState.Keys.ToList())
            {
                if (!key.Contains(
                        "Password",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                ModelState.SetModelValue(
                    key,
                    new ValueProviderResult(string.Empty));
            }
        }
    }
}
