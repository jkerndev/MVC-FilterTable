using CookieShop.Mvc.Data;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllersWithViews();
builder.Services.AddDbContext<CookieShopDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("CookieShop") ?? "Data Source=cookies.db"));

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

using (var scope = app.Services.CreateScope())
{
    var dbContext = scope.ServiceProvider.GetRequiredService<CookieShopDbContext>();
    CookieSeedData.Seed(dbContext);
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Cookies}/{action=Index}/{id?}")
    .WithStaticAssets();


app.Run();
