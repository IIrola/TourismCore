using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Tourism.Api.Common;
using Tourism.Application;
using Tourism.Infrastructure;
using Tourism.Infrastructure.Security;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers().AddJsonOptions(options =>
{
    // Enums travel by name, not by ordinal.
    //
    // Without this they serialise as numbers, and a number says nothing about itself: a public
    // directory page receiving `badge: 3` has to map 3 to "Gold" by position, which is a
    // hand-maintained list — and inserting a badge in the middle of the enum silently relabels
    // everything already published. `TourismBadge.Undetermined` is -1, which is worse still for
    // any client that treats the value as an index.
    //
    // Scoped to what this API answers. The outbound clients to PIMA and Platform build their own
    // JsonSerializerOptions, so the wire between services is untouched by this.
    options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Paste the access token issued by Platform's /auth/login. No 'Bearer ' prefix needed."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference { Type = ReferenceType.SecurityScheme, Id = "Bearer" }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddApplication();
builder.Services.AddInfrastructure(builder.Configuration);

// Read independently of the DI-bound IOptions<JwtOptions>: AddJwtBearer's configuration
// delegate has no access to the service provider, and this only needs to run once at
// startup. AddInfrastructure's ValidateOnStart (a hosted check that runs during host
// startup, before Kestrel accepts any connection) is what actually fails the app fast when
// the signing key is missing or too short — by the time this delegate could ever run for a
// real request, that check has already passed. Same pattern Platform and PIMA use.
var jwtOptions = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();

builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        // Without this, ASP.NET Core silently rewrites well-known short claim names
        // ("sub") into long legacy claim-type URIs on validation, which would break every
        // Principal.FindFirstValue("sub") lookup in HttpContextCurrentUser.
        options.MapInboundClaims = false;
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtOptions.SigningKey)),
            ClockSkew = TimeSpan.Zero
        };
    });

builder.Services.AddAuthorization(options =>
{
    // Authentication is required unless a route says otherwise with [AllowAnonymous]. The
    // default is deny so that forgetting an attribute closes an endpoint instead of opening
    // it — the failure mode has to be the safe one. Same policy Platform and PIMA apply.
    options.FallbackPolicy = new AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});

var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
