using Microsoft.EntityFrameworkCore;
using AtlasRestaurantPOS.Web.Data;
using AtlasRestaurantPOS.Web.Models;
using AtlasRestaurantPOS.Web.Services.Auditoria;
using AtlasRestaurantPOS.Web.Services.Bootstrap;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<AtlasRestaurantDbContext>(options =>
    options.UseMySql(builder.Configuration.GetConnectionString("AtlasRestaurantConnection"),
        ServerVersion.AutoDetect(builder.Configuration.GetConnectionString("AtlasRestaurantConnection"))));

builder.Services.AddScoped<IPasswordHasher<Usuario>, PasswordHasher<Usuario>>();
builder.Services.AddScoped<IInitialSetupService, InitialSetupService>();
builder.Services.AddScoped<IAuditoriaService, AuditoriaService>();
builder.Services.AddHttpContextAccessor();

builder.Services.AddAuthentication("AtlasRestaurantCookie")
    .AddCookie("AtlasRestaurantCookie", options =>
    {
        options.Cookie.Name = "AtlasRestaurantCookie";
        options.LoginPath = "/Acceso/Login";
        options.AccessDeniedPath = "/Acceso/Denegado";
        options.Cookie.HttpOnly = true;
        options.Cookie.SameSite = SameSiteMode.Lax;
        options.SlidingExpiration = true;
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
        options.Events = new CookieAuthenticationEvents
        {
            OnRedirectToLogin = ctx => RespuestaRedirect(ctx, 401),
            OnRedirectToAccessDenied = ctx => RespuestaRedirect(ctx, 403)
        };
    });

builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

static Task RespuestaRedirect(RedirectContext<CookieAuthenticationOptions> contexto, int estatus)
{
    if (EsPeticionFetch(contexto.Request))
    {
        contexto.Response.StatusCode = estatus;
        return Task.CompletedTask;
    }

    contexto.Response.Redirect(contexto.RedirectUri);
    return Task.CompletedTask;
}

static bool EsPeticionFetch(HttpRequest request)
{
    return string.Equals(request.Headers["X-Requested-With"], "XMLHttpRequest", StringComparison.OrdinalIgnoreCase)
        || request.Headers.Accept.Any(a => a != null && a.Contains("application/json", StringComparison.OrdinalIgnoreCase));
}
