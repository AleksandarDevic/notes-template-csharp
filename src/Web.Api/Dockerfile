# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build

WORKDIR /src

# restore
COPY ["Directory.Packages.props", "."]
COPY ["Directory.Build.props", "."]
COPY ["src/Web.Api/Web.Api.csproj", "Web.Api/"]
COPY ["src/Application/Application.csproj", "Application/"]
COPY ["src/Infrastructure/Infrastructure.csproj", "Infrastructure/"]
COPY ["src/Domain/Domain.csproj", "Domain/"]
COPY ["src/SharedKernel/SharedKernel.csproj", "SharedKernel/"]

RUN dotnet restore "Web.Api/Web.Api.csproj"

# build
COPY ["src/", "."]
WORKDIR /src/Web.Api
RUN dotnet build "Web.Api.csproj" -c Release -o /app/build

# Stage 2: Publish the application
FROM build AS publish
RUN dotnet publish "Web.Api.csproj" -c Release -o /app/publish

# Stage 3: Run the application
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
EXPOSE 8080
EXPOSE 8081
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT [ "dotnet", "Web.Api.dll" ]