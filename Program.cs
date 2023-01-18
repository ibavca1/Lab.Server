using Lab.Server.Data;
using Lab.Server.Helpers;
using Lab.Server.Models;
using Lab.Server.Remote;
using Lab.Server.Shared;
using Lab.Server.ViewModels;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Refit;
using System.Collections;
using System.Data.Common;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Text;
using System.Timers;
using Blazorise;
using Blazorise.Bootstrap;
using Blazorise.Icons.FontAwesome;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddServerSideBlazor();
builder.Services.AddSingleton<WeatherForecastService>();
builder.Services.AddSingleton<IDeviceListViewModel, DeviceListViewModel>();
builder.Services.AddSingleton<IServer, Server>();
builder.Services.AddRefitClient<OpenApi>()
    .ConfigureHttpClient(c =>
    {
        c.BaseAddress = new Uri("http://localhost:5142");
    });
builder.Services.AddBlazorise(options =>
    {
        options.Immediate = true;
    }).AddBootstrapProviders()
    .AddFontAwesomeIcons();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
}


app.UseStaticFiles();

app.UseRouting();

app.MapBlazorHub();
app.MapFallbackToPage("/_Host");



app.Run();

