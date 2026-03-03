using A1.Api.Models;
using A1.Api.Repositories;
using A1.Api.Services;
using A1.Api.Utilities;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Reflection;
using System.Linq.Expressions;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthorization(options =>
{
    options.FallbackPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder()
        .RequireAuthenticatedUser()
        .Build();
});
builder.Services.AddControllers();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? new[]
{
    "https://172.32.3.219:3000",
    "http://172.32.3.219:3000",
    "http://localhost:3000",
    "https://localhost:3000",
    "http://192.168.18.13:3000",
    "https://192.168.18.13:3000"
};

builder.Services.AddCors(o => o.AddPolicy("AllowFrontend", p => p
    .WithOrigins(allowedOrigins)
    .AllowAnyHeader()
    .AllowAnyMethod()
    .AllowCredentials()));

builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

var jwtSection = builder.Configuration.GetSection("Jwt");
var key = jwtSection.GetValue<string>("Key") ?? "change-me-secret-key-should-be-strong";
var issuer = jwtSection.GetValue<string>("Issuer") ?? "A1.Api";
var audience = jwtSection.GetValue<string>("Audience") ?? "A1.Api";

builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateIssuerSigningKey = true,
        ValidateLifetime = true,
        ClockSkew = TimeSpan.FromMinutes(1),
        ValidIssuer = issuer,
        ValidAudience = audience,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key))
    };
});

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => { c.EnableAnnotations(); });


// Configure ADO.NET and Generic Repository
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));
builder.Services.AddScoped<IAuditLogService, AuditLogService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");

app.UseAuthentication();
app.UseAuthorization();

// Generic Minimal API Endpoints
app.MapGet("/api/{entityName}", async (string entityName, HttpContext httpContext, IServiceProvider sp) =>
{
    var entityType = GetEntityType(entityName);
    if (entityType == null) return Results.NotFound();

    var db = sp.GetRequiredService<ApplicationDbContext>();
    IQueryable query = GetEntityQueryable(db, entityType);
    var scope = await DataAccessScopeHelper.ResolveAsync(httpContext.User, db);
    query = DataAccessScopeHelper.ApplyScope(query, entityType, scope);

    var result = await query.Cast<object>().ToListAsync();
    return Results.Ok(result);
}).RequireAuthorization().RequireCors("AllowFrontend");

app.MapGet("/api/{entityName}/{id}", async (string entityName, int id, HttpContext httpContext, IServiceProvider sp) =>
{
    var entityType = GetEntityType(entityName);
    if (entityType == null) return Results.NotFound();

    var db = sp.GetRequiredService<ApplicationDbContext>();
    IQueryable query = GetEntityQueryable(db, entityType);
    var scope = await DataAccessScopeHelper.ResolveAsync(httpContext.User, db);
    query = DataAccessScopeHelper.ApplyScope(query, entityType, scope);
    query = ApplyIdFilter(query, entityType, id);
    var entity = await query.Cast<object>().FirstOrDefaultAsync();
    return entity != null ? Results.Ok(entity) : Results.NotFound();
}).RequireAuthorization().RequireCors("AllowFrontend");

app.MapPost("/api/{entityName}", async (string entityName, System.Text.Json.JsonElement jsonEntity, IServiceProvider sp) =>
{
    var repo = GetRepository(sp, entityName);
    if (repo == null) return Results.NotFound();

    var entityType = GetEntityType(entityName);
    if (entityType == null) return Results.NotFound();

    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var entityObj = System.Text.Json.JsonSerializer.Deserialize(jsonEntity.GetRawText(), entityType, options);
    if (entityObj == null) return Results.BadRequest("Invalid entity data.");

    // Set CreatedDate if present and not set
    var createdDateProp = entityType.GetProperty("CreatedDate");
    if (createdDateProp != null && createdDateProp.GetValue(entityObj) == null)
    {
        createdDateProp.SetValue(entityObj, DateTime.UtcNow);
    }

    await ((dynamic)repo).AddAsync((dynamic)entityObj);
    var pkPropPost = GetPrimaryKey(entityType);
    var idValPost = pkPropPost.GetValue(entityObj);
    var idIntPost = Convert.ToInt32(idValPost);
    return Results.Created($"/api/{entityName}/{idIntPost}", entityObj);
}).RequireAuthorization().RequireCors("AllowFrontend");

