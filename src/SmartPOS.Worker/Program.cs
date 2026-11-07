using SmartPOS.Infrastructure;
using SmartPOS.Infrastructure.Services;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddInfrastructure(builder.Configuration);
builder.Services.AddAuthentication();
builder.Services.AddAuthorization();
builder.Services.AddHostedService<CustomerBirthdayWorker>();
var host = builder.Build();
host.Run();
