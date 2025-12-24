using A1.Api.Models;
using A1.Api.Repositories;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddAuthorization();
builder.Services.AddControllers();
builder.Services.AddCors(o=>o.AddPolicy("AllowFrontend",p=>p.WithOrigins("http://localhost:3000","https://localhost:3000", "http://192.168.18.13:3000", "https://192.168.18.13:3000").AllowAnyHeader().AllowAnyMethod()));

// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c => { c.EnableAnnotations(); });


// Configure ADO.NET and Generic Repository
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped(typeof(IGenericRepository<>), typeof(GenericRepository<>));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseCors("AllowFrontend");
app.UseCors("AllowReact");

app.UseAuthorization();

// Generic Minimal API Endpoints
app.MapGet("/api/{entityName}", async (string entityName, IServiceProvider sp) =>
{
    var repo = GetRepository(sp, entityName);
    if (repo == null) return Results.NotFound();
    var result = await ((dynamic)repo).GetAllAsync();
    return Results.Ok(result);
});

app.MapGet("/api/{entityName}/{id}", async (string entityName, int id, IServiceProvider sp) =>
{
    var repo = GetRepository(sp, entityName);
    if (repo == null) return Results.NotFound();
    var entity = await ((dynamic)repo).GetByIdAsync(id);
    return entity != null ? Results.Ok(entity) : Results.NotFound();
});

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
});

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
});

app.MapDelete("/api/{entityName}/{id}", async (string entityName, int id, IServiceProvider sp) =>
{
    var repo = GetRepository(sp, entityName);
    if (repo == null) return Results.NotFound();
    var entity = await ((dynamic)repo).GetByIdAsync(id);
    if (entity == null) return Results.NotFound();
    await ((dynamic)repo).DeleteAsync(entity);
    return Results.NoContent();
});

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

app.MapControllers();

app.MapGet("/", () =>
{
    var assembly = Assembly.GetExecutingAssembly();
    var entities = assembly.GetTypes().Where(t => typeof(BaseEntity).IsAssignableFrom(t) && t.IsClass && !t.IsAbstract);
    var routes = entities.Select(t => $"/api/{t.Name}s").OrderBy(x => x);
    return string.Join("\n", routes);
});

app.Run();
