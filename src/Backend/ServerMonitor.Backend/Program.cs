namespace Odland.ServerMonitor.Backend
{
    public static class Program
    {
        public static void Main(string[] args)
        {
            var builder = WebApplication.CreateBuilder(args);

            // Add OpenApi support for the project (during development)
            if (builder.Environment.IsDevelopment())
            {
                builder.Services.AddOpenApi();
            }


            // Add services to the container



            // Build the application
            var app = builder.Build();

            app.RunAsync();
        }
    }
}