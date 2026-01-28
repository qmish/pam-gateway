using PamGateway.Ui;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpClient<ApiClient>(client =>
{
    var baseUrl = builder.Configuration.GetValue<string>("Api:BaseUrl") ?? "http://localhost:8080";
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/'));
});
builder.Services.AddRazorPages();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.Run();
