var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

// 1. Error-Handling Middleware (First)
app.Use(async (context, next) =>
{
    try
    {
        await next(context);
    }
    catch (Exception)
    {
        context.Response.StatusCode = StatusCodes.Status500InternalServerError;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Internal server error." });
    }
});

// 2. Authentication Middleware (Next)
app.Use(async (context, next) =>
{
    var path = context.Request.Path.Value ?? string.Empty;
    // Skip auth for openapi/swagger
    if (path.StartsWith("/openapi") || path.StartsWith("/swagger"))
    {
        await next(context);
        return;
    }

    if (!context.Request.Headers.TryGetValue("Authorization", out var authHeader) || authHeader.ToString() != "Bearer my-secret-token")
    {
        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { error = "Unauthorized." });
        return;
    }

    await next(context);
});

// 3. Logging Middleware (Last)
app.Use(async (context, next) =>
{
    var method = context.Request.Method;
    var pathUrl = context.Request.Path;
    app.Logger.LogInformation($"Incoming Request: {method} {pathUrl}");
    
    await next(context);
    
    app.Logger.LogInformation($"Outgoing Response: {method} {pathUrl} - Status: {context.Response.StatusCode}");
});

// In-memory data store for users
var users = new List<User>
{
    new User { Id = 1, FirstName = "John", LastName = "Doe", Email = "john.doe@example.com", Department = "IT" },
    new User { Id = 2, FirstName = "Jane", LastName = "Smith", Email = "jane.smith@example.com", Department = "HR" }
};

// GET: Retrieve a list of users (optimized with pagination option)
app.MapGet("/users", (int? page, int? pageSize) => 
{
    try
    {
        var p = page ?? 1;
        var s = pageSize ?? 10;
        var pagedUsers = users.Skip((p - 1) * s).Take(s).ToList();
        return Results.Ok(pagedUsers);
    }
    catch (Exception ex)
    {
        return Results.Problem("An error occurred while retrieving users: " + ex.Message);
    }
})
.WithName("GetUsers");

// GET: Retrieve a specific user by ID
app.MapGet("/users/{id}", (int id) =>
{
    try
    {
        var user = users.FirstOrDefault(u => u.Id == id);
        return user is not null ? Results.Ok(user) : Results.NotFound(new { Message = $"User with ID {id} not found." });
    }
    catch (Exception ex)
    {
        return Results.Problem("An error occurred while retrieving the user: " + ex.Message);
    }
})
.WithName("GetUserById");

// POST: Add a new user
app.MapPost("/users", (User newUser) =>
{
    try
    {
        // Validation
        if (string.IsNullOrWhiteSpace(newUser.FirstName) || string.IsNullOrWhiteSpace(newUser.LastName))
            return Results.BadRequest(new { Message = "First name and last name are required." });
        if (string.IsNullOrWhiteSpace(newUser.Email) || !newUser.Email.Contains("@"))
            return Results.BadRequest(new { Message = "A valid email address is required." });

        newUser.Id = users.Count > 0 ? users.Max(u => u.Id) + 1 : 1;
        users.Add(newUser);
        return Results.Created($"/users/{newUser.Id}", newUser);
    }
    catch (Exception ex)
    {
        return Results.Problem("An error occurred while creating the user: " + ex.Message);
    }
})
.WithName("CreateUser");

// PUT: Update an existing user's details
app.MapPut("/users/{id}", (int id, User updatedUser) =>
{
    try
    {
        // Validation
        if (string.IsNullOrWhiteSpace(updatedUser.FirstName) || string.IsNullOrWhiteSpace(updatedUser.LastName))
            return Results.BadRequest(new { Message = "First name and last name are required." });
        if (string.IsNullOrWhiteSpace(updatedUser.Email) || !updatedUser.Email.Contains("@"))
            return Results.BadRequest(new { Message = "A valid email address is required." });

        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null) return Results.NotFound(new { Message = $"User with ID {id} not found." });

        user.FirstName = updatedUser.FirstName;
        user.LastName = updatedUser.LastName;
        user.Email = updatedUser.Email;
        user.Department = updatedUser.Department;

        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.Problem("An error occurred while updating the user: " + ex.Message);
    }
})
.WithName("UpdateUser");

// DELETE: Remove a user by ID
app.MapDelete("/users/{id}", (int id) =>
{
    try
    {
        var user = users.FirstOrDefault(u => u.Id == id);
        if (user is null) return Results.NotFound(new { Message = $"User with ID {id} not found." });

        users.Remove(user);
        return Results.NoContent();
    }
    catch (Exception ex)
    {
        return Results.Problem("An error occurred while deleting the user: " + ex.Message);
    }
})
.WithName("DeleteUser");

app.Run();

// User model
class User
{
    public int Id { get; set; }
    public string FirstName { get; set; } = string.Empty;
    public string LastName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Department { get; set; } = string.Empty;
}
