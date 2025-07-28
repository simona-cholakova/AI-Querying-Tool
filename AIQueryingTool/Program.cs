using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.Extensions.AI;
using Microsoft.SemanticKernel.Embeddings;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.OpenApi.Models;
using TodoApi.Models;
using TodoApi.Plugins;
using TodoApi.Utils;
using WebApplication2.Services;

var builder = WebApplication.CreateBuilder(args);

// ────── Load Secrets ──────
builder.Configuration.AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

var openAiKey = builder.Configuration["OpenAI:ApiKey"];
var openAiModel = builder.Configuration["OpenAi:ModelId"];
var openAiEmbeddingModel = builder.Configuration["OpenAi:EmbeddingModelId"];

var geminiKey = builder.Configuration["Gemini:ApiKey"];
var geminiModel = builder.Configuration["Gemini:ModelId"];

// ────── Register Embedding Service ──────
builder.Services.AddOpenAITextEmbeddingGeneration(
    modelId: openAiEmbeddingModel,
    apiKey: openAiKey
);

#pragma warning disable SKEXP0010
builder.Services.AddOpenAIEmbeddingGenerator(modelId: openAiEmbeddingModel, apiKey: openAiKey);
#pragma warning restore SKEXP0010

// ────── Register ASP.NET Services ──────
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();

// ────── Swagger Setup with Auth ──────
builder.Services.AddSwaggerGen(c =>
{
    c.CustomSchemaIds(x => x.FullName);  // Prevents schema conflicts
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "API", Version = "v1" });

    // JWT Bearer auth
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Bearer token. Example: Bearer {token}",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT"
    });

    // Cookie auth
    c.AddSecurityDefinition("cookieAuth", new OpenApiSecurityScheme
    {
        Name = ".AspNetCore.Identity.Application",
        Type = SecuritySchemeType.ApiKey,
        In = ParameterLocation.Cookie,
        Description = "Cookie-based authentication"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" } },
            Array.Empty<string>()
        },
        {
            new OpenApiSecurityScheme { Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "cookieAuth" } },
            Array.Empty<string>()
        }
    });
});

// ────── Authentication & Authorization ──────
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = IdentityConstants.BearerScheme;
    options.DefaultChallengeScheme = IdentityConstants.BearerScheme;
    options.DefaultSignInScheme = IdentityConstants.ApplicationScheme;
})
.AddCookie(IdentityConstants.ApplicationScheme)
.AddBearerToken(IdentityConstants.BearerScheme);

builder.Services.AddAuthorizationBuilder();

// ────── EF Core & Identity Setup ──────
builder.Services.AddDbContext<TodoContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("TodoContext"), o => o.UseVector()));

builder.Services.AddIdentityCore<User>()
    .AddEntityFrameworkStores<TodoContext>()
    .AddApiEndpoints();

// ────── Semantic Kernel & Plugins Setup ──────
builder.Services.AddScoped<Microsoft.SemanticKernel.Kernel>(sp =>
{
    var embeddingGenerator = sp.GetRequiredService<IEmbeddingGenerator<string, Embedding<float>>>();

    var kernel = Microsoft.SemanticKernel.Kernel.CreateBuilder()
        .AddOpenAIChatCompletion(modelId: openAiModel, apiKey: openAiKey)
        //.AddGoogleAIGeminiChatCompletion(modelId: geminiModel, apiKey: geminiKey)
        .Build();
    
    var loggerSeq = sp.GetRequiredService<ILogger<SeqPlugin>>();
    var loggerFile = sp.GetRequiredService<ILogger<FilePlugin>>();
    var loggerTodo = sp.GetRequiredService<ILogger<ToDoPlugin>>();
    var loggerGit = sp.GetRequiredService<ILogger<GitPlugin>>();

    var dbContext = sp.GetRequiredService<TodoContext>();
    var filePlugin = new FilePlugin(sp, dbContext, embeddingGenerator, loggerFile);
    var todoPlugin = new ToDoPlugin(sp, dbContext, embeddingGenerator, new TodoService(dbContext, new HttpContextAccessor()), loggerTodo);
    var seqPlugin = new SeqPlugin(loggerSeq);
    var gitPlugin = new GitPlugin(loggerGit);

    kernel.Plugins.AddFromObject(filePlugin, "FilePlugin");
    kernel.Plugins.AddFromObject(todoPlugin, "ToDoPlugin");
    kernel.Plugins.AddFromObject(seqPlugin, "SeqPlugin");
    kernel.Plugins.AddFromObject(gitPlugin, "GitPlugin");

    return kernel;
});

// ────── Optional Utility Services ──────
builder.Services.AddScoped<AIOptionService>();
builder.Services.AddScoped<KernelUtils>();
builder.Services.AddScoped<KernelService>();


// ────── Use OpenAI Chat by Default ──────
builder.Services.AddScoped<IChatCompletionService>(sp =>
{
    var kernel = sp.GetRequiredService<Microsoft.SemanticKernel.Kernel>();
    return kernel.GetRequiredService<IChatCompletionService>(); // No key needed
});

// ────── CORS for Frontend ──────
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy.WithOrigins("http://localhost:3000")
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});

var app = builder.Build();

// ────── Middleware ──────
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", "API V1"));
}

app.UseHttpsRedirection();
app.UseCors("AllowFrontend");
app.UseAuthentication();
app.UseAuthorization();

app.MapIdentityApi<User>();
app.MapControllers();

app.Run();
