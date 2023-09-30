FROM mcr.microsoft.com/dotnet/aspnet:7.0 AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:7.0 AS build
WORKDIR /src
COPY ["Products.Cache.API/Products.Cache.API.csproj", "Products.Cache.API/"]
RUN dotnet restore "Products.Cache.API/Products.Cache.API.csproj"
COPY . .
WORKDIR "/src/Products.Cache.API"
RUN dotnet build "Products.Cache.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Products.Cache.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Products.Cache.API.dll"]
