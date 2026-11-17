using System.Security.Claims;
using CookShare.Data;
using CookShare.Helpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CookShare.Controllers;

public sealed class AccountAvatarViewComponent(ApplicationDbContext db) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync()
    {
        string? userId = UserClaimsPrincipal.FindFirstValue(ClaimTypes.NameIdentifier);
        string? avatar = userId == null ? null : await db.Users.AsNoTracking()
            .Where(user => user.Id == userId).Select(user => user.AvatarUrl)
            .SingleOrDefaultAsync(HttpContext.RequestAborted);
        return View("Default", ProfileContent.ImageSource(avatar) ?? ProfileContent.DefaultAvatar);
    }
}
