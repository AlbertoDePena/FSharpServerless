namespace FSharpServerless.Web

open System
open Microsoft.AspNetCore.Hosting
open Microsoft.Extensions.Hosting
open Microsoft.AspNetCore.Builder
open Microsoft.Extensions.DependencyInjection
open Microsoft.AspNetCore.Http

module Program =

    let functionsPort =
        Environment.GetEnvironmentVariable("ASPNETCORE_URLS")
        |> (fun urls -> urls.Substring(urls.LastIndexOf(":") + 1))
        |> int32
    
    let functionsDll =
        let dll = Environment.GetEnvironmentVariable("FUNCTIONS_DLL")
        if String.IsNullOrWhiteSpace(dll) then failwith "DLL must be provided via FUNCTIONS_DLL environment variable" else dll
    
    let configureServices (context : WebHostBuilderContext) (services: IServiceCollection) =
        
        services.AddCors(fun options -> 
            options.AddPolicy("CorsPolicy", fun builder -> 
                builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader() |> ignore)) |> ignore
        
        services.AddMvcCore() |> ignore

    let configure (context : WebHostBuilderContext) (builder : IApplicationBuilder) =
        
        if (context.HostingEnvironment.IsDevelopment()) then
            builder.UseDeveloperExceptionPage() |> ignore

        builder.UseCors("CorsPolicy") |> ignore
        builder.UseRouting() |> ignore

        Initializer.loadEnvironmentVariables functionsDll
        
        builder.UseEndpoints(fun endpoints ->
            printfn "HTTP Triggers:\n"

            let httpTriggers = HttpTrigger.load functionsDll

            for httpTrigger in httpTriggers do
                let methodInfo = httpTrigger.MethodInfo
                let methods = httpTrigger.Attribute.Methods
                let endpoint = sprintf "api/%s" httpTrigger.Attribute.Name

                for method in methods do
                    match method with
                    | "OPTIONS" -> endpoints.Map(endpoint, RequestDelegate HttpTrigger.handleCors) |> ignore
                    | "GET" -> endpoints.MapGet(endpoint, RequestDelegate (HttpTrigger.handle methodInfo)) |> ignore
                    | "POST" -> endpoints.MapPost(endpoint, RequestDelegate (HttpTrigger.handle methodInfo)) |> ignore
                    | "PUT" -> endpoints.MapPut(endpoint, RequestDelegate (HttpTrigger.handle methodInfo)) |> ignore
                    | "DELETE" -> endpoints.MapDelete(endpoint, RequestDelegate (HttpTrigger.handle methodInfo)) |> ignore                    
                    | _ -> ()

                printfn "http://localhost:%i/%s - %s\n" functionsPort endpoint (String.Join(" ", methods))

            ) |> ignore

    [<EntryPoint>]
    let main args =
        
        Host.CreateDefaultBuilder(args)
            .ConfigureWebHostDefaults(fun builder ->
                builder
                    .Configure(Action<WebHostBuilderContext, IApplicationBuilder> configure)
                    .ConfigureServices(Action<WebHostBuilderContext, IServiceCollection> configureServices)
                    |> ignore)
            .Build()
            .Run()

        0 // exit code
