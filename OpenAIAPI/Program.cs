using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.ML;
using OpenAIAPI.Models;
using Serilog;
using Microsoft.OpenApi.Models;
using System.Reflection;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using OpenAIAPI.Services;
using OpenAIAPI.Filters;

var builder = WebApplication.CreateBuilder(args);

//建立 SeqLog
Log.Logger = new LoggerConfiguration().ReadFrom.Configuration(builder.Configuration).Enrich.WithProperty("Application", "Open AI QA機器人和ChatGPT API").CreateLogger();

try
{
    //Log
    Log.Information("Open AI QA機器人和ChatGPT API 啟動");
    //Add services to the container.
    //DB
    AddDBServices(builder.Configuration, builder.Services);
    //JWT
    AddJWTService(builder.Configuration, builder.Services);
    //Swagger
    AddSwaggerService(builder.Configuration, builder.Services);
    //DI注入
    AddDependencyInjectionService(builder.Services);
    //Other
    AddOtherService(builder.Services);
    //Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
    //啟用Serilog,ASP.Net Core 5是使用WebHost,ASP.Net Core 6改為Host
    builder.Host.UseSerilog();
    //Configuration套用順序
    builder.Configuration.AddJsonFile("appsettings.json").AddEnvironmentVariables();

    var app = builder.Build();

    string pathBase = builder.Configuration.GetValue<string>("HostUrl:PathBase");
    if (pathBase != "")
    {
        app.UsePathBase("/" + pathBase);
        app.UseRouting();
    }
    //判斷是否開發環境
    if (app.Environment.IsDevelopment())
    {
        app.UseSwagger();
        app.UseSwaggerUI();
    }
    app.UseHttpsRedirection();
    //設定 HTTP 回應壓縮
    app.UseResponseCompression();
    //CORS
    app.UseCors();
    //提供身份驗證支援
    app.UseAuthentication();
    //提供角色授權支援,緊接著Authentication之後執行,設定授權檢查
    app.UseAuthorization();
    app.MapControllers();
    app.Run();
    return 0;
}
catch (Exception ex)
{
    Log.Fatal(ex, "Open AI API Fatal Exception");
    return 1;
}
finally
{
    Log.CloseAndFlush();
}

void AddDBServices(IConfiguration configuration, IServiceCollection services)
{
    //MS SQL資料庫
    var conStrBuilder = new SqlConnectionStringBuilder(configuration.GetConnectionString("DefaultConnection"))
    {
        Password = configuration.GetValue<string>("Connection:Key")
    };
    var connection = conStrBuilder.ConnectionString;
    services.AddDbContext<OpenAIContext>(options => options.UseSqlServer(connection));
}

void AddDependencyInjectionService(IServiceCollection services)
{
    //DI服務注入
    services.AddSingleton<MLContext>();
    services.AddHttpClient<IOpenAIHttpService, OpenAIHttpService>();
    services.AddScoped<IOpenAIEmbeddings, OpenAIEmbeddings>();
    services.AddScoped<IOpenAIGPTToken, OpenAIGPTToken>();
    services.AddScoped<IOpenAIPrompt, OpenAIPrompt>();
    services.AddScoped<IJWTToken, JWTToken>();
}

void AddSwaggerService(IConfiguration configuration, IServiceCollection services)
{
    //設定Swagger 
    services.AddSwaggerGen(options =>
    {
        //設置Swagger文檔的API服務簡介，包括版本號、標題和描述。
        options.SwaggerDoc("v1", new OpenApiInfo
        {
            Version = "v1",
            Title = "Open AI QA機器人和ChatGPT Restful API",
            Description = "使用Open AI API建立的ChatGPT和QA機器人<br><br>1.Google登入服務<br>2.ChatGPT3.5聊天室操作API<br>3.QA服務<br>4.Chat GPT 3.5串接<br>5.Embedding文本轉換和寫入",
        });
        //從應用程式的配置文件中讀取HostUrl: PathBase的值。
        string pathBase = configuration.GetValue<string>("HostUrl:PathBase");
        if (pathBase != "")
        {
            //用於替換Swagger UI上的basePath，並使用自定義的SwaggerBasePathFilter類設置basePath，例如：/api
            options.DocumentFilter<SwaggerBasePathFilter>("/" + pathBase);
        }
        //定義一個安全性方案，此處為名為Basic的HTTP基本認證方案。
        options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "Bearer",
            Description = "輸入Token進行認證"
        });
        //為每個操作添加安全性要求。
        options.OperationFilter<SwaggerSecurityRequirementsOperationFilter>();
        //使用SwaggerSchemaFilter類過濾器，過濾掉具有IgnoreDataMember屬性的屬性。
        options.SchemaFilter<SwaggerSchemaFilter>();
        //用XML文件註釋來描述操作和模型，幫助Swagger生成更詳細的API文檔。
        var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
        var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
        options.IncludeXmlComments(xmlPath);
    });
}

void AddOtherService(IServiceCollection services)
{
    //封包壓縮的服務
    services.AddResponseCompression();
    //CORS
    services.AddCors(options =>
    {
        options.AddDefaultPolicy(builder =>
        {
            builder.AllowAnyOrigin()
                .AllowAnyMethod()
                .AllowAnyHeader();
        });
    });
    services.AddControllers();
}

void AddJWTService(IConfiguration configuration, IServiceCollection services)
{
    services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = false,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = configuration.GetValue<string>("JwtSettings:Issuer"),
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration.GetValue<string>("JwtSettings:SignKey")))
        };
    });

    services.AddAuthorization();
}