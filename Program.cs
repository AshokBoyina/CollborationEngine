using CollaborationEngine.API.Services;
using CollaborationEngine.API.Hubs;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new Microsoft.OpenApi.Models.OpenApiInfo { Title = "Collaboration Engine API", Version = "v1" });
});

// CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll", policy =>
    {
        policy.AllowAnyOrigin()
              .AllowAnyMethod()
              .AllowAnyHeader();
    });
});

// SignalR
builder.Services.AddSignalR();

// Custom Services
builder.Services.AddSingleton<ILocalDataService, LocalDataService>();
builder.Services.AddSingleton<ICollaborationService, CollaborationService>();
builder.Services.AddSingleton<IAgentAssignmentService, AgentAssignmentService>();
builder.Services.AddSingleton<ISupervisorAssignmentService, SupervisorAssignmentService>();
builder.Services.AddSingleton<IStreamingService, StreamingService>();
builder.Services.AddSingleton<IChatBotService, ChatBotService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("AllowAll");
app.UseStaticFiles();

app.MapControllers();
app.MapHub<CollaborationHub>("/collaborationHub");

app.Run();
