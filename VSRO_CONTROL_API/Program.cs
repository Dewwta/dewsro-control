using CoreLib.Tools.Logging;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using VSRO_CONTROL_API.Settings;
using VSRO_CONTROL_API.VSRO;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy;
using VSRO_CONTROL_API.VSRO.AsynchronousProxy.Achivements;
using VSRO_CONTROL_API.VSRO.Backup;
using VSRO_CONTROL_API.VSRO.ServerCfg;
using VSRO_CONTROL_API.VSRO.Tools;


var builder = WebApplication.CreateBuilder(args);

Logger.Init(new LoggerSettings
{
    OldestLogAllowedInDays = 30,
    CleanupTimerInHours = 48,
    SaveTimerInHours = 12
});
if (SettingsLoader.LoadSettings() == true)
{
    Logger.Info(typeof(Program), "Succesfully loaded Settings/settings.xml");
}
var dbUrl = builder.Configuration["DBUrl"];
if (!string.IsNullOrEmpty(dbUrl)) 
{
    await DBConnect.Initialize(dbUrl);
}
else
{
    Logger.Error(typeof(Program), $"You need to add your DBUrl to appsettings.json before continuing.");
}

await AgentTools.Init();
GameObjectNameResolver.Load(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "reference_data"));
InviteCodeStore.Load();


await RegionResolver.InitializeAsync();
if (!AchievementLoader.Load())
{
    Logger.Info(typeof(Program), "Achievement definitions failed to load — system disabled.");
}
else
{
    bool achReady = await AchievementService.Initialize();
    if (!achReady)
        Logger.Info(typeof(Program), "Achievement DB init failed — system disabled.");
}

Logger.SetDebug(SettingsLoader.Settings!.DebugMode);

await Overseer.Initialize();

// Initialize server.cfg parser
var rawCfgPath = SettingsLoader.Settings?.ServerCfgPath;
if (!string.IsNullOrWhiteSpace(rawCfgPath))
{
    var resolvedCfgPath = Path.IsPathRooted(rawCfgPath)
        ? rawCfgPath
        : Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), rawCfgPath));
    ServerCfgParser.Initialize(resolvedCfgPath);
}
else
{
    Logger.Warn(typeof(Program), "ServerCfgPath not set in settings.xml. Server rates will be unavailable.");
}

// Configure JWT Authentication
var jwtSecret = builder.Configuration["JwtSettings:Secret"];
if (!string.IsNullOrEmpty(jwtSecret))
{
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.ASCII.GetBytes(jwtSecret)),
                ValidateIssuer = false,
                ValidateAudience = false,
                ClockSkew = TimeSpan.Zero
            };
        });
}
else
{
    Logger.Warn(typeof(Program), "JwtSettings:Secret not configured. JWT authentication will not work.");
}

// Add services to the container.
builder.Services.AddCors(options =>
{
    options.AddPolicy("SvelteCors", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowAnyOrigin();
    });
});
builder.Services.AddHostedService<DatabaseBackupService>();
builder.Services.AddControllers()
    .AddJsonOptions(opts =>
        opts.JsonSerializerOptions.Converters.Add(
            new System.Text.Json.Serialization.JsonStringEnumConverter()));
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
try
{
    var ip = SettingsLoader.Settings!.Network!.IP;
    if (string.IsNullOrWhiteSpace(ip))
    {
        Logger.Warn(typeof(Program), "IP is not set correctly in settings, defaulting...");
        ip = "0.0.0.0";
    }
    var portHttp = SettingsLoader.Settings!.Network!.Port;
    var portHttps = SettingsLoader.Settings!.Network.Port! + 1;
    var httpsSettings = builder.Configuration.GetSection("HttpsSettings");
    var pfxPath = httpsSettings["pfxPath"];
    var pfxPassword = httpsSettings["PfxPassword"];



    builder.WebHost.ConfigureKestrel(options =>
    {
        // Optional HTTP
        options.Listen(System.Net.IPAddress.Parse(ip), portHttp);


        options.Listen(System.Net.IPAddress.Parse(ip), portHttps, listenOptions =>
        {
            listenOptions.UseHttps(pfxPath!, pfxPassword);
        });
    });

}
catch (Exception ex)
{
    Logger.Error(typeof(Program), $"Error configuring Kestrel: {ex.Message}");

    // Run on default localhost ports
    builder.WebHost.UseUrls("http://0.0.0.0:5085");
}
var app = builder.Build();
await DllBridge.Instance.StartAsync();
app.Lifetime.ApplicationStopping.Register(() => DllBridge.Instance.Stop());

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("SvelteCors");

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