app.MapPut("/api/{entityName}/{id}", async (string entityName, int id, System.Text.Json.JsonElement jsonEntity, IServiceProvider sp) =>
{
    var repo = GetRepository(sp, entityName);
    if (repo == null) return Results.NotFound();

    var entityType = GetEntityType(entityName);
    if (entityType == null) return Results.NotFound();

    var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
    var entityObj = System.Text.Json.JsonSerializer.Deserialize(jsonEntity.GetRawText(), entityType, options);
    if (entityObj == null) return Results.BadRequest("Invalid entity data.");

    var pkProp = GetPrimaryKey(entityType);
    var currentPkVal = pkProp.GetValue(entityObj);
    if (IsDefault(currentPkVal, pkProp.PropertyType))
    {
        var targetType = Nullable.GetUnderlyingType(pkProp.PropertyType) ?? pkProp.PropertyType;
        pkProp.SetValue(entityObj, Convert.ChangeType(id, targetType));
    }
    else
    {
        var entityIdInt = Convert.ToInt32(currentPkVal);
        if (id != entityIdInt) return Results.BadRequest("ID mismatch.");
    }

    await ((dynamic)repo).UpdateAsync((dynamic)entityObj);
    return Results.NoContent();
}).RequireAuthorization().RequireCors("AllowFrontend");

app.MapDelete("/api/{entityName}/{id}", async (string entityName, int id, IServiceProvider sp) =>
{
    var repo = GetRepository(sp, entityName);
    if (repo == null) return Results.NotFound();
    var entity = await ((dynamic)repo).GetByIdAsync(id);
    if (entity == null) return Results.NotFound();
    await ((dynamic)repo).DeleteAsync(entity);
    return Results.NoContent();
}).RequireAuthorization().RequireCors("AllowFrontend");

// Helper to get the generic repository
object? GetRepository(IServiceProvider sp, string entityName)
{
    var entityType = GetEntityType(entityName);
    if (entityType == null) return null;
    var repoType = typeof(IGenericRepository<>).MakeGenericType(entityType);
    return sp.GetService(repoType);
}

// Helper to get entity type by name
        Type? GetEntityType(string entityName)
        {
            var assembly = Assembly.GetExecutingAssembly();
            Type? type = assembly.GetTypes().FirstOrDefault(t => typeof(BaseEntity).IsAssignableFrom(t) && t.Name.Equals(entityName, StringComparison.OrdinalIgnoreCase));
            return type;
        }

System.Reflection.PropertyInfo GetPrimaryKey(Type type)
{
    var props = type.GetProperties(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
    var typeNameId = type.Name + "Id";
    var idProp = props.FirstOrDefault(p => p.Name.Equals(typeNameId, StringComparison.OrdinalIgnoreCase))
                ?? props.FirstOrDefault(p => p.Name.Equals("Id", StringComparison.OrdinalIgnoreCase))
                ?? props.FirstOrDefault(p => p.Name.EndsWith("Id", StringComparison.OrdinalIgnoreCase));
    if (idProp == null)
    {
        throw new InvalidOperationException($"No primary key property found on {type.Name}.");
    }
    return idProp;
}

bool IsDefault(object? value, Type type)
{
    var t = Nullable.GetUnderlyingType(type) ?? type;
    if (value == null) return true;
    var defaultVal = t.IsValueType ? Activator.CreateInstance(t) : null;
    return Equals(value, defaultVal);
}

IQueryable ApplyIdFilter(IQueryable source, Type entityType, int id)
{
    var pk = GetPrimaryKey(entityType);
    var parameter = Expression.Parameter(entityType, "e");
    var property = Expression.Property(parameter, pk);
    var targetType = Nullable.GetUnderlyingType(pk.PropertyType) ?? pk.PropertyType;
    var value = Expression.Constant(Convert.ChangeType(id, targetType), targetType);
    Expression comparison = pk.PropertyType == targetType
        ? Expression.Equal(property, value)
        : Expression.Equal(property, Expression.Convert(value, pk.PropertyType));
    var lambda = Expression.Lambda(comparison, parameter);

    var whereMethod = typeof(Queryable)
        .GetMethods()
        .First(m => m.Name == nameof(Queryable.Where) && m.GetParameters().Length == 2)
        .MakeGenericMethod(entityType);

    return (IQueryable)whereMethod.Invoke(null, new object[] { source, lambda })!;
}

IQueryable GetEntityQueryable(ApplicationDbContext context, Type entityType)
{
    var setMethod = typeof(DbContext)
        .GetMethods()
        .First(m => m.Name == nameof(DbContext.Set) && m.IsGenericMethod && m.GetParameters().Length == 0)
        .MakeGenericMethod(entityType);

    return (IQueryable)setMethod.Invoke(context, null)!;
}

app.MapControllers();

app.MapGet("/", () =>
{
    var assembly = Assembly.GetExecutingAssembly();
    var entities = assembly.GetTypes().Where(t => typeof(BaseEntity).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);
    var routes = entities.Select(t => $"/api/{t.Name}s").OrderBy(x => x);
    return string.Join("\n", routes);
});

app.Run();
