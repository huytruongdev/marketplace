using Listings.CommandService.Infrastructure;
using Listings.CommandService.Infrastructure.Services;
using Microsoft.EntityFrameworkCore;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

builder.Services.AddDbContext<AppDbContext>(opt =>
{
    var cs = builder.Configuration.GetConnectionString("Postgres");
    opt.UseNpgsql(cs);
});

builder.Services.AddScoped<IListingCommandService, ListingCommandService>();

var app = builder.Build();

// MVP auto migrate (prod bạn có thể chuyển sang migrate lúc deploy)
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    await db.Database.MigrateAsync();
}

app.MapControllers();
app.Run();
